namespace MD

open System
open System.Collections.Generic

/// A compositable operation.
type Action =
    | Nil
    | Custom of (unit -> unit)
    | Composite of Action * Action
    | Dynamic of Action ref

    /// Invokes this action.
    member this.Invoke () = 
        match this with
        | Nil -> ()
        | Custom x -> x ()
        | Composite (x, y) ->
            x.Invoke ()
            y.Invoke ()
        | Dynamic ref -> (!ref).Invoke ()

    /// Combines two action.
    static member (+) (a : Action, b : Action) =
        match (a, b) with
        | (Nil, x) -> x
        | (x, Nil) -> x
        | (x, y) -> Composite (x, y)

    /// Gets a action operation that invokes a delegate action.
    static member FromDelegate (action : System.Action) = Custom action.Invoke

/// An operation that can be used to retract a previously submitted object, modification or callback. Retract actions
/// may be called at most once.
type RetractAction = Action

/// A mutable linked list that allows the use of retract action to remove items.
type Registry<'a> () =
    inherit LinkedList<'a> ()

    /// Adds an item to this registery and returns a retract action to later remove it.
    member this.Add (item : 'a) =
        let node = this.AddFirst item
        Action.Custom (fun () -> this.Remove node)

    /// Performs an action on all items in this registry, while still allowing items to be added or removed.
    member this.Forall (action : 'a -> unit) =
        let mutable cur = this.First
        while cur <> null do
            let next = cur.Next
            action cur.Value
            cur <- next

/// An operation that releases control of resources. Release actions must be called exactly once, but only when the objects
/// using the resources will no longer be used.
type ReleaseAction = Action