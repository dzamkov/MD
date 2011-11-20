namespace MD.Codec

open System

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

    /// Indicates that the context will no longer be used. This should not have an effect on the current content of the context, it will
    /// only prevent future calls to NextFrame.
    abstract member Finish : unit -> unit