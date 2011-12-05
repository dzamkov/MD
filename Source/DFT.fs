namespace MD

open Util
open System
open Microsoft.FSharp.NativeInterop

/// Contains cache information to compute a FFT of a specific size.
type FFTCache (size : int) =
    let magnitude = log2 (uint32 size)
    let unitSize = 8
    let rounds = magnitude - log2 (uint32 unitSize)
    let units = size / unitSize
    let unitOffsets = Array.zeroCreate<int> (int units)
    let twiddles = Array.zeroCreate<Complex> (size / 2)

    do
        // Initialize units offsets to bit-reversed indices.
        let rl = 32 - rounds
        for unit = 0 to int units - 1 do
            unitOffsets.[unit] <- int (bitrev (uint32 unit) >>> rl)

        // Initialize twiddle factors
        let n = twiddles.Length
        for k = 0 to n - 1 do
            twiddles.[k] <- Complex.ExpImag (-2.0 * Math.PI * float k / float n)

    /// Gets the magnitude of the FFT window. This is log2 of the total size.
    member this.Magnitude = magnitude

    /// Gets the total size of the FFT window. This should be a multiple of two.
    member this.Size = size

    /// Gets the amount of "butterfly" rounds to be completed by the fft.
    member this.Rounds = rounds

    /// Gets the size, in samples, for groups of samples (units) that are computed by directly evaluating
    /// the DFT. This should be a multiple of two less than or equal to the total size.
    member this.UnitSize = unitSize

    /// Gets the total amount of units for the FFT.
    member this.Units = unitOffsets.Length

    /// Gets the initial input offsets for units of the FFT.
    member this.UnitOffsets = unitOffsets

    /// Gets the precomputed twiddle factors (of the form e ^ (-2.0 * pi * i * k / N)) for the FFT.
    member this.Twiddles = twiddles

/// Contains functions for evaluating discrete Fourier transforms.
module DFT =
    
    /// Computes a DFT on real data (with a power of two size).
    let computeReal (source : nativeptr<float>) (destination : nativeptr<Complex>) (cache : FFTCache) =
        
        // Initialize destination with direct DFT's of units
        let twiddles = cache.Twiddles
        let unitOffsets = cache.UnitOffsets
        let unitSize = int cache.UnitSize
        let units = cache.Units
        let mutable curDestination = destination
        for unit = 0 to unitOffsets.Length - 1 do
            let offset = int unitOffsets.[unit]
            for k = 0 to unitSize - 1 do
                let mutable total = Complex.Zero
                for n = 0 to unitSize - 1 do
                    total <- total + twiddles.[k * n * (twiddles.Length / unitSize) % twiddles.Length] * NativePtr.get source (n * units + offset)
                NativePtr.write curDestination total
                curDestination <- NativePtr.add curDestination 1

        // Apply butterfly rounds
        let rounds = cache.Rounds
        let mutable halfSize = unitSize
        let mutable units = units >>> 1
        for round = 0 to rounds - 1 do
            let mutable curDestination = destination
            for unit = 0 to units - 1 do
                for k = 0 to halfSize - 1 do
                    let e = NativePtr.get curDestination k
                    let o = NativePtr.get curDestination (k + halfSize)
                    let twiddle = twiddles.[k * (twiddles.Length / halfSize) / 2]
                    NativePtr.set curDestination k (e + twiddle * o)
                    NativePtr.set curDestination (k + halfSize) (e - twiddle * o)
                curDestination <- NativePtr.add curDestination (halfSize * 2)
            units <- units >>> 1
            halfSize <- halfSize <<< 1