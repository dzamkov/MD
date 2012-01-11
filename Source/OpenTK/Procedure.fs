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

    /// Invokes the given procedure using the given context returning true if it was successfully and fully
    /// performed.
    static member Invoke (procedure : Procedure byref, context) = procedure.Invoke (context, &procedure)
    
    /// Invokes this procedure using the given context. A reference to the procedure to be used for the next
    /// invokation is provided to allow the procedure to defer to another. This returns true if the procedure
    /// was successfully and fully performed.
    abstract member Invoke : Context * Procedure byref -> bool

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

    override this.Invoke (_, _) = true

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
        let result = Procedure.Invoke (&inner, context)
        context.Pop ()
        result

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
        let result = Procedure.Invoke (&inner, context)
        context.Pop ()
        result

/// A procedure that draws a line.
type LineProcedure (line : Line) =
    inherit Procedure ()
    override this.Invoke (context, _) = 
        context.RenderLine line
        true

/// A procedure that renders a full 2D texture to the unit square.
type TextureProcedure (texture : Texture exclusive) =
    inherit Procedure ()
    override this.Invoke (context, _) = 
        context.RenderTexture texture.Object
        true

/// A procedure that invokes a sequence of component procedures sequentially.
type SequentialProcedure (procedures : Procedure[]) =
    inherit Procedure ()

    override this.Invoke (context, _) =
        let mutable index = 0
        let mutable result = true
        while index < procedures.Length do
            result <- Procedure.Invoke (&procedures.[index], context) && result
            index <- index + 1
        result

/// A procedure that checks wether any part of the given bounding rectangle is visible before invoking the
/// inner procedure.
type BoundsCheckProcedure (bounds : Rectangle, inner : Procedure) =
    inherit Procedure ()
    let mutable inner = inner

    override this.Invoke (context, _) =
        if context.IsVisible bounds then Procedure.Invoke (&inner, context)
        else true

/// A procedure that handles a query for a figure and defers to its procedure when available.
type QueryProcedure (query : Figure query, createProcedure : Figure -> Procedure) =
    inherit Procedure ()
    let mutable state = Idle

    /// Gets the current state of this query procedure.
    member this.State = state

    override this.Invoke (context, procedure) =
        match state with
        | Idle ->
            let load figure = state <- HasFigure figure
            state <- Waiting (query.Register load)
            false
        | Waiting _ ->
            false
        | HasFigure figure ->
            procedure <- createProcedure figure
            state <- HasProcedure procedure
            Procedure.Invoke (&procedure, context)
        | HasProcedure a ->
            procedure <- a
            Procedure.Invoke (&procedure, context)

/// Describes a state of the query in a query procedure.
and QueryState =
    | Idle
    | Waiting of RetractAction
    | HasFigure of Figure
    | HasProcedure of Procedure

/// A procedure that switches between two similar procedures with different levels of detail based on
/// the given resolution limit.
type LODProcedure (simple : Procedure, complex : Procedure, resolutionLimit : float) =
    inherit Procedure ()
    let mutable simple = simple
    let mutable complex = complex

    override this.Invoke (context, procedure) =
        if context.Resolution > resolutionLimit then 
            Procedure.Invoke (&complex, context) || Procedure.Invoke (&simple, context)
        else Procedure.Invoke (&simple, context)

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
            | Nil -> true
            | Transform (transform, figure) ->
                context.PushTransform transform
                let result = this.Render (context, figure)
                context.Pop ()
                result
            | Modulate (paint, figure) ->
                context.PushModulate paint
                let result = this.Render (context, figure)
                context.Pop ()
                result
            | Clip (clip, figure) ->
                context.PushClip clip
                let result = this.Render (context, figure)
                context.Pop ()
                result
            | Composite (a, b) ->
                let aResult = this.Render (context, a)
                let bResult = this.Render (context, b)
                aResult && bResult
            | Line line ->
                context.RenderLine line
                true
            | TransformDynamic (transform, figure) ->
                context.PushTransform transform.Current
                let result = this.Render (context, figure)
                context.Pop ()
                result
            | figure -> 
                let procedure = cache.Create figure
                cache.Submit (figure, procedure)
                procedure.Invoke context

    override this.Invoke (context, _) = this.Render (context, figure.Current)