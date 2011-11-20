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
    let bsrc : nativeptr<byte> = NativePtr.ofNativeInt src
    let bdest : nativeptr<byte> = NativePtr.ofNativeInt dest

    for t = 0 to size do
        NativePtr.get bsrc t |> NativePtr.set bdest t

/// Copies data from the given array to the given destination.
let copyap (src : 'a[], size : int, offset : int) (dest : nativeint) =
    let handle = GCHandle.Alloc (src, GCHandleType.Pinned)
    let sizea = sizeof<'a>
    let start = handle.AddrOfPinnedObject () + nativeint (offset * size)
    let copysize = size * sizea 
    copypp start dest copysize
    handle.Free ()