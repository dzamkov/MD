namespace MD.OpenTK

open MD
open MD.UI
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

/// Defines useful extension functions for the GL class.
module GLExtensions =
    type GL with
        static member Vertex2 (point : Point) =
            GL.Vertex2 (point.X, point.Y)

        static member TexCoord2 (point : Point) =
            GL.TexCoord2 (point.X, point.Y)

        static member Color4 (paint : Paint) =
            let color = paint.Color
            GL.Color4 (color.R, color.G, color.B, paint.Alpha)

        static member BindTexture2D (texture : Texture) =
            GL.BindTexture (TextureTarget.Texture2D, texture.ID)

        static member UnbindTexture2D () =
            GL.BindTexture (TextureTarget.Texture2D, 0)

/// A context that gives information about the current render state and defines primitive rendering operations.
[<AbstractClass>]
type Context () =
    
    /// Pushes an effect that applies a transform to subsequent rendering operations.
    abstract member PushTransform : Transform -> unit

    /// Pushes an effect that applies color-modulation to subsequent rendering operations.
    abstract member PushModulate : Paint -> unit

    /// Pushes an effect that clips all subsequent rendering operations to the given rectangle (in worldspace).
    abstract member PushClip : Rectangle -> unit

    /// Pops the most recent effect on the effect stack.
    abstract member Pop : unit -> unit
    
    /// Gets a point that gives the resolution (pixel area per unit) of each axis in worldspace.
    abstract member Resolution : Point

    /// Gets the current transform from worldspace to viewspace.
    abstract member Transform : Transform

    /// Renders a line using this context.
    abstract member RenderLine : Line -> unit

    /// Renders a 2D texture to the unit square using this context.
    abstract member RenderTexture : Texture -> unit

    /// Gets the smallest rectangle that contains all points visible by the current view.
    member this.ViewBounds = 
        let view = this.Transform.Inverse
        let a, b, c, d = new Point (-1.0, -1.0), new Point (1.0, -1.0), new Point (-1.0, 1.0), new Point (1.0, 1.0)
        let a, b, c, d = view * a, view * b, view * c, view * d
        let minX = a.X |> min b.X |> min c.X |> min d.X
        let minY = a.Y |> min b.Y |> min c.Y |> min d.Y
        let maxX = a.X |> max b.X |> max c.X |> max d.X
        let maxY = a.Y |> max b.Y |> max c.Y |> max d.Y
        new Rectangle (minX, maxX, minY, maxY)

    /// Determines wether the given rectangle is in the current view.
    member this.IsVisible (rect : Rectangle) =

        // Checks if one of the edges of rectangle "A" is a separating axis for rectangle "B" when
        // the given transform is applied to "B".
        let check (A : Rectangle) (B : Rectangle) (transform : Transform) =
            let a, b, c, d = B.BottomLeft, B.BottomRight, B.TopLeft, B.TopRight
            let a, b, c, d = transform * a, transform * b, transform * c, transform * d
            (a.X < A.Left && b.X < A.Left && c.X < A.Left && d.X < A.Left) ||
            (a.Y < A.Bottom && b.Y < A.Bottom && c.Y < A.Bottom && d.Y < A.Bottom) ||
            (a.X > A.Right && b.X > A.Right && c.X > A.Right && d.X > A.Right) ||
            (a.Y > A.Top && b.Y > A.Top && c.Y > A.Top && d.Y > A.Top)

        let transform = this.Transform
        not (check Rectangle.View rect transform || check rect Rectangle.View transform.Inverse)