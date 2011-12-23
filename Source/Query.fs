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

/// Contains functions for constructing and manipulating queries.
module Query =

    /// Constructs a query with the given precomputed value.
    let make value = new ReturnQuery<'a> (value) :> 'a query

    /// Constructs a query that gets its result from an asynchronous task.
    let task task = new TaskQuery<'a> (task) :> 'a query

    /// Constructs a query that depends on the result of another.
    let bind second first = new BindQuery<'b, 'a> (first, second) :> 'b query

    /// Constructs a collation of two queries.
    let collate first second = new CollateQuery<'a, 'b> (first, second) :> ('a * 'b) query