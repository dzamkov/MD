﻿namespace MD

open System
open System.IO
open Microsoft.FSharp.NativeInterop

/// An interface to a reader for items of a certain type.
[<AbstractClass>]
type Stream<'a> () =

    /// Tries reading a single item from this stream. Returns None if the end of the stream has
    /// been reached.
    abstract member Read : unit -> 'a option

    /// Copies items from this stream into a native array and returns the amount of
    /// items read. If the returned amount is under the requested size, the end of
    /// the stream has been reached.
    abstract member Read : buffer : 'a[] * offset : int * size : int -> int

    /// Copies items from this stream to the given memory location and returns the amount
    /// of items read. If the returned amount is under the requested size, the end of
    /// the stream has been reached. This should only be used for blittable types.
    abstract member Read : destination : nativeint * size : int -> int

/// An immutable collection of items indexed by an integer.
[<AbstractClass>]
type Data<'a> () = 

    /// Gets the current size of the array.
    abstract member Size : int

    /// Reads the item at the given index in this data.
    abstract member Read : index : int -> 'a

    /// Copies items from this data (starting at the given index) into the given buffer.
    abstract member Read : index : int * buffer : 'a[] * offset : int * size : int -> unit

    /// Copies items from this data (starting at the given index) into the given memory location.
    /// This should only be used for blittable types.
    abstract member Read : index : int * destination : nativeint * size : int -> unit

    /// Creates a stream to read this array beginning at the given index. The given
    /// size sets a limit on the amount of items that can be read from the resulting
    /// stream, but does not ensure the stream will end after the given amount of items
    /// are read.
    abstract member Read : index : int * size : int -> Stream<'a> exclusive

    /// Gets the item at the given index in this data.
    member this.Item with get x = this.Read x

// Create type abbreviations for data.
type 'a stream = Stream<'a>
type 'a data = Data<'a>

/// A stream that reads from a data source.
[<Sealed>]
type DataStream<'a> (source : 'a data, index : int) =
    inherit Stream<'a> ()
    let mutable index = index

    /// Gets the data source this stream is reading from.
    member this.Source = source

    /// Gets the current index in the source data this stream is reading from.
    member this.Index = index

    override this.Read () =
        if index < source.Size then
            let item = source.[index]
            index <- index + 1
            Some item
        else None

    override this.Read (buffer, offset, size) =
        let readsize = min size (source.Size - index)
        source.Read (index, buffer, offset, readsize)
        index <- index + readsize
        readsize

    override this.Read (destination, size) =
        let readsize = min size (source.Size - index)
        source.Read (index, destination, readsize)
        index <- index + readsize
        readsize


/// A stream that reads from a buffer (array).
[<Sealed>]
type BufferStream<'a> (buffer : 'a[], offset : int) =
    inherit Stream<'a> ()
    let mutable offset = offset

    /// Gets the buffer this stream is reading from.
    member this.Buffer = buffer

    /// Gets the current offset of the stream in the source buffer.
    member this.Offset = offset

    override this.Read () = 
        if offset < buffer.Length then
            let item = buffer.[offset]
            offset <- offset + 1
            Some item
        else None

    override this.Read (destbuffer, destoffset, size) =
        let readsize = min size (buffer.Length - offset)
        Array.blit buffer offset destbuffer destoffset readsize
        offset <- offset + readsize
        readsize

    override this.Read (destination, size) =
        let readsize = min size (buffer.Length - offset)
        Memory.Copy (buffer, offset, destination, uint32 readsize)
        offset <- offset + readsize
        readsize

/// Data from a buffer (array).
[<Sealed>]
type BufferData<'a> (buffer : 'a[], offset : int, size : int) =
    inherit Data<'a> ()
    
    /// Gets the buffer for this data.
    member this.Buffer = buffer

    /// Gets this data's offset in the source buffer.
    member this.Offset = offset

    override this.Size = size
    override this.Read index = buffer.[index + offset]
    override this.Read (index, destbuffer, destoffset, size) = Array.blit buffer (index + offset) destbuffer destoffset size
    override this.Read (index, destination, size) = Memory.Copy (buffer, index + offset, destination, uint32 size)
    override this.Read (index, size) = new BufferStream<'a> (buffer, index + offset) :> 'a stream |> Exclusive.``static``

