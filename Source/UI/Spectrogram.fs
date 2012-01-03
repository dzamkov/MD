namespace MD.UI

open System
open System.Collections.Generic

open MD
open MD.Util
open MD.DSP

/// Defines a coloring for components in a spectrogram, based on the relative frequency (where 0.0 is 0 hz and 
/// 1.0 is the Nyquist frequency) and component value.
type SpectrogramColoring = Map<float * Complex, Color>

/// A time-frequency representation of a discrete waveform derived with a certain frame.
type Spectrogram (samples : Data<float>, frame : Frame) =

    /// Gets the sample data for this spectrogram.
    member this.Samples = samples

    /// Gets the frame for this spectrogram.
    member this.Frame = frame

    /// Creates a figure to display this spectrogram.
    member this.CreateFigure (coloring : SpectrogramColoring, area : Rectangle) =
        let sampleCount = 65536 * 8
        let height = 1024
        let supports = Frame.getSpectralSupportsPartial 0.008 frame sampleCount 0 (frame.Size / height)
        let width = supports.[0].Data.Length

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
        for t = 0 to height - 1 do
            let tempBuffer = sampleBuffer.Cast ()
            let support = supports.[t]
            Frame.applySupport spectrumBuffer sampleCount support tempBuffer
            Util.conjugate tempBuffer width
            DFT.computeComplex tempBuffer outputBuffer width
            Util.conjugate outputBuffer width
            for x = 0 to width - 1 do
                image.[x, height - t - 1] <- coloring.[0.0, outputBuffer.[x]]

        unpinSpectrum ()
        unpinSample ()
        unpinOutput ()

        Figure.placeImage area (Image.opaque image, image.Size) ImageInterpolation.Linear