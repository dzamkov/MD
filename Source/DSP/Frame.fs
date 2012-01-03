namespace MD.DSP

open System

open MD
open MD.Util

/// An collection of convolution kernels that localize a discrete signal in time and frequency. Kernel's are
/// ordered such that a kernel with a higher index has higher frequency content.
[<AbstractClass>]
type Frame (size : int) =

    /// Gets the amount of kernels in this frame.
    member this.Size = size

    /// Gets the size (in temporal samples) of the given kernel. This is the minimum size of the buffer
    /// the kernel can be written to.
    abstract GetKernelSize : int -> int

    /// Gets the maximum size (in temporal samples) of all kernels in the given range.
    abstract GetMaximumKernelSize : int * int -> int
    default this.GetMaximumKernelSize (index, count) =
        let rec getMax curMax (index, count) =
            if count = 0 then curMax
            else getMax (max curMax (this.GetKernelSize index)) (index + 1, count - 1)
        getMax 0 (index, count)

    /// Writes the temporal kernel with the given index to the given cyclical buffer. Note that the kernel will be 
    /// centered on sample 0, since that makes it more useful for convolution. If the buffer has extra space,
    /// the extra center samples will be zero-padded. 
    abstract GetTemporalKernel : int -> Buffer<Complex> * int -> unit
    default this.GetTemporalKernel index (buffer, size) =
        let tempArray = Array.zeroCreate<Complex> size
        let tempBuffer, unpinTemp = Buffer.PinArray tempArray
        this.GetSpectralKernel index (tempBuffer, size)
        DFT.computeInverse tempBuffer buffer size
        unpinTemp ()

    /// Writes the spectral kernel with the given index to the given buffer. If the buffer has extra space, the
    /// kernel will be interpolated along it.
    abstract GetSpectralKernel : int -> Buffer<Complex> * int -> unit
    default this.GetSpectralKernel index (buffer, size) =
        let tempArray = Array.zeroCreate<Complex> size
        let tempBuffer, unpinTemp = Buffer.PinArray tempArray
        this.GetTemporalKernel index (tempBuffer, size)
        DFT.computeComplex tempBuffer buffer size
        unpinTemp ()

/// A frame that contains linearly-spaced duplicate spectral kernels based on window functions. The "full" parameter determines
/// what portion of the spectrum the frame covers. A value of true indicates that the frame covers the entire spectrum and is
/// invertible for all signals. A value of false indicates that the frame covers the first half of the spectrum and is invertible
/// for real signals.
type LinearFrame (window : Window, windowSize : float, size : int, full : bool) =
    inherit Frame (size)
    let kernelSize = round (int (ceil windowSize)) 2 + 1

    /// Indicates wether this frame covers the full spectrum, as opposed to half.
    member this.Full = full

    override this.GetKernelSize index = kernelSize
    override this.GetMaximumKernelSize (index, count) = kernelSize

    override this.GetTemporalKernel index (buffer, size) =
        let mutable buffer = buffer
        let leadingSize = kernelSize / 2
        let trailingSize = kernelSize - leadingSize
        let paddingSize = size - kernelSize
        let freq = (if full then 1.0 else 0.5) * float index / float this.Size
        let mult = 1.0 / windowSize

        // Trailing half
        for t = 0 to trailingSize - 1 do
            buffer.[t] <- Complex.ExpImag (2.0 * Math.PI * freq * float t) * window (float t / windowSize) * mult
        buffer <- buffer.Advance trailingSize

        // Padding
        for t = 0 to paddingSize - 1 do
            buffer.[t] <- Complex.Zero
        buffer <- buffer.Advance paddingSize

        // Leading half
        for t = 0 to leadingSize - 1 do
            buffer.[t] <- Complex.ExpImag (2.0 * Math.PI * freq * float (t - leadingSize)) * window (float t / windowSize - 0.5) * mult

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

    /// Constructs a linear frame that covers the first half of the spectrum. This frame is invertible when applied to
    /// real signals (if there are enough kernels to cover the spectrum).
    let linearHalf window windowSize kernels = new LinearFrame (window, windowSize, kernels, false) :> Frame

    /// Constructs a linear frame that covers the first half of the spectrum. This frame is invertible when applied to
    /// complex signals (if there are enough kernels to cover the spectrum).
    let linearFull window windowSize kernels = new LinearFrame (window, windowSize, kernels, true) :> Frame

    /// Gets a support for a full-sized kernel. The support will have a power-of-two size (to allow for quick DFT's) and
    /// will contain all values of the kernel whose absolute values are above the given threshold.
    let getSupport threshold (kernel : Buffer<Complex>) kernelSize =
        let threshold = threshold * threshold

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

        // Determine the smallest interval that includes all samples above the threshold. Note that
        // the interval may wrap around the ends of the kernel.
        let supportStart, supportSize =
            let normalSize = lastAbove - firstAbove + 1
            let wrapSize = kernelSize - largestBelowSize
            if normalSize < wrapSize then (firstAbove, normalSize)
            else (largestBelowStart + largestBelowSize, wrapSize)

        // Round the size up to the nearest power of two, pushing the start position back to center the support on
        // the high values.
        let nSupportSize = int (npow2 (uint32 supportSize))
        let supportStart = supportStart - (nSupportSize - supportSize) / 2
        let supportSize = nSupportSize

        // If the support start offset is negative, make it wrap around.
        let supportStart = (supportStart % kernelSize + kernelSize) % kernelSize

        // Create support data.
        let supportData = Array.zeroCreate<Complex> supportSize
        for t = 0 to supportSize - 1 do
            supportData.[t] <- kernel.[(t + supportStart) % kernelSize]
        { Data = supportData; Offset = supportStart }

    /// Gets the supports for the spectral kernels (or a subset of them) in the given frame.
    let getSpectralSupportsPartial threshold (frame : Frame) kernelSize first delta =
        let supports = Array.zeroCreate<Support> (frame.Size / delta)
        let kernelArray = Array.zeroCreate<Complex> kernelSize
        let kernelBuffer, unpinKernel = Buffer.PinArray kernelArray
        match frame with
        | :? LinearFrame as frame -> 

            // By definition, the kernels for a linear frame are all the same, just shifted by a constant amount.
            frame.GetSpectralKernel 0 (kernelBuffer, kernelSize)
            let support = getSupport threshold kernelBuffer kernelSize
            let shiftOffset = kernelSize / frame.Size
            let shiftOffset = if frame.Full then shiftOffset else shiftOffset / 2
            for t = 0 to supports.Length - 1 do
                supports.[t] <- { Data = support.Data; Offset = (support.Offset + (shiftOffset * (first + t * delta))) % kernelSize }

        | frame ->
            for t = 0 to supports.Length - 1 do
                frame.GetSpectralKernel (first + t * delta) (kernelBuffer, kernelSize)
                supports.[t] <- getSupport threshold kernelBuffer kernelSize 
        unpinKernel ()
        supports

    /// Gets the supports for the spectral kernels in the given frame.
    let getSpectralSupports threshold (frame : Frame) kernelSize = getSpectralSupportsPartial threshold (frame : Frame) kernelSize 0 1