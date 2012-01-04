namespace MD.DSP

open System

open MD
open MD.Util

/// A continuous collection of discrete convolution kernels localized in time and frequency. Kernels are parameterized and ordered by
/// their frequency content such that kernel 0.0 will have most of its frequency content near 0 Hz, kernel 0.5 will have most of
/// its frequency content near the Nyquist frequency and kernel 1.0 will have most of its frequency content near the sample rate.
/// Every frequency in the spectrum should be included in at least one kernel of the frame, in order to allow the frame to be
/// invertible.
[<AbstractClass>]
type Frame () =

    /// Gets the size (in temporal samples) of the kernel for the given frequency parameter. This is the minimum size of the buffer
    /// the kernel can be written to.
    abstract GetKernelSize : float -> int

    /// Reads the temporal kernel with the given frequency parameter into the given cyclical buffer. Note that the kernel will be 
    /// centered on sample 0, since that makes it more useful for convolution. If the buffer has extra space, the extra center samples
    /// will be zero-padded. 
    abstract ReadTemporalKernel : float -> Buffer<Complex> * int -> unit
    default this.ReadTemporalKernel param (buffer, size) =
        let tempArray = Array.zeroCreate<Complex> size
        let tempBuffer, unpinTemp = Buffer.PinArray tempArray
        this.ReadSpectralKernel param (tempBuffer, size)
        DFT.computeInverse tempBuffer buffer size
        unpinTemp ()

    /// Reads the spectral kernel with the given frequency parameter to the given buffer. If the buffer has extra space, the
    /// kernel will be interpolated along it.
    abstract ReadSpectralKernel : float -> Buffer<Complex> * int -> unit
    default this.ReadSpectralKernel param (buffer, size) =
        let tempArray = Array.zeroCreate<Complex> size
        let tempBuffer, unpinTemp = Buffer.PinArray tempArray
        this.ReadTemporalKernel param (tempBuffer, size)
        DFT.computeComplex tempBuffer buffer size
        unpinTemp ()

/// A frame that contains kernels which are congruent and linearly-spaced on the spectrum. The basic temporal kernel is defined
/// by a window function.
type LinearFrame (window : Window, windowSize : float) =
    inherit Frame ()
    let kernelSize = round (int (ceil windowSize)) 2 + 1

    override this.GetKernelSize param = kernelSize

    override this.ReadTemporalKernel param (buffer, size) =
        let mutable buffer = buffer
        let leadingSize = kernelSize / 2
        let trailingSize = kernelSize - leadingSize
        let paddingSize = size - kernelSize
        let mult = 1.0 / windowSize

        // Trailing half
        for t = 0 to trailingSize - 1 do
            buffer.[t] <- Complex.ExpImag (2.0 * Math.PI * param * float t) * window (float t / windowSize) * mult
        buffer <- buffer.Advance trailingSize

        // Padding
        for t = 0 to paddingSize - 1 do
            buffer.[t] <- Complex.Zero
        buffer <- buffer.Advance paddingSize

        // Leading half
        for t = 0 to leadingSize - 1 do
            buffer.[t] <- Complex.ExpImag (2.0 * Math.PI * param * float (t - leadingSize)) * window (float t / windowSize - 0.5) * mult

/// Data for a part of kernel where values are highest.
type Support = {

    /// The data for the support.
    Data : Complex[]

    /// The offset of the support in the full kernel. If the data for the support goes past
    /// the end of the full kernel, it will wrap around to the beginning.
    Offset : int

    }

