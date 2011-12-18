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

    /// The full sized (InputSize) window for the spectrogram.
    FullWindow : float[]

    /// Instances of the spectrogram window of various depths (downscaling factors). A window
    /// at depth x will have a size of (InputSize * 2 ^ -x).
    Windows : Dictionary<int, float[]>

    /// Methods for DFT's of various sizes.
    DFTs : Dictionary<int, DFT>

    } with

    /// Initializes a new spectrogram cache based on the given parameters.
    static member Initialize (parameters : SpectrogramParameters) = 
        let inputSize = parameters.WindowSize |> uint32 |> npow2 |> int
        let windows = new Dictionary<int, float[]> ()
        let fullWindow = Window.create parameters.Window parameters.WindowSize inputSize
        windows.Add (0, fullWindow)
        {
            Parameters = parameters
            InputSize = inputSize
            FullWindow = fullWindow
            Windows = windows
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
type SpectrogramTile (cache : SpectrogramCache, minTime : float, maxTime : float, depth : int, frequency : int, area : Rectangle) =
    inherit Tile (area)
    new (parameters : SpectrogramParameters, area : Rectangle) = new SpectrogramTile (SpectrogramCache.Initialize parameters, 0.0, 1.0, 0, 0, area)

    /// Gets the parameters for this spectrogram tile.
    member this.Parameters = cache.Parameters

    override this.RequestImage (suggestedSize, callback) =
        let width, height = suggestedSize
        let parameters = cache.Parameters
        let samples = parameters.Samples
        let totalSampleCount = samples.Size
        let windowSize = parameters.WindowSize
        let inputSize = cache.InputSize

        // Height must be a power of two size in order to use an FFT.
        let height = height |> uint32 |> npow2 |> int

        // Set an upper bound on height such that there is no frequency interpolation in
        // the result.
        let height = min height (inputSize / (1 <<< depth) / 2)

        // Determine the time range and input delta for the spectrogram.
        let minTime = minTime * float totalSampleCount
        let maxTime = maxTime * float totalSampleCount
        let timeRange = maxTime - minTime
        let inputDelta = timeRange / float width

        // Determine how to blit DFT output data to an image.
        let gradient = parameters.Gradient
        let scaling = parameters.Scaling
        let frequencyRange = 1.0 / float (1 <<< depth)
        let minFrequency = float frequency * frequencyRange
        let frequencyDelta = frequencyRange / float height 
        let blitLine (outputBuffer : Complex[]) (image : ArrayImage<float>) x =
            let mutable y = 0
            let mutable frequency = minFrequency + frequencyDelta * 0.5
            while y < height do
                let scaling = scaling frequency
                image.[x, height - y - 1] <- scaling outputBuffer.[y].Abs
                y <- y + 1
                frequency <- frequency + frequencyDelta

        // In every case, the DFT is the final operation before outputing color data and it is always
        // twice the height of the spectrogram (the upper half in the output is redundant).
        let dftSize = height * 2
        let dft = cache.GetDFT dftSize

        // Define the decimation function. This function will take a real time-domain signal and 
        // remove all frequency components not of interest to the spectrogram. The signal will be downscaled by
        // a factor of 2 ^ depth.
        let decimate (signal : Buffer<float>) (temp : Buffer<float>) size =

            // Precomputed, positive values of sinc, normalized to sum to 1.0
            let sinc1 = 1.030563412320212
            let sinc2 = 0.969436587679788
            let sinc3 = 0.617162499773511
            let sinc4 = 0.205720833257837
            let sinc5 = 0.123432499954702
            let sinc6 = 0.0881660713962159
            let sinc7 = 0.0685736110859457

            // Downsamples a signal by a factor of two, leaving only low frequency content.
            let decimateLow (input : Buffer<float>) (output : Buffer<float>) (size : int) =
                let mutable output = output
                let mutable index = 0
                while index < size / 2 do
                    let dindex = index * 2
                    let value = input.[dindex] * sinc2
                    let value = value + (input.[(dindex + size - 1) % size] + input.[(dindex + 1) % size]) * sinc3
                    let value = value - (input.[(dindex + size - 3) % size] + input.[(dindex + 3) % size]) * sinc4
                    let value = value + (input.[(dindex + size - 5) % size] + input.[(dindex + 5) % size]) * sinc5
                    let value = value - (input.[(dindex + size - 7) % size] + input.[(dindex + 7) % size]) * sinc6
                    let value = value + (input.[(dindex + size - 9) % size] + input.[(dindex + 9) % size]) * sinc7
                    output.[index] <- value
                    index <- index + 1

            // Downsamples a signal by a factor of two, leaving only high frequency content.
            let decimateHigh (input : Buffer<float>) (output : Buffer<float>) (size : int) =
                let mutable output = output
                let mutable index = 0
                while index < size / 2 do
                    let dindex = index * 2
                    let value = input.[dindex] * sinc1
                    let value = value - (input.[(dindex + size - 1) % size] + input.[(dindex + 1) % size]) * sinc3
                    let value = value + (input.[(dindex + size - 3) % size] + input.[(dindex + 3) % size]) * sinc4
                    let value = value - (input.[(dindex + size - 5) % size] + input.[(dindex + 5) % size]) * sinc5
                    let value = value + (input.[(dindex + size - 7) % size] + input.[(dindex + 7) % size]) * sinc6
                    let value = value - (input.[(dindex + size - 9) % size] + input.[(dindex + 9) % size]) * sinc7

                    // Normally, the high frequency content would appear upside-down, but, by negating every other
                    // other sample, the frequency components (below and above the Nyquist frequency) are reversed.
                    let value = if index % 2 = 0 then value else -value

                    output.[index] <- value
                    index <- index + 1

            let mutable signal = signal
            let mutable temp = temp
            let mutable size = size
            let mutable depth = depth
            let mutable frequency = frequency
            while depth > 0 do
                depth <- depth - 1
                if frequency &&& (1 <<< depth) > 0 then
                    decimateHigh signal temp size
                else
                    decimateLow signal temp size

                // Swap temp and source signals for the next operation.
                let newSignal, newTemp = (temp, signal)
                signal <- newSignal
                temp <- newTemp

                size <- size / 2

            // Return the buffer with the decimated signal.
            signal
                

        // Determine wether there is any overlap between windows. This decides wether separate sample
        // data is read for each window, or if the sample data is read (and processed) once for all
        // windows. Note that if the depth is zero, there is no benefit in doing the all at once approach
        // because no decimation is required.
        if inputDelta > float inputSize || true then

            // Use the full window, since decimation comes after window application using this
            // method.
            let windowArray = cache.FullWindow

            // Define task.
            let task () =
                let image = new ArrayImage<float> (width, height)
                let inputArray = Array.zeroCreate<float> inputSize
                let tempArray = Array.zeroCreate<float> (inputSize / 2)
                let outputArray = Array.zeroCreate<Complex> dftSize

                // Pin arrays.
                let inputBuffer, unpinInput = Buffer.PinArray inputArray
                let tempBuffer, unpinTemp = Buffer.PinArray tempArray
                let outputBuffer, unpinOutput = Buffer.PinArray outputArray
                let windowBuffer, unpinWindow = Buffer.PinArray windowArray

                let mutable index = 0
                while index < width do

                    // Load window data into input array.
                    let readStart = int64 (minTime + inputDelta * (float index + 0.5)) - int64 (inputSize / 2)
                    let readOffset, readStart = if readStart < 0L then (int -readStart, 0UL) else (0, uint64 readStart)
                    let readSize = int (min (uint64 (inputSize - readOffset)) (totalSampleCount - readStart))
                    if readSize <> inputSize then Array.fill inputArray 0 inputSize 0.0
                    samples.Read (readStart, inputArray, readOffset, readSize)

                    // Apply window function.
                    DSignal.windowReal windowBuffer inputBuffer inputSize

                    // Before decimation, we only need a signal with 2 * height * 2 ^ depth frequency samples, so
                    // we can apply some frequency downsampling to make the rest of the process easier.
                    let intermediateSize = height * (2 <<< depth)
                    DSignal.downsampleFrequencyReal (inputSize / intermediateSize) inputBuffer inputSize

                    // Now decimate to remove unwanted frequency content.
                    let intermediateBuffer = decimate inputBuffer tempBuffer intermediateSize

                    // Finally DFT!
                    dft.ComputeReal (intermediateBuffer, outputBuffer)

                    // And output the line.
                    blitLine outputArray image index
                    index <- index + 1

                // Unpin buffers.
                unpinTemp ()
                unpinInput ()
                unpinOutput ()
                unpinWindow ()

                // Return image.
                image |> Image.gradient gradient |> Image.opaque |> Exclusive.make

            Task.start (task >> callback)
        else
            new NotImplementedException () |> raise
        

    override this.Divisions =
        let nextDepth = depth + 1
        let lowFrequency = frequency * 2
        let highFrequency = lowFrequency + 1
        let midTime = (minTime + maxTime) / 2.0
        let center = area.Center
        Seq.singleton [|
                new SpectrogramTile (cache, minTime, midTime, nextDepth, lowFrequency, new Rectangle (area.Left, center.X, area.Bottom, center.Y))
                new SpectrogramTile (cache, minTime, midTime, nextDepth, highFrequency, new Rectangle (area.Left, center.X, center.Y, area.Top))
                new SpectrogramTile (cache, midTime, maxTime, nextDepth, lowFrequency, new Rectangle (center.X, area.Right, area.Bottom, center.Y))
                new SpectrogramTile (cache, midTime, maxTime, nextDepth, highFrequency, new Rectangle (center.X, area.Right, center.Y, area.Top))
            |]