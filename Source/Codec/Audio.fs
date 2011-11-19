namespace MD.Codec

open MD.Data

/// Identifies an audio format for a sample of a single channel.
type AudioFormat =
    | PCM8 = 0
    | PCM16 = 1
    | PCM32 = 2
    | Float = 3
    | Double = 4

/// An interface to audio content in a container.
type AudioContent (sampleRate : double, channels : int, format : AudioFormat) =
    inherit Content ()
    let mutable data : Data<byte> option = None

    /// Determines the amount of bytes in a sample of the given audio format.
    static member BytesPerSample (format : AudioFormat) =
        match format with
        | AudioFormat.PCM8 -> 1
        | AudioFormat.PCM16 -> 2
        | AudioFormat.PCM32 -> 4
        | AudioFormat.Float -> 4
        | AudioFormat.Double -> 8
        | x -> 0

    /// Gets the sample rate, in samples per second, for this audio content.
    member this.SampleRate = sampleRate

    /// Gets the amount of channels in this audio content. Multichannel audio content will have
    /// sample data for channels interleaved in the data array.
    member this.Channels = channels

    /// Gets the format for this audio content.
    member this.Format = format

    /// Gets or sets the audio data for the current frame. This should be updated when a call to
    /// Context.NextFrame returns a content index for this audio content.
    member this.Data
        with get () = data
        and set x = data <- x