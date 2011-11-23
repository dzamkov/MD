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
    abstract member Read : buffer : 'a[] * size : int * offset : int -> int

    /// Copies items from this stream to the given memory location and returns the amount
    /// of items read. If the returned amount is under the requested size, the end of
    /// the stream has been reached. This should only be used for streams of value types.
    abstract member Read : destination : nativeint * size : int -> int

    /// Indicates that the stream will no longer be used.
    abstract member Finish : unit -> unit

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


/// A stream that reads through streams (called chunks) generated by a retrieve function.
type ChunkStream<'a, 'b> (initialState : 'b, retrieve : 'b -> (Stream<'a> * 'b) option) =
    let mutable current = retrieve initialState

    let rec read () =
        match current with
        | Some (stream, state) ->
            match stream.Read () with
            | Some x -> Some x
            | None -> 
                stream.Finish ()
                current <- retrieve state
                read ()
        | None -> None

    let rec readtobuf (buffer : 'a[], size : int, offset: int) (totalreadsize : int) =
        match current with
        | Some (stream, state) ->
            let readsize = stream.Read (buffer, size, offset)
            if readsize < size then
                stream.Finish ()
                current <- retrieve state
                readtobuf (buffer, size - readsize, offset + readsize) (totalreadsize + readsize)
            else totalreadsize + size
        | None -> totalreadsize

    let rec readtoptr (destination : nativeint, size : int) (totalreadsize : int) =
        match current with
        | Some (stream, state) ->
            let readsize = stream.Read (destination, size)
            if readsize < size then
                stream.Finish ()
                current <- retrieve state
                readtoptr (destination + nativeint (Unsafe.sizeof<'a> * readsize), size - readsize) (totalreadsize + readsize)
            else totalreadsize + size
        | None -> totalreadsize  
    
    /// Gets the function used for retrieving chunks.
    member this.Retrieve = retrieve

    interface Stream<'a> with
        member this.Read () = read ()
        member this.Read (buffer, size, offset) = readtobuf (buffer, size, offset) 0
        member this.Read (destination, size) = readtoptr (destination, size) 0
        member this.Finish () =
            match current with
            | Some (stream, _) -> stream.Finish ()
            | None -> ()

/// A byte stream whose source is a region of memory.
type UnsafeStream (regionStart : nativeptr<byte>, regionEnd : nativeptr<byte>) =
    let mutable cur = regionStart

    /// Gets the remaining size of the stream.
    member this.Size = int ((NativePtr.toNativeInt this.End) - (NativePtr.toNativeInt cur))

    /// Gets the pointer to the current position of the stream.
    member this.Current = cur

    /// Gets the end of the memory region readable by the stream.
    member this.End = regionEnd

    interface Stream<byte> with
        member this.Read () =
            if this.Current = this.End then None
            else 
                let item = NativePtr.read cur
                cur <- NativePtr.add cur 1
                Some item

        member this.Read (buffer, size, offset) =
            let readsize = min size this.Size
            Unsafe.copypa (NativePtr.toNativeInt cur) (buffer, size, offset)
            cur <- NativePtr.add cur readsize
            readsize

        member this.Read (destination, size) =
            let readsize = min size this.Size
            Unsafe.copypp (NativePtr.toNativeInt cur) destination readsize
            cur <- NativePtr.add cur readsize
            readsize

        member this.Finish () = ()

/// Byte data whose source is a region of memory.
type UnsafeData (regionStart : nativeptr<byte>, regionEnd : nativeptr<byte>) =

    /// Gets the size of the data.
    member this.Size = int ((NativePtr.toNativeInt this.End) - (NativePtr.toNativeInt this.Start))
    
    /// Gets the start of the memory region referenced by this data.
    member this.Start = regionStart

    /// Gets the end of the memory region referenced by this data.
    member this.End = regionEnd

    interface Data<byte> with
        member this.Size = this.Size
        member this.Item with get x = NativePtr.get this.Start x
        member this.Read (start, size) = new UnsafeStream (NativePtr.add this.Start start, this.End) :> Stream<byte>

/// A byte stream based on a System.IO stream.
type IOStream (source : Stream, closeOnFinish : bool) =

    /// Gets the System.IO stream source for this stream.
    member this.Source = source

    /// Indicates wether the source stream for this stream is closed when
    /// this stream is finished.
    member this.CloseOnFinish = closeOnFinish

    interface Stream<byte> with
        member this.Read () =
            match source.ReadByte () with
            | -1 -> None
            | x -> Some (byte x)

        member this.Read (buffer, size, offset) = 
            source.Read (buffer, offset, size)

        member this.Read (destination, size) =
            let readBuffer = Array.create size 0uy
            let readSize = source.Read (readBuffer, 0, size)
            Unsafe.copyap (readBuffer, size, 0) destination
            readSize

        member this.Finish () =
            if closeOnFinish then source.Close ()
            else ()

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
            new IOStream (source, false) :> Stream<byte>


/// Contains functions for constructing and manipulating streams.
module Stream =

    /// Constructs a stream to read from the file at the given path.
    let file (path : MD.Path) = new IOStream (new FileStream (path.Source, FileMode.Open), true) :> Stream<byte>

    /// Constructs a stream that concatenates a series of chunks produced by the given retrieve function.
    let chunk initial retrieve = new ChunkStream<'a, 'b> (initial, retrieve) :> Stream<'a>

    /// Constructs a stream that reads from the given memory region.
    let unsafe regionStart regionEnd = new UnsafeStream (regionStart, regionEnd) :> Stream<byte>

/// Contains functions for constructing and manipulating data.
module Data =

    /// Constructs data for the file at the given path.
    let file (path : MD.Path) = new IOData (new FileStream (path.Source, FileMode.Open)) :> Data<byte>

    /// Constructs data whose source is an IO stream.
    let io (source : System.IO.Stream) = new IOData (source) :> Data<byte>

    /// Constructs data whose source is the given memory region.
    let unsafe regionStart regionEnd = new UnsafeData (regionStart, regionEnd) :> Data<byte>

    /// Constructs a stream to read the entirety of the given data.
    let read (data : Data<'a>) = data.Read (0, data.Size)