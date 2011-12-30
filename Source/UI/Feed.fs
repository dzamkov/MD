namespace MD.UI

open MD
open System
open System.Threading
open System.Collections.Generic

/// An interface to a source of discrete events of a certain type.
[<AbstractClass>]
type EventFeed<'a> () =

    /// Registers a callback to be called whenever an event occurs in this
    /// feed. The returned retract operation can be used to remove the callback, but
    /// is not guranteed to stop calls to it.
    abstract member Register : ('a -> unit) -> Retract

    /// Registers a callback to be called at most once, the next time this event occurs.
    member this.RegisterOnce callback = 
        let retract = ref Retract.Nil
        let newCallback event =
            (!retract).Invoke ()
            retract := Retract.Nil
            callback event
        retract := this.Register callback

/// Contains information about a change in a value of a certain type.
type Change<'a> = {
    Old : 'a
    New : 'a
    }

/// An interface to a dynamic value of a certain type.
[<AbstractClass>]
type SignalFeed<'a> () =

    /// Gets the current value of this feed. Note that the returned value will not be influenced
    /// by future changes to the signal feed; it acts as an immutable snapshot of the feed
    /// at the current moment.
    abstract member Current : 'a

    /// Gets an event feed that fires an event for every change that occurs in this signal feed, or None 
    /// if not possible (the signal changes continuously or it is derived from another signal with unknown properties).
    /// During an event fired by the returned feed, this signal will use the new value as the current value. Note that 
    /// this feed may be fired even when there is no change, in which case, New and Old on the change will be the same.
    abstract member Delta : EventFeed<Change<'a>> option
    default this.Delta = None

// Create type abbreviations
type 'a change = Change<'a>
type 'a signal = SignalFeed<'a>
type 'a event = EventFeed<'a>

/// An event feed that never fires an event.
[<Sealed>]
type NullEventFeed<'a> private () =
    inherit EventFeed<'a> ()
    static let instance = new NullEventFeed<'a> ()

    /// Gets the only instance of this type.
    static member Instance = instance

    override this.Register _ = Retract.Nil

/// A signal feed that maintains a constant value.
[<Sealed>]
type ConstSignalFeed<'a> (value : 'a) =
    inherit SignalFeed<'a> ()

    /// Gets the value of this constant signal feed.
    member this.Value = value
    
    override this.Current = value
    override this.Delta = Some (NullEventFeed<Change<'a>>.Instance :> 'a change event)

/// An event feed that allows events to be manually fired.
[<Sealed>]
type ControlEventFeed<'a> () =
    inherit EventFeed<'a> ()
    let callbacks = new Registry<'a -> unit> ()

    /// Fires the given event in this feed.
    member this.Fire event = callbacks.Forall (fun a -> a event)

    override this.Register callback = callbacks.Add callback

/// A signal feed that maintains a manually set value.
[<Sealed>]
type ControlSignalFeed<'a> (initial : 'a) =
    inherit SignalFeed<'a> ()
    let mutable value : 'a = initial
    let delta = new ControlEventFeed<'a change> ()

    /// Updates the value for this signal feed.
    member this.Update newValue = 
        let change = { Old = value; New = newValue }
        value <- newValue
        delta.Fire change

    override this.Current = value
    override this.Delta = Some (delta :> 'a change event)

/// A signal feed that performs a query operation to get the current value of the signal.
[<Sealed>]
type QuerySignalFeed<'a> (query : unit -> 'a) =
    inherit SignalFeed<'a> ()

    override this.Current = query ()

