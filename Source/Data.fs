namespace MD

open Util
open System
open System.IO

/// An immutable collection of items indexed by an integer.
[<AbstractClass>]
type Data<'a when 'a : unmanaged> (alignment : int) = 

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

    /// Copies items from this data (starting at the given index) into the given array.
    abstract member Read : index : uint64 * array : 'a[] * offset : int * size : int -> unit

    /// Creates a stream to read this data beginning at the given index. The given
    /// size sets a limit on the amount of items that can be read from the resulting
    /// stream, but does not ensure the stream will end after the given amount of items
    /// are read. The returned stream will have an alignment that is a divisor of this
    /// data's alignment.
    abstract member Lock : index : uint64 * size : uint64 -> Stream<'a> exclusive

    /// Creates a stream to read this data beginning at the given index.
    member this.Lock (index : uint64) = this.Lock (index, this.Size - index) |> Exclusive.map (Stream.limit (this.Size - index))

     /// Creates a stream to read this data.
    member this.Lock () = this.Lock (0UL, this.Size) |> Exclusive.map (Stream.limit this.Size)

    override this.ToString () = String.Format ("{0} of {1}", this.SizeString, typeof<'a>.Name)

/// A stream that reads from a data source.
[<Sealed>]
type DataStream<'a when 'a : unmanaged> (source : Data<'a>, index : uint64) =
    inherit Stream<'a> (source.Alignment)
    let mutable index = index

    /// Gets the data source this stream is reading from.
    member this.Source = source

    /// Gets the current index in the source data this stream is reading from.
    member this.Index = index

    override this.Read (array, offset, size) =
        let readSize = int (min (uint64 size) (source.Size - index))
        source.Read (index, array, offset, readSize)
        index <- index + uint64 readSize
        readSize

/// Data from a buffer.
[<Sealed>]
type BufferData<'a when 'a : unmanaged> (buffer : Buffer<'a>, size : int) =
    inherit Data<'a> (1)
    
    /// Gets the buffer for this data.
    member this.Buffer = buffer

    /// Gets the size of this data.
    member this.NativeSize = size

    override this.Size = uint64 size
    override this.Read (index, array, offset, size) = buffer.CopyTo (array, offset, size)
    override this.Lock (index, size) = Stream.buffer (buffer.Advance (int index)) |> Exclusive.make

/// Data from an array.
[<Sealed>]
type ArrayData<'a when 'a : unmanaged> (array : 'a[], offset : int, size : int) =
    inherit Data<'a> (1)
    
    /// Gets the array for this data.
    member this.Array = array

    /// Gets the offset of this data in the array.
    member this.Offset = offset

    /// Gets the size of this data.
    member this.NativeSize = size

    override this.Size = uint64 size
    override this.Read (index, targetArray, targetOffset, size) = Array.blit array (offset + int index) targetArray targetOffset size
    override this.Lock (index, size) = Stream.array array (offset + int index) |> Exclusive.make

/// Data that applies a mapping function to source data.
type MapData<'a, 'b when 'a : unmanaged and 'b : unmanaged> (source : Data<'b>, map : 'b -> 'a) =
    inherit Data<'a> (source.Alignment)

    override this.Size = source.Size

    override this.Read (index, array, offset, size) =
        let tempArray = Array.zeroCreate size
        source.Read (index, tempArray, 0, size)
        for index = 0 to tempArray.Length - 1 do
            array.[offset + index] <- map tempArray.[index]

    override this.Lock (index, size) = source.Lock (index, size) |> Exclusive.map (Stream.map map)

/// Data that combines fixed-size groups of items into single items.
type CombineData<'a, 'b when 'a : unmanaged and 'b : unmanaged> (source : Data<'b>, groupSize : int, combine : 'b[] * int -> 'a) =
    inherit Data<'a> (fit groupSize source.Alignment)

    override this.Size = source.Size / uint64 groupSize

    override this.Read (index, array, offset, size) =
        let sourceSize = size * groupSize
        let tempArray = Array.zeroCreate sourceSize
        source.Read (index * uint64 groupSize, tempArray, 0, sourceSize)
        for index = 0 to size - 1 do
            array.[offset + index] <- combine (tempArray, index * groupSize)
    
    override this.Lock (index, size) = source.Lock (index * uint64 groupSize, size * uint64 groupSize) |> Exclusive.map (Stream.combine groupSize combine)

/// Data created from a series of concatenated chunks.
type ChunkData<'a when 'a : unmanaged> (alignment : int) =
    inherit Data<'a> (alignment)
    let chunks = new System.Collections.Generic.List<uint64 * Data<'a>> ()
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

    override this.Read (index, array, offset, size) =
        let chunk, chunkIndex, chunkOffset = find index
        let rec read offset size (chunk : Data<'a>) chunkIndex chunkOffset =
            let chunkReadSize = chunk.Size - chunkOffset
            if size > int chunkReadSize then
                chunk.Read (chunkOffset, array, offset, int chunkReadSize)
                read (offset + int chunkReadSize) (size - int chunkReadSize) (snd chunks.[chunkIndex + 1]) (chunkIndex + 1) 0UL
            else
                chunk.Read (chunkOffset, array, offset, size)
        read offset size chunk chunkIndex chunkOffset

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

/// Data based on a seekable System.IO stream.
[<Sealed>]
type IOData (source : Stream) =
    inherit Data<byte> (1)

    /// Gets the System.IO stream source for this data.
    member this.Source = source

    override this.Size = uint64 source.Length

    override this.Read (index, array, offset, size) =
        source.Position <- int64 index
        source.Read (array, offset, size) |> ignore

    override this.Lock (index, size) = 
        source.Position <- int64 index
        new IOStream (source) :> Stream<byte> |> Exclusive.make

/// Contains functions for constructing and manipulating data.
module Data =

    /// Constructs data based on a buffer. Note that the buffer is referenced directly and 
    /// changes to the buffer will be reflected in the data.
    let buffer buffer size = new BufferData<'a> (buffer, size) :> Data<'a>

    /// Constructs data based on an array. Note that the array is referenced directly and 
    /// changes to the buffer will be reflected in the data.
    let array array offset size = new ArrayData<'a> (array, offset, size) :> Data<'a>

    /// Constructs data from the remaining items in the given stream.
    let make chunkSize (stream : Stream<'a> exclusive) =
        let data = new ChunkData<'a> (1)
        let streamobj = stream.Object
        let rec readChunk () =
            let array = Array.zeroCreate chunkSize
            let size = streamobj.Read (array, 0, chunkSize)
            data.Append (new ArrayData<'a> (array, 0, size) :> Data<'a>)
            if size = chunkSize then readChunk ()
        readChunk ()
        stream.Finish ()
        data :> Data<'a>

    /// Constructs data for the file at the given path.
    let file (path : MD.Path) = 
        let fs = new FileStream (path.Source, FileMode.Open)
        fs |> Exclusive.dispose |> Exclusive.map (fun fs -> new IOData (fs) :> Data<byte>)

    /// Constructs data whose source is an IO stream.
    let io (source : System.IO.Stream) = new IOData (source) :> Data<byte>

    /// Constructs a mapped form of the given data.
    let map map source = new MapData<'a, 'b> (source, map) :> Data<'a>

    /// Constructs data that combines fixed-sized groups into single items.
    let combine groupSize combine source = new CombineData<'a, 'b> (source, groupSize, combine) :> Data<'a>

    /// Constructs data that combines fixed-sized groups into single items.
    let split groupSize split source = new NotImplementedException () |> raise

    /// Gets a complete array copy of the given data. This should only be used on relatively small data.
    let toArray (data : Data<'a>) =
        let array = Array.zeroCreate (int data.Size)
        let stream = lock data
        data.Read (0UL, array, 0, array.Length)
        new ArrayData<'a> (array, 0, array.Length)

    /// Returns a version of the given data whose alignment is a factor of the requested alignment.
    let checkAlignment alignment (data : Data<'a>) =
        if data.Alignment % alignment = 0 then data
        else new NotImplementedException() |> raise

    /// Matches data for a buffer representation, if possible.
    let (|Buffer|_|) (data : Data<'a>) =
        match data with
        | :? BufferData<'a> as data -> Some data
        | _ -> None

    /// Matches data for a complete (no offset, matching size) array representation, if possible.
    let (|ArrayComplete|_|) (data : Data<'a>) =
        match data with
        | :? ArrayData<'a> as data when data.Offset = 0 && data.NativeSize = data.Array.Length -> Some data
        | _ -> None

    /// Matches data for an array representation, if possible.
    let (|Array|_|) (data : Data<'a>) =
        match data with
        | :? ArrayData<'a> as data -> Some data
        | _ -> None