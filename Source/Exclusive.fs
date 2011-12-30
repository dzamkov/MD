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

// Create type abbreviation.
type 'a exclusive = Exclusive<'a>

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