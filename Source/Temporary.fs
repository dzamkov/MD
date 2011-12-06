namespace MD

open System
open System.Threading

/// A temporary handle to an immutable value.
type Temporary<'a> =

    /// Tries accessing the value for the handle. If successful, an exclusive handle to the value will be returned, and the
    /// temporary handle will be prevented from being invalidated until the exclusive handle is released. If this returns None,
    /// the handle has already been invalidate and it is no longer possible to retrieve its value.
    abstract member Access : unit -> 'a exclusive option

// Create type abbreviation
type 'a temp = Temporary<'a>

/// A temporary handle to a static value that can never be invalidated.
type ReturnTemporary<'a> (value : 'a) =
    interface Temporary<'a> with
        member this.Access () = Some (Exclusive.make value)

/// A manually-controlled temporary handle. Note that a ManualResetEvent must be provided to manage
/// threading.
type ControlTemporary<'a> (value : 'a, wait : ManualResetEvent) =
    let mutable valid = true
    let mutable locks = 0

    /// Gets wether this temporary handle is currently locked (has active access handles).
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
        member this.Access () =
            let lock () =
                Monitor.Enter this
                if valid then
                    locks <- locks + 1
                    Monitor.Exit this
                    Some value
                else None

            let unlock () =
                Monitor.Enter this
                locks <- locks - 1
                wait.Set () |> ignore
                Monitor.Exit this

            match lock () with
            | None -> None
            | Some value -> Some (Exclusive.custom unlock value)

/// Contains functions for constructing and manipulating temporary handles.
module Temp =

    /// Creates a temporary handle for a static value.
    let make value = new ReturnTemporary<'a> (value) :> 'a temp

    /// Tries using the given temporary handle to perform an action, or does nothing if the handle is already invalid.
    let tryUse action (handle : 'a temp) =
        match handle.Access () with
        | Some x ->
            action x.Object
            x.Finish ()
        | None -> ()