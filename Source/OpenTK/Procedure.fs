namespace MD.OpenTK

open MD
open MD.UI
open global.OpenTK
open OpenTK.Graphics.OpenGL
open OpenTK.Graphics

/// Tracks state information while rendering.
type RenderState = {

    /// The current transform from renderspace to viewspace.
    mutable Transform : Transform

    }

/// A procedure that manipulates the graphics context in order to render a 
/// visual object or effect.
type Procedure = 

    /// Maintains the current transform while performing the inner procedure.
    | ProtectTransform of Procedure

    /// Applies a transform to subsequent drawing operations.
    | ApplyTransform of Transform

    /// Sets or clears the texture for subsequent drawing operations.
    | SetTexture of Texture shared option

    /// Sets the color for subsequent drawing operations.
    | SetColor of Color

    /// Sets the paint for subsequent drawing operations.
    | SetPaint of Paint

    /// Clears the color buffer with the given color.
    | ClearColor of Color

    /// Performs a sequence of procedures in order.
    | Sequential of Procedure[]

    /// Releases all resources used by this procedure.
    member this.Finish () =
        match this with
        | SetTexture (Some texture) -> texture.Finish ()
        | Sequential procs -> procs |> Array.iter (fun proc -> proc.Finish ()) 
        | _ -> ()

    /// Creates a transformation matrix for the given transform.
    static member GetMatrix (transform : Transform, matrix : Matrix4d byref) =
        let x = transform.Offset.X
        let y = transform.Offset.Y
        let a = transform.X.X
        let b = transform.X.Y
        let c = transform.Y.X
        let d = transform.Y.Y
        matrix <- new Matrix4d(a, b, 0.0, 0.0, c, d, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, x, y, 0.0, 1.0)

    /// Invokes this procedure on the current graphics context.
    member this.Invoke (state : RenderState) =
        match this with
        | ProtectTransform inner ->
            let curTransform = state.Transform
            GL.PushMatrix ()
            inner.Invoke state
            GL.PopMatrix ()
            state.Transform <- curTransform
        | ApplyTransform transform ->
            let mutable matrix = Unchecked.defaultof<Matrix4d>
            Procedure.GetMatrix (transform, &matrix)
            GL.MultMatrix (&matrix)
            state.Transform <- transform * state.Transform
        | SetTexture None ->
            GL.BindTexture (TextureTarget.Texture2D, 0)
        | SetTexture (Some texture) ->
            (!!texture).Bind2D ()
        | SetColor color ->
            GL.Color3 (color.R, color.G, color.B)
        | SetPaint paint ->
            let color = paint.Color
            GL.Color4 (color.R, color.G, color.B, paint.Alpha)
        | ClearColor color ->
            GL.ClearColor (float32 color.R, float32 color.G, float32 color.B, 1.0f)
            GL.Clear ClearBufferMask.ColorBufferBit
        | Sequential procedures ->
            for procedure in procedures do
                procedure.Invoke state