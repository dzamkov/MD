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

/// A manually-controlled temporary handle.
type ControlTemporary<'a> (value : 'a) =
    let mutable valid = true
    let mutable locks = 0
    let wait = new ManualResetEvent (false)

    /// Gets wether this temporary handle is currently locked.
    member this.Locked = locks > 0

    /// Blocks the current thread until this handle is no longer locked, then sets it as invalid. If the handle is
    /// already invalid, nothing happens.
    member this.Invalidate () = 
        while valid do
            Monitor.Enter this
            if locks <= 0 then
                valid <- false
                Monitor.Exit this
            else
                Monitor.Exit this
                wait.Reset () |> ignore
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
            Monitor.Exit this
            wait.Set () |> ignore

    interface IDisposable with
        member this.Dispose () = wait.Dispose ()

/// A handle to a temporary value based on the value of a source handle.
type MapTemporary<'a, 'b> (source : 'b temp, map : 'b -> 'a) =
    interface Temporary<'a> with
        member this.Lock () = Option.map map (source.Lock ())
        member this.Unlock () = source.Unlock ()

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