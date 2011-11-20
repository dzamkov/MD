namespace MD

open System.Collections.Generic

/// A callback that can be used to retract a previously submitted object, modification or callback. If a
/// method returns a retract action of null, then no modification were made, and no retraction is required.
type RetractAction = delegate of unit -> unit

/// A mutable linked list that allows the use of RetractAction's to remove items.
type Registry<'a> () =
    inherit LinkedList<'a> ()

    /// Adds an item to this registery and returns a retract action to later remove it.
    member this.Add (item : 'a) =
        let node = this.AddFirst item
        RetractAction(fun () -> this.Remove node)