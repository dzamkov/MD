namespace MD

open System

/// A shared, reference-counted handle to an exclusive object. The object will be released
/// when all shared handles to it have been released.
type Shared<'a> (source : 'a exclusive) =
    inherit Exclusive<'a> (source.Object)
    let mutable count = 1

    /// Gets the source handle for this shared handle.
    member this.Source = source

    /// Splits this shared handle to get another shared reference to its object.
    member this.Split () =
        count <- count + 1
        this

    override this.Finish () =
        count <- count - 1
        if count <= 0 then
            source.Finish ()

// Create type abbreviation.
type 'a shared = Shared<'a>

/// Contains functions for constructing and manipulating shared handles.
module Shared =

    /// Gets a shared handle for the given exclusive handle.
    let share (handle : 'a exclusive) =
        match handle with
        | :? Shared<'a> as shared -> shared
        | _ -> new Shared<'a> (handle)

    /// Splits a shared handle to get another handle for its object.
    let split (handle : 'a shared) = handle.Split ()