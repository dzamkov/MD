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