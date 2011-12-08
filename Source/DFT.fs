namespace MD

open Util
open System
open Microsoft.FSharp.NativeInterop

/// Contains parameters and precomputed information to compute a FFT of a specific size.
type FFTParameters (size : int, unitSize : int) =
    let magnitude = log2 (uint32 size)
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

    new (size : int) = new FFTParameters (size, 8)

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

    /// Applies the "butterfly" rounds for a FFT.
    let applyRounds (destination : nativeptr<Complex>) (parameters : FFTParameters) =
        let twiddles = parameters.Twiddles
        let unitSize = int parameters.UnitSize
        let units = parameters.Units
        let rounds = parameters.Rounds
        let mutable halfSize = unitSize
        let mutable units = units >>> 1
        while units > 0 do
            let mutable curDestination = destination
            let mutable unit = 0
            while unit < units do
                let mutable k = 0
                while k < halfSize do
                    let e = NativePtr.get curDestination k
                    let o = NativePtr.get curDestination (k + halfSize)
                    let twiddle = twiddles.[k * (twiddles.Length / halfSize) / 2]
                    NativePtr.set curDestination k (e + twiddle * o)
                    NativePtr.set curDestination (k + halfSize) (e - twiddle * o)
                    k <- k + 1
                curDestination <- NativePtr.add curDestination (halfSize * 2)
                unit <- unit + 1
            units <- units >>> 1
            halfSize <- halfSize <<< 1

    /// Initializes FFT units from a real source.
    let initializeUnitsReal (source : nativeptr<float>) (destination : nativeptr<Complex>) (parameters : FFTParameters) = 
        let twiddles = parameters.Twiddles
        let unitOffsets = parameters.UnitOffsets
        let unitSize = int parameters.UnitSize
        let units = parameters.Units
        let mutable curDestination = destination
        let mutable unit = 0
        while unit < unitOffsets.Length do
            let offset = int unitOffsets.[unit]
            let mutable k = 0
            while k < unitSize do
                let mutable total = Complex.Zero
                let mutable n = 0
                while n < unitSize do
                    total <- total + twiddles.[k * n * (twiddles.Length / unitSize) % twiddles.Length] * NativePtr.get source (n * units + offset)
                    n <- n + 1
                NativePtr.write curDestination total
                curDestination <- NativePtr.add curDestination 1
                k <- k + 1
            unit <- unit + 1
        

    /// Computes a DFT on real data (with a power of two size).
    let computeReal (source : nativeptr<float>) (destination : nativeptr<Complex>) (parameters : FFTParameters) =
        initializeUnitsReal source destination parameters
        applyRounds destination parameters