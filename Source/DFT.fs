﻿namespace MD

open Util
open System
open Microsoft.FSharp.NativeInterop

/// A method of computing a DFT (Discrete Fourier Transform) and corresponding IDFT of a certain size.
[<AbstractClass>]
type DFT (size : int) =
    
    /// Gets the sample size this DFT method is for.
    member this.Size = size

    /// Computes the DFT on real input using this method.
    abstract member ComputeReal : Buffer<float> * Buffer<Complex> -> unit

    /// Computes the DFT on complex input using this method.
    abstract member ComputeComplex : Buffer<Complex> * Buffer<Complex> -> unit

    /// Computes the IDFT on complex input using this method.
    abstract member ComputeInverse : Buffer<Complex> * Buffer<Complex> -> unit

    default this.ComputeInverse (source, destination) =
        DSignal.conjugate source size
        this.ComputeComplex (source, destination)
        DSignal.conjugate source size
        DSignal.conjugate destination size
        DSignal.scaleComplex (1.0 / float size) destination size

/// A radix-2 Cooley Tukey FFT method. Note that both the total size and unit size of the
/// FFT must be a power of two.
type CooleyTukeyDFT (size : int, unitSize : int) =
    inherit DFT (size)
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
        let m = -2.0 * Math.PI / float n
        for k = 0 to n - 1 do
            twiddles.[k] <- Complex.ExpImag (m * float k)

    new (size : int) = new CooleyTukeyDFT (size, min size 8)

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

    /// Initializes units from a generic source.
    static member inline InitializeUnitsGeneric (dft : CooleyTukeyDFT, source : Buffer<'a>, destination : Buffer<Complex>) =
        let twiddles = dft.Twiddles
        let unitOffsets = dft.UnitOffsets
        let unitSize = int dft.UnitSize
        let units = dft.Units
        let mutable destination = destination
        let mutable unit = 0
        while unit < unitOffsets.Length do
            let offset = int unitOffsets.[unit]
            let mutable k = 0
            while k < unitSize do
                let mutable total = Complex.Zero
                let mutable n = 0
                while n < unitSize do
                    total <- total + twiddles.[k * n * (twiddles.Length / unitSize) % twiddles.Length] * source.[n * units + offset]
                    n <- n + 1
                destination.[k] <- total
                k <- k + 1
            destination <- destination.Advance unitSize
            unit <- unit + 1

    /// Initializes units from a real source.
    static member InitializeUnitsReal (dft, source : Buffer<float>, destination) = CooleyTukeyDFT.InitializeUnitsGeneric (dft, source, destination)

    /// Initializes units from a complex source.
    static member InitializeUnitsComplex (dft, source : Buffer<Complex>, destination) = CooleyTukeyDFT.InitializeUnitsGeneric (dft, source, destination)

    /// Applies the "butterfly" rounds for a FFT.
    static member ApplyRounds (dft : CooleyTukeyDFT, destination : Buffer<Complex>) =
        let twiddles = dft.Twiddles
        let unitSize = int dft.UnitSize
        let units = dft.Units
        let rounds = dft.Rounds
        let mutable halfSize = unitSize
        let mutable units = units >>> 1
        while units > 0 do
            let mutable destination = destination
            let mutable unit = 0
            while unit < units do
                let mutable k = 0
                while k < halfSize do
                    let e = destination.[k]
                    let o = destination.[k + halfSize]
                    let twiddle = twiddles.[k * (twiddles.Length / halfSize) / 2]
                    destination.[k] <- (e + twiddle * o)
                    destination.[k + halfSize] <- (e - twiddle * o)
                    k <- k + 1
                destination <- destination.Advance (halfSize * 2)
                unit <- unit + 1
            units <- units >>> 1
            halfSize <- halfSize <<< 1

    override this.ComputeReal (source, destination) =
        CooleyTukeyDFT.InitializeUnitsReal (this, source, destination)
        CooleyTukeyDFT.ApplyRounds (this, destination)

    override this.ComputeComplex (source, destination) =
        CooleyTukeyDFT.InitializeUnitsComplex (this, source, destination)
        CooleyTukeyDFT.ApplyRounds (this, destination)