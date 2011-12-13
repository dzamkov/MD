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
    Samples : Data<float>

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

    /// Methods for DFT's of various sizes.
    DFTs : Dictionary<int, DFT>

    } with

    /// Initializes a new spectrogram cache based on the given parameters.
    static member Initialize (parameters : SpectrogramParameters) = {
            Parameters = parameters
            Window = Window.create parameters.Window parameters.WindowSize (parameters.WindowSize |> ceil |> uint32 |> npow2 |> int)
            DFTs = new Dictionary<int, DFT> ()
        }

    /// Gets the DFT method for a fourier transform of the given size.
    member this.GetDFT size =
        let mutable dftMethod = Unchecked.defaultof<DFT>
        if this.DFTs.TryGetValue (size, &dftMethod) then dftMethod
        else
            let dftMethod = new CooleyTukeyDFT (size) :> DFT
            this.DFTs.Add (size, dftMethod)
            dftMethod

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

        let windowArray = cache.Window
        let windowArraySize = windowArray.Length
        let windowSize = parameters.WindowSize
        let inputSize = windowArraySize
        let dftSize = inputSize
        let dft = cache.GetDFT dftSize

        let sampleCount = float sampleCount
        let inputDelta = sampleCount / float width

        // Determine how input data will be loaded.
        let getDataArrays, getData =
            let readStart = sampleStart + uint64 (inputDelta * 0.5) - uint64 (inputSize / 2)
            if float inputSize > inputDelta then

                // Size of input is greater than delta between inputs, use just one buffer for the
                // entire span of data.
                let padding = max 0 (inputSize - int (inputDelta * 0.5))
                let totalInputSize = int (ceil (inputDelta * (float width - 1.0))) + inputSize
                let readOffset = max 0 -(int readStart)
                let readSize = int (min (uint64 totalInputSize) (totalSampleCount - readStart - uint64 readOffset))
                let getDataArrays () =
                    let inputArray = Array.zeroCreate totalInputSize
                    samples.ReadArray (readStart, inputArray, readOffset, readSize)
                    inputArray, Array.zeroCreate inputSize // A seperate intermediate array is needed to allow scaling and windowing.
                let getData inputArray intermediateArray index =
                    let start = min (int (float index * inputDelta)) (totalInputSize - inputSize)
                    Array.blit inputArray start intermediateArray 0 inputSize
                getDataArrays, getData
            else

                // Size of delta is greater than the size of the input, read data in chunks.
                let getDataArrays () = 
                    let array = Array.zeroCreate inputSize
                    array, array
                let getData inputArray intermediateArray index =
                    let readStart = readStart + uint64 (float index * inputDelta)
                    let readOffset = max 0 -(int readStart)
                    let readSize = int (min (uint64 inputSize) (totalSampleCount - readStart - uint64 readOffset))
                    if readSize <> inputSize then Array.fill intermediateArray 0 inputSize 0.0
                    samples.ReadArray (readStart, intermediateArray, readOffset, readSize)
                getDataArrays, getData

        // Determine how to blit DFT output data to an image.
        let gradient = parameters.Gradient
        let scaling = parameters.Scaling
        let minOutputFrequency = int (minFrequency * float dftSize)
        let maxOutputFrequency = int (maxFrequency * float dftSize)
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
            let inputArray, intermediateArray = getDataArrays ()
            let outputArray = Array.zeroCreate<Complex> dftSize

            // Pin arrays
            let outputBuffer, unpinOutput = Buffer.PinArray outputArray
            let intermediateBuffer, unpinIntermediate = Buffer.PinArray intermediateArray
            let windowBuffer, unpinWindow = Buffer.PinArray windowArray

            let mutable index = 0
            while index < width do
                getData inputArray intermediateArray index
                DSignal.windowReal windowBuffer intermediateBuffer windowArraySize
                dft.ComputeReal (intermediateBuffer, outputBuffer)
                blitLine outputArray image index
                index <- index + 1

            // Unpin buffers
            unpinOutput ()
            unpinIntermediate ()
            unpinWindow ()

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
            Some [|
                    new SpectrogramTile (cache, sampleStart, leftSampleCount, minFrequency, midFrequency, new Rectangle (area.Left, center.X, area.Bottom, center.Y))
                    new SpectrogramTile (cache, sampleStart, leftSampleCount, midFrequency, maxFrequency, new Rectangle (area.Left, center.X, center.Y, area.Top))
                    new SpectrogramTile (cache, midSampleStart, rightSampleCount, minFrequency, midFrequency, new Rectangle (center.X, area.Right, area.Bottom, center.Y))
                    new SpectrogramTile (cache, midSampleStart, rightSampleCount, midFrequency, maxFrequency, new Rectangle (center.X, area.Right, center.Y, area.Top))
                |]
        else None