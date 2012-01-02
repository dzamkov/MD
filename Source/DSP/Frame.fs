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
    /// kernel will be interpolated along its entirety.
    abstract GetSpectralKernel : int -> Buffer<Complex> * int -> unit
    default this.GetSpectralKernel index (buffer, size) =
        let tempArray = Array.zeroCreate<Complex> size
        let tempBuffer, unpinTemp = Buffer.PinArray tempArray
        this.GetTemporalKernel index (tempBuffer, size)
        DFT.computeComplex tempBuffer buffer size
        unpinTemp ()

/// A frame that contains linearly-spaced spectral kernels based on window functions. The "full" parameter determines
/// what portion of the spectrum the frame covers. A value of true indicates that the frame covers the entire spectrum and is
/// invertible for all signals. A value of false indicates that the frame covers the first half of the spectrum and is invertible
/// for real signals.
type LinearFrame (window : Window, windowSize : float, size : int, full : bool) =
    inherit Frame (size)
    let kernelSize = round (int (ceil windowSize)) 2 + 1

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