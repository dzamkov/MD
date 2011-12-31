namespace MD

open System

open MD

/// An exclusive reference to an object with an obligation to invoke a release action when the object will
/// no longer be used.
type Exclusive<'a> (obj : 'a, release : ReleaseAction) =
    struct

        /// Gets the object for the given handle.
        static member (!!) (a : Exclusive<'a>) = a.Object

        /// Gets the object for this handle.
        member this.Object = obj

        /// Gets the release action for this handle.
        member this.Release = release

    end

/// A shared reference-counted handle to an exclusive object.
type Shared<'a> (handle : Exclusive<'a>) =
    let mutable count = 0

    /// Gets the object for the given handle.
    static member (!!) (a : Shared<'a>) = a.Object

    /// Gets the object for this handle.
    member this.Object = handle.Object

    /// Gets the current reference count for this handle.
    member this.Count = count

    /// Increments the reference count for this shared handle. Note that a shared handle starts with
    /// a reference count of 1.
    member this.Reference () = count <- count + 1 

    /// Decrements the reference count for this shared handle and releases the
    /// handle if the new count is zero.
    member this.Unreference () =
        count <- count - 1
        if count <= 0 then handle.Release.Invoke ()

    /// Gets an exclusive handle for this shared handle.
    member this.GetExclusiveHandle () =
        this.Reference ()
        new Exclusive<'a> (handle.Object, Action.Custom this.Unreference)

// Create type abbreviations.
type 'a exclusive = Exclusive<'a>
type 'a shared = Shared<'a>

/// Contains functions for constructing and manipulating exclusive handles.
module Exclusive =

    /// Gets the object for an exclusive handle.
    let get (handle : 'a exclusive) = handle.Object

    /// Creates an exclusive handle for an object that does nothing upon release.
    let make obj = new Exclusive<'a> (obj, Action.Nil) : 'a exclusive

    /// Creates an exclusive handle for a disposable object to be disposed upon release.
    let dispose obj =
        let disposable = 
            match obj :> Object with
            | :? IDisposable as obj -> obj
            | _ -> null
        let dispose () =
            match disposable with
            | null -> ()
            | disposable -> disposable.Dispose ()
        new Exclusive<'a> (obj, Action.Custom dispose) : 'a exclusive

    /// Creates an exclusive handle that calls the given function upon release.
    let custom release obj = new Exclusive<'a> (obj, Action.Custom release) : 'a exclusive

    /// Determines wether the given handle is static. If so, there is no need to call Finish on it.
    let isStatic (handle : 'a exclusive) = 
        match handle.Release with
        | Nil -> true
        | _ -> false

    /// Maps an exclusive handle.
    let map map (handle : 'a exclusive) =
        new Exclusive<'b> (map handle.Object, handle.Release) : 'b exclusive

    /// Monadically binds an exclusive handle.
    let bind map (handle : 'a exclusive) =
        let res = (map handle.Object) : 'b exclusive
        new Exclusive<'b> (res.Object, handle.Release + res.Release) : 'b exclusive

    /// Releases the given handle.
    let release (handle : 'a exclusive) = handle.Release.Invoke ()

    /// Creates a shared handle for the given exclusive handle.
    let share (handle : 'a exclusive) = new Shared<'a> (handle) : 'a shared