namespace MD

/// An interface to a mouse-like user input source on a two-dimensional coordinate system.
type Probe = {
    /// The position of this probe.
    Position : Point signal

    /// The state of the primary button.
    Primary : bool signal

    /// The state of the secondary button.
    Secondary : bool signal

    /// Fires an event whenever the scroll wheel is used, with the amount that was scrolled by.
    Scroll : float event
    }

/// Contains functions for constructing and manipulating probes.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Probe =

    /// Transforms a probe in window coordinates to a probe in view coordinates, given the size of the window.
    let windowToView (size : Point signal) (probe : Probe) = 
        let npos = Feed.collate size probe.Position |> Feed.maps (fun (size, pos) -> new Point (pos.X / size.X * 2.0 - 1.0, -pos.Y / size.Y * 2.0 + 1.0))
        { probe with Position = npos }

/// An interface for user input.
type Input = {

    /// A collection of probes present in this input interface.
    Probes : Probe collection
    }