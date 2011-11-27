namespace MD

/// An interface to multimedia content within a container format.
type Content () = 
    let mutable ignore : bool = false

    /// Gets or sets wether this content is to be ignored in the reading context. If so, frames for this content will still 
    /// be read, but will not be interpreted.
    member this.Ignore
        with get () = ignore
        and set x = ignore <- x

/// A context for a container that allows content to be read.
[<AbstractClass>]
type Context (content : Content[]) =
    
    /// Gets the content available in this context.
    member this.Content = content

    /// Reads the next frame of the context and updates the data of the content it corresponds to (if Ignore on that content is
    /// set to false). The parameter of this function will be set to the index (in the Content array of this file) of the content read.
    /// Returns false if there are no more frames in the container.
    abstract member NextFrame : contentIndex : int byref -> bool

/// Describes a multimedia container format that can store content within a stream.
[<AbstractClass>]
type Container (name : string) =
    static let mutable registry = new Registry<Container> ()
    static let mutable loadRegistry = new Registry<LoadContainerAction> ()

    /// Registers a new container format.
    static member Register (container : Container) = registry.Add container

    /// Registers a new load action to be used when loading containers. The given action
    /// will be given priority over all current load actions.
    static member RegisterLoad (load : LoadContainerAction) = loadRegistry.Add load

    /// Gets all registered container formats.
    static member Available : seq<Container> = seq(registry)

    /// Tries loading a context from data (with an optionally specified filename) using a previously-registered load
    /// action. If no action is able to load the data, None is returned. 
    static member Load (data : byte data exclusive, filename : string) = 
        loadRegistry |> Seq.tryPick (fun load -> load.Invoke (data, filename))

    /// Tries loading a context from the given file using a previously-registered load
    /// action. If no action is able to load the data, None is returned. 
    static member Load (file : Path) = 
        Container.Load (Data.file file, file.Name)

    /// Gets the user-friendly name of this container format.
    member this.Name = name

    /// Tries decoding content from the given input stream using this format.
    abstract member Decode : stream : byte stream exclusive -> Context exclusive option

    /// Tries encoding content to the given stream using this format.
    abstract member Encode : context : Context exclusive -> byte stream exclusive option

/// An action that loads a context from data (with an optionally-specified filename) using an unspecified container format. If
/// the action can not load the container, None is returned.
and LoadContainerAction = delegate of data : byte data exclusive * filename : string -> (Container * Context exclusive) option


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
    let mutable data : byte data option = None

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