/// A stream that reads through streams (called chunks) generated by a retrieve function.
[<Sealed>]
type ChunkStream<'a, 'b> (initialState : 'b, retrieve : 'b -> ('a stream exclusive * 'b) option) =
    inherit Stream<'a> ()
    let mutable current = retrieve initialState

    let rec read () =
        match current with
        | Some (stream, state) ->
            match stream.Object.Read () with
            | Some x -> Some x
            | None -> 
                stream.Finish ()
                current <- retrieve state
                read ()
        | None -> None

    let rec readtobuf (buffer : 'a[], offset: int, size : int) (totalreadsize : int) =
        match current with
        | Some (stream, state) ->
            let readsize = stream.Object.Read (buffer, offset, size)
            if readsize < size then
                stream.Finish ()
                current <- retrieve state
                readtobuf (buffer, offset + readsize, size - readsize) (totalreadsize + readsize)
            else totalreadsize + size
        | None -> totalreadsize

    let rec readtoptr (destination : nativeint, size : int) (totalreadsize : int) =
        match current with
        | Some (stream, state) ->
            let readsize = stream.Object.Read (destination, size)
            if readsize < size then
                stream.Finish ()
                current <- retrieve state
                readtoptr (destination + nativeint (Memory.SizeOf<'a> () * uint32 readsize), size - readsize) (totalreadsize + readsize)
            else totalreadsize + size
        | None -> totalreadsize  
    
    /// Gets the function used for retrieving chunks.
    member this.Retrieve = retrieve

    /// Releases this stream and all subordinate chunk streams.
    member this.Finish () =
        match current with
        | Some (stream, _) -> stream.Finish ()
        | None -> ()

    override this.Read () = read ()
    override this.Read (buffer, offset, size) = readtobuf (buffer, offset, size) 0
    override this.Read (destination, size) = readtoptr (destination, size) 0

/// A stream that combines fixed size groups in a source stream.
[<Sealed>]
type CombineStream<'a, 'b> (source : 'b stream, groupSize : int, group : 'b[] -> 'a) =
    inherit Stream<'a> ()
    let buf : 'b[] = Array.zeroCreate groupSize
    let loadone () = source.Read (buf, 0, groupSize) = groupSize
    let readone () = group buf

    let readtobuf (buffer : 'a[], offset, size) = 
        let mutable size = size
        let mutable cur = offset
        while size > 0 && loadone() do
            buffer.[cur] <- readone()
            cur <- cur + 1
            size <- size - 1
        cur - offset

    let readtoptr (destination, size) =
        let itemsize = Memory.SizeOf<'a> ()
        let initsize = size
        let mutable size = size
        let mutable cur = destination
        while size > 0 && loadone() do
            Memory.Write (cur, readone())
            cur <- cur + nativeint (itemsize)
            size <- size - 1
        initsize - size

    override this.Read () = if loadone () then Some (readone ()) else None
    override this.Read (buffer, offset, size) = readtobuf (buffer, offset, size)
    override this.Read (destination, size) = readtoptr (destination, size)

/// A byte stream whose source is a region of memory.
[<Sealed>]
type UnsafeStream<'a when 'a : unmanaged> (regionStart : nativeint, regionEnd : nativeint) =
    inherit Stream<'a> ()
    let mutable cur = regionStart
    let itemsize = Memory.SizeOf<'a> ()

    /// Gets the remaining size of the stream.
    member this.Size = int32 (uint32 (regionEnd - cur) / itemsize)

    /// Gets the pointer to the current position of the stream.
    member this.Current = cur

    /// Gets the end of the memory region readable by the stream.
    member this.End = regionEnd

    override this.Read () =
        let next = cur + nativeint itemsize
        if next <= regionEnd then
            let item = Memory.Read cur
            cur <- next
            Some item
        else None

    override this.Read (buffer, offset, size) =
        let readsize = min size this.Size
        Memory.Copy (cur, buffer, offset, uint32 readsize)
        cur <- cur + nativeint (uint32 readsize * itemsize)
        readsize

    override this.Read (destination, size) =
        let readsize = min size this.Size
        Memory.Copy (cur, destination, uint32 readsize)
        cur <- cur + nativeint (uint32 readsize * itemsize)
        readsize

/// Byte data whose source is a region of memory.
[<Sealed>]
type UnsafeData<'a when 'a : unmanaged> (regionStart : nativeint, regionEnd : nativeint) =
    inherit Data<'a> ()
    let itemsize = Memory.SizeOf<'a> ()

    /// Gets the start of the memory region referenced by this data.
    member this.Start = regionStart

    /// Gets the end of the memory region referenced by this data.
    member this.End = regionEnd

    override this.Size = int32 (uint32 (regionEnd - regionStart) / itemsize)
    override this.Read index = Memory.Read (regionStart + nativeint (uint32 index * itemsize))
    override this.Read (index, buffer, offset, size) = Memory.Copy (regionStart + nativeint (uint32 index * itemsize), buffer, offset, uint32 size)
    override this.Read (index, destination, size) = Memory.Copy (regionStart + nativeint (uint32 index * itemsize), destination, uint32 size)
    override this.Read (index, size) = new UnsafeStream<'a> (regionStart + nativeint (uint32 index * itemsize), regionEnd) :> 'a stream |> Exclusive.``static``

/// A byte stream based on a System.IO stream.
[<Sealed>]
type IOStream (source : Stream) =
    inherit Stream<byte> ()

    /// Gets the System.IO stream source for this stream.
    member this.Source = source

    override this.Read () =
        match source.ReadByte () with
        | -1 -> None
        | x -> Some (byte x)

    override this.Read (buffer, offset, size) = source.Read (buffer, offset, size)

    override this.Read (destination, size) =
        let readbuffer = Array.zeroCreate size
        let readsize = source.Read (readbuffer, 0, size)
        Memory.Copy (readbuffer, 0, destination, uint32 readsize)
        readsize

/// Data based on a seekable System.IO stream.
[<Sealed>]
type IOData (source : Stream) =
    inherit Data<byte> ()

    /// Gets the System.IO stream source for this data.
    member this.Source = source

    override this.Size = int source.Length

    override this.Read index =
        source.Position <- int64 index
        byte (source.ReadByte ())

    override this.Read (index, buffer, offset, size) =
        source.Position <- int64 index
        source.Read (buffer, offset, size) |> ignore

    override this.Read (index, destination, size) =
        source.Position <- int64 index
        let readbuffer = Array.zeroCreate size
        let readsize = source.Read (readbuffer, 0, size)
        Memory.Copy (readbuffer, 0, destination, uint32 readsize)

    override this.Read (index, size) = 
        source.Position <- int64 index
        new IOStream (source) :> byte stream |> Exclusive.``static``

/// Contains functions for constructing and manipulating streams.
module Stream =

    /// Reads the given amount of items from a stream into a buffer. If the stream does not have the requested
    /// amount of items, a smaller buffer of only the read items will be returned.
    let read size (stream : 'a stream) =
        let buf = Array.zeroCreate size
        let readsize = stream.Read (buf, 0, size)
        if readsize < size then
            let nbuf = Array.zeroCreate readsize
            Array.blit buf 0 nbuf 0 readsize
            nbuf
        else buf

    /// Constructs a stream to read from a buffer. Note that the buffer is referenced directly and 
    /// changes to the buffer will be reflected in the stream.
    let buffer buffer offset = new BufferStream<'a> (buffer, offset)

    /// Constructs a stream to read from the file at the given path.
    let file (path : MD.Path) =
        let fs = new FileStream (path.Source, FileMode.Open)
        let is = new IOStream (fs) :> byte stream
        Exclusive.custom fs.Dispose is

    /// Constructs a stream that concatenates a series of chunks produced by the given retrieve function.
    let chunk initial retrieve = 
        let cs = new ChunkStream<'a, 'b> (initial, retrieve)
        Exclusive.custom cs.Finish (cs :> 'a stream)

    /// Constructs a stream that combines fixed-sized groups in the source stream into single items.
    let combine groupSize group source = new CombineStream<'a, 'b> (source, groupSize, group) :> 'a stream

    /// Constructs a stream that reads from the given memory region.
    let unsafe regionStart regionEnd = new UnsafeStream<'a> (regionStart, regionEnd) :> 'a stream

/// Contains functions for constructing and manipulating data.
module Data =

    /// Constructs data based on a buffer. Note that the buffer is referenced directly and 
    /// changes to the buffer will be reflected in the data.
    let buffer buffer offset size = new BufferData<'a> (buffer, offset, size) :> 'a data

    /// Constructs data for the file at the given path.
    let file (path : MD.Path) = 
        let fs = new FileStream (path.Source, FileMode.Open)
        fs |> Exclusive.dispose |> Exclusive.map (fun fs -> new IOData (fs) :> byte data)

    /// Constructs data whose source is an IO stream.
    let io (source : System.IO.Stream) = new IOData (source) :> byte data

    /// Constructs data whose source is the given memory region.
    let unsafe regionStart regionEnd = new UnsafeData<'a> (regionStart, regionEnd) :> 'a data

    /// Constructs a stream to read the entirety of the given data.
    let read (data : 'a data) : 'a stream exclusive = data.Read (0, data.Size)

    /// Gets a complete buffer copy of the given data.
    let getBuffer (data : 'a data) =
        let buf = Array.zeroCreate data.Size
        let str = read data
        str.Object.Read (buf, 0, buf.Length) |> ignore
        str.Finish ()
        buf

    /// Matches data for an unsafe pointer representation, if possible.
    let (|Unsafe|_|) (data : 'a data) =
        match data with
        | :? UnsafeData<'a> as x -> Some (x.Start, x.End)
        | _ -> None

    /// Matches data for a complete (no offset) buffer representation.
    let (|BufferComplete|) (data : 'a data) =
        match data with
        | :? BufferData<'a> as x when x.Offset = 0 && x.Size = x.Buffer.Length -> x.Buffer
        | x -> getBuffer x

    /// Matches data for a buffer representation.
    let (|Buffer|) (data : 'a data) =
        match data with
        | :? BufferData<'a> as x -> (x.Buffer, x.Offset, x.Size)
        | x -> (getBuffer x, 0, x.Size)