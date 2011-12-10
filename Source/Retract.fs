namespace MD

open System
open System.Collections.Generic

/// A operation that can be used to retract a previously submitted object, modification or callback. If a
/// method returns a retract operation of nil, then no modification were made, and no retraction is required.
type Retract =
    | Nil
    | Single of (unit -> unit)
    | Binary of Retract * Retract

    /// Invokes this retract operation.
    member this.Invoke () = 
        match this with
        | Nil -> ()
        | Single x -> x ()
        | Binary (x, y) ->
            x.Invoke ()
            y.Invoke ()

    /// Determines wether this retract operation will perform an operation when invoked.
    member this.HasAction =
        match this with
        | Nil -> false
        | _ -> true

    /// Combines two retract operations.
    static member (+) (a : Retract, b : Retract) =
        match (a, b) with
        | (Nil, x) -> x
        | (x, Nil) -> x
        | (x, y) -> Binary (x, y)

    /// Gets a retract operation that invokes an action.
    static member FromAction (action : Action) = Single action.Invoke

/// A mutable linked list that allows the use of retract operations to remove items.
type Registry<'a> () =
    inherit LinkedList<'a> ()

    /// Adds an item to this registery and returns a retract operation to later remove it.
    member this.Add (item : 'a) =
        let node = this.AddFirst item
        Retract.Single (fun () -> this.Remove node)

    /// Performs an action on all items in this registry, while still allowing items to be added or removed.
    member this.Forall (action : 'a -> unit) =
        let mutable cur = this.First
        while cur <> null do
            let next = cur.Next
            action cur.Value
            cur <- next