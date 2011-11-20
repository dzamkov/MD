namespace MD.Codec

open MD
open MD.Data

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
    static member Load (data : Data<byte>, filename : string) = 
        loadRegistry |> Seq.tryPick (fun load -> load.Invoke (data, filename))

    /// Gets the user-friendly name of this container format.
    member this.Name = name

    /// Tries decoding content from the given input stream using this format.
    abstract member Decode : stream : Stream<byte> -> Context option

    /// Tries encoding content to the given stream using this format.
    abstract member Encode : context : Context -> Stream<byte> option

/// An action that loads a context from data (with an optionally-specified filename) using an unspecified container format. If
/// the action can not load the container, None is returned.
and LoadContainerAction = delegate of data : Data<byte> * filename : string -> (Container * Context) option