namespace MD

/// An immutable mapping from one type to another that can be directly evaluated and can be analyzed
/// using dynamic casting and type information.
[<AbstractClass>]
type Map<'a, 'b> () = 

    /// Gets the value for an item in this map.
    abstract member Get : 'a -> 'b

    /// Gets the value for an item in this map.
    member this.Item with get param = this.Get param

/// A mapping from a function.
type FuncMap<'a, 'b> (func : 'a -> 'b) =
    inherit Map<'a, 'b> ()
    override this.Get param = func param

/// A composition of two mappings.
type ComposeMap<'a, 'b, 'c> (first : Map<'a, 'b>, second : Map<'b, 'c>) =
    inherit Map<'a, 'c> ()
    override this.Get param = second.[first.[param]]

/// Contains functions and methods for manipulating mappings.
module Map =

    /// Constructs a mapping from a (consistent) function.
    let func func = new FuncMap<'a, 'b> (func) :> Map<'a, 'b>

    /// Composes two mappings.
    let compose first second = new ComposeMap<'a, 'b, 'c> (first, second) :> Map<'a, 'c>

    /// Applies a mapping function to a mapping (same as compose, but with arguments swapped).
    let map second first = new ComposeMap<'a, 'b, 'c> (first, second) :> Map<'a, 'c>

    /// Matches a mapping for a function representation.
    let (|Func|_|) (map : Map<'a, 'b>) =
        match map with
        | :? FuncMap<'a, 'b> as map -> Some map
        | _ -> None

    /// Matches a mapping for a composite representation.
    let (|Compose|_|) (map : Map<'a, 'b>) =
        match map with
        | :? ComposeMap<'a, 'c, 'b> as map -> Some map
        | _ -> None