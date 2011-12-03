namespace MD.OpenTK

open MD
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

/// An interface to an OpenGL graphics device that tracks resources and allows rendering.
type Graphics () =
    let textures = new Dictionary<Image, Texture> ()
    
    /// Creates a new graphics context.
    static member Create () = 
        new Graphics ()

    /// Initializes graphics on the current context.
    static member Initialize () =
        GL.Enable EnableCap.Texture2D
        GL.Enable EnableCap.CullFace
        GL.Enable EnableCap.Blend
        GL.BlendFunc (BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha)

    /// Sets up the viewport on the current graphics context.
    static member Setup (view : Transform, width : int, height : int, invertY : bool) =
        GL.Viewport (0, 0, width, height)
        Graphics.SetProjection (view.Inverse, invertY)

    /// Updates the current matrix of the curren graphics context to the given projection, given by a transform.
    static member SetProjection (inverseView : Transform, invertY : bool) =
        let x = inverseView.Offset.X
        let y = inverseView.Offset.Y
        let a = inverseView.X.X
        let b = inverseView.X.Y
        let c = inverseView.Y.X
        let d = inverseView.Y.Y
        let i = if invertY then -1.0 else 1.0
        let mutable mat = new Matrix4d(a, b * i, 0.0, 0.0, c, d * i, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, x, y * i, 0.0, 1.0)
        GL.LoadMatrix &mat

    /// Applies the given transform to the current graphics context.
    static member Transform (transform : Transform) =
        let x = transform.Offset.X
        let y = transform.Offset.Y
        let a = transform.X.X
        let b = transform.X.Y
        let c = transform.Y.X
        let d = transform.Y.Y
        let mutable mat = new Matrix4d(a, b, 0.0, 0.0, c, d, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, x, y, 0.0, 1.0)
        GL.MultMatrix &mat

    /// Pushes the matrix stack on the current graphics context.
    static member PushTransform () =
        GL.PushMatrix ()

    /// Pops the matrix stack on the current graphics context.
    static member PopTransform () =
        GL.PopMatrix ()

    /// Clears the current graphics context with the given color.
    static member Clear (color : Color) =
        GL.ClearColor (float32 color.R, float32 color.G, float32 color.B, float32 1.0)
        GL.Clear ClearBufferMask.ColorBufferBit

    /// Sets the given color as the current color.
    static member SetColor (color : Color) =
        GL.Color3 (color.R, color.G, color.B)

    /// Sets the given paint as the current paint.
    static member SetPaint (paint : Paint) =
        let color = paint.Color
        GL.Color4 (color.R, color.G, color.B, paint.Alpha)

    /// Outputs the given color to the current graphics context using immediate mode.
    static member OutputColor (color : Color) = Graphics.SetColor color

    /// Outputs the given paint to the current graphics context using immediate mode.
    static member OutputPaint (paint : Paint) = Graphics.SetPaint paint

    /// Outputs the given point as a vertex to the current graphics context using immediate mode.
    static member OutputVertex (point : Point) =
        GL.Vertex2 (point.X, point.Y)

    /// Outputs the given point as a UV coordinate to the current graphics context using immediate mode.
    static member OutputUV (point : Point) =
        GL.TexCoord2 (point.X, point.Y)

    /// Begins rendering using the given mode.
    static member Begin (mode : BeginMode) =
        GL.Begin mode

    /// Ends rendering.
    static member End () =
        GL.End ()

    /// Gets the texture for the given image.
    member this.GetTexture image =
        let mutable texture = Texture.Null
        if not (textures.TryGetValue (image, &texture)) then
            texture <- Texture.Create image
            textures.[image] <- texture
            Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.Linear, TextureMagFilter.Linear)
        texture

    /// Renders the given figure using this graphics context.
    member this.Render figure =
        match figure with
        | Line (a, b, w, p) ->
            let off = b - a
            let wo = off.Cross.Normal * (w / 2.0)
            Graphics.SetPaint p
            Graphics.Begin BeginMode.Quads
            Graphics.OutputVertex (a + wo)
            Graphics.OutputVertex (a - wo)
            Graphics.OutputVertex (b - wo)
            Graphics.OutputVertex (b + wo)
            Graphics.End ()
        | Image (image, interpolation, source, area) ->
            let tex = this.GetTexture image
            tex.Bind2D ()
            match interpolation with
            | ImageInterpolation.Nearest -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.Nearest, TextureMagFilter.Nearest)
            | ImageInterpolation.Linear -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.Linear, TextureMagFilter.Linear)
            | _ -> ()
            Graphics.SetPaint Paint.White
            Graphics.Begin BeginMode.Quads
            Graphics.OutputUV source.TopLeft
            Graphics.OutputVertex area.TopLeft
            Graphics.OutputUV source.BottomLeft
            Graphics.OutputVertex area.BottomLeft
            Graphics.OutputUV source.BottomRight
            Graphics.OutputVertex area.BottomRight
            Graphics.OutputUV source.TopRight
            Graphics.OutputVertex area.TopRight
            Graphics.End ()
        | Transform (trans, fig) ->
            Graphics.PushTransform ()
            Graphics.Transform trans
            this.Render fig
            Graphics.PopTransform ()
        | Composite (a, b) ->
            this.Render a
            this.Render b
        | _ -> ()