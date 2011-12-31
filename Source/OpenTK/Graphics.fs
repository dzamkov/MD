namespace MD.OpenTK

open MD
open MD.UI
open MD.OpenTK
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

/// An interface to an OpenGL graphics context that tracks resources and continuity when rendering static and
/// dynamic figures.
[<AbstractClass>]
type Graphics () as this =
    let staticCache = new ManualCache<Figure, Procedure> (this.CreateProcedure, ignore)
    let dynamicCache = new ManualCache<Figure signal, Procedure> (this.CreateProcedure, ignore)

    /// Initializes the OpenGL graphics context on the current thread.
    abstract member Initialize : unit -> unit

    /// Creates a default context for this graphics given the target viewport size. The context should render
    /// to the OpenGL context on the current thread.
    abstract member CreateContext : ImageSize -> Context

    /// Creates a procedure to render the given static figure.
    abstract member CreateProcedure : Figure -> Procedure

    /// Creates a procedure to render the dynamic figure given by the given signal.
    abstract member CreateProcedure : Figure signal -> Procedure

    /// Gets the procedure to render the given figure.
    member this.GetProcedure (figure : Figure) = staticCache.[figure]

    /// Gets the procedure to render the given dynamic figure.
    member this.GetProcedure (figure : Figure signal) = dynamicCache.[figure]

    /// Renders the given static figure using the given context.
    abstract member RenderFigure : Context * Figure -> unit
    default this.RenderFigure (context, figure) =
        match figure with
        | Null -> ()
        | Transform (transform, figure) ->
            context.PushTransform transform
            this.RenderFigure (context, figure)
            context.Pop ()
        | Modulate (paint, figure) ->
            context.PushModulate paint
            this.RenderFigure (context, figure)
            context.Pop ()
        | Clip (clip, figure) ->
            context.PushClip clip
            this.RenderFigure (context, figure)
            context.Pop ()
        | Composite (a, b) ->
            this.RenderFigure (context, a)
            this.RenderFigure (context, b)
        | Line line ->
            context.RenderLine line
        | figure -> (this.GetProcedure figure).Invoke context

/// A context for a graphics interface that gives information about the current render state and 
/// defines primitive rendering operations.
and [<AbstractClass>] Context (graphics : Graphics) =

    /// Gets the graphics interface this context is for.
    member this.Graphics = graphics
    
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

    /// Renders the given figure using this context.
    member this.RenderFigure figure = graphics.RenderFigure (this, figure)

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

/// Describes a rendering procedure that manipulates a graphics context in order to produce a visual object or
/// effect. Procedures are used for rendering operations that occur often, are complex, or require continuity.
and [<AbstractClass>] Procedure () =
    
    /// Invokes this procedure using the given context.
    abstract member Invoke : Context -> unit

/// Tracks resources and continuity for rendering a specified dynamic figure using a graphics interface.
type Display (graphics : Graphics, figure : Figure signal) =
    let main = graphics.GetProcedure figure
    
    /// Renders the current figure for the display to the current OpenGL context, given the target viewport
    /// size.
    member this.Render size = main.Invoke (graphics.CreateContext size)
