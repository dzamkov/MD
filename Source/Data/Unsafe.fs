namespace MD.Data

open System
open Microsoft.FSharp.NativeInterop
open MD

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
        member this.Read item =
            if this.Current = this.End then false
            else 
                item <- NativePtr.read cur
                cur <- NativePtr.add cur 1
                true

        member this.Read (buffer, size, offset) =
            let readsize = min size this.Size
            for t = 0 to readsize do
                buffer.[offset + t] <- NativePtr.get cur t
            cur <- NativePtr.add cur readsize
            readsize

        member this.Read (destination, size) =
            let readsize = min size this.Size
            Unsafe.copypp (NativePtr.toNativeInt cur) destination readsize
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