namespace MD.OpenTK

open MD
open MD.UI
open System
open System.Collections.Generic

/// An interface to an OpenGL graphics context that tracks resources and continuity for rendering a dynamic figure given
/// by a signal.
[<AbstractClass>]
type Display (figure : Figure signal) =

    /// Gets the signal that gives the figures rendered by this display.
    member this.Figure = figure

    /// Sets up, and updates the display with the OpenGL context on the current thread, using the given viewport size.
    abstract member Setup : width : int * height : int -> unit

    /// Renders the current figure for the display to the current OpenGL context.
    abstract member Render : unit -> unit