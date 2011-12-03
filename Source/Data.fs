namespace MD

open System
open System.IO
open Microsoft.FSharp.NativeInterop

/// An immutable collection of items indexed by an integer.
[<AbstractClass>]
type Data<'a> () = 

    /// Gets the current size of the array.
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

    /// Reads the item at the given index in this data.
    abstract member Read : index : uint64 -> 'a

    /// Copies items from this data (starting at the given index) into the given buffer.
    abstract member Read : index : uint64 * buffer : 'a[] * offset : int * size : int -> unit

    /// Copies items from this data (starting at the given index) into the given memory location.
    /// This should only be used for blittable types.
    abstract member Read : index : uint64 * destination : nativeint * size : int -> unit

    /// Creates a stream to read this data beginning at the given index. The given
    /// size sets a limit on the amount of items that can be read from the resulting
    /// stream, but does not ensure the stream will end after the given amount of items
    /// are read.
    abstract member Lock : index : uint64 * size : uint64 -> Stream<'a> exclusive

    /// Creates a stream to read this data beginning at the given index.
    member this.Lock (index : uint64) = this.Lock (index, this.Size - index)

     /// Creates a stream to read this data.
    member this.Lock () = this.Lock (0UL, this.Size)

    /// Gets the item at the given index in this data.
    member this.Item with get x = this.Read x

    override this.ToString () = String.Format ("{0} of {1}s", this.SizeString, typeof<'a>.Name)

// Create type abbreviation.
type 'a data = Data<'a>

/// A stream that reads from a data source.
[<Sealed>]
type DataStream<'a> (source : 'a data, index : uint64) =
    inherit Stream<'a> ()
    let mutable index = index

    /// Gets the data source this stream is reading from.
    member this.Source = source

    /// Gets the current index in the source data this stream is reading from.
    member this.Index = index

    override this.Read () =
        if index < source.Size then
            let item = source.[index]
            index <- index + 1UL
            Some item
        else None

    override this.Read (buffer, offset, size) =
        let readsize = int (min (uint64 size) (source.Size - index))
        source.Read (index, buffer, offset, readsize)
        index <- index + uint64 readsize
        readsize

    override this.Read (destination, size) =
        let readsize = int (min (uint64 size) (source.Size - index))
        source.Read (index, destination, readsize)
        index <- index + uint64 readsize
        readsize

/// Data from a buffer (array).
[<Sealed>]
type BufferData<'a> (buffer : 'a[], offset : int, size : int) =
    inherit Data<'a> ()
    
    /// Gets the buffer for this data.
    member this.Buffer = buffer

    /// Gets this data's offset in the source buffer.
    member this.Offset = offset

    /// Gets the size of this data.
    member this.NativeSize = size

    override this.Size = uint64 size
    override this.Read index = buffer.[int index + offset]
    override this.Read (index, destbuffer, destoffset, size) = Array.blit buffer (int index + offset) destbuffer destoffset size
    override this.Read (index, destination, size) = Memory.Copy (buffer, int index + offset, destination, uint32 size)
    override this.Lock (index, size) = new BufferStream<'a> (buffer, int index + offset) :> 'a stream |> Exclusive.``static``

