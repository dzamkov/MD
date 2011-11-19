module MD.Unsafe

open Microsoft.FSharp.NativeInterop


/// Copies memory from the given source location to the given destination.
let copy (src : nativeint) (dest : nativeint) (size : int) =
    let bsrc : nativeptr<byte> = NativePtr.ofNativeInt src
    let bdest : nativeptr<byte> = NativePtr.ofNativeInt dest

    for t = 0 to size do
        NativePtr.get bsrc t |> NativePtr.set bdest t