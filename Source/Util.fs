module MD.Util

open System
open System.Runtime.InteropServices

/// Computes the greatest common divisor between two positive integers.
let rec gcd a b =
    if b = 0 then a
    else gcd b (a % b)

/// Computes the least common multiple of two positive integers.
let lcm a b = a * b / gcd a b

/// Gets the smallest integer that can be multipled by "a" to get a multiple of "b".
let fit a b = b / gcd a b

/// Rounds "a" up to the next highest multiple of "b"
let round a b = 
    let t = a + b - 1
    t - (t % b)

/// Creates a pinned handle to the given object and returns the handle and the address to the object.
let pin obj = 
    let handle = GCHandle.Alloc (obj, GCHandleType.Pinned)
    (handle, handle.AddrOfPinnedObject ())

/// Releases the given pinned handle.
let unpin (handle : GCHandle) = handle.Free()

/// Reverses the order of the bits in an integer.
let bitrev (x : uint32) =
    let x = ((x &&& 0xaaaaaaaau) >>> 1) ||| ((x &&& 0x55555555u) <<< 1)
    let x = ((x &&& 0xccccccccu) >>> 2) ||| ((x &&& 0x33333333u) <<< 2)
    let x = ((x &&& 0xf0f0f0f0u) >>> 4) ||| ((x &&& 0x0f0f0f0fu) <<< 4)
    let x = ((x &&& 0xff00ff00u) >>> 8) ||| ((x &&& 0x00ff00ffu) <<< 8)
    (x >>> 16) ||| (x <<< 16)

/// Calculates the log-base-2 of an integer.
let log2 (x : uint32) =
    let mutable x = x
    let mutable i = 0
    while x > 1u do
        i <- i + 1
        x <- x >>> 1
    i

/// Gets the next highest power of two of an integer.
let npow2 (x : uint32) =
    let x = x - 1u
    let x = x ||| (x >>> 1);
    let x = x ||| (x >>> 2);
    let x = x ||| (x >>> 4);
    let x = x ||| (x >>> 8);
    let x = x ||| (x >>> 16);
    x + 1u