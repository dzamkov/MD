namespace MD

open System
open System.Threading

/// A temporary handle to an immutable value.
type Temporary<'a> =

    /// Tries getting the value for the handle and prevents the handle from being invalidated before unlocking. If this
    /// returns None, the handle has been invalidated and it is no longer possible to retrieve its value.
    abstract member Lock : unit -> 'a option

    /// Releases the handle after locking. Note that the value obtained from the corresponding Lock call may
    /// no longer be used after unlocking.
    abstract member Unlock : unit -> unit

// Create type abbreviation
type 'a temp = Temporary<'a>

/// A temporary handle to a static value that can never be invalidated.
type StaticTemporary<'a> (value : 'a) =
    interface Temporary<'a> with
        member this.Lock () = Some value
        member this.Unlock () = ()

/// A manually-controlled temporary handle. Note that a ManualResetEvent must be provided to manage
/// threading.
type ControlTemporary<'a> (value : 'a, wait : ManualResetEvent) =
    let mutable valid = true
    let mutable locks = 0

    /// Gets wether this temporary handle is currently locked.
    member this.Locked = locks > 0

    /// Gets wether this temporary handle is still valid.
    member this.Valid = valid

    /// Blocks the current thread until this handle is no longer locked, then sets it as invalid. If the handle is
    /// already invalid, nothing happens.
    member this.Invalidate () = 
        while valid do
            Monitor.Enter this
            if locks <= 0 then
                valid <- false
                Monitor.Exit this
            else
                wait.Reset () |> ignore
                Monitor.Exit this
                wait.WaitOne () |> ignore

    interface Temporary<'a> with
        member this.Lock () = 
            let mutable res = None
            Monitor.Enter this
            if valid then
                locks <- locks + 1
                res <- Some value
            Monitor.Exit this
            res

        member this.Unlock () =
            Monitor.Enter this
            locks <- locks - 1
            wait.Set () |> ignore
            Monitor.Exit this

/// A handle to a temporary value based on the value of a source handle.
type MapTemporary<'a, 'b> (source : 'b temp, map : 'b -> 'a) =
    interface Temporary<'a> with
        member this.Lock () = Option.map map (source.Lock ())
        member this.Unlock () = source.Unlock ()

/// A signal feed that gives values using a series of manually-controlled temporary objects. This is useful
/// for signals that need to give large, complex values that change often; instead of creating an immutable object
/// for each value, the signal gives temporary handles to a mutable object that changes with the signal.
/// Note that a ManualResetEvent must be provided to manage threading.
type TemporarySignalFeed<'a> (wait : ManualResetEvent) =
    let mutable cur = Unchecked.defaultof<ControlTemporary<'a>>
    let delta = new ControlEventFeed<'a temp change> ()

    /// Blocks the current thread until the previous temporary handle is no longer locked, then sets it as
    /// invalid. This allows changes to be made to the source object(s) of this signal.
    member this.Invalidate () = cur.Invalidate ()

    /// Publishes the next value for the signal feed. This new value will be available until the next call to
    /// invalidate. If the value of the feed is requested between calls to Invalidate and Publish, the requesting thread
    /// will be blocked until Publish is called (this implies that the time between calls to Invalidate and Publish should be 
    /// minimized). Note that this must be called to set the initial value of the signal.
    member this.Publish (value : 'a) =
        let last = cur
        cur <- new ControlTemporary<'a> (value, wait)
        wait.Set () |> ignore

        // Fire change event if needed.
        if last <> Unchecked.defaultof<ControlTemporary<'a>> then
            delta.Fire { Old = last; New = cur }

    interface SignalFeed<'a temp> with
        member this.Current =
            while not cur.Valid do
                wait.WaitOne () |> ignore
                wait.Reset() |> ignore
            cur :> 'a temp
        member this.Delta = Some (delta :> 'a temp change event)

/// Contains functions for constructing and manipulating temporary handles.
module Temp =

    /// Creates a temporary handle for a static value.
    let ``static`` value = new StaticTemporary<'a> (value) :> 'a temp

    /// Maps a value in a temporary handle.
    let map map source = new MapTemporary<'b, 'a> (source, map) :> 'b temp

    /// Tries using the given handle to perform an action, or does nothing if the handle is already invalid.
    let tryUse action (handle : 'a temp) =
        match handle.Lock () with
        | Some x ->
            action x
            handle.Unlock ()
        | None -> ()