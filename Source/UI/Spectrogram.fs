namespace MD.UI

open MD
open Util
open System
open System.Collections.Generic
open Microsoft.FSharp.NativeInterop

/// Defines a possible scaling of values in a spectrogram, based on frequency (where 0.0 is 0 hz and 
/// 1.0 is the sampling frequency). 
type SpectrogramScaling = float -> float -> float

/// Contains parameters for a spectrogram.
type SpectrogramParameters = {
    
    /// The monochannel sample data for the spectrogram
    Samples : float data

    /// The window to use for the spectrogram.
    Window : Window

    /// The size of the window, in samples.
    WindowSize : float

    /// The final scaling factor of spectrogram values.
    Scaling : SpectrogramScaling 

    /// The color gradient for the spectrogram.
    Gradient : Gradient

    }

/// Contains cached data for a spectrogram.
type SpectrogramCache = {

    /// The parameters this cache corresponds to.
    Parameters : SpectrogramParameters

    /// An instance of the window for the spectrogram.
    Window : float[]

    /// Parameters for FFT's of various sizes.
    FFTParameters : Dictionary<int, FFTParameters>

    } with

    /// Initializes a new spectrogram cache based on the given parameters.
    static member Initialize (parameters : SpectrogramParameters) = {
            Parameters = parameters
            Window = Window.create parameters.Window parameters.WindowSize (parameters.WindowSize |> ceil |> uint32 |> npow2 |> int)
            FFTParameters = new Dictionary<int, FFTParameters> ()
        }

    /// Gets the FFT parmeters for a fourier transform of the given size.
    member this.GetFFTParameters size =
        let mutable parameters = Unchecked.defaultof<FFTParameters>
        if this.FFTParameters.TryGetValue (size, &parameters) then parameters
        else
            let parameters = new FFTParameters (size)
            this.FFTParameters.Add (size, parameters)
            parameters