/// Data created from a series of concatenated chunks.
type ChunkData<'a> () =
    inherit Data<'a> ()
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

    override this.Read index = 
        let chunk, chunkindex, chunkoffset = find index
        chunk.Read chunkoffset

    override this.Read (index, buffer, offset, size) =
        let chunk, chunkindex, chunkoffset = find index
        let rec read (offset, size, chunk : 'a data, chunkindex, chunkoffset) totalreadsize =
            let chunkremsize = chunk.Size - chunkoffset
            if size > int chunkremsize then
                chunk.Read (chunkoffset, buffer, offset, int chunkremsize)
                read (offset + int chunkremsize, size - int chunkremsize, snd chunks.[chunkindex], chunkindex + 1, 0UL) (totalreadsize + int chunkremsize)
            else
                chunk.Read (chunkoffset, buffer, offset, size)
        read (offset, size, chunk, chunkindex, chunkoffset) 0

    override this.Read (index, destination, size) =
        let itemsize = Memory.SizeOf<'a> ()
        let chunk, chunkindex, chunkoffset = find index
        let rec read (destination, size, chunk : 'a data, chunkindex, chunkoffset) totalreadsize =
            let chunkremsize = chunk.Size - chunkoffset
            if size > int chunkremsize then
                chunk.Read (chunkoffset, destination, int chunkremsize)
                read (destination + nativeint (uint32 chunkremsize * itemsize), size - int chunkremsize, snd chunks.[chunkindex], chunkindex + 1, 0UL) (totalreadsize + int chunkremsize)
            else
                chunk.Read (chunkoffset, destination, size)
        read (destination, size, chunk, chunkindex, chunkoffset) 0

    override this.Lock (index, size) =
        let chunk, chunkindex, chunkoffset = find index
        let chunkremsize = chunk.Size - chunkoffset
        if size < chunkremsize then
            chunk.Lock (chunkoffset, size)
        else
            let retrieve index =
                if index < chunks.Count then Some ((snd chunks.[index]).Lock (), index + 1)
                else None
            Stream.chunkInit (Some (chunk.Lock chunkoffset, chunkindex + 1)) retrieve

/// Byte data whose source is a region of memory.
[<Sealed>]
type UnsafeData<'a when 'a : unmanaged> (regionStart : nativeint, regionEnd : nativeint) =
    inherit Data<'a> ()
    let itemsize = Memory.SizeOf<'a> ()

    /// Gets the start of the memory region referenced by this data.
    member this.Start = regionStart

    /// Gets the end of the memory region referenced by this data.
    member this.End = regionEnd

    override this.Size = uint64 (regionEnd - regionStart) / uint64 itemsize
    override this.Read index = Memory.Read (regionStart + nativeint (uint32 index * itemsize))
    override this.Read (index, buffer, offset, size) = Memory.Copy (regionStart + nativeint (index * uint64 itemsize), buffer, offset, uint32 size)
    override this.Read (index, destination, size) = Memory.Copy (regionStart + nativeint (index * uint64 itemsize), destination, uint32 size)
    override this.Lock (index, size) = Stream.unsafe (regionStart + nativeint (index * uint64 itemsize)) regionEnd |> Exclusive.``static``

/// Data based on a seekable System.IO stream.
[<Sealed>]
type IOData (source : Stream) =
    inherit Data<byte> ()

    /// Gets the System.IO stream source for this data.
    member this.Source = source

    override this.Size = uint64 source.Length

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

    override this.Lock (index, size) = 
        source.Position <- int64 index
        new IOStream (source) :> byte stream |> Exclusive.``static``

/// Contains functions for constructing and manipulating data.
module Data =

    /// Constructs data based on a buffer. Note that the buffer is referenced directly and 
    /// changes to the buffer will be reflected in the data.
    let buffer buffer offset size = new BufferData<'a> (buffer, offset, size) :> 'a data

    /// Constructs data from the remaining items in the given stream.
    let make chunkSize (stream : 'a stream exclusive) =
        let data = new ChunkData<'a> ()
        let streamobj = stream.Object
        let rec readChunk () =
            let buffer = Array.zeroCreate chunkSize
            let size = streamobj.Read (buffer, 0, chunkSize)
            data.Append (new BufferData<'a> (buffer, 0, size) :> 'a data)
            if size = chunkSize then readChunk ()
        readChunk ()
        stream.Finish ()
        data

    /// Constructs data for the file at the given path.
    let file (path : MD.Path) = 
        let fs = new FileStream (path.Source, FileMode.Open)
        fs |> Exclusive.dispose |> Exclusive.map (fun fs -> new IOData (fs) :> byte data)

    /// Constructs data whose source is an IO stream.
    let io (source : System.IO.Stream) = new IOData (source) :> byte data

    /// Constructs data whose source is the given memory region.
    let unsafe regionStart regionEnd = new UnsafeData<'a> (regionStart, regionEnd) :> 'a data

    /// Constructs a stream to read the entirety of the given data.
    let lock (data : 'a data) : 'a stream exclusive = data.Lock ()

    /// Gets a complete buffer copy of the given data. This should only be used on relatively small data.
    let getBuffer (data : 'a data) =
        let buf = Array.zeroCreate (int data.Size)
        let str = lock data
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
        | :? BufferData<'a> as x when x.Offset = 0 && x.NativeSize = x.Buffer.Length -> x.Buffer
        | x -> getBuffer x

    /// Matches data for a buffer representation.
    let (|Buffer|) (data : 'a data) =
        match data with
        | :? BufferData<'a> as x -> (x.Buffer, x.Offset, x.NativeSize)
        | x -> (getBuffer x, 0, int x.Size)