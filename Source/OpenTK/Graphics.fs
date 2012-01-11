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
    default this.CreateProcedure (figure : Figure) =
        match figure with
        | Nil -> NullProcedure.Instance :> Procedure
        | Transform (transform, figure) -> new TransformProcedure (this.GetProcedure figure, transform) :> Procedure
        | Composite (a, b) -> new SequentialProcedure [| this.GetProcedure a; this.GetProcedure b |] :> Procedure
        | CompositeMany figures -> new SequentialProcedure (Array.map this.GetProcedure figures) :> Procedure
        | Line line -> new LineProcedure (line) :> Procedure
        | Image (image, size, interpolation) ->
            let texture = Texture.Create (image, size)
            Texture.CreateMipmap GenerateMipmapTarget.Texture2D
            match interpolation with
            | ImageInterpolation.Nearest -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Nearest)
            | _ -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear)
            new TextureProcedure (Exclusive.custom texture.Delete texture) :> Procedure
        | LOD (simple, complex, res) -> new LODProcedure (this.GetProcedure simple, this.GetProcedure complex, res) :> Procedure
        | Bounded (bounds, figure) -> new BoundsCheckProcedure (bounds, this.GetProcedure figure) :> Procedure
        | Lazy figure -> this.CreateProcedure (figure.Force ())
        | Query query -> new QueryProcedure (query, this.GetProcedure) :> Procedure
        | TransformDynamic (transform, figure) -> new TransformDynamicProcedure (this.GetProcedure figure, transform) :> Procedure
        | Dynamic figure -> new DefaultDynamicProcedure (figure, cache) :> Procedure
        | _ -> new NotImplementedException () |> raise

    /// Gets the procedure to render the given figure.
    member this.GetProcedure (figure : Figure) = cache.[figure]

    /// Renders the given figure using the given context.
    member this.RenderFigure (context, figure) = cache.[figure].Invoke context

/// Tracks resources and continuity for rendering a specified figure using a graphics interface.
type Display (graphics : Graphics, figure : Figure) =
    let mutable main = graphics.GetProcedure figure
    
    /// Renders the current figure for the display to the current OpenGL context, given the target viewport
    /// size.
    member this.Render size = Procedure.Invoke (&main, graphics.CreateContext size) |> ignore
