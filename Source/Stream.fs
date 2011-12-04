﻿namespace MD

open Util
open System
open System.IO
open Microsoft.FSharp.NativeInterop

/// An interface to a reader for items of a certain type.
[<AbstractClass>]
type Stream<'a> (alignment : int) =

    /// Gets the alignment of this stream. This is the smallest amount of items the stream can
    /// individually access. All read operations should have a size that is some multiple of
    /// this integer.
    member this.Alignment = alignment

    /// Copies items from this stream into a native array and returns the amount of
    /// items read. If the returned amount is under the requested size, the end of
    /// the stream has been reached.
    abstract member Read : buffer : 'a[] * offset : int * size : int -> int

    /// Copies items from this stream to the given memory location and returns the amount
    /// of items read. If the returned amount is under the requested size, the end of
    /// the stream has been reached. This should only be used for blittable types.
    abstract member Read : destination : nativeint * size : int -> int

// Create type abbreviation.
type 'a stream = Stream<'a>

/// A stream that reads from a buffer (array).
[<Sealed>]
type BufferStream<'a> (buffer : 'a[], offset : int) =
    inherit Stream<'a> (1)
    let mutable offset = offset

    /// Gets the buffer this stream is reading from.
    member this.Buffer = buffer

    /// Gets the current offset of the stream in the source buffer.
    member this.Offset = offset

    override this.Read (destBuffer, destOffset, size) =
        let readSize = min size (buffer.Length - offset)
        Array.blit buffer offset destBuffer destOffset readSize
        offset <- offset + readSize
        readSize

    override this.Read (destination, size) =
        let itemSize = Memory.SizeOf<'a> ()
        let readSize = min size (buffer.Length - offset)
        Memory.Copy (buffer, offset, destination, uint32 readSize * itemSize)
        offset <- offset + readSize
        readSize

/// A stream that limits the amount of items that can be read.
[<Sealed>]
type LimitStream<'a> (source : 'a stream, size : int) =
    inherit Stream<'a> (source.Alignment)
    let mutable size = size

    /// Gets the remaining size of this stream.
    member this.Size = size

    override this.Read (buffer, offset, readSize) =
        let readSize = source.Read (buffer, offset, min readSize size)
        size <- size - readSize
        readSize

    override this.Read (destination, readSize) =
        let readSize = source.Read (destination, min readSize size)
        size <- size - readSize
        readSize

