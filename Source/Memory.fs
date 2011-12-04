module Memory

open System
open System.Runtime.InteropServices

    /// Pins an object and performs some operation on a pointer to it.
    let pin obj func =
        let handle = GCHandle.Alloc (obj, GCHandleType.Pinned)
        func (handle.AddrOfPinnedObject ())
        handle.Free ()