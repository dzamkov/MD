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

/// Pins an object and performs some operation on a pointer to it.
let pin obj func =
    let handle = GCHandle.Alloc (obj, GCHandleType.Pinned)
    func (handle.AddrOfPinnedObject ())
    handle.Free ()