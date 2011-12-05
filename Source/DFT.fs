namespace MD

open System
open Microsoft.FSharp.NativeInterop

/// Contains functions for evaluating discrete Fourier transforms.
module DFT =
    
    /// Computes a DFT on real data (with a power of two size).
    let computeReal (source : nativeptr<float>) (destination : nativeptr<Complex>) size =
        for k = 0 to size - 1 do
            let mutable total = Complex.Zero
            let exp = -2.0 * Math.PI * float k / float size
            for n = 0 to size - 1 do
                let phase = Complex.ExpImag (exp * float n)
                total <- total + phase * NativePtr.get source n
            NativePtr.set destination k total