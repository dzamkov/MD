namespace MD.OpenTK

open MD
open MD.UI
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

/// An interface to an OpenGL graphics device that tracks resources and continuity when rendering.
type Graphics () =
    let procedureCache = new ManualCache<Figure, Procedure> (fun proc -> proc.Finish ())
    
    /// Creates a new graphics context.
    static member Create () = 
        new Graphics ()

    /// Initializes graphics on the current context.
    static member Initialize () =
        GL.Enable EnableCap.Texture2D
        GL.Enable EnableCap.CullFace
        GL.Enable EnableCap.Blend
        GL.BlendFunc (BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha)

    /// Renders the given figure using this graphics context and the given parameters.
    member this.Render (width : int, height : int, figure : Figure) =
        GL.Viewport (0, 0, width, height)
        let renderState = {
                Transform = Transform.Identity
            }
        this.Render (renderState, figure)

    /// Renders the given figure, using the given render state.
    member this.Render (state : RenderState, figure : Figure) =
        ()