/// An event feed that applies a mapping and filter to all events from a source feed.
[<Sealed>]
type MapFilterEventFeed<'a, 'b> (source : 'b event, map : 'b -> 'a option) =
    inherit EventFeed<'a> ()
    let mapCallback callback event =
        match map event with
        | Some x -> callback x
        | None -> ()

    override this.Register callback = source.Register (mapCallback callback)

/// A signal feed that applies a mapping to values from a source feed.
[<Sealed>]
type MapSignalFeed<'a, 'b> (source : 'b signal, map : 'b -> 'a) =
    inherit SignalFeed<'a> ()

    override this.Current = map source.Current
    override this.Delta = 
        let mapchange (x : Change<'b>) = Some { Old = map x.Old; New = map x.New }
        match source.Delta with
        | Some sourcedelta -> Some (new MapFilterEventFeed<Change<'a>, Change<'b>> (sourcedelta, mapchange) :> Change<'a> event)
        | None -> None

/// An event feed that combines events from two source feeds.
[<Sealed>]
type UnionEventFeed<'a> (sourceA : 'a event, sourceB : 'a event) =
    inherit EventFeed<'a> ()

    override this.Register callback = sourceA.Register callback + sourceB.Register callback

/// An event feed that tags events with a value from the given signal.
[<Sealed>]
type TagEventFeed<'a, 'b> (event : 'a event, signal : 'b signal) =
    inherit EventFeed<'a * 'b> ()
    let tagCallback callback event = callback (event, signal.Current)

    override this.Register callback = event.Register (tagCallback callback)

/// A signal feed that collates two signals into a tuple signal.
[<Sealed>]
type CollateSignalFeed<'a, 'b> (sourceA : 'a signal, sourceB : 'b signal) =
    inherit SignalFeed<'a * 'b> ()

    override this.Current = (sourceA.Current, sourceB.Current)
    override this.Delta = 
        match sourceA.Delta, sourceB.Delta with
        | (Some a, Some b) ->
            let amap (change : 'a change, b) = Some { Old = (change.Old, b); New = (change.New, b) }
            let bmap (change : 'b change, a) = Some { Old = (a, change.Old); New = (a, change.New) }
            let atagged = new TagEventFeed<'a change, 'b> (a, sourceB)
            let btagged = new TagEventFeed<'b change, 'a> (b, sourceA)
            let amapped = new MapFilterEventFeed<('a * 'b) change, 'a change * 'b> (atagged, amap)
            let bmapped = new MapFilterEventFeed<('a * 'b) change, 'b change * 'a> (btagged, bmap)
            Some (new UnionEventFeed<('a * 'b) change> (amapped, bmapped) :> ('a * 'b) change event)
        | _ -> None

/// An event feed that polls changes in a source feed on program updates.
[<Sealed>]
type ChangePollEventFeed<'a when 'a : equality> (source : 'a signal) =
    inherit EventFeed<'a change> ()
    let mutable last = source.Current
    let mutable retractUpdate : Retract = Retract.Nil
    let callbacks : Registry<'a change -> unit> = new Registry<'a change -> unit> ()

    // Polling function
    let poll time =
        let cur = source.Current
        if last <> cur then 
            let change = { Old = last; New = cur }
            for callback in callbacks do
                callback change
        last <- cur

    override this.Register callback = 
        if callbacks.Count = 0 then retractUpdate <- Update.register poll
        let retractCallback = callbacks.Add callback
        let retract () =
            retractCallback.Invoke ()
            if callbacks.Count = 0 then retractUpdate.Invoke ()
        Retract.Single retract

/// A signal feed that gives the current time in seconds.
[<Sealed>]
type TimeSignalFeed private () =
    inherit SignalFeed<float> ()
    static let instance = new TimeSignalFeed ()
    let mutable time = 0.0
    let update x = time <- time + x
    do Update.register update |> ignore

    /// Gets the only instance of this type.
    static member Instance = instance

    override this.Current = time
    override this.Delta = None

/// A signal feed that gives a compound value, a value that is composed of independant, indexed, homogeneously-typed elements
/// whose signals can be individually accessed.
[<AbstractClass>]
type CompoundSignalFeed<'a, 'b> () =
    inherit SignalFeed<'a -> 'b> ()

    /// Gets the signal for the given element of this feed.
    abstract member GetElementSignal : 'a -> 'b signal

/// A compound signal feed that gives an alias (mapping) to each element index.
[<Sealed>]
type AliasCompoundSignalFeed<'a, 'b, 'c> (source : CompoundSignalFeed<'c, 'b>, alias : 'a -> 'c) =
    inherit CompoundSignalFeed<'a, 'b> ()

    override this.Current = alias >> source.Current
    override this.Delta = 
        let mapChange change = Some { Old = alias >> change.Old; New = alias >> change.New }
        match source.Delta with
        | Some delta -> Some (new MapFilterEventFeed<('a -> 'b) change, ('c -> 'b) change> (delta, mapChange) :> ('a -> 'b) change event)
        | None -> None
    override this.GetElementSignal index = source.GetElementSignal (alias index)

/// Contains functions for constructing and manipulating feeds.
module Feed =
    
    /// An event feed that never fires.
    let nil<'a> = NullEventFeed<'a>.Instance :> 'a event

    /// Constructs a signal feed with a constant value.
    let constant value = new ConstSignalFeed<'a> (value) :> 'a signal

    /// Constructs a signal feed that performs a query operation to get its current value.
    let query query = new QuerySignalFeed<'a> (query) :> 'a signal

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

    /// Constructs a filtered event feed.
    let filter pred source = new MapFilterEventFeed<'a, 'a> (source, fun x -> if pred x then (Some x) else None) :> 'a event

    /// Constructs a mapped and filtered event feed.
    let mapfilter map source = new MapFilterEventFeed<'b, 'a> (source, map) :> 'b event

    /// Replaces the information for events from the given event feed with the given value.
    let replace value source = new MapFilterEventFeed<'a, 'b> (source, fun x -> Some value) :> 'a event

    /// Stips event information from an event feed.
    let strip source = replace () source

    /// Combines two event feeds.
    let union a b = new UnionEventFeed<'a> (a, b) :> 'a event

    /// Collates two signal feeds into a tuple.
    let collate a b = new CollateSignalFeed<'a, 'b> (a, b) :> ('a * 'b) signal

    /// Tags an event feed with values from a signal feed.
    let tag a b = new TagEventFeed<'a, 'b> (a, b) :> ('a * 'b) event

    /// Gets an event feed that fires when a change occurs in the source signal feed. If it is not possible to determine
    /// exactly when a change occurs, the source will be polled on every program-wide update, and an event will be fired
    /// when a change occurs. Note that this may allow some changes to slip through, if the signal changes and returns to
    /// its original value in-between program updates.
    let delta (source : 'a signal) : 'a change event =
        match source.Delta with
        | Some sourcedelta -> mapfilter (fun x -> if x.Old <> x.New then Some x else None) sourcedelta
        | None -> new ChangePollEventFeed<'a> (source) :> 'a change event

    /// Constructs an event feed that fires whenever the source signal changes, giving the new value.
    let change source = mape (fun x -> x.New) (delta source)

    /// Constructs an event feed that fires whenever the source signal changes from false to true.
    let rising source = mapfilter (fun x -> if x.New = true then Some () else None) (delta source)

    /// Constructs an event feed that fires whenever the source signal changes from true to false.
    let falling source = mapfilter (fun x -> if x.New = false then Some () else None) (delta source)

    /// Gives an alias to the parameters of a function (or compound) signal.
    let alias (alias : 'a -> 'b) (source : ('b -> 'c) signal) = 
        match source with
        | :? CompoundSignalFeed<'b, 'c> as source -> new AliasCompoundSignalFeed<'a, 'c, 'b> (source, alias) :> ('a -> 'c) signal
        | source -> maps (fun value -> alias >> value) source

    /// Gets an indexed element from a compound signal.
    let element index (source : ('a -> 'b) signal) = 
        match source with 
        | :? CompoundSignalFeed<'a, 'b> as source -> source.GetElementSignal index
        | source -> maps (fun value -> value index) source