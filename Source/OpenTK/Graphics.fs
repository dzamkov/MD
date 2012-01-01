namespace MD.OpenTK

open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

open MD
open MD.UI
open MD.OpenTK

/// An interface to an OpenGL graphics context that tracks resources and continuity when rendering figures.
[<AbstractClass>]
type Graphics () as this =
    let cache = new ManualCache<Figure, Procedure> (this.CreateProcedure, ignore)

    /// Initializes the OpenGL graphics context on the current thread.
    abstract member Initialize : unit -> unit

    /// Creates a default context for this graphics given the target viewport size. The context should render
    /// to the OpenGL context on the current thread.
    abstract member CreateContext : ImageSize -> Context

    /// Creates a procedure to render the given figure.
    abstract member CreateProcedure : Figure -> Procedure
    default this.CreateProcedure figure =
        match figure with
        | Null -> NullProcedure.Instance :> Procedure
        | Transform (transform, figure) -> new TransformProcedure (this.CreateProcedure figure, transform) :> Procedure
        | Line line -> new LineProcedure (line) :> Procedure
        | Image (image, size, interpolation) ->
            let texture = Texture.Create (image, size)
            Texture.CreateMipmap GenerateMipmapTarget.Texture2D
            match interpolation with
            | ImageInterpolation.Nearest -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Nearest)
            | _ -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear)
            new TextureProcedure (Exclusive.custom texture.Delete texture) :> Procedure
        | _ -> new NotImplementedException () |> raise

    /// Gets the procedure to render the given figure.
    member this.GetProcedure (figure : Figure) = cache.[figure]

    /// Renders the given figure using the given context.
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
        | Dynamic figure -> this.RenderFigure (context, figure.Current)
        | figure -> (this.GetProcedure figure).Invoke context

/// Tracks resources and continuity for rendering a specified figure using a graphics interface.
type Display (graphics : Graphics, figure : Figure) =
    let mutable main = graphics.GetProcedure figure
    
    /// Renders the current figure for the display to the current OpenGL context, given the target viewport
    /// size.
    member this.Render size = Procedure.Invoke (&main, graphics.CreateContext size)
