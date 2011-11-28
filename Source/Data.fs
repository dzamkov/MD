﻿namespace MD

open System
open System.IO
open Microsoft.FSharp.NativeInterop

/// An interface to a reader for items of a certain type.
type Stream<'a> =

    /// Tries reading a single item from this stream. Returns None if the end of the stream has
    /// been reached.
    abstract member Read : unit -> 'a option

    /// Copies items from this stream into a native array and returns the amount of
    /// items read. If the returned amount is under the requested size, the end of
    /// the stream has been reached.
    abstract member Read : buffer : 'a[] * offset : int * size : int -> int

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
    abstract member Read : start : int * size : int -> Stream<'a> exclusive

// Create type abbreviations for data
type 'a stream = Stream<'a>
type 'a data = Data<'a>

/// A stream that reads from a buffer (array).
type BufferStream<'a> (buffer : 'a[], offset : int) =
    let mutable offset = offset
    interface Stream<'a> with
        member this.Read () =
            let cur = offset
            if cur < buffer.Length 
            then 
                offset <- cur + 1
                Some buffer.[cur]
            else None

        member this.Read (destbuffer, destoffset, size) =
            let readsize = min size (buffer.Length - offset)
            Array.Copy (buffer, offset, destbuffer, destoffset, readsize)
            offset <- offset + readsize
            readsize

        member this.Read (dest, size) =
            let readsize = min size (buffer.Length - offset)
            Unsafe.copyap (buffer, offset) dest readsize
            readsize

/// Data from a buffer (array).
type BufferData<'a> (buffer : 'a[], offset : int, size : int) =
    
    /// Gets the buffer for this data.
    member this.Buffer = buffer

    /// Gets this data's offset in the source buffer.
    member this.Offset = offset

    /// Gets the size of this data.
    member this.Size = size

    interface Data<'a> with
        member this.Size = buffer.Length
        member this.Item with get x = buffer.[offset + x]
        member this.Read (start, size) = new BufferStream<'a> (buffer, offset + start) :> Stream<'a> |> Exclusive.``static``

/// A stream that reads through streams (called chunks) generated by a retrieve function.
type ChunkStream<'a, 'b> (initialState : 'b, retrieve : 'b -> ('a stream exclusive * 'b) option) =
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
                readtoptr (destination + nativeint (Unsafe.sizeof<'a> * readsize), size - readsize) (totalreadsize + readsize)
            else totalreadsize + size
        | None -> totalreadsize  
    
    /// Gets the function used for retrieving chunks.
    member this.Retrieve = retrieve

    /// Releases this stream and all subordinate chunk streams.
    member this.Finish () =
        match current with
        | Some (stream, _) -> stream.Finish ()
        | None -> ()

    interface Stream<'a> with
        member this.Read () = read ()
        member this.Read (buffer, offset, size) = readtobuf (buffer, offset, size) 0
        member this.Read (destination, size) = readtoptr (destination, size) 0

/// A byte stream whose source is a region of memory.
type UnsafeStream (regionStart : nativeint, regionEnd : nativeint) =
    let mutable cur = regionStart

    /// Gets the remaining size of the stream.
    member this.Size = int (regionEnd - cur)

    /// Gets the pointer to the current position of the stream.
    member this.Current = cur

    /// Gets the end of the memory region readable by the stream.
    member this.End = regionEnd

    interface Stream<byte> with
        member this.Read () =
            if this.Current = this.End then None
            else 
                let item = NativePtr.read (NativePtr.ofNativeInt cur)
                cur <- cur + nativeint 1
                Some item

        member this.Read (buffer, offset, size) =
            let readsize = min size this.Size
            Unsafe.copypa cur (buffer, offset) size
            cur <- cur + nativeint readsize
            readsize

        member this.Read (destination, size) =
            let readsize = min size this.Size
            Unsafe.copypp cur destination readsize
            cur <- cur + nativeint readsize
            readsize

/// Byte data whose source is a region of memory.
type UnsafeData (regionStart : nativeint, regionEnd : nativeint) =

    /// Gets the size of the data.
    member this.Size = int (regionEnd - regionStart)
    
    /// Gets the start of the memory region referenced by this data.
    member this.Start = regionStart

    /// Gets the end of the memory region referenced by this data.
    member this.End = regionEnd

    interface Data<byte> with
        member this.Size = this.Size
        member this.Item with get x = NativePtr.get (NativePtr.ofNativeInt regionStart) x
        member this.Read (start, size) = new UnsafeStream (regionStart + nativeint start, regionEnd) :> Stream<byte> |> Exclusive.``static``

/// A byte stream based on a System.IO stream.
type IOStream (source : Stream) =

    /// Gets the System.IO stream source for this stream.
    member this.Source = source

    interface Stream<byte> with
        member this.Read () =
            match source.ReadByte () with
            | -1 -> None
            | x -> Some (byte x)

        member this.Read (buffer, offset, size) = 
            source.Read (buffer, offset, size)

        member this.Read (destination, size) =
            let readbuffer = Array.zeroCreate size
            let readsize = source.Read (readbuffer, 0, size)
            Unsafe.copyap (readbuffer, 0) destination readsize
            readsize

/// Data based on a System.IO stream.
type IOData (source : Stream) =

    /// Gets the System.IO stream source for this data.
    member this.Source = source

    interface Data<byte> with
        member this.Size = int source.Length
        member this.Item 
            with get x =
                source.Position <- int64 x
                byte (source.ReadByte ())
        member this.Read (start, size) =
            source.Position <- int64 start
            new IOStream (source) :> byte stream |> Exclusive.``static``

/// Contains functions for constructing and manipulating streams.
module Stream =

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

    /// Constructs a stream that reads from the given memory region.
    let unsafe regionStart regionEnd = new UnsafeStream (regionStart, regionEnd) :> byte stream

/// Contains functions for constructing and manipulating data.
module Data =

    /// Constructs data based on a buffer. Note that the buffer is referenced directly and 
    /// changes to the buffer will be reflected in the data.
    let buffer buffer offset size = new BufferData<'a> (buffer, offset, size) :> Data<'a>

    /// Constructs data for the file at the given path.
    let file (path : MD.Path) = 
        let fs = new FileStream (path.Source, FileMode.Open)
        let id = new IOData (fs) :> byte data
        Exclusive.custom fs.Dispose id

    /// Constructs data whose source is an IO stream.
    let io (source : System.IO.Stream) = new IOData (source) :> byte data

    /// Constructs data whose source is the given memory region.
    let unsafe regionStart regionEnd = new UnsafeData (regionStart, regionEnd) :> byte data

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
        | :? UnsafeData as x -> Some (x.Start, x.End)
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