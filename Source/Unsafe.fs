module MD.Unsafe

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

/// Gets the size in bytes of an object of the given type.
let sizeof<'a> = 
    let t = typeof<'a>
    if t.IsValueType then Marshal.SizeOf t
    else IntPtr.Size

/// Copies memory from the given source location to the given destination.
let copypp (src : nativeint) (dest : nativeint) (size : int) =

    // Copy majority using ints
    let mutable isrc : nativeptr<int> = NativePtr.ofNativeInt src
    let mutable idest : nativeptr<int> = NativePtr.ofNativeInt dest
    let mutable tsize = size
    while tsize > 3 do
        NativePtr.read isrc |> NativePtr.write idest
        isrc <- NativePtr.add isrc 1
        idest <- NativePtr.add idest 1
        tsize <- tsize - 4

    // Copy remainder with bytes
    let mutable bsrc : nativeptr<byte> = NativePtr.ofNativeInt (NativePtr.toNativeInt isrc)
    let mutable bdest : nativeptr<byte> = NativePtr.ofNativeInt (NativePtr.toNativeInt idest)
    while tsize > 0 do
        NativePtr.read bsrc |> NativePtr.write bdest
        bsrc <- NativePtr.add bsrc 1
        bdest <- NativePtr.add bdest 1
        tsize <- tsize - 1

/// Copies data from the given array to the given destination.
let copyap (src : 'a[], offset : int) (dest : nativeint) (size : int) =
    let handle = GCHandle.Alloc (src, GCHandleType.Pinned)
    let sizea = sizeof<'a>
    let start = handle.AddrOfPinnedObject () + nativeint (offset * sizea)
    let copysize = size * sizea 
    copypp start dest copysize
    handle.Free ()

/// Copies data from the given memory location to the given destination array.
let copypa (src : nativeint) (dest : 'a[], offset : int) (size : int) =
    let handle = GCHandle.Alloc (dest, GCHandleType.Pinned)
    let sizea = sizeof<'a>
    let start = handle.AddrOfPinnedObject () + nativeint (offset * sizea)
    let copysize = size * sizea 
    copypp src start copysize
    handle.Free ()