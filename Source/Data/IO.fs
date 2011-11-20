namespace MD.Data

open System
open System.IO
open MD

/// A byte stream based on a System.IO stream.
type IOStream (source : Stream, closeOnFinish : bool) =

    /// Gets the System.IO stream source for this stream.
    member this.Source = source

    /// Indicates wether the source stream for this stream is closed when
    /// this stream is finished.
    member this.CloseOnFinish = closeOnFinish

    interface Stream<byte> with
        member this.Read item =
            match source.ReadByte () with
            | -1 -> false
            | x -> item <- byte x; true

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

    new (file : Path) = new IOData (new FileStream (file.Source, FileMode.Open))

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