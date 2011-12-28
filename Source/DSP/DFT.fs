namespace MD.DSP

open MD
open MD.Util
open MD.DSP.Util
open System

/// A method of computing a DFT (Discrete Fourier Transform) and corresponding IDFT of a certain size.
[<AbstractClass>]
type DFT (size : int) =
    
    /// Gets the sample size this DFT method is for.
    member this.Size = size

    /// Computes the DFT on real input using this method.
    abstract member ComputeReal : Buffer<float> * Buffer<Complex> -> unit

    /// Computes the DFT on complex input using this method.
    abstract member ComputeComplex : Buffer<Complex> * Buffer<Complex> -> unit

    /// Computes the IDFT on complex input using this method for the base DFT.
    member this.ComputeInverse (input, output) =
        conjugate input size
        this.ComputeComplex (input, output)
        conjugate input size
        conjugate output size
        scaleComplex (1.0 / float size) output size

/// A hard-coded, fast DFT method on 4 samples.
type QuickDFT private () =
    inherit DFT(4)
    static let instance = new QuickDFT ()

    /// The only instance of this class.
    static member Instance = instance

    override this.ComputeReal (input : Buffer<float>, output : Buffer<Complex>) =
        let mutable output = output
        let sample0 = input.[0]
        let sample1 = input.[1]
        let sample2 = input.[2]
        let sample3 = input.[3]
        output.[0] <- new Complex(sample0 + sample1 + sample2 + sample3, 0.0)
        output.[1] <- new Complex(sample0 - sample2, sample1 - sample3)
        output.[2] <- new Complex(sample0 - sample1 + sample2 - sample3, 0.0)
        output.[3] <- new Complex(sample0 - sample2, sample3 - sample1)

    override this.ComputeComplex (input : Buffer<Complex>, output : Buffer<Complex>) =
        let mutable output = output
        let sample0 = input.[0]
        let sample1 = input.[1]
        let sample2 = input.[2]
        let sample3 = input.[3]
        output.[0] <- new Complex(sample0.Real + sample1.Real + sample2.Real + sample3.Real, sample0.Imag + sample1.Imag + sample2.Imag + sample3.Imag)
        output.[1] <- new Complex(sample0.Real - sample1.Imag - sample2.Real + sample3.Imag, sample0.Imag + sample1.Real - sample2.Imag - sample3.Real)
        output.[2] <- new Complex(sample0.Real - sample1.Real + sample2.Real - sample3.Real, sample0.Imag - sample1.Imag + sample2.Imag - sample3.Imag)
        output.[3] <- new Complex(sample0.Real + sample1.Imag - sample2.Real - sample3.Imag, sample0.Imag - sample1.Real - sample2.Imag + sample3.Real)

/// A radix-2 Cooley Tukey FFT method. The given unit DFT method will be used to compute the DFT of
/// small sections of the input, which are then combined using the Cooley Tukey method. The total
/// size of the DFT must be some power of two multipled by the unit size.
type CooleyTukeyDFT (size : int, unit : DFT) =
    inherit DFT (size)
    let units = size / unit.Size
    let rounds = log2 (uint32 units)
    let units = size / unit.Size
    let unitOffsets = Array.zeroCreate<int> (int units)
    let twiddles = Array.zeroCreate<Complex> (size / 2)

    do 
        // Initialize units offsets to bit-reversed indices.
        let rl = 32 - rounds
        for unit = 0 to int units - 1 do
            unitOffsets.[unit] <- int (bitrev (uint32 unit) >>> rl)

        // Initialize twiddle factors
        let n = twiddles.Length
        let m = -Math.PI / float n
        for k = 0 to n - 1 do
            twiddles.[k] <- Complex.ExpImag (m * float k)

    new (size : int) = new CooleyTukeyDFT (size, QuickDFT.Instance)

    /// Gets the total size of the FFT window. This should be a multiple of two.
    member this.Size = size

    /// Gets the amount of "butterfly" rounds to be completed by the fft.
    member this.Rounds = rounds

    /// Gets the DFT method used for units with this FFT.
    member this.Unit = unit

    /// Gets the size of a unit for this FFT.
    member this.UnitSize = unit.Size

    /// Gets the total amount of units for the FFT.
    member this.Units = unitOffsets.Length

    /// Gets the initial input offsets for units of the FFT.
    member this.UnitOffsets = unitOffsets

    /// Gets the precomputed twiddle factors (of the form e ^ (-2.0 * pi * i * k / N)) for the FFT.
    member this.Twiddles = twiddles

    /// Initializes units from a real source.
    member this.InitializeUnitsReal (input : Buffer<float>, output : Buffer<Complex>) =
        let unitOffsets = this.UnitOffsets
        let unit = this.Unit
        let unitSize = unit.Size
        let units = this.Units
        let mutable unitIndex = 0
        while unitIndex < unitOffsets.Length do
            let offset = int unitOffsets.[unitIndex]
            let unitInput = (input.Advance offset).Skip units
            let unitOutput = output.Advance (unitSize * unitIndex)
            unit.ComputeReal (unitInput, unitOutput)
            unitIndex <- unitIndex + 1

    /// Initializes units from a complex source.
    member this.InitializeUnitsComplex (input : Buffer<Complex>, output : Buffer<Complex>) =
        let unitOffsets = this.UnitOffsets
        let unit = this.Unit
        let unitSize = unit.Size
        let units = this.Units
        let mutable unitIndex = 0
        while unitIndex < unitOffsets.Length do
            let offset = int unitOffsets.[unitIndex]
            let unitInput = (input.Advance offset).Skip units
            let unitOutput = output.Advance (unitSize * unitIndex)
            unit.ComputeComplex (unitInput, unitOutput)
            unitIndex <- unitIndex + 1

    /// Applies the "butterfly" rounds for a FFT.
    member this.ApplyRounds (output : Buffer<Complex>) =
        let twiddles = this.Twiddles
        let unitSize = this.UnitSize
        let units = this.Units
        let rounds = this.Rounds
        let mutable halfSize = unitSize
        let mutable units = units >>> 1
        while units > 0 do
            let mutable output = output
            let mutable unit = 0
            while unit < units do
                let mutable k = 0
                while k < halfSize do
                    let e = output.[k]
                    let o = output.[k + halfSize]
                    let twiddle = twiddles.[k * (twiddles.Length / halfSize)]
                    output.[k] <- (e + twiddle * o)
                    output.[k + halfSize] <- (e - twiddle * o)
                    k <- k + 1
                output <- output.Advance (halfSize * 2)
                unit <- unit + 1
            units <- units >>> 1
            halfSize <- halfSize <<< 1

    override this.ComputeReal (input, output) =
        this.InitializeUnitsReal (input, output)
        this.ApplyRounds (output)

    override this.ComputeComplex (input, output) =
        this.InitializeUnitsComplex (input, output)
        this.ApplyRounds (output)