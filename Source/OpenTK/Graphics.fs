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
        | Transform (transform, figure) -> new TransformProcedure (this.CreateProcedure figure, transform) :> Procedure
        | Composite (a, b) ->
        
            // Try to reduce the amount of sequential procedures needed by combining chained composite figures into a single procedure.
            let rec count current figure =
                match figure with
                | Composite (a, _) -> count (current + 1) a
                | _ -> current
            let count = count 2 a
            let procedures = Array.zeroCreate<Procedure> count
            let rec fill index figure =
                match figure with
                | Composite (a, b) -> 
                    procedures.[index] <- this.CreateProcedure b
                    fill (index - 1) a
                | figure -> procedures.[index] <- this.CreateProcedure figure
            procedures.[count - 1] <- this.CreateProcedure b
            fill (count - 2) a

            new SequentialProcedure (procedures) :> Procedure

        | Line line -> new LineProcedure (line) :> Procedure
        | Image (image, size, interpolation) ->
            let texture = Texture.Create (image, size)
            Texture.CreateMipmap GenerateMipmapTarget.Texture2D
            match interpolation with
            | ImageInterpolation.Nearest -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Nearest)
            | _ -> Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear)
            new TextureProcedure (Exclusive.custom texture.Delete texture) :> Procedure
        | TransformDynamic (transform, figure) -> new TransformDynamicProcedure (this.CreateProcedure figure, transform) :> Procedure
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
    member this.Render size = Procedure.Invoke (&main, graphics.CreateContext size)
