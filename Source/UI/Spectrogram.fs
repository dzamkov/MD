namespace MD.UI

open MD
open Util
open System
open System.Collections.Generic
open Microsoft.FSharp.NativeInterop

/// Defines a possible scaling of values in a spectrogram, based on frequency (where 0.0 is 0 hz and 
/// 1.0 is the Nyquist frequency). 
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
    
    /// The amount of input samples needed for a vertical (frequency) line of the spectrogram. This is the
    /// next power-of-two size after the window size.
    InputSize : int

    /// Instances of the spectrogram window of various depths (downscaling factors). A window
    /// at depth x will have a size of (InputSize * 2 ^ -x).
    Windows : Dictionary<int, float[]>

    /// Methods for DFT's of various sizes.
    DFTs : Dictionary<int, DFT>

    } with

    /// Initializes a new spectrogram cache based on the given parameters.
    static member Initialize (parameters : SpectrogramParameters) = {
            Parameters = parameters
            InputSize = parameters.WindowSize |> uint32 |> npow2 |> int
            Windows = new Dictionary<int, float[]> ()
            DFTs = new Dictionary<int, DFT> ()
        }

    /// Gets the DFT method for a fourier transform of the given size.
    member this.GetDFT size =
        let mutable dft = Unchecked.defaultof<DFT>
        if this.DFTs.TryGetValue (size, &dft) then dft
        else
            let dft = new CooleyTukeyDFT (size) :> DFT
            this.DFTs.Add (size, dft)
            dft

    /// Gets a spectrogram window for the given depth.
    member this.GetWindow depth =
        let mutable window = null
        if this.Windows.TryGetValue (depth, &window) then window
        else
            let parameters = this.Parameters
            let divisor = 1 <<< depth
            let window = Window.create parameters.Window (parameters.WindowSize / float divisor) (this.InputSize / divisor)
            this.Windows.Add (depth, window)
            window

/// A tile image for a spectrogram.
type SpectrogramTile (cache : SpectrogramCache, sampleStart : uint64, sampleCount : uint64, depth : int, frequency : int, area : Rectangle) =
    inherit Tile (area)
    new (parameters : SpectrogramParameters, area : Rectangle) = new SpectrogramTile (SpectrogramCache.Initialize parameters, 0UL, parameters.Samples.Size, 0, 0, area)

    /// Gets the parameters for this spectrogram tile.
    member this.Parameters = cache.Parameters

    override this.RequestImage (suggestedSize, callback) =
        let width, height = suggestedSize
        let parameters = cache.Parameters
        let samples = parameters.Samples
        let totalSampleCount = samples.Size

        let windowArray = cache.GetWindow depth
        let windowArraySize = windowArray.Length
        let windowSize = parameters.WindowSize
        let inputSize = cache.InputSize

        // Height must be a power of two size in order to use an FFT.
        let height = height |> uint32 |> npow2 |> int

        // The maximum possible height, without frequency interpolation, is one-half the window size for this 
        // depth. The upper half of the frequency spectrum is ignored, since it is redundant.
        let height = min height (windowArraySize / 2)

        // Instead of doing one FFT on the whole window, we can break it down into multiple FFT's which we
        // sum together. This reduces computation time at the cost of decreasing frequency resolution, but 
        // since we only need a fixed amount of frequency samples, noone will notice.
        let dftSize = height * 2
        let dftCount = windowArraySize / dftSize
        let multipleDFT = dftCount > 1
        let dft = cache.GetDFT dftSize

        // Determine how input data will be loaded.
        let sampleCount = float sampleCount
        let inputDelta = sampleCount / float width
        let readStart = sampleStart + uint64 (inputDelta * 0.5) - uint64 (inputSize / 2)
        let getData inputArray index =
            let readStart = readStart + uint64 (float index * inputDelta)
            let readOffset = max 0 -(int readStart)
            let readSize = int (min (uint64 inputSize) (totalSampleCount - readStart - uint64 readOffset))
            if readSize <> inputSize then Array.fill inputArray 0 inputSize 0.0
            samples.ReadArray (readStart, inputArray, readOffset, readSize)

        // Determine how to blit DFT output data to an image.
        let gradient = parameters.Gradient
        let scaling = parameters.Scaling
        let frequencyRange = 1.0 / float (1 <<< depth)
        let minFrequency = float frequency * frequencyRange
        let frequencyDelta = frequencyRange / float height 
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
            let inputArray = Array.zeroCreate<float> inputSize
            let outputArray = Array.zeroCreate<Complex> (if dftCount > 1 then dftSize * 2 else dftSize)

            // Pin arrays
            let inputBuffer, unpinInput = Buffer.PinArray inputArray
            let outputBuffer, unpinOutput = Buffer.PinArray outputArray
            let windowBuffer, unpinWindow = Buffer.PinArray windowArray

            // If there are multiple DFTs, we need another section of the output buffer to store the temporary
            // results before adding it to actual output buffer.
            let tempOutputBuffer = outputBuffer.Advance dftSize

            let mutable index = 0
            while index < width do
                getData inputArray index
                DSignal.windowReal windowBuffer inputBuffer windowArraySize

                // Perform initial DFT to the actual output buffer.
                dft.ComputeReal (inputBuffer, outputBuffer)

                // Perform additional DFTs.
                let mutable inputBuffer = inputBuffer.Advance dftSize
                for dftIndex = 1 to dftCount - 1 do
                    dft.ComputeReal (inputBuffer, tempOutputBuffer)
                    DSignal.addComplex tempOutputBuffer outputBuffer dftSize
                    inputBuffer <- inputBuffer.Advance dftSize

                blitLine outputArray image index
                index <- index + 1

            // Unpin buffers
            unpinInput ()
            unpinOutput ()
            unpinWindow ()

            // Return image
            image :> Image |> Exclusive.make

        Task.start (task >> callback)

    override this.Children =
        if sampleCount > 1UL then
            let leftSampleCount = sampleCount / 2UL
            let rightSampleCount = sampleCount - leftSampleCount
            let midSampleStart = sampleStart + leftSampleCount
            let nextDepth = depth + 1
            let lowFrequency = frequency * 2
            let highFrequency = lowFrequency + 1
            let center = area.Center
            Some [|
                    new SpectrogramTile (cache, sampleStart, leftSampleCount, nextDepth, lowFrequency, new Rectangle (area.Left, center.X, area.Bottom, center.Y))
                    new SpectrogramTile (cache, sampleStart, leftSampleCount, nextDepth, highFrequency, new Rectangle (area.Left, center.X, center.Y, area.Top))
                    new SpectrogramTile (cache, midSampleStart, rightSampleCount, nextDepth, lowFrequency, new Rectangle (center.X, area.Right, area.Bottom, center.Y))
                    new SpectrogramTile (cache, midSampleStart, rightSampleCount, nextDepth, highFrequency, new Rectangle (center.X, area.Right, center.Y, area.Top))
                |]
        else None