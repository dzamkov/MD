namespace MD.OpenTK

open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

open MD
open MD.UI
open MD.OpenTK

/// Describes a rendering procedure that manipulates a graphics context in order to produce a visual object or
/// effect. Procedures are used for rendering operations that occur often, are complex, or require continuity.
[<AbstractClass>]
type Procedure () =

    /// Invokes the given procedure using the given context.
    static member Invoke (procedure : Procedure byref, context) = procedure.Invoke (context, &procedure)
    
    /// Invokes this procedure using the given context. A reference to the procedure to be used for the next
    /// invokation is provided to allow the procedure to defer to another.
    abstract member Invoke : Context * Procedure byref -> unit

    /// Invokes this procedure using the given context.
    member this.Invoke context =
        let mutable dummy = this
        this.Invoke (context, &dummy)

/// A procedure that does nothing.
type NullProcedure private () =
    inherit Procedure ()
    static let instance = new NullProcedure ()

    /// Gets the only instance of this class.
    static member Instance = instance

    override this.Invoke (_, _) = ()

/// A procedure that applies a transformation to an inner procedure.
type TransformProcedure (inner : Procedure, transform : Transform) =
    inherit Procedure ()
    let mutable inner = inner

    /// Gets the inner procedure for this procedure.
    member this.Inner = inner

    /// Gets the transform for this procedure.
    member this.Transform = transform

    override this.Invoke (context, _) =
        context.PushTransform transform
        Procedure.Invoke (&inner, context)
        context.Pop ()

/// A procedure that applies a dynamic transformation to an inner procedure.
type TransformDynamicProcedure (inner : Procedure, transform : Transform signal) =
    inherit Procedure ()
    let mutable inner = inner

    /// Gets the inner procedure for this procedure.
    member this.Inner = inner

    /// Gets the transform signal for this procedure.
    member this.Transform = transform

    override this.Invoke (context, _) =
        context.PushTransform transform.Current
        Procedure.Invoke (&inner, context)
        context.Pop ()

/// A procedure that draws a line.
type LineProcedure (line : Line) =
    inherit Procedure ()
    override this.Invoke (context, _) = context.RenderLine line

/// A procedure that renders a full 2D texture to the unit square.
type TextureProcedure (texture : Texture exclusive) =
    inherit Procedure ()
    override this.Invoke (context, _) = context.RenderTexture texture.Object

/// A procedure that invokes a sequence of component procedures sequentially.
type SequentialProcedure (procedures : Procedure[]) =
    inherit Procedure ()

    override this.Invoke (context, _) =
        let mutable index = 0
        while index < procedures.Length do
            Procedure.Invoke (&procedures.[index], context)
            index <- index + 1

/// A procedure that renders a dynamic figure directly by rendering each frame of the figure individually,
/// as needed. When available or required, the procedure may call another given by a cache.
type DefaultDynamicProcedure (figure : Figure signal, cache : Cache<Figure, Procedure>) =
    inherit Procedure ()

    /// Renders the given figure using the given context.
    member this.Render (context : Context, figure) =
        match cache.Fetch figure with
        | Some procedure -> procedure.Invoke context
        | None ->
            match figure with
            | Null -> ()
            | Transform (transform, figure) ->
                context.PushTransform transform
                this.Render (context, figure)
                context.Pop ()
            | Modulate (paint, figure) ->
                context.PushModulate paint
                this.Render (context, figure)
                context.Pop ()
            | Clip (clip, figure) ->
                context.PushClip clip
                this.Render (context, figure)
                context.Pop ()
            | Composite (a, b) ->
                this.Render (context, a)
                this.Render (context, b)
            | Line line ->
                context.RenderLine line
            | TransformDynamic (transform, figure) ->
                context.PushTransform transform.Current
                this.Render (context, figure)
                context.Pop ()
            | figure -> 
                let procedure = cache.Create figure
                cache.Submit (figure, procedure)
                procedure.Invoke context

    override this.Invoke (context, _) = this.Render (context, figure.Current)