namespace MD.OpenTK

open MD
open MD.UI
open MD.OpenTK
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

/// An interface to an OpenGL graphics context that tracks resources and continuity when rendering static and
/// dynamic figures.
[<AbstractClass>]
type Graphics () as this =
    let staticCache = new ManualCache<Figure, Procedure> (this.CreateProcedure, Procedure.Delete)
    let dynamicCache = new ManualCache<Figure signal, Procedure> (this.CreateProcedure, Procedure.Delete)

    /// Initializes the OpenGL graphics context on the current thread.
    abstract member Initialize : unit -> unit

    /// Creates a default context for this graphics given the target viewport size. The context should render
    /// to the OpenGL context on the current thread.
    abstract member CreateContext : ImageSize -> Context

    /// Creates a procedure to render the given static figure.
    abstract member CreateProcedure : Figure -> Procedure
    default this.CreateProcedure (figure : Figure) =
        match figure with
        | Null -> NullProcedure.Instance :> Procedure
        | Transform (transform, figure) -> new TransformProcedure (this.GetProcedure figure, transform) :> Procedure
        | Line line -> new LineProcedure (line) :> Procedure
        | Image (image, size, interpolation) ->
            let texture = Texture.Create (image, size)
            Texture.CreateMipmap GenerateMipmapTarget.Texture2D
            match interpolation with
            | ImageInterpolation.Nearest -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Nearest)
            | _ -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear)
            new TextureProcedure (texture |> Exclusive.custom (fun texture -> texture.Delete ())) :> Procedure
        | _ -> new NotImplementedException () |> raise

    /// Creates a procedure to render the dynamic figure given by the given signal.
    abstract member CreateProcedure : Figure signal -> Procedure
    default this.CreateProcedure (figure : Figure signal) =
        new DynamicFigureProcedure (figure, this.RenderFigure) :> Procedure

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

/// Tracks resources and continuity for rendering a specified dynamic figure using a graphics interface.
type Display (graphics : Graphics, figure : Figure signal) =
    let main = graphics.GetProcedure figure
    
    /// Renders the current figure for the display to the current OpenGL context, given the target viewport
    /// size.
    member this.Render size = main.Invoke (graphics.CreateContext size)
