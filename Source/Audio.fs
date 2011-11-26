namespace MD

/// Identifies an audio control event.
type AudioControl =
    | Play = 0
    | Pause = 1
    | Stop = 2

/// Contains parameters for an audio output source.
type AudioOutputParameters = {

    /// The stream from which the raw audio data is read. Note that multichannel samples should be interleaved
    /// in this stream.
    Stream : byte stream

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

/// An interface to an audio output device.
type AudioOutput =
    
    /// Tries creating an audio output source with the given parameters. If successful, a signal feed
    /// will be returned giving the current play position of the audio source in the input stream.
    /// The audio source will begin in the paused state and requires a Play control event to start.
    abstract member Begin : AudioOutputParameters -> int signal option

    /// Stops all currently-playing sources and indicates the output will no longer be used.
    abstract member Finish : unit -> unit