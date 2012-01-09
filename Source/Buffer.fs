namespace MD

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

/// A sizeless, unmanaged alternative to an array. Buffers are generally faster than array when it comes
/// to retrieving and storing data, but they need a mutable data source to read and write from.
type Buffer<'a when 'a : unmanaged> (start : nativeint, stride : uint32) =
    struct
        
        /// Creates a buffer for an array. Note that the array should be pinned in order to access it
        /// with a buffer.
        static member FromArray (array : 'a[], offset : int) = 
            new Buffer<'a> (NativePtr.toNativeInt &&array.[0], uint32 sizeof<'a>)

        /// Pins an array and returns a buffer for it, along with a function to later unpin the array.
        static member PinArray (array : 'a[]) =
            let handle = GCHandle.Alloc (array, GCHandleType.Pinned)
            let buffer = new Buffer<'a> (handle.AddrOfPinnedObject (), uint32 sizeof<'a>)
            let unpin () = handle.Free ()
            (buffer, unpin)

        /// Gets or sets an item in this buffer.
        member inline this.Item
            with get index = NativePtr.read<'a> (NativePtr.ofNativeInt (this.Start + nativeint this.Stride * nativeint index))
            and set index value = NativePtr.write<'a> (NativePtr.ofNativeInt (this.Start + nativeint this.Stride * nativeint index)) value

            /// Gets or sets an item in this buffer.
        member inline this.Item
            with get (index : uint32) = NativePtr.read<'a> (NativePtr.ofNativeInt (this.Start + nativeint this.Stride * nativeint index))
            and set (index : uint32) value = NativePtr.write<'a> (NativePtr.ofNativeInt (this.Start + nativeint this.Stride * nativeint index)) value

        /// Advances this buffer by a certain amount of items, setting the new item zero to be the item at the specified index.
        member this.Advance index = new Buffer<'a> (start + nativeint stride * nativeint index, stride)

        /// Modifies this buffer to skips over items sequential items by multiplying the stride by the given amount.
        member this.Skip amount = new Buffer<'a> (start, stride * uint32 amount)

        /// Gets the pointer to the start of this buffer.
        member this.Start = start

        /// Gets the stride, or amount of bytes between items, for this buffer.
        member this.Stride = stride

        /// Casts this buffer to a buffer of another type.
        member this.Cast () = new Buffer<'b> (start, uint32 sizeof<'b>)

        /// Gets a buffer for a certain field of the items in this buffer, given the byte offset of the
        /// field from the start of an item.
        member this.Field offset = new Buffer<'b> (start + nativeint offset, stride)

        /// Creates a buffer from a native pointer. The stride of the buffer will be the actual size of the items
        /// in the buffer.
        static member FromPointer (pointer : nativeptr<'a>) =
            new Buffer<'a> (NativePtr.toNativeInt pointer, uint32 sizeof<'a>)

        /// Copies items from a source buffer into a destination buffer.
        static member Copy (source : Buffer<'a>, destination : Buffer<'a>, size : int) =
            let mutable destination = destination
            for t = 0 to size - 1 do
                destination.[t] <- source.[t]

        /// Copies items from a buffer to an array.
        static member Copy (source : Buffer<'a>, destination : 'a[], offset : int, size : int) =
            for t = 0 to size - 1 do
                destination.[t + offset] <- source.[t]

        /// Copies items from an array to a buffer.
        static member Copy (source : 'a[], offset : int, destination : Buffer<'a>, size : int) =
            let mutable destination = destination
            for t = 0 to size - 1 do
                destination.[t] <- source.[t + offset]

    end

/// Contains functions and methods related to Frames.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Buffer =

    /// Fills a buffer of the given size with the given value.
    let fill value (buffer : Buffer<'a>) size =
        let mutable buffer = buffer
        for t = 0 to size - 1 do
            buffer.[t] <- value

    /// Copies data between buffers.
    let copybb (source : Buffer<'a>) (destination : Buffer<'a>) size =
        Buffer.Copy (source, destination, size)

    /// Copies data from an array into a buffer.
    let copyba (source : Buffer<'a>) (destination : 'a[]) offset size =
        Buffer.Copy (source, destination, offset, size)

    /// Copies data from a buffer into an array.
    let copyab (source : 'a[]) offset (destination : Buffer<'a>) size =
        Buffer.Copy (source, offset, destination, size)