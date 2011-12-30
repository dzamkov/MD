namespace MD.OpenTK

open MD
open MD.UI
open MD.OpenTK
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

/// A procedure that does nothing.
type NullProcedure private () =
    inherit Procedure ()
    static let instance = new NullProcedure ()

    /// Gets the only instance of this class.
    static member Instance = instance

    override this.Invoke _ = ()

/// A procedure that applies a transform to an inner procedure.
type TransformProcedure (inner : Procedure, transform : Transform) =
    inherit Procedure ()

    /// Gets the inner procedure for this procedure.
    member this.Inner = inner

    /// Gets the transform applied by this procedure.
    member this.Transform = transform

    override this.Invoke context =
        context.PushTransform transform
        inner.Invoke context
        context.Pop ()

    override this.Delete () = inner.Delete ()

/// A procedure that draws a line.
type LineProcedure (line : Line) =
    inherit Procedure ()

    override this.Invoke context = context.RenderLine line

/// A procedure that renders a full 2D texture to the unit square.
type TextureProcedure (texture : Texture exclusive) =
    inherit Procedure ()

    /// Gets the texture for this procedure.
    member this.Texture = texture.Object

    override this.Invoke context = context.RenderTexture texture.Object
    override this.Delete () = texture.Release.Invoke ()

/// A procedure that invokes a sequence of component procedures sequentially.
type SequentialProcedure (procedures : seq<Procedure>) =
    inherit Procedure ()

    /// Gets the component procedures of this procedure.
    member this.Procedures = procedures

    override this.Invoke context =
        for procedure in procedures do procedure.Invoke context

    override this.Delete () =
        for procedure in procedures do procedure.Delete ()

/// A procedure that renders a dynamic figure given by a signal.
type DynamicFigureProcedure (figure : Figure signal) =
    inherit Procedure ()
    override this.Invoke context = context.RenderFigure figure.Current

/// Contains functions for constructing and manipulating procedures.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Procedure =

    /// Creates the default procedure for rendering the given static figure.
    let createDefaultStatic (create : Figure -> Procedure) (figure : Figure) =
        match figure with
        | Null -> NullProcedure.Instance :> Procedure
        | Transform (transform, figure) -> new TransformProcedure (create figure, transform) :> Procedure
        | Line line -> new LineProcedure (line) :> Procedure
        | Image (image, size, interpolation) ->
            let texture = Texture.Create (image, size)
            Texture.CreateMipmap GenerateMipmapTarget.Texture2D
            match interpolation with
            | ImageInterpolation.Nearest -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Nearest)
            | _ -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear)
            new TextureProcedure (texture.MakeExclusive ()) :> Procedure
        | _ -> new NotImplementedException () |> raise

    /// Creates the default procedure for rendering the given dynamic figure.
    let createDefaultDynamic (create : Figure signal -> Procedure) (figure : Figure signal) =
        new DynamicFigureProcedure (figure) :> Procedure