/// A stream that reads through streams (called chunks) generated by a retrieve function.
[<Sealed>]
type ChunkStream<'a, 'b> (alignment : int, initial : ('a stream exclusive * 'b) option, retrieve : 'b -> ('a stream exclusive * 'b) option) =
    inherit Stream<'a> (alignment)
    let mutable current = initial
    new (alignment, initialState : 'b, retrieve : 'b -> ('a stream exclusive * 'b) option) = new ChunkStream<'a, 'b> (alignment, retrieve initialState, retrieve)

    /// Gets the function used for retrieving chunks.
    member this.Retrieve = retrieve

    /// Releases this stream and all subordinate chunk streams.
    member this.Finish () =
        match current with
        | Some (stream, _) -> stream.Finish ()
        | None -> ()

    override this.Read (buffer, offset, size) = 
        let rec read (buffer : 'a[], offset: int, size : int) (totalReadSize : int) =
            match current with
            | Some (stream, state) ->
                let readSize = stream.Object.Read (buffer, offset, size)
                if readSize < size then
                    stream.Finish ()
                    current <- retrieve state
                    read (buffer, offset + readSize, size - readSize) (totalReadSize + readSize)
                else totalReadSize + size
            | None -> totalReadSize
        read (buffer, offset, size) 0

    override this.Read (destination, size) = 
        let itemSize = Memory.SizeOf<'a> ()
        let rec read (destination : nativeint, size : int) (totalReadSize : int) =
            match current with
            | Some (stream, state) ->
                let readSize = stream.Object.Read (destination, size)
                if readSize < size then
                    stream.Finish ()
                    current <- retrieve state
                    read (destination + nativeint (uint32 readSize * itemSize), size - readSize) (totalReadSize + readSize)
                else totalReadSize + size
            | None -> totalReadSize
        read (destination, size) 0

/// A stream that maps items with a mapping function.
[<Sealed>]
type MapStream<'a, 'b> (source : 'b stream, map : 'b -> 'a) =
    inherit Stream<'a> (source.Alignment)
    let buffer = Array.zeroCreate base.Alignment
    let loadBuffer () = source.Read (buffer, 0, buffer.Length) = buffer.Length

    override this.Read (destBuffer, offset, size) = 
        let mutable size = size
        let mutable cur = offset
        while size > 0 && loadBuffer() do
            for index = 0 to buffer.Length - 1 do
                destBuffer.[cur] <- map buffer.[index]
                cur <- cur + 1
            size <- size - buffer.Length
        cur - offset

    override this.Read (destination, size) = 
        let initialSize = size
        let itemSize = Memory.SizeOf<'a> ()
        let mutable size = size
        let mutable cur = destination
        while size > 0 && loadBuffer() do
            for index = 0 to this.Alignment - 1 do
                Memory.Write (cur, map buffer.[index])
                cur <- cur + nativeint itemSize
            size <- size - this.Alignment
        initialSize - size

/// A stream that combines fixed-size groups of items into single items.
[<Sealed>]
type CombineStream<'a, 'b> (source : 'b stream, groupSize : int, combine : 'b[] * int -> 'a) =
    inherit Stream<'a> (fit groupSize source.Alignment)
    let buffer = Array.zeroCreate (base.Alignment * groupSize)
    let loadBuffer () = source.Read (buffer, 0, buffer.Length) = buffer.Length

    override this.Read (destBuffer, offset, size) = 
        let mutable size = size
        let mutable cur = offset
        while size > 0 && loadBuffer() do
            for index = 0 to this.Alignment - 1 do
                destBuffer.[cur] <- combine (buffer, index * groupSize)
                cur <- cur + 1
            size <- size - this.Alignment
        cur - offset

    override this.Read (destination, size) = 
        let initialSize = size
        let itemSize = Memory.SizeOf<'a> ()
        let mutable size = size
        let mutable cur = destination
        while size > 0 && loadBuffer() do
            for index = 0 to this.Alignment - 1 do
                Memory.Write (cur, combine (buffer, index * groupSize))
                cur <- cur + nativeint itemSize
            size <- size - this.Alignment
        initialSize - size

/// A stream that splits single items into fixed-size groups.
[<Sealed>]
type SplitStream<'a, 'b> (source : 'b stream, groupSize : int, split : 'b * 'a[] * int -> unit) =
    inherit Stream<'a> (source.Alignment * groupSize)
    let buffer = Array.zeroCreate (source.Alignment)
    let loadBuffer () = source.Read (buffer, 0, buffer.Length) = buffer.Length

    override this.Read (destBuffer, offset, size) =
        let mutable size = size
        let mutable cur = offset
        while size > 0 && loadBuffer() do
            for index = 0 to buffer.Length - 1 do
                split (buffer.[index], destBuffer, cur)
                cur <- cur + groupSize
            size <- size - this.Alignment
        cur - offset

    override this.Read (destination, size) =
        let initialSize = size
        let itemSize = Memory.SizeOf<'a> ()
        let mutable size = size
        let mutable cur = destination
        let tempBuffer = Array.zeroCreate (this.Alignment)
        while size > 0 && loadBuffer() do
            for index = 0 to buffer.Length - 1 do
                split (buffer.[index], tempBuffer, index * groupSize)
            Memory.Copy (tempBuffer, 0, cur,  uint32 this.Alignment * itemSize)
            cur <- cur + nativeint (uint32 this.Alignment * itemSize)
            size <- size - this.Alignment
        initialSize - size

/// A stream that cast items in a source stream by reinterpreting the byte representations of sequential items.
[<Sealed>]
type CastStream<'a, 'b> (source : 'b stream, asize : uint32, bsize : uint32) =
    inherit Stream<'a> (fit (int asize) (source.Alignment * int bsize))
    new (source) = new CastStream<'a, 'b> (source, Memory.SizeOf<'a> (), Memory.SizeOf<'b> ())
    
    override this.Read (buffer, offset, size) = 
        let sourceSize = size * int asize / int bsize
        let readBuffer = Array.zeroCreate sourceSize
        let readSize = source.Read (readBuffer, 0, sourceSize)
        Memory.Copy (readBuffer, 0, buffer, offset, bsize * uint32 readSize)
        readSize * int bsize / int asize

    override this.Read (destination, size) = source.Read (destination, size * int asize / int bsize) * int bsize / int asize

/// A byte stream whose source is a region of memory.
[<Sealed>]
type UnsafeStream<'a> (regionStart : nativeint, regionEnd : nativeint) =
    inherit Stream<'a> (1)
    let mutable cur = regionStart
    let itemsize = Memory.SizeOf<'a> ()

    /// Gets the remaining size of the stream.
    member this.Size = int32 (uint32 (regionEnd - cur) / itemsize)

    /// Gets the pointer to the current position of the stream.
    member this.Current = cur

    /// Gets the end of the memory region readable by the stream.
    member this.End = regionEnd

    override this.Read (buffer, offset, size) =
        let readsize = min size this.Size
        Memory.Copy (cur, buffer, offset, uint32 readsize * itemsize)
        cur <- cur + nativeint (uint32 readsize * itemsize)
        readsize

    override this.Read (destination, size) =
        let readsize = min size this.Size
        Memory.Copy (cur, destination, uint32 readsize * itemsize)
        cur <- cur + nativeint (uint32 readsize * itemsize)
        readsize

/// A byte stream based on a System.IO stream.
[<Sealed>]
type IOStream (source : Stream) =
    inherit Stream<byte> (1)

    /// Gets the System.IO stream source for this stream.
    member this.Source = source

    override this.Read (buffer, offset, size) = source.Read (buffer, offset, size)

    override this.Read (destination, size) =
        let readbuffer = Array.zeroCreate size
        let readsize = source.Read (readbuffer, 0, size)
        Memory.Copy (readbuffer, 0, destination, uint32 readsize)
        readsize

/// Contains functions for constructing and manipulating streams.
module Stream =

    /// Reads the given amount of items from a stream into a buffer. If the stream does not have the requested
    /// amount of items, a smaller buffer of only the read items will be returned.
    let read size (stream : 'a stream) =
        let buffer = Array.zeroCreate size
        let readSize = stream.Read (buffer, 0, size)
        if readSize < size then
            let newBuffer = Array.zeroCreate readSize
            Array.blit buffer 0 newBuffer 0 readSize
            newBuffer
        else buffer

    /// Constructs a stream to read from a buffer. Note that the buffer is referenced directly and 
    /// changes to the buffer will be reflected in the stream.
    let buffer buffer offset = new BufferStream<'a> (buffer, offset) :> 'a stream

    /// Constructs a stream to read from the file at the given path.
    let file (path : MD.Path) =
        let fs = new FileStream (path.Source, FileMode.Open)
        let is = new IOStream (fs) :> byte stream
        Exclusive.custom fs.Dispose is

    /// Constructs a stream that concatenates a series of chunks produced by the given retrieve function.
    let chunk alignment (initial : 'b) retrieve = 
        let cs = new ChunkStream<'a, 'b> (alignment, initial, retrieve)
        Exclusive.custom cs.Finish (cs :> 'a stream)

    /// Constructs a stream that concatenates a series of chunks produced by the given retrieve function. The initial chunk
    /// must be provided when using this function.
    let chunkInit alignment (initial : ('a stream exclusive * 'b) option) retrieve = 
        let cs = new ChunkStream<'a, 'b> (alignment, initial, retrieve)
        Exclusive.custom cs.Finish (cs :> 'a stream)

    /// Constructs a mapped form of a stream.
    let map map source = new MapStream<'a, 'b> (source, map) :> 'a stream

    /// Constructs a stream that combines fixed-sized groups into single items.
    let combine groupSize group source = new CombineStream<'a, 'b> (source, groupSize, group) :> 'a stream

    /// Constructs a stream that splits single items into fixed-sized groups.
    let split groupSize split source = new SplitStream<'a, 'b> (source, groupSize, split) :> 'a stream

    /// Constructs a stream based on a source stream that reinterprets the byte representations of source items in order
    /// to form items of other types.
    let cast (source : 'b stream) =
        match source with
        | :? UnsafeStream<'b> as source -> new UnsafeStream<'a> (source.Current, source.End) :> 'a stream
        | _ -> new CastStream<'a, 'b> (source) :> 'a stream

    /// Constructs a size-limited form of the given stream.
    let limit size source = new LimitStream<'a> (source, size) :> 'a stream

    /// Constructs a stream that reads from the given memory region.
    let unsafe regionStart regionEnd = new UnsafeStream<'a> (regionStart, regionEnd) :> 'a stream