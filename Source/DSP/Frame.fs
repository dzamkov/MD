namespace MD.DSP

open System

open MD
open MD.Util

/// A spectral convolution kernel that localizes a signal in frequency. Kernels are compactly-supported,
/// (have a limited bandwidth) over the spectrum.
type Kernel = {

    /// The window function that defines the shape of the kernel.
    Window : Window

    /// The size of the kernel in relation to the spectrum.
    Bandwidth : float

    /// The center of the kernel in relation to the spectrum. Note that a kernel may wrap around the ends of
    /// a spectrum due to aliasing.
    Center : float

    }

/// A sampled kernel for a discrete spectrum of a certain size.
type DiscreteKernel = {

    /// The sampled window for the kernel.
    Window : float[]

    /// The sample offset of the window in the spectrum. Note that the window may wrap around the ends of
    /// the spectrum.
    Offset : int

    }

/// Contains functions and methods related to frames and kernels.
module Frame =

    /// Creates a discrete form of the given kernel for a spectrum of the given size. The kernel will be normalized so
    /// that the total of all values is 1.0.
    let createDiscreteKernel spectrumSize kernelSize (kernel : Kernel) =
        let windowSize = float spectrumSize * kernel.Bandwidth
        let offset = int (float spectrumSize * kernel.Center) - kernelSize / 2
        let offset = (offset + spectrumSize) % spectrumSize
        { Window = Window.create kernel.Window windowSize kernelSize; Offset = offset }

    /// Applies a discrete kernel to a spectrum and reads the windowed spectral content to the given output buffer.
    let applyDiscreteKernel (spectrumBuffer : Buffer<Complex>) spectrumSize (kernel : DiscreteKernel) (outputBuffer : Buffer<Complex>) =
        let mutable outputBuffer = outputBuffer
        let window = kernel.Window
        let offset = kernel.Offset
        for t = 0 to window.Length - 1 do
            outputBuffer.[t] <- spectrumBuffer.[(t + offset) % spectrumSize] * window.[t]