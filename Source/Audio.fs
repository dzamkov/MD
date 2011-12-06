namespace MD

/// Identifies an audio control event.
type AudioControl =
    | Play
    | Pause
    | Stop

/// Contains parameters for an audio output source.
type AudioOutputSourceParameters = {

    /// The stream from which the raw audio data is read. Note that multichannel samples should be interleaved
    /// in this stream.
    Stream : byte stream exclusive

    /// The sample rate, in samples per second for the audio output.
    SampleRate : int

    /// The amount of channels in the audio output.
    Channels : int

    /// The format to use for the audio output.
    Format : AudioFormat

    /// An event feed giving control signals for the audio output.
    Control : AudioControl event

    /// A feed giving the volume multiplier of the audio output.
    Volume : double signal

    /// A feed giving pitch (sample rate multiplier) of the audio output.
    Pitch : double signal
    }

/// Contains information about an audio output source.
type AudioOutputSource = {

    /// The currently playing sample in relation to the start of the source stream.
    Position : uint64 signal
}

/// An interface to an audio output device.
type AudioOutput =
    
    /// Tries creating an audio output source with the given parameters. The audio source will begin in the 
    /// paused state and will require a Play control event to start.
    abstract member Begin : AudioOutputSourceParameters -> AudioOutputSource option

    /// Stops all currently-playing sources and indicates the output will no longer be used.
    abstract member Finish : unit -> unit