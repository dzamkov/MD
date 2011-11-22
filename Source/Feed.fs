namespace MD

open System

/// An interface to a source of discrete events of a certain type.
type EventFeed<'a> =

    /// Registers a callback to be called whenever an event occurs in this
    /// feed. The returned retract action can be used to remove the callback, but
    /// is not guranteed to stop calls to it.
    abstract member Register : Action<'a> -> RetractAction

/// Contains information about a change in a value of a certain type.
type Change<'a> = {
    Old : 'a
    New : 'a
    }

/// An interface to a dynamic value of a certain type.
type SignalFeed<'a> =

    /// Gets the current value of this feed. Note that the returned value will not be influenced
    /// by future changes to the signal feed; it acts as an immutable snapshot of the feed
    /// at the current moment.
    abstract member Current : 'a

    /// Gets an event feed that fires an event whenever a change occurs in this signal feed, or
    /// None if this feed does not change at discrete moments. During an event fired by the returned
    /// feed, this signal will use the new value as the current value.
    abstract member Delta : EventFeed<Change<'a>> option

/// An action that registers callbacks and objects on an item and returns a retract action
/// to later revert the modifications.
type RegisterItemAction<'a> = delegate of 'a -> RetractAction

/// An interface to a dynamic collection of objects of a certain type.
type CollectionFeed<'a> =

    /// Registers a callback to be called on every current item in the collection, and every
    /// time an item is added. When an item is removed, the retract action returned by the corresponding
    /// register item action will be called. When the returned retract action of this method is called,
    /// the retract action of every registered item will be called.
    abstract member Register : RegisterItemAction<'a> -> RetractAction


/// An event feed that never fires an event.
type NullEventFeed<'a> private () =
    static let instance = new NullEventFeed<'a> ()

    /// Gets the only instance of this type.
    static member Instance = instance

    interface EventFeed<'a> with
        member this.Register x = null

/// A signal feed that maintains a constant value.
type ConstSignalFeed<'a> (value : 'a) =

    /// Gets the value of this constant signal feed.
    member this.Value = value
    
    interface SignalFeed<'a> with
        member this.Current = value
        member this.Delta = Some (NullEventFeed<Change<'a>>.Instance :> EventFeed<Change<'a>>)

/// An event feed that allows events to be manually fired.
type ControlEventFeed<'a> () =
    let mutable callback : Action<'a> = null

    /// Fires the given event in this feed.
    member this.Fire event =
        match callback with
        | null -> ()
        | x -> x.Invoke event

    interface EventFeed<'a> with
        member this.Register x = 
            callback <- Delegate.Combine (callback, x) :?> Action<'a>
            RetractAction (fun () -> callback <- Delegate.Remove (callback, x) :?> Action<'a>)

/// A signal feed that maintains a manually set value.
type ControlSignalFeed<'a> (initial : 'a) =
    let mutable value : 'a = initial
    let delta = new ControlEventFeed<Change<'a>> ()

    /// Gets or sets the current value of this feed.
    member this.Current
        with get () = value
        and set x =
            let change = { Old = value; New = x }
            value <- x
            delta.Fire change

    interface SignalFeed<'a> with
        member this.Current = this.Current
        member this.Delta = Some (delta :> EventFeed<Change<'a>>)
    

/// Contains functions for constructing and manipulating feeds.
module Feed =
    
    /// An event feed that never fires.
    let ``null``<'a> = NullEventFeed<'a>.Instance :> EventFeed<'a>

    /// Constructs a signal feed with a constant value.
    let ``const`` value = new ConstSignalFeed<'a> (value)