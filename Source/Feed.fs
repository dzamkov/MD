namespace MD

open System
open System.Collections.Generic

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
    /// feed, this signal will use the new value as the current value. Note that this feed may be fired
    /// even when there is no change, in which case, New and Old on the given change will be the same.
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

/// A collection feed that allows items to be manually added or removed.
type ControlCollectionFeed<'a> () =
    let items = new LinkedList<'a> ()
    let callbacks = new LinkedList<RegisterItemAction<'a>> ()
    let relations = new LinkedList<LinkedListNode<'a> * LinkedListNode<RegisterItemAction<'a>> * RetractAction> ()

    /// Adds an item to this collection feed and returns a retract handler to later remove it.
    member this.Add (item : 'a) = 
        let itemnode = items.AddFirst item
        let mutable callbacknode = callbacks.First
        while callbacknode <> null do
            let retract = callbacknode.Value.Invoke itemnode.Value
            if retract <> null then relations.AddFirst ((itemnode, callbacknode, retract)) |> ignore
            callbacknode <- callbacknode.Next
        let remove () =
            items.Remove itemnode

            // Remove and retract all relations with itemnode.
            let mutable relationnode = relations.First
            while relationnode <> null do
                let relation = relationnode.Value
                match relation with
                | (x, _, retract) when x = itemnode ->
                    let nextnode = relationnode.Next
                    relations.Remove relationnode
                    relationnode <- nextnode
                    retract.Invoke ()
                | _ -> relationnode <- relationnode.Next
        RetractAction remove

    /// Registers a callback for this feed.
    member this.Register (callback : RegisterItemAction<'a>) =
        let callbacknode = callbacks.AddFirst callback
        let mutable itemnode = items.First
        while itemnode <> null do
            let retract = callbacknode.Value.Invoke itemnode.Value
            if retract <> null then relations.AddFirst ((itemnode, callbacknode, retract)) |> ignore
            itemnode <- itemnode.Next
        let remove () =
            callbacks.Remove callbacknode

            // Remove and retract all relations with itemnode.
            let mutable relationnode = relations.First
            while relationnode <> null do
                let relation = relationnode.Value
                match relation with
                | (_, x, retract) when x = callbacknode ->
                    let nextnode = relationnode.Next
                    relations.Remove relationnode
                    relationnode <- nextnode
                    retract.Invoke ()
                | _ -> relationnode <- relationnode.Next
        RetractAction remove

    interface CollectionFeed<'a> with
        member this.Register callback = this.Register callback

/// An event feed that applies a mapping and filter to all events from a source feed.
type MapFilterEventFeed<'a, 'b> (source : EventFeed<'b>, map : 'b -> 'a option) =
    interface EventFeed<'a> with
        member this.Register callback = source.Register (fun x -> 
            match map x with
            | Some y -> callback.Invoke y
            | None -> ())

/// A signal feed that applies a mapping to values from a source feed.
type MapSignalFeed<'a, 'b> (source : SignalFeed<'b>, map : 'b -> 'a) =
    interface SignalFeed<'a> with
        member this.Current = map source.Current
        member this.Delta = 
            let mapchange (x : Change<'b>) = Some { Old = map x.Old; New = map x.New }
            match source.Delta with
            | Some sourcedelta -> Some (new MapFilterEventFeed<Change<'a>, Change<'b>> (sourcedelta, mapchange) :> EventFeed<Change<'a>>)
            | None -> None

/// A collection feed that applies a mapping and filter to all items from a source feed.
type MapFilterCollectionFeed<'a, 'b> (source : CollectionFeed<'b>, map : 'b -> 'a option) =
    interface CollectionFeed<'a> with
        member this.Register callback = source.Register (fun x ->
            match map x with
            | Some y -> callback.Invoke y
            | None -> null)

/// An event feed that combines events from two source feeds.
type UnionEventFeed<'a> (sourceA : EventFeed<'a>, sourceB : EventFeed<'a>) =
    interface EventFeed<'a> with
        member this.Register callback = Delegate.Combine (sourceA.Register callback, sourceB.Register callback) :?> RetractAction

/// A collection feed that combines items from two source feeds.
type UnionCollectionFeed<'a> (sourceA : CollectionFeed<'a>, sourceB : CollectionFeed<'a>) =
    interface CollectionFeed<'a> with
        member this.Register callback = Delegate.Combine (sourceA.Register callback, sourceB.Register callback) :?> RetractAction

/// Contains functions for constructing and manipulating feeds.
module Feed =
    
    /// An event feed that never fires.
    let ``null``<'a> = NullEventFeed<'a>.Instance :> EventFeed<'a>

    /// Constructs a signal feed with a constant value.
    let ``const`` value = new ConstSignalFeed<'a> (value)

    /// Constructs an event feed for a native event source.
    let native (source : IEvent<'a, 'b>) =
        let control = new ControlEventFeed<'b> ()
        source.Add control.Fire
        control :> EventFeed<'b>

    /// Constructs a mapped event feed.
    let mape map source = new MapFilterEventFeed<'a, 'b> (source, map >> Some) :> EventFeed<'a>

    /// Constructs a mapped signal feed.
    let maps map source = new MapSignalFeed<'a, 'b> (source, map) :> SignalFeed<'a>

    /// Constructs a mapped collection feed.
    let mapc map source = new MapFilterCollectionFeed<'a, 'b> (source, map >> Some) :> CollectionFeed<'a>

    /// Constructs a filtered event feed.
    let filtere pred source = new MapFilterEventFeed<'a, 'a> (source, fun x -> if pred x then (Some x) else None) :> EventFeed<'a>

    /// Constructs a filtered collection feed.
    let filterc pred source = new MapFilterCollectionFeed<'a, 'a> (source, fun x -> if pred x then (Some x) else None) :> CollectionFeed<'a>

    /// Constructs a mapped and filtered event feed.
    let mapfiltere map source = new MapFilterEventFeed<'a, 'b> (source, map) :> EventFeed<'a>

    /// Constructs a mapped and filtered collection feed.
    let mapfilterc map source = new MapFilterCollectionFeed<'a, 'b> (source, map) :> CollectionFeed<'a>

    /// Replaces the information for events from the given event feed with the given value.
    let replace value source = new MapFilterEventFeed<'a, 'b> (source, fun x -> Some value) :> EventFeed<'a>

    /// Stips event information from an event feed.
    let strip source = replace () source

    /// Combines two event feeds.
    let unione a b = new UnionEventFeed<'a> (a, b)

    /// Combines two collection feeds.
    let unionc a b = new UnionCollectionFeed<'a> (a, b)

    /// Constructs an event feed that fires whenever the source signal changes, giving its the new value.
    let change (source : SignalFeed<'a>) =
        match source.Delta with
        | Some sourcedelta -> Some (mapfiltere (fun x -> if x.New <> x.Old then Some x.New else None) sourcedelta)
        | None -> None

    /// Constructs an event feed that fires whenever the source signal changes from false to true.
    let rising (source : SignalFeed<bool>) =
        match source.Delta with
        | Some sourcedelta -> Some (mapfiltere (fun x -> if x.Old = false && x.New = true then Some () else None) sourcedelta)
        | None -> None

    /// Constructs an event feed that fires whenever the source signal changes from true to false.
    let falling (source : SignalFeed<bool>) =
        match source.Delta with
        | Some sourcedelta -> Some (mapfiltere (fun x -> if x.Old = true && x.New = false then Some () else None) sourcedelta)
        | None -> None