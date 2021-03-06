﻿namespace MD

open Util
open System
open System.IO

/// An interface to a reader for items of a certain blittable type.
[<AbstractClass>]
type Stream<'a when 'a : unmanaged> (alignment : int) =

    /// Gets the alignment of this stream. This is the smallest amount of items the stream can
    /// individually access. All read operations should have a size that is some multiple of
    /// this integer.
    member this.Alignment = alignment

    /// Copies items from this stream into a array and returns the amount of
    /// items read. If the returned amount is under the requested size, the end of
    /// the stream has been reached.
    abstract member Read : array : 'a[] * offset : int * size : int -> int

/// A stream that reads from an array.
type ArrayStream<'a when 'a : unmanaged> (array : 'a[], offset : int) =
    inherit Stream<'a> (1)
    let mutable offset = offset

    /// Gets the array this stream is reading from.
    member this.Array = array

    /// Gets the current offset of the stream in the source array.
    member this.Offset = offset

    override this.Read (targetArray, targetOffset, size) =
        let readSize = min size (array.Length - offset)
        Array.blit array offset targetArray targetOffset readSize
        offset <- offset + readSize
        readSize

/// A stream that reads from a buffer.
[<Sealed>]
type BufferStream<'a when 'a : unmanaged> (buffer : Buffer<'a>) =
    inherit Stream<'a> (1)
    let mutable buffer = buffer

    /// Gets the current buffer this stream is reading from. Note that this buffer
    /// advances in memory location with each read operation.
    member this.Buffer = buffer

    override this.Read (array, offset, size) =
        Buffer.copyba buffer array offset size
        buffer <- buffer.Advance size
        size

/// A stream that limits the amount of items that can be read.
[<Sealed>]
type LimitStream<'a when 'a : unmanaged> (source : Stream<'a>, size : uint64) =
    inherit Stream<'a> (source.Alignment)
    let mutable size = size

    /// Gets the remaining size of this stream.
    member this.Size = size

    override this.Read (array, offset, readSize) =
        let readSize = source.Read (array, offset, int (min (uint64 readSize) (uint64 size)))
        size <- size - uint64 readSize
        readSize

/// A stream that reads through streams (called chunks) generated by a retrieve function.
[<Sealed>]
type ChunkStream<'a, 'b when 'a : unmanaged> (alignment : int, initial : (Stream<'a> exclusive * 'b) option, retrieve : 'b -> (Stream<'a> exclusive * 'b) option) =
    inherit Stream<'a> (alignment)
    let mutable current = initial
    new (alignment, initialState : 'b, retrieve : 'b -> (Stream<'a> exclusive * 'b) option) = new ChunkStream<'a, 'b> (alignment, retrieve initialState, retrieve)

    /// Gets the function used for retrieving chunks.
    member this.Retrieve = retrieve

    /// Releases this stream and all subordinate chunk streams.
    member this.Release () =
        match current with
        | Some (stream, _) -> stream.Release.Invoke ()
        | None -> ()

    override this.Read (array, offset, size) = 
        let rec read (array : 'a[], offset: int, size : int) (totalReadSize : int) =
            match current with
            | Some (stream, state) ->
                let readSize = stream.Object.Read (array, offset, size)
                if readSize < size then
                    stream.Release.Invoke ()
                    current <- retrieve state
                    read (array, offset + readSize, size - readSize) (totalReadSize + readSize)
                else totalReadSize + size
            | None -> totalReadSize
        read (array, offset, size) 0

/// A stream that maps items with a mapping function.
[<Sealed>]
type MapStream<'a, 'b when 'a : unmanaged and 'b : unmanaged> (source : Stream<'b>, map : 'b -> 'a) =
    inherit Stream<'a> (source.Alignment)

    override this.Read (array, offset, size) =
        let tempArray = Array.zeroCreate size
        let readSize = source.Read (tempArray, 0, tempArray.Length)
        for index = 0 to readSize - 1 do
            array.[offset + index] <- map tempArray.[index]
        readSize

/// A stream that combines fixed-size groups of items into single items.
[<Sealed>]
type CombineStream<'a, 'b when 'a : unmanaged and 'b : unmanaged> (source : Stream<'b>, groupSize : int, combine : 'b[] * int -> 'a) =
    inherit Stream<'a> (fit groupSize source.Alignment)

    override this.Read (array, offset, size) =
        let tempArray = Array.zeroCreate (size * groupSize)
        let readSize = source.Read (tempArray, 0, tempArray.Length)
        let readSize = readSize / groupSize
        for index = 0 to readSize - 1 do
            array.[offset + index] <- combine (tempArray, index * groupSize)
        readSize

/// A stream that splits single items into fixed-size groups.
[<Sealed>]
type SplitStream<'a, 'b when 'a : unmanaged and 'b : unmanaged> (source : Stream<'b>, groupSize : int, split : 'b * 'a[] * int -> unit) =
    inherit Stream<'a> (source.Alignment * groupSize)

    override this.Read (array, offset, size) =
        let tempArray = Array.zeroCreate (size / groupSize)
        let readSize = source.Read (tempArray, 0, tempArray.Length)
        for index = 0 to readSize - 1 do
            split (tempArray.[index], array, offset + index * groupSize)
        readSize * groupSize

/// A stream that cast items in a source stream by reinterpreting the byte representations of sequential items.
[<Sealed>]
type CastStream<'a, 'b when 'a : unmanaged and 'b : unmanaged> (source : Stream<'b>, asize : uint32, bsize : uint32) =
    inherit Stream<'a> (fit (int asize) (source.Alignment * int bsize))
    new (source) = new CastStream<'a, 'b> (source, uint32 sizeof<'a>, uint32 sizeof<'b>)
    
    override this.Read (array, offset, size) = 
        let sourceSize = size * int asize / int bsize
        let tempArray = Array.zeroCreate sourceSize
        let readSize = source.Read (tempArray, 0, sourceSize)
        
        let tempBuffer, unpinTemp = Buffer.PinArray tempArray
        let targetBuffer, unpinTarget = Buffer.PinArray array
        Buffer.copybb tempBuffer (targetBuffer.Cast ()) sourceSize
        unpinTemp ()
        unpinTarget ()

        readSize * int bsize / int asize

/// A byte stream based on a System.IO stream.
[<Sealed>]
type IOStream (source : Stream) =
    inherit Stream<byte> (1)

    /// Gets the System.IO stream source for this stream.
    member this.Source = source

    override this.Read (array, offset, size) = source.Read (array, offset, size)

/// Contains functions for constructing and manipulating streams.
module Stream =

    /// Reads the given amount of items from a stream into an array. If the stream does not have the requested
    /// amount of items, a smaller array of only the read items will be returned.
    let read size (stream : Stream<'a>) =
        let array = Array.zeroCreate size
        let readSize = stream.Read (array, 0, size)
        if readSize < size then
            let newArray = Array.zeroCreate readSize
            Array.blit array 0 newArray 0 readSize
            newArray
        else array

    /// Constructs a stream to read from a buffer. Note that the buffer is referenced directly and 
    /// changes to the buffer will be reflected in the stream.
    let buffer buffer = new BufferStream<'a> (buffer) :> Stream<'a>

    /// Constructs a stream to read from an array. Note that the array is referenced directly and 
    /// changes to the array will be reflected in the stream.
    let array array offset = new ArrayStream<'a> (array, offset) :> Stream<'a>

    /// Constructs a stream to read from the file at the given path.
    let file (path : MD.Path) =
        let fs = new FileStream (path.Source, FileMode.Open)
        let is = new IOStream (fs) :> Stream<byte>
        Exclusive.custom fs.Dispose is

    /// Constructs a stream that concatenates a series of chunks produced by the given retrieve function.
    let chunk alignment (initial : 'b) retrieve = 
        let cs = new ChunkStream<'a, 'b> (alignment, initial, retrieve)
        Exclusive.custom cs.Release (cs :> Stream<'a>)

    /// Constructs a stream that concatenates a series of chunks produced by the given retrieve function. The initial chunk
    /// must be provided when using this function.
    let chunkInit alignment (initial : (Stream<'a> exclusive * 'b) option) retrieve = 
        let cs = new ChunkStream<'a, 'b> (alignment, initial, retrieve)
        Exclusive.custom cs.Release (cs :> Stream<'a>)

    /// Constructs a mapped form of a stream.
    let map map source = new MapStream<'a, 'b> (source, map) :> Stream<'a>

    /// Constructs a stream that combines fixed-sized groups into single items.
    let combine groupSize group source = new CombineStream<'a, 'b> (source, groupSize, group) :> Stream<'a>

    /// Constructs a stream that splits single items into fixed-sized groups.
    let split groupSize split source = new SplitStream<'a, 'b> (source, groupSize, split) :> Stream<'a>

    /// Constructs a stream based on a source stream that reinterprets the byte representations of source items in order
    /// to form items of other types.
    let cast (source : Stream<'b>) =
        match source with
        | :? BufferStream<'b> as source -> new BufferStream<'a> (source.Buffer.Cast ()) :> Stream<'a>
        | _ -> new CastStream<'a, 'b> (source) :> Stream<'a>

    /// Constructs a size-limited form of the given stream.
    let limit size source = new LimitStream<'a> (source, size) :> Stream<'a>

    /// Returns a version of the given stream whose alignment is a factor of the requested stream.
    let checkAlignment alignment (stream : Stream<'a>) =
        if stream.Alignment % alignment = 0 then stream
        else new NotImplementedException() |> raise