/// A tile image for a spectrogram.
type SpectrogramTile (cache : SpectrogramCache, sampleStart : uint64, sampleCount : uint64, minFrequency : float, maxFrequency : float, area : Rectangle) =
    inherit Tile (area)
    new (parameters : SpectrogramParameters, area : Rectangle) = new SpectrogramTile (SpectrogramCache.Initialize parameters, 0UL, parameters.Samples.Size, 0.0, 0.5, area)

    /// Gets the parameters for this spectrogram tile.
    member this.Parameters = cache.Parameters

    override this.RequestImage (suggestedSize, callback) =
        let width, height = suggestedSize
        let parameters = cache.Parameters
        let samples = parameters.Samples
        let totalSampleCount = samples.Size

        let windowBuffer = cache.Window
        let windowBufferSize = windowBuffer.Length
        let windowSize = parameters.WindowSize
        let inputSize = windowBufferSize
        let fftSize = inputSize
        let fftParameters = cache.GetFFTParameters fftSize

        let sampleCount = float sampleCount
        let inputDelta = sampleCount / float width

        // Determine how input data will be loaded.
        let getBuffers, getData =
            let readStart = sampleStart + uint64 (inputDelta * 0.5) - uint64 (inputSize / 2)
            if float inputSize > inputDelta then

                // Size of input is greater than delta between inputs, use just one buffer for the
                // entire span of data.
                let padding = max 0 (inputSize - int (inputDelta * 0.5))
                let totalInputSize = int (ceil (inputDelta * (float width - 1.0))) + inputSize
                let readOffset = max 0 -(int readStart)
                let readSize = int (min (uint64 totalInputSize) (totalSampleCount - readStart - uint64 readOffset))
                let getBuffers () =
                    let inputBuffer = Array.zeroCreate totalInputSize
                    samples.Read (readStart, inputBuffer, readOffset, readSize)
                    inputBuffer, Array.zeroCreate inputSize // A seperate intermediate buffer is needed to allow scaling and windowing.
                let getData inputBuffer intermediateBuffer index =
                    let start = min (int (float index * inputDelta)) (totalInputSize - inputSize)
                    Array.blit inputBuffer start intermediateBuffer 0 inputSize
                getBuffers, getData
            else

                // Size of delta is greater than the size of the input, read data in chunks.
                let getBuffers () = 
                    let buffer = Array.zeroCreate inputSize
                    buffer, buffer
                let getData inputBuffer intermediateBuffer index =
                    let readStart = readStart + uint64 (float index * inputDelta)
                    let readOffset = max 0 -(int readStart)
                    let readSize = int (min (uint64 inputSize) (totalSampleCount - readStart - uint64 readOffset))
                    if readSize <> inputSize then Array.fill intermediateBuffer 0 inputSize 0.0
                    samples.Read (readStart, intermediateBuffer, readOffset, readSize)
                getBuffers, getData

        // Determine how to blit FFT output data to an image.
        let gradient = parameters.Gradient
        let scaling = parameters.Scaling
        let minOutputFrequency = int (minFrequency * float fftSize)
        let maxOutputFrequency = int (maxFrequency * float fftSize)
        let height = maxOutputFrequency - minOutputFrequency
        let frequencyDelta = (maxFrequency - minFrequency) / float height 
        let blitLine (outputBuffer : Complex[]) (image : ColorBufferImage) x =
            let mutable y = 0
            let mutable frequency = minFrequency + frequencyDelta * 0.5
            while y < height do
                let scaling = scaling frequency
                image.[x, height - y - 1] <- gradient.GetColor (scaling outputBuffer.[y].Abs)
                y <- y + 1
                frequency <- frequency + frequencyDelta

        // Create image fill task.
        let task () =
            let image = new ColorBufferImage (width, height)
            let inputBuffer, intermediateBuffer = getBuffers ()
            let outputBuffer = Array.zeroCreate<Complex> fftSize

            // Pin buffers
            let outputHandle, outputPtr = pin outputBuffer
            let intermediateHandle, intermediatePtr = pin intermediateBuffer
            let windowHandle, windowPtr = pin windowBuffer

            let mutable index = 0
            while index < width do
                getData inputBuffer intermediateBuffer index
                DSignal.windowReal (NativePtr.ofNativeInt windowPtr) (NativePtr.ofNativeInt intermediatePtr) windowBufferSize
                DFT.computeReal (NativePtr.ofNativeInt intermediatePtr) (NativePtr.ofNativeInt outputPtr) fftParameters
                blitLine outputBuffer image index
                index <- index + 1

            // Unpin buffers
            unpin outputHandle
            unpin intermediateHandle
            unpin windowHandle

            // Return image
            image :> Image |> Exclusive.make

        Task.start (task >> callback)

    override this.Children =
        if sampleCount > 1UL then
            let leftSampleCount = sampleCount / 2UL
            let rightSampleCount = sampleCount - leftSampleCount
            let midSampleStart = sampleStart + leftSampleCount
            let midFrequency = (minFrequency + maxFrequency) / 2.0
            let center = area.Center
            if minFrequency = 0.0 then 
                Some [|
                        new SpectrogramTile (cache, sampleStart, leftSampleCount, minFrequency, midFrequency, new Rectangle (area.Left, center.X, area.Bottom, center.Y))
                        new SpectrogramTile (cache, sampleStart, leftSampleCount, midFrequency, maxFrequency, new Rectangle (area.Left, center.X, center.Y, area.Top))
                        new SpectrogramTile (cache, midSampleStart, rightSampleCount, minFrequency, midFrequency, new Rectangle (center.X, area.Right, area.Bottom, center.Y))
                        new SpectrogramTile (cache, midSampleStart, rightSampleCount, midFrequency, maxFrequency, new Rectangle (center.X, area.Right, center.Y, area.Top))
                    |]
            else
                
                // Only the lowest tiles of the spectrogram can have their frequency information refined.
                Some [|
                        new SpectrogramTile (cache, sampleStart, leftSampleCount, minFrequency, maxFrequency, new Rectangle (area.Left, center.X, area.Bottom, area.Top))
                        new SpectrogramTile (cache, midSampleStart, rightSampleCount, minFrequency, maxFrequency, new Rectangle (center.X, area.Right, area.Bottom, area.Top))
                    |]
        else None