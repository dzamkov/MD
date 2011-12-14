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
            new Buffer<'a> (Memory.AddressOf (array, offset), Memory.SizeOf<'a> ())

        /// Pins an array and returns a buffer for it, along with a function to later unpin the array.
        static member PinArray (array : 'a[]) =
            let handle = GCHandle.Alloc (array, GCHandleType.Pinned)
            let buffer = new Buffer<'a> (handle.AddrOfPinnedObject (), Memory.SizeOf<'a> ())
            let unpin () = handle.Free ()
            (buffer, unpin)

        /// Creates a buffer from a native pointer. The stride of the buffer will be the actual size of the items
        /// in the buffer.
        static member FromPointer (pointer : nativeptr<'a>) =
            new Buffer<'a> (NativePtr.toNativeInt pointer, Memory.SizeOf<'a> ())

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
        member this.Cast () = new Buffer<'b> (start, Memory.SizeOf<'b> ())

        /// Copies the items in this buffer into another buffer.
        member this.CopyTo (buffer : Buffer<'a>, size : int) =
            if size > 0 then
                if uint32 stride <= Memory.SizeOf<'a> () && buffer.Stride = stride then
                    Memory.Copy (start, buffer.Start, uint32 size * uint32 stride)
                else
                    let mutable buffer = buffer
                    let mutable index = 0
                    while index < size do
                        buffer.[index] <- this.[index]
                        index <- index + 1

        /// Copies the items in this buffer into an array.
        member this.CopyTo (array : 'a[], offset : int, size : int) =
            if size > 0 then
                if uint32 stride = Memory.SizeOf<'a> () then
                    Memory.Copy (start, array, offset, uint32 size * uint32 stride)
                else
                    let mutable index = 0
                    while index < size do
                        array.[index] <- this.[index]
                        index <- index + 1

        /// Copies the items from a buffer into this buffer.
        member this.CopyFrom (buffer : Buffer<'a>, size : int) =
            if size > 0 then
                if uint32 stride <= Memory.SizeOf<'a> () && buffer.Stride = stride then
                    Memory.Copy (buffer.Start, start, uint32 size * uint32 stride)
                else
                    let mutable index = 0
                    while index < size do
                        this.[index] <- buffer.[index]
                        index <- index + 1

        /// Copies items from an array into this buffer.
        member this.CopyFrom (array : 'a[], offset : int, size : int) =
            if size > 0 then
                if uint32 stride = Memory.SizeOf<'a> () then
                    Memory.Copy (array, offset, start, uint32 size * uint32 stride)
                else
                    let mutable index = 0
                    while index < size do
                        this.[index] <- array.[index]
                        index <- index + 1

    end