namespace MD.OpenTK

open MD
open MD.UI
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open GLExtensions

/// A render context for a graphics interface using a fixed-function pipeline.
type FixedContext () =
    inherit OpenTK.Context ()
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
        GL.Color4 Paint.White
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

/// A graphics interface that uses a fixed-function pipeline (OpenGL 2.1 and below).
type FixedGraphics () =
    inherit Graphics ()

    override this.Initialize () =
        GL.Enable EnableCap.Texture2D
        GL.Enable EnableCap.CullFace
        GL.Enable EnableCap.Blend
        GL.BlendFunc (BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha)

    override this.CreateContext () = new FixedContext () :> OpenTK.Context

    override this.CreateProcedure figure =
        match figure with
        | Transform (transform, figure) -> new TransformProcedure (this.GetProcedure figure, transform) :> Procedure
        | Composite (a, b) -> new SequentialProcedure [| this.GetProcedure a; this.GetProcedure b |] :> Procedure
        | Line line -> new LineProcedure (line) :> Procedure
        | Image (image, interpolation) ->
            let texture = Texture.Create image
            Texture.CreateMipmap GenerateMipmapTarget.Texture2D
            match interpolation with
            | ImageInterpolation.Nearest -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Nearest)
            | _ -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear)
            new TextureProcedure (texture |> Exclusive.custom (fun tex -> tex.Delete ())) :> Procedure
        | _ -> NullProcedure.Instance :> Procedure