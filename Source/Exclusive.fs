namespace MD

open System

/// An exclusive handle to a value or object that will need to be released at some point. Exclusive handles
/// may not be shared or copied. They can be transfered by being passed as arguments or returned from methods,
/// in which case the original handle must be forgotten. A handle can only be destroyed after Finish is called
/// on it.
[<AbstractClass>]
type Exclusive<'a> (obj : 'a) =

    /// Dereferences an exclusive handle.
    static member (!!) (a : Exclusive<'a>) = a.Object

    /// Gets the object for this exclusive handle.
    member this.Object = obj

    /// Releases this handle.
    abstract member Finish : unit -> unit

// Create type abbreviation.
type 'a exclusive = Exclusive<'a>

/// An exclusive handle that does nothing upon release.
type StaticExclusive<'a> (obj : 'a) =
    inherit Exclusive<'a> (obj)
    override this.Finish () = ()

/// An exclusive handle to a disposable object to be disposed upon release.
type DisposeExclusive<'a> (obj : 'a) =
    inherit Exclusive<'a> (obj)
    override this.Finish () =
        match this.Object :> System.Object with
        | :? IDisposable as x -> x.Dispose()
        | _ -> ()

/// An exclusive handle that calls a given function upon release.
type CustomExclusive<'a> (obj : 'a, finish : unit -> unit) =
    inherit Exclusive<'a> (obj)
    override this.Finish () = finish ()

/// An exclusive handle that gives an object while separately managing another exclusive handle.
type MapExclusive<'a, 'b> (obj : 'a, sub : 'b exclusive) =
    inherit Exclusive<'a> (obj)
    override this.Finish () = sub.Finish ()

/// An exclusive handle that manages two others.
type CombineExclusive<'a, 'b, 'c> (obj : 'a, subA : 'b exclusive, subB : 'c exclusive) =
    inherit Exclusive<'a> (obj)
    override this.Finish () =
        subA.Finish ()
        subB.Finish ()

/// Contains functions for constructing and manipulating exclusive handles.
module Exclusive =

    /// Creates a static exclusive handle for an object.
    let ``static`` obj = new StaticExclusive<'a> (obj) :> 'a exclusive

    /// Creates an exclusive handle for a disposable object to be disposed upon release.
    let dispose obj = new DisposeExclusive<'a> (obj) :> 'a exclusive

    /// Creates an exclusive handle that calls the given function upon release.
    let custom finish obj = new CustomExclusive<'a> (obj, finish) :> 'a exclusive

    /// Combines two exclusive handles into one.
    let combine obj subA subB = new CombineExclusive<'a, 'b, 'c> (obj, subA, subB) :> 'a exclusive

    /// Determines wether the given handle is static. If so, there is no need to call Finish on it.
    let isStatic (handle : 'a exclusive) = 
        match handle with
        | :? StaticExclusive<'a> -> true
        | _ -> false

    /// Maps an exclusive handle.
    let map map (handle : 'a exclusive) =
        let res : 'b = map handle.Object
        new MapExclusive<'b, 'a> (res, handle) :> 'b exclusive

    /// Monadically binds an exclusive handle.
    let bind map (handle : 'a exclusive) =
        let res : 'b exclusive = map handle.Object
        new CombineExclusive<'b, 'b, 'a> (res.Object, res, handle) :> 'b exclusive

    /// Calls finish on the given handle.
    let finish (handle : 'a exclusive) = handle.Finish ()