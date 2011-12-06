namespace MD

open Util
open System
open System.IO
open Microsoft.FSharp.NativeInterop

/// An immutable collection of items indexed by an integer.
[<AbstractClass>]
type Data<'a> (alignment : int) = 

    /// Gets the alignment of this stream. This is the size of the smallest group of items that can
    /// be individually access. All read operations should have a size and index that is some multiple of
    /// this integer.
    member this.Alignment = alignment

    /// Gets the size of the array.
    abstract member Size : uint64

    /// Gets a user-friendly string for the given size in bytes.
    static member GetSizeString (size : uint64) =
        let suffixes = [ "B"; "KiB"; "MiB"; "GiB"; "TiB"; "PiB"; "EiB" ]
        let mutable size = decimal size
        let mutable mag = 0
        while size >= 1024m do
            size <- size / 1024m
            mag <- mag + 1
        String.Format ("{0:####.###} {1}", size, suffixes.[mag])

    /// Gets a user-friendly string for the size of this data.
    member this.SizeString = Data<'a>.GetSizeString (this.Size * uint64 (Memory.SizeOf<'a> ()))

    /// Copies items from this data (starting at the given index) into the given buffer.
    abstract member Read : index : uint64 * buffer : 'a[] * offset : int * size : int -> unit

    /// Copies items from this data (starting at the given index) into the given memory location.
    /// This should only be used for blittable types.
    abstract member Read : index : uint64 * destination : nativeint * size : int -> unit

    /// Creates a stream to read this data beginning at the given index. The given
    /// size sets a limit on the amount of items that can be read from the resulting
    /// stream, but does not ensure the stream will end after the given amount of items
    /// are read. The returned stream will have an alignment that is a divisor of this
    /// data's alignment.
    abstract member Lock : index : uint64 * size : uint64 -> Stream<'a> exclusive

    /// Creates a stream to read this data beginning at the given index.
    member this.Lock (index : uint64) = this.Lock (index, this.Size - index)

     /// Creates a stream to read this data.
    member this.Lock () = this.Lock (0UL, this.Size)

    override this.ToString () = String.Format ("{0} of {1}", this.SizeString, typeof<'a>.Name)

// Create type abbreviation.
type 'a data = Data<'a>

/// A stream that reads from a data source.
[<Sealed>]
type DataStream<'a> (source : 'a data, index : uint64) =
    inherit Stream<'a> (source.Alignment)
    let mutable index = index

    /// Gets the data source this stream is reading from.
    member this.Source = source

    /// Gets the current index in the source data this stream is reading from.
    member this.Index = index

    override this.Read (buffer, offset, size) =
        let readSize = int (min (uint64 size) (source.Size - index))
        source.Read (index, buffer, offset, readSize)
        index <- index + uint64 readSize
        readSize

    override this.Read (destination, size) =
        let readSize = int (min (uint64 size) (source.Size - index))
        source.Read (index, destination, readSize)
        index <- index + uint64 readSize
        readSize

/// Data from a buffer (array).
[<Sealed>]
type BufferData<'a> (buffer : 'a[], offset : int, size : int) =
    inherit Data<'a> (1)
    
    /// Gets the buffer for this data.
    member this.Buffer = buffer

    /// Gets this data's offset in the source buffer.
    member this.Offset = offset

    /// Gets the size of this data.
    member this.NativeSize = size

    override this.Size = uint64 size
    override this.Read (index, destBuffer, destOffset, size) = Array.blit buffer (int index + offset) destBuffer destOffset size
    override this.Read (index, destination, size) = Memory.Copy (buffer, int index + offset, destination, uint32 size * Memory.SizeOf<'a> ())
    override this.Lock (index, size) = Stream.buffer buffer (int index + offset) |> Exclusive.make

/// Data that applies a mapping function to source data.
type MapData<'a, 'b> (source : 'b data, map : 'b -> 'a) =
    inherit Data<'a> (source.Alignment)

    override this.Size = source.Size

    override this.Read (index, destBuffer, destOffset, size) =
        let tempBuffer = Array.zeroCreate size
        source.Read (index, tempBuffer, 0, size)
        for index = 0 to tempBuffer.Length - 1 do
            destBuffer.[destOffset + index] <- map tempBuffer.[index]

    override this.Read (index, destination, size) = 
        let mutable destination = destination
        let itemSize = Memory.SizeOf<'a> ()
        let tempBuffer = Array.zeroCreate size
        source.Read (index, tempBuffer, 0, size)
        for index = 0 to tempBuffer.Length - 1 do
            Memory.Write (destination, map tempBuffer.[index])
            destination <- destination + nativeint itemSize

    override this.Lock (index, size) = source.Lock (index, size) |> Exclusive.map (Stream.map map)

/// Data that combines fixed-size groups of items into single items.
type CombineData<'a, 'b> (source : 'b data, groupSize : int, combine : 'b[] * int -> 'a) =
    inherit Data<'a> (fit groupSize source.Alignment)

    override this.Size = source.Size / uint64 groupSize

    override this.Read (index, destBuffer, destOffset, size) =
        let sourceSize = size * groupSize
        let tempBuffer = Array.zeroCreate sourceSize
        source.Read (index, tempBuffer, 0, sourceSize)
        for index = 0 to size - 1 do
            destBuffer.[destOffset + index] <- combine (tempBuffer, index * groupSize)

    override this.Read (index, destination, size) = 
        let mutable destination = destination
        let itemSize = Memory.SizeOf<'a> ()
        let sourceSize = size * groupSize
        let tempBuffer = Array.zeroCreate sourceSize
        source.Read (index, tempBuffer, 0, sourceSize)
        for index = 0 to size - 1 do
            Memory.Write (destination, combine (tempBuffer, index * groupSize))
            destination <- destination + nativeint itemSize
    
    override this.Lock (index, size) = source.Lock (index * uint64 groupSize, size * uint64 groupSize) |> Exclusive.map (Stream.combine groupSize combine)

/// Data created from a series of concatenated chunks.
type ChunkData<'a> (alignment : int) =
    inherit Data<'a> (alignment)
    let chunks = new System.Collections.Generic.List<uint64 * 'a data> ()
    let mutable size = 0UL

    // Finds the chunk and offset for the given absolute index in this data.
    let find (index : uint64) =
        let rec search (index, start, stop) =
            let mid = (start + stop) / 2
            let (midindex, midchunk) = chunks.[mid]
            if index < midindex then search (index, start, mid)
            elif index - midindex < midchunk.Size then (midchunk, mid, index - midindex)
            else search (index, mid, stop)
        search (index, 0, chunks.Count)

    /// Appends a chunk to this data.
    member this.Append chunk =
        chunks.Add ((size, chunk))
        size <- size + chunk.Size

    override this.Size = size

    override this.Read (index, buffer, offset, size) =
        let chunk, chunkIndex, chunkOffset = find index
        let rec read (offset, size, chunk : 'a data, chunkIndex, chunkOffset) totalReadSize =
            let chunkReadSize = chunk.Size - chunkOffset
            if size > int chunkReadSize then
                chunk.Read (chunkOffset, buffer, offset, int chunkReadSize)
                read (offset + int chunkReadSize, size - int chunkReadSize, snd chunks.[chunkIndex], chunkIndex + 1, 0UL) (totalReadSize + int chunkReadSize)
            else
                chunk.Read (chunkOffset, buffer, offset, size)
        read (offset, size, chunk, chunkIndex, chunkOffset) 0

    override this.Read (index, destination, size) =
        let itemSize = Memory.SizeOf<'a> ()
        let chunk, chunkIndex, chunkOffset = find index
        let rec read (destination, size, chunk : 'a data, chunkIndex, chunkOffset) totalReadSize =
            let chunkReadSize = chunk.Size - chunkOffset
            if size > int chunkReadSize then
                chunk.Read (chunkOffset, destination, int chunkReadSize)
                read (destination + nativeint (uint32 chunkReadSize * itemSize), size - int chunkReadSize, snd chunks.[chunkIndex], chunkIndex + 1, 0UL) (totalReadSize + int chunkReadSize)
            else
                chunk.Read (chunkOffset, destination, size)
        read (destination, size, chunk, chunkIndex, chunkOffset) 0

    override this.Lock (index, size) =
        let chunk, chunkIndex, chunkOffset = find index
        let chunkReadSize = chunk.Size - chunkOffset
        if size < chunkReadSize then
            chunk.Lock (chunkOffset, size)
        else
            let retrieve index =
                if index < chunks.Count then Some ((snd chunks.[index]).Lock (), index + 1)
                else None
            Stream.chunkInit this.Alignment (Some (chunk.Lock chunkOffset, chunkIndex + 1)) retrieve

/// Byte data whose source is a region of memory.
[<Sealed>]
type UnsafeData<'a when 'a : unmanaged> (regionStart : nativeint, regionEnd : nativeint) =
    inherit Data<'a> (1)
    let itemSize = Memory.SizeOf<'a> ()

    /// Gets the start of the memory region referenced by this data.
    member this.Start = regionStart

    /// Gets the end of the memory region referenced by this data.
    member this.End = regionEnd

    override this.Size = uint64 (regionEnd - regionStart) / uint64 itemSize
    override this.Read (index, buffer, offset, size) = Memory.Copy (regionStart + nativeint (index * uint64 itemSize), buffer, offset, uint32 size * itemSize)
    override this.Read (index, destination, size) = Memory.Copy (regionStart + nativeint (index * uint64 itemSize), destination, uint32 size * itemSize)
    override this.Lock (index, size) = Stream.unsafe (regionStart + nativeint (index * uint64 itemSize)) regionEnd |> Exclusive.make

/// Data based on a seekable System.IO stream.
[<Sealed>]
type IOData (source : Stream) =
    inherit Data<byte> (1)

    /// Gets the System.IO stream source for this data.
    member this.Source = source

    override this.Size = uint64 source.Length

    override this.Read (index, buffer, offset, size) =
        source.Position <- int64 index
        source.Read (buffer, offset, size) |> ignore

    override this.Read (index, destination, size) =
        source.Position <- int64 index
        let readBuffer = Array.zeroCreate size
        let readSize = source.Read (readBuffer, 0, size)
        Memory.Copy (readBuffer, 0, destination, uint32 readSize)

    override this.Lock (index, size) = 
        source.Position <- int64 index
        new IOStream (source) :> byte stream |> Exclusive.make

/// Contains functions for constructing and manipulating data.
module Data =

    /// Constructs data based on a buffer. Note that the buffer is referenced directly and 
    /// changes to the buffer will be reflected in the data.
    let buffer buffer offset size = new BufferData<'a> (buffer, offset, size) :> 'a data

    /// Constructs data from the remaining items in the given stream.
    let make chunkSize (stream : 'a stream exclusive) =
        let data = new ChunkData<'a> (1)
        let streamobj = stream.Object
        let rec readChunk () =
            let buffer = Array.zeroCreate chunkSize
            let size = streamobj.Read (buffer, 0, chunkSize)
            data.Append (new BufferData<'a> (buffer, 0, size) :> 'a data)
            if size = chunkSize then readChunk ()
        readChunk ()
        stream.Finish ()
        data :> 'a data

    /// Constructs data for the file at the given path.
    let file (path : MD.Path) = 
        let fs = new FileStream (path.Source, FileMode.Open)
        fs |> Exclusive.dispose |> Exclusive.map (fun fs -> new IOData (fs) :> byte data)

    /// Constructs data whose source is an IO stream.
    let io (source : System.IO.Stream) = new IOData (source) :> byte data

    /// Constructs a mapped form of the given data.
    let map map source = new MapData<'a, 'b> (source, map) :> 'a data

    /// Constructs data that combines fixed-sized groups into single items.
    let combine groupSize combine source = new CombineData<'a, 'b> (source, groupSize, combine) :> 'a data

    /// Constructs data that combines fixed-sized groups into single items.
    let split groupSize split source = new NotImplementedException () |> raise

    /// Constructs data whose source is the given memory region.
    let unsafe regionStart regionEnd = new UnsafeData<'a> (regionStart, regionEnd) :> 'a data

    /// Constructs a stream to read the entirety of the given data.
    let lock (data : 'a data) : 'a stream exclusive = data.Lock ()

    /// Gets a complete buffer copy of the given data. This should only be used on relatively small data.
    let getBuffer (data : 'a data) =
        let buffer = Array.zeroCreate (int data.Size)
        let stream = lock data
        stream.Object.Read (buffer, 0, buffer.Length) |> ignore
        stream.Finish ()
        buffer

    /// Matches data for an unsafe pointer representation, if possible.
    let (|Unsafe|_|) (data : 'a data) =
        match data with
        | :? UnsafeData<'a> as x -> Some (x.Start, x.End)
        | _ -> None

    /// Matches data for a complete (no offset) buffer representation.
    let (|BufferComplete|) (data : 'a data) =
        match data with
        | :? BufferData<'a> as x when x.Offset = 0 && x.NativeSize = x.Buffer.Length -> x.Buffer
        | x -> getBuffer x

    /// Matches data for a buffer representation.
    let (|Buffer|) (data : 'a data) =
        match data with
        | :? BufferData<'a> as x -> (x.Buffer, x.Offset, x.NativeSize)
        | x -> (getBuffer x, 0, int x.Size)