namespace MD

/// An identifier based on a reference object. Identifiers that reference the same object are
/// considered equivalent.
[<CustomEquality;NoComparison>]
type Identifier (obj : obj) =
    struct

        /// Creates an identifier for the given object.
        static member Create obj = new Identifier (obj :> obj)

        /// Creates a new unique identifier.
        static member Unique () = new Identifier (new obj ())

        /// Gets the object for an identifier.
        static member (!!) (a : Exclusive<'a>) = a.Object

        /// Gets the object for this identifier. Since identifiers are meant only for equality testing, this
        /// should be used sparingly.
        member this.Object = obj

        override this.Equals other =
            match other with
            | :? Identifier as id -> this.Object = id.Object
            | _ -> false

        override this.GetHashCode () =
            // This gets a hashcode for the reference of an object and ignores any overrides of GetHashCode.
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode obj
    end

// Create type abbreviation.
type identifier = Identifier