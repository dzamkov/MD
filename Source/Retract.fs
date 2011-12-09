namespace MD

open System
open System.Collections.Generic

/// A callback that can be used to retract a previously submitted object, modification or callback. If a
/// method returns a retract action of null, then no modification were made, and no retraction is required.
type RetractAction = delegate of unit -> unit

/// Contains functions for constructing and manipulating RetractAction's.
module Retract =

    /// Gets a retract action that does nothing when invoked.
    let nil = null : RetractAction

    /// Combines two retract actions.
    let combine (a : RetractAction) (b : RetractAction) = Delegate.Combine (a, b) :?> RetractAction

    /// Invokes the given retract action.
    let invoke (action : RetractAction) = if action <> null then action.Invoke ()

/// A mutable linked list that allows the use of RetractAction's to remove items.
type Registry<'a> () =
    inherit LinkedList<'a> ()

    /// Adds an item to this registery and returns a retract action to later remove it.
    member this.Add (item : 'a) =
        let node = this.AddFirst item
        RetractAction (fun () -> this.Remove node)

    /// Performs an action on all items in this registry, while still allowing items to be added or removed.
    member this.Forall (action : 'a -> unit) =
        let mutable cur = this.First
        while cur <> null do
            let next = cur.Next
            action cur.Value
            cur <- next