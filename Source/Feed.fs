namespace MD

open System
open System.Threading
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

    /// Gets an event feed that fires an event for every change that occurs in this signal feed, or None 
    /// if not possible (the signal changes continuously or it is derived from another signal with unknown properties).
    /// During an event fired by the returned feed, this signal will use the new value as the current value. Note that 
    /// this feed may be fired even when there is no change, in which case, 
    /// New and Old on the given change will be the same.
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

// Create type abbreviations
type 'a change = Change<'a>
type 'a signal = SignalFeed<'a>
type 'a event = EventFeed<'a>
type 'a collection = CollectionFeed<'a>

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
        member this.Delta = Some (NullEventFeed<Change<'a>>.Instance :> 'a change event)

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
    let delta = new ControlEventFeed<'a change> ()

    /// Gets or sets the current value of this feed.
    member this.Current
        with get () = value
        and set x =
            let change = { Old = value; New = x }
            value <- x
            delta.Fire change

    interface SignalFeed<'a> with
        member this.Current = this.Current
        member this.Delta = Some (delta :> 'a change event)

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
type MapFilterEventFeed<'a, 'b> (source : 'b event, map : 'b -> 'a option) =
    interface EventFeed<'a> with
        member this.Register callback = source.Register (fun x -> 
            match map x with
            | Some y -> callback.Invoke y
            | None -> ())

/// A signal feed that applies a mapping to values from a source feed.
type MapSignalFeed<'a, 'b> (source : 'b signal, map : 'b -> 'a) =
    interface SignalFeed<'a> with
        member this.Current = map source.Current
        member this.Delta = 
            let mapchange (x : Change<'b>) = Some { Old = map x.Old; New = map x.New }
            match source.Delta with
            | Some sourcedelta -> Some (new MapFilterEventFeed<Change<'a>, Change<'b>> (sourcedelta, mapchange) :> Change<'a> event)
            | None -> None

/// A collection feed that applies a mapping and filter to all items from a source feed.
type MapFilterCollectionFeed<'a, 'b> (source : 'b collection, map : 'b -> 'a option) =
    interface CollectionFeed<'a> with
        member this.Register callback = source.Register (fun x ->
            match map x with
            | Some y -> callback.Invoke y
            | None -> null)

/// An event feed that combines events from two source feeds.
type UnionEventFeed<'a> (sourceA : 'a event, sourceB : 'a event) =
    interface EventFeed<'a> with
        member this.Register callback = Delegate.Combine (sourceA.Register callback, sourceB.Register callback) :?> RetractAction

/// A collection feed that combines items from two source feeds.
type UnionCollectionFeed<'a> (sourceA : 'a collection, sourceB : 'a collection) =
    interface CollectionFeed<'a> with
        member this.Register callback = Delegate.Combine (sourceA.Register callback, sourceB.Register callback) :?> RetractAction

/// A signal feed that collates two signals into a tuple signal.
type CollateSignalFeed<'a, 'b> (sourceA : 'a signal, sourceB : 'b signal) =
    interface SignalFeed<'a * 'b> with
        member this.Current = (sourceA.Current, sourceB.Current)
        member this.Delta = new NotImplementedException () |> raise

/// An event feed that polls changes in a source feed on program updates.
type ChangePollEventFeed<'a when 'a : equality> (source : 'a signal) =
    let mutable last = source.Current
    let mutable callback : Action<'a change> = null
    let mutable retractUpdate : RetractAction = null

    // Polling function
    let poll time =
        let cur = source.Current
        if last <> cur && callback <> null then callback.Invoke { Old = last; New = cur }
        last <- cur

    interface EventFeed<Change<'a>> with
        member this.Register x =
            if callback = null then retractUpdate <- Update.register poll
            callback <- Delegate.Combine (callback, x) :?> Action<'a change>
            let retract () = 
                callback <- Delegate.Remove (callback, x) :?> Action<'a change>
                if callback = null then retractUpdate.Invoke ()
            RetractAction retract

/// A signal feed that gives the current time in seconds.
type TimeSignalFeed private () =
    static let instance = new TimeSignalFeed ()
    let mutable time = 0.0
    let update x = time <- time + x
    do Update.register update |> ignore

    /// Gets the only instance of this type.
    static member Instance = instance

    interface SignalFeed<double> with
        member this.Current = time
        member this.Delta = None

/// Contains functions for constructing and manipulating feeds.
module Feed =
    
    /// An event feed that never fires.
    let nil<'a> = NullEventFeed<'a>.Instance :> 'a event

    /// Constructs a signal feed with a constant value.
    let constant value = new ConstSignalFeed<'a> (value) :> 'a signal

    /// A signal feed that gives the amount of real-world time, in seconds, that has
    /// elapsed since the start of the program.
    let time = TimeSignalFeed.Instance :> double signal

    /// Constructs an event feed for a native event source.
    let native (source : IEvent<'a, 'b>) =
        let control = new ControlEventFeed<'b> ()
        source.Add control.Fire
        control :> 'b event

    /// Constructs a mapped event feed.
    let mape map source = new MapFilterEventFeed<'b, 'a> (source, map >> Some) :> 'b event

    /// Constructs a mapped signal feed.
    let maps map source = new MapSignalFeed<'b, 'a> (source, map) :> 'b signal

    /// Constructs a mapped collection feed.
    let mapc map source = new MapFilterCollectionFeed<'b, 'a> (source, map >> Some) :> 'b collection

    /// Constructs a filtered event feed.
    let filtere pred source = new MapFilterEventFeed<'a, 'a> (source, fun x -> if pred x then (Some x) else None) :> 'a event

    /// Constructs a filtered collection feed.
    let filterc pred source = new MapFilterCollectionFeed<'a, 'a> (source, fun x -> if pred x then (Some x) else None) :> 'a collection

    /// Constructs a mapped and filtered event feed.
    let mapfiltere map source = new MapFilterEventFeed<'b, 'a> (source, map) :> 'b event

    /// Constructs a mapped and filtered collection feed.
    let mapfilterc map source = new MapFilterCollectionFeed<'b, 'a> (source, map) :> 'b collection

    /// Replaces the information for events from the given event feed with the given value.
    let replace value source = new MapFilterEventFeed<'a, 'b> (source, fun x -> Some value) :> 'a event

    /// Stips event information from an event feed.
    let strip source = replace () source

    /// Combines two event feeds.
    let unione a b = new UnionEventFeed<'a> (a, b) :> 'a event

    /// Combines two collection feeds.
    let unionc a b = new UnionCollectionFeed<'a> (a, b) :> 'a collection

    /// Collates two signal feeds into a tuple.
    let collate a b = new CollateSignalFeed<'a, 'b> (a, b) :> ('a * 'b) signal

    /// Gets an event feed that fires when a change occurs in the source signal feed. If it is not possible to determine
    /// exactly when a change occurs, the source will be polled on every program-wide update, and an event will be fired
    /// when a change occurs. Note that this may allow some changes to slip through, if the signal changes and returns to
    /// its original value in-between program updates.
    let delta (source : 'a signal) : 'a change event =
        match source.Delta with
        | Some sourcedelta -> mapfiltere (fun x -> if x.Old <> x.New then Some x else None) sourcedelta
        | None -> new ChangePollEventFeed<'a> (source) :> 'a change event

    /// Constructs an event feed that fires whenever the source signal changes, giving the new value.
    let change source = mape (fun x -> x.New) (delta source)

    /// Constructs an event feed that fires whenever the source signal changes from false to true.
    let rising source = mapfiltere (fun x -> if x.New = true then Some () else None) (delta source)

    /// Constructs an event feed that fires whenever the source signal changes from true to false.
    let falling source = mapfiltere (fun x -> if x.New = false then Some () else None) (delta source)