/// Contains functions and methods related to Frames.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Frame =

    /// Constructs a linear frame from the given window function.
    let linear window windowSize = new LinearFrame (window, windowSize) :> Frame

    /// Gets the minimum size of a support that includes all components in the given kernel
    /// with a magnitude above the given threshold.
    let getSupportSize threshold (kernel : Buffer<Complex>) kernelSize =

        // Find the first and last values that are above the threshold and the
        // largest consecutive sequence of values that are below the threshold.
        let mutable firstAbove = kernelSize
        let mutable lastAbove = 0
        let mutable largestBelowStart = 0
        let mutable largestBelowSize = 0
        let mutable currentBelowStart = 0
        for t = 0 to kernelSize - 1 do
            if kernel.[t].SquareAbs > threshold then
                firstAbove <- min firstAbove t
                lastAbove <- t

                if t - currentBelowStart > largestBelowSize then
                    largestBelowStart <- currentBelowStart
                    largestBelowSize <- t - currentBelowStart
                currentBelowStart <- t + 1
        if kernelSize - currentBelowStart > largestBelowSize then 
            largestBelowStart <- currentBelowStart
            largestBelowSize <- kernelSize - currentBelowStart

        // Find the smallest possible support size based on the minimum sizes of a wrapping support (wraps around the ends of the
        // kernel) and a non-wrapping support.
        let normalSize = lastAbove - firstAbove + 1
        let wrapSize = kernelSize - largestBelowSize
        min normalSize wrapSize

    /// Finds the offset of the best support for a full-sized kernel such that the support will have the given size and
    /// and the greatest possible sum of the square magnitudes of its components.
    let findBestSupportOffset (kernel : Buffer<Complex>) kernelSize supportSize =
        let mutable total = 0.0

        /// Compute initial total square magnitude for when the offset is zero.
        for t = 0 to supportSize - 1 do
            total <- total + kernel.[t].SquareAbs

        /// Search for the offset that has the greatest total square magnitude. (Note that the support can wrap around the ends of the kernel)
        let mutable maxTotal = total
        let mutable maxOffset = 0
        for t = 0 to kernelSize - 2 do
            total <- total + kernel.[(t + supportSize) % kernelSize].SquareAbs - kernel.[t].SquareAbs
            if total > maxTotal then
                maxTotal <- total
                maxOffset <- t + 1

        maxOffset

    /// Reads a support from a kernel into a buffer, given the support size and offset in the kernel.
    let readSupport (kernel : Buffer<Complex>) kernelSize (support : Buffer<Complex>) supportSize supportOffset =
        let mutable support = support
        for t = 0 to supportSize - 1 do
            support.[t] <- kernel.[(t + supportOffset) % kernelSize]

    /// Gets the supports for the spectral kernels in the given frame. The spectrum size should have a power-of-two size 
    /// that is greater than or equal to the sizes of all kernels in the specified range. All returned supports will have 
    /// a power-of-two size in order to allow for quick DFT's. 
    let getSpectralSupports threshold (frame : Frame) spectrumSize kernelStart kernelDelta kernelCount =
        let supports = Array.zeroCreate<Support> kernelCount
        let kernelArray = Array.zeroCreate<Complex> spectrumSize
        let tempArray = Array.zeroCreate<Complex> spectrumSize
        let kernelBuffer, unpinKernel = Buffer.PinArray kernelArray
        let tempBuffer, unpinTemp = Buffer.PinArray tempArray
        
        let supportBuffer = tempBuffer
        let supportTemporalBuffer = kernelBuffer

        for t = 0 to kernelCount - 1 do
            let kernelParam = kernelStart + kernelDelta * float t
            let kernelSize = npow2i (frame.GetKernelSize kernelParam)

            // Read spectral kernel data and find its support.
            frame.ReadSpectralKernel kernelParam (kernelBuffer, kernelSize)
            let supportSize = npow2i (getSupportSize threshold kernelBuffer kernelSize)
            let supportOffset = findBestSupportOffset kernelBuffer kernelSize supportSize
            readSupport kernelBuffer kernelSize supportBuffer supportSize supportOffset

            // Upsample the support to fit the spectrum size.
            let upsampleFactor = spectrumSize / kernelSize
            let newSupportSize = supportSize * upsampleFactor
            Util.scaleComplex (1.0 / float supportSize) supportBuffer supportSize
            DFT.computeComplex supportBuffer supportTemporalBuffer supportSize
            Util.conjugate supportTemporalBuffer supportSize
            Buffer.copy (supportTemporalBuffer.Advance (supportSize / 2)) (supportTemporalBuffer.Advance (newSupportSize - supportSize / 2)) (supportSize / 2)
            Buffer.fill Complex.Zero (supportTemporalBuffer.Advance (supportSize / 2 + 1)) (newSupportSize - supportSize - 1)
           
            let supportSize = newSupportSize
            let supportOffset = supportOffset * upsampleFactor

            let supportArray = Array.zeroCreate<Complex> supportSize
            let supportBuffer, unpinSupport = Buffer.PinArray supportArray

            DFT.computeComplex supportTemporalBuffer supportBuffer supportSize
            Util.conjugate supportBuffer supportSize

            unpinSupport ()

            // Set support.
            supports.[t] <- { Data = supportArray; Offset = supportOffset }

            
        unpinTemp ()
        unpinKernel ()
        supports

    /// Applies a support to a signal and writes the resulting product to the given output buffer.
    let applySupport (signal : Buffer<Complex>) signalSize (support : Support) (output : Buffer<Complex>) =
        let supportData = support.Data
        let supportSize = supportData.Length
        let supportOffset = support.Offset

        let mutable output = output
        for t = 0 to supportSize - 1 do
            output.[t] <- signal.[(supportOffset + t) % signalSize] * supportData.[t]