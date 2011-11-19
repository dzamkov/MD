namespace MD.Data

open System
open Microsoft.FSharp.NativeInterop

/// An interface to a reader for items of a certain type.
type Stream<'a> =

    /// Tries reading a single item from this stream. Returns false if the
    /// end of the stream has been reached.
    abstract member Read : item : 'a byref -> bool

    /// Copies items from this stream into a native array and returns the amount of
    /// items read. If the returned amount is under the requested size, the end of
    /// the stream has been reached.
    abstract member Read : buffer : 'a[] * size : int * offset : int -> int

    /// Copies items from this stream to the given memory location and returns the amount
    /// of items read. If the returned amount is under the requested size, the end of
    /// the stream has been reached. This should only be used for streams of value types.
    abstract member Read : destination : nativeint * size : int -> int

/// A mutable collection of items indexed by an integer.
type Data<'a> = 

    /// Gets the current size of the array.
    abstract member Size : int

    /// Gets the current value of the array at the given index.
    abstract member Item : index : int -> 'a with get

    /// Creates a stream to read this array beginning at the given index. The given
    /// size sets a limit on the amount of items that can be read from the resulting
    /// stream, but does not ensure the stream will end after the given amount of items
    /// are read.
    abstract member Read : start : int * size : int -> Stream<'a>