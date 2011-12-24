namespace MD

open System
open System.Threading
open System.Threading.Tasks

/// Represents an operation that results in a value of a certain type.
[<AbstractClass>]
type Query<'a> () =

    /// Registers a callback to be called with the result of this query when it is known 
    /// (may be immediately). The returned retract action can be used to retract the request, 
    /// but does not gurantee the callback won't be called.
    abstract member Register : ('a -> unit) -> Retract

// Create type abbreviation.
type 'a query = Query<'a>

/// A query that immediately gives a static, precomputed value.
type ReturnQuery<'a> (value : 'a) =
    inherit Query<'a> ()

    override this.Register callback =
        callback value
        Retract.Nil

/// A query that performs a task asynchronously upon request.
type TaskQuery<'a> (task : unit -> 'a) =
    inherit Query<'a> ()

    override this.Register callback =
        let cancelSource = new CancellationTokenSource ()
        let task = new Task (Action (task >> callback), cancelSource.Token)
        task.Start ()
        Retract.Single cancelSource.Cancel

/// A query that applies a mapping to the result of a source query.
type MapQuery<'a, 'b> (source : 'b query, map : 'b -> 'a) =
    inherit Query<'a> ()

    override this.Register callback = source.Register (map >> callback)

/// A combination of two queries where the second query depends on the result
/// of the first.
type BindQuery<'a, 'b> (first : 'b query, second : 'b -> 'a query) =
    inherit Query<'a> ()

    override this.Register callback =
        let retract = ref Unchecked.defaultof<Retract>
        let next value = retract := (second value).Register callback
        retract := first.Register next
        Retract.Dynamic retract

/// The state for an unfinished collate query at any one time.
type CollateQueryState<'a, 'b> =
    | FirstReady of 'a
    | SecondReady of 'b
    | NoneReady

/// A collation of two queries.
type CollateQuery<'a, 'b> (first : 'a query, second : 'b query) =
    inherit Query<'a * 'b> ()

    override this.Register callback =
        let state = ref NoneReady
        let firstCallback value =
            match !state with
            | SecondReady secondValue -> callback (value, secondValue)
            | _ -> state := FirstReady value
        let secondCallback value =
            match !state with
            | FirstReady firstValue -> callback (firstValue, value)
            | _ -> state := SecondReady value
        (first.Register firstCallback) + (second.Register secondCallback)

/// A query that caches the result of a source query.
type CacheQuery<'a> (source : 'a query) =
    inherit Query<'a> ()
    let callbacks = new Registry<'a -> unit> ()
    let mutable result = None
    let mutable retractSource = None

    override this.Register callback =
        match result with
        | Some result ->
            callback result
            Retract.Nil
        | None ->
            Monitor.Enter this
            let retractCallback = callbacks.Add callback

            // Start the request for the source.
            if callbacks.Count >= 1 && Option.isNone retractSource then
                let sourceCallback value =
                    Monitor.Enter this
                    result <- Some value
                    for callback in callbacks do
                        callback value
                    Monitor.Exit this
                retractSource <- Some (source.Register sourceCallback)

            // If there are no more callbacks left, retract the source request.
            let retract () =
                Monitor.Enter this
                if callbacks.Count = 0 && Option.isSome retractSource then
                    (Option.get retractSource).Invoke ()
                    retractSource <- None
                Monitor.Exit this
            let retract = retractCallback + Retract.Single retract

            Monitor.Exit this
            retract

/// Contains functions for constructing and manipulating queries.
module Query =

    /// Constructs a query with the given precomputed value.
    let make value = new ReturnQuery<'a> (value) :> 'a query

    /// Constructs a query that gets its result from an asynchronous task.
    let task task = new TaskQuery<'a> (task) :> 'a query

    /// Constructs a mapped form of a query.
    let map map source = new MapQuery<'b, 'a> (source, map) :> 'b query

    /// Constructs a query that depends on the result of another.
    let bind second first = new BindQuery<'b, 'a> (first, second) :> 'b query

    /// Constructs a collation of two queries.
    let collate first second = new CollateQuery<'a, 'b> (first, second) :> ('a * 'b) query

    /// Constructs a cached form of the given query.
    let cache source = new CacheQuery<'a> (source) :> 'a query