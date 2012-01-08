namespace MD.UI

open System
open System.Collections.Generic

open MD
open MD.Util
open MD.DSP

/// Defines a coloring for components in a spectrogram, based on the relative frequency (where 0.0 is 0 hz and 
/// 1.0 is the Nyquist frequency) and square of the absolute value of the component.
type SpectrogramColoring = Map<float * float, Color>

/// Identifies a method of localizing a signal in time and frequency in a spectrogram.
type SpectrogramFrame =
    | Linear of Window * float
    | ConstantQ of Window * float * float

    /// Gets the kernel for the frame at the given y position.
    member this.GetKernel y =
        match this with
        | Linear (window, bandwidth) -> { Window = window; Bandwidth = bandwidth; Center = y * 0.5 }
        | ConstantQ (window, q, minFreq) ->
            let s = Math.Log minFreq
            let r = Math.Log (1.0 / 2.0)
            let center = Math.Exp (s + y * (r - s))
            let bandwidth = center / q
            { Window = window; Bandwidth = bandwidth; Center = center }

/// A time-frequency representation of a discrete waveform derived using a certain frame
type Spectrogram (samples : Data<float>, frame : SpectrogramFrame) =

    /// Gets the sample data for this spectrogram.
    member this.Samples = samples

    /// Gets the frame for this spectrogram.
    member this.Frame = frame

    /// Creates a figure to display this spectrogram.
    member this.CreateFigure (coloring : SpectrogramColoring, area : Rectangle) =
        let sampleCount = 65536 * 16
        let height = 1024
        let width = 2048
        let getKernel index =
            let kernel = frame.GetKernel (float index / float height)
            (kernel, Frame.createDiscreteKernel sampleCount width kernel)
        let kernels = Array.init height getKernel

        let image = new ArrayImage<Color> (width, height)

        let sampleArray = Array.zeroCreate<float> sampleCount
        samples.Read (0UL, sampleArray, 0, sampleCount)

        let outputArray = Array.zeroCreate<Complex> width
        let spectrumArray = Array.zeroCreate<Complex> sampleCount
        let spectrumBuffer, unpinSpectrum = Buffer.PinArray spectrumArray
        let sampleBuffer, unpinSample = Buffer.PinArray sampleArray
        let outputBuffer, unpinOutput = Buffer.PinArray outputArray

        DFT.computeReal sampleBuffer spectrumBuffer sampleCount
        let dft = DFT.get width
        for t = 0 to height - 1 do
            let kernel, discreteKernel = kernels.[t]
            let tempBuffer = sampleBuffer.Cast ()
            Frame.applyDiscreteKernel spectrumBuffer sampleCount discreteKernel tempBuffer
            Util.conjugate tempBuffer width
            dft.ComputeComplex (tempBuffer, outputBuffer)

            for x = 0 to width - 1 do
                image.[x, height - t - 1] <- coloring.[kernel.Center, outputBuffer.[x].SquareAbs / float width]

        unpinSpectrum ()
        unpinSample ()
        unpinOutput ()

        Figure.placeImage area (Image.opaque image, image.Size) ImageInterpolation.Linear