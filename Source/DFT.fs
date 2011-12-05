namespace MD

open System
open System.Numerics
open Microsoft.FSharp.NativeInterop

/// Contains functions for evaluating discrete Fourier transforms.
module DFT =
    
    /// Computes a DFT directly on real data.
    let computeReal (source : nativeptr<float>) (destination : nativeptr<Complex>) size =
        for k = 0 to size - 1 do
            let mutable total = Complex.Zero
            for n = 0 to size - 1 do
                let phase = Complex.Exp (new Complex (0.0, -2.0 * Math.PI * float k * float n / float size))
                total <- total + new Complex (NativePtr.get source n, 0.0) * phase
            NativePtr.set destination k total