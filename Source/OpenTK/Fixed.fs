namespace MD.OpenTK

open MD
open MD.UI
open MD.OpenTK
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open GLExtensions

/// A render context for a graphics interface using a fixed-function pipeline.
type FixedContext (graphics : Graphics, size : ImageSize) =
    inherit OpenTK.Context (graphics)
    let effectRemoveStack = new Stack<unit -> unit> ()
    let mutable transform = Transform.Identity

    override this.PushTransform newTransform =
        let curTransform = transform
        let remove () = transform <- curTransform
        transform <- newTransform * transform
        effectRemoveStack.Push remove

    override this.PushClip _ =
        new NotImplementedException() |> raise
        effectRemoveStack.Push ignore

    override this.PushModulate _ =
        new NotImplementedException() |> raise
        effectRemoveStack.Push ignore

    override this.Pop () =
        let removeEffect = effectRemoveStack.Pop ()
        removeEffect ()

    override this.Resolution =
        let hRes = transform.X
        let vRes = transform.Y
        let unitArea = hRes.Normal ^^ vRes.Normal
        let hRes = hRes.Length * unitArea
        let vRes = vRes.Length * unitArea
        new Point (hRes * float size.Width / 2.0, vRes * float size.Height / 2.0)

    override this.Transform = transform

    override this.RenderLine line =
    
        // Use immediate mode, for now.
        let off = line.B - line.A
        let wo = off.Cross.Normal * (line.Weight / 2.0)
        GL.Color4 line.Paint
        GL.UnbindTexture2D ()
        GL.Begin BeginMode.Quads
        GL.Vertex2 (transform * (line.A + wo))
        GL.Vertex2 (transform * (line.A - wo))
        GL.Vertex2 (transform * (line.B - wo))
        GL.Vertex2 (transform * (line.B + wo))
        GL.End ()

    override this.RenderTexture texture =
        
        // Use immediate mode, for now.
        GL.Color3 (1.0, 1.0, 1.0)
        GL.BindTexture2D texture
        GL.Begin BeginMode.Quads
        GL.TexCoord2 (0.0, 0.0)
        GL.Vertex2 (transform * new Point (0.0, 1.0))
        GL.TexCoord2 (0.0, 1.0)
        GL.Vertex2 (transform * new Point (0.0, 0.0))
        GL.TexCoord2 (1.0, 1.0)
        GL.Vertex2 (transform * new Point (1.0, 0.0))
        GL.TexCoord2 (1.0, 0.0)
        GL.Vertex2 (transform * new Point (1.0, 1.0))
        GL.End ()

/// A graphics interface that uses the fixed-function pipeline (OpenGL 2.1 and below).
type FixedGraphics () =
    inherit Graphics ()

    override this.Initialize () =
        GL.Enable EnableCap.Texture2D
        GL.Enable EnableCap.CullFace
        GL.Enable EnableCap.Blend
        GL.BlendFunc (BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha)

    override this.CreateProcedure figure = Procedure.createDefaultStatic this.GetProcedure figure
    
    override this.CreateProcedure figure = Procedure.createDefaultDynamic this.GetProcedure figure

    override this.CreateContext size =
        GL.Viewport (0, 0, size.Width, size.Height)
        new FixedContext (this, size) :> Context