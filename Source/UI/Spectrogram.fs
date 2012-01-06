namespace MD.UI

open System
open System.Collections.Generic

open MD
open MD.Util
open MD.DSP

/// Defines a coloring for components in a spectrogram, based on the relative frequency (where 0.0 is 0 hz and 
/// 1.0 is the Nyquist frequency) and component value.
type SpectrogramColoring = Map<float * Complex, Color>

/// A time-frequency representation of a discrete waveform.
type Spectrogram (samples : Data<float>) =

    /// Gets the sample data for this spectrogram.
    member this.Samples = samples

    /// Creates a figure to display this spectrogram.
    member this.CreateFigure (coloring : SpectrogramColoring, area : Rectangle) =
        let sampleCount = 65536 * 16
        let height = 2048
        let width = 4096
        let getKernel index =
            let center = float index / (float height * 2.0)
            Frame.createDiscreteKernel sampleCount width { Window = Window.hann; Bandwidth = 1.0 / 1024.0; Center = center }
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
        Util.scaleComplex (1.0 / float sampleCount) spectrumBuffer sampleCount
        let dft = DFT.get width
        for t = 0 to height - 1 do
            let tempBuffer = sampleBuffer.Cast ()
            Frame.applyDiscreteKernel spectrumBuffer sampleCount kernels.[t] tempBuffer
            Util.conjugate tempBuffer width
            dft.ComputeComplex (tempBuffer, outputBuffer)
            Util.conjugate outputBuffer width
            for x = 0 to width - 1 do
                image.[x, height - t - 1] <- coloring.[0.0, outputBuffer.[x]]

        unpinSpectrum ()
        unpinSample ()
        unpinOutput ()

        Figure.placeImage area (Image.opaque image, image.Size) ImageInterpolation.Linear