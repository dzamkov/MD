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

/// An interface for user input.
type Input = {

    /// A collection of unlocked probes available for use in this input interface.
    Probes : (Probe * identifier) collection

    /// Locks the probe with the given identifier and returns a retract action to later unlock it.
    Lock : identifier -> RetractAction

    }

/// Contains functions for constructing and manipulating inputs.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Input =

    /// Applies a mapping to all probes in the given input.
    let mapProbes map (input : Input) = { input with Probes = input.Probes |> Feed.mapc (fun (probe, identifier) -> (map probe, identifier)) }

    /// Applies a constant spatial transformation to the given probe.
    let transformProbe (transform : Transform) (probe : Probe) =
        let newPosition = probe.Position |> Feed.maps transform.Apply
        { probe with Position = newPosition }
    
    /// Applies a constant spatial transformation to the given input context.
    let transform transform = mapProbes (transformProbe transform)

    /// Transforms a probe from window coordinates to viewport coordinates, given the size
    /// of the window over time.
    let windowToViewProbe (windowSize : Point signal) (probe : Probe) =
        let transform (windowSize : Point, position : Point) = new Point (2.0 * position.X / windowSize.X - 1.0, 1.0 - 2.0 * position.Y / windowSize.Y)
        let newPosition = probe.Position |> Feed.collate windowSize |> Feed.maps transform
        { probe with Position = newPosition }

    /// Transforms an input context from window coordinates to viewport coordinates, given the size
    /// of the window over time.
    let windowToView (windowSize : Point signal) = mapProbes (windowToViewProbe windowSize)