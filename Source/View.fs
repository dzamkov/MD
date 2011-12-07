namespace MD

open System

// Note: coordinates in "viewspace" are similar to the ones used in OpenGL (see ASCII art).
// "worldspace" is the rectangular area that the view is projected to.
//   .------ 1.0 ------.
//   |        |        |
//   |        |        |
//   |        |        |
// -1.0-------+-------1.0
//   |        |        |
//   |        |        |
//   |        |        |
//   .----- -1.0 ------.


/// A possible state for a view.
type ViewState = {
    
    /// The worldspace point at the center of the view.
    Center : Point

    /// The movement velocity of the view, in viewspace (yes, viewspace) units per second.
    Velocity : Point

    /// The zoom level of the view, such that the ideal view will have
    /// a square worldspace area with edge length 2 * 2 ^ -Zoom.
    Zoom : float

    /// The zoom velocity of the view, in zoom units per second.
    ZoomVelocity : float

    }

/// Contains parameters for a view.
type ViewParameters = {

    /// The initial view state for the view.
    InitialState : ViewState

    /// An event feed that will cause the view state to immediately change to recieved events.
    ChangeState : ViewState event

    /// The bounds of the viewing area. The view will never allow a projection to an area outside these bounds.
    Bounds : Rectangle
    
    /// The input interface controlling the view, using viewport coordinates.
    Input : Input

    /// The portion of velocity that is retained after each second.
    VelocityDamping : float

    /// The portion of zoom velocity that is retained after each second.
    ZoomVelocityDamping : float

    }

/// Contains information for a user-controlled, zoomable axis-aligned view.
type View private (parameters : ViewParameters) =
    let mutable state = parameters.InitialState
    let input = parameters.Input
    let bounds = parameters.Bounds
    let velocityDamping = parameters.VelocityDamping
    let zoomVelocityDamping = parameters.ZoomVelocityDamping

    // Changes the state of the view to the given state after checking if it is
    // within bounds and correcting if needed.
    let changeState newState = state <- newState
    let retractChangeState = parameters.ChangeState.Register changeState

    // Updates the state of the view by the given amount of time in seconds.
    let update time =
        let dv = Math.Pow (velocityDamping, time)
        let dzv = Math.Pow (zoomVelocityDamping, time)
        let scale = Math.Pow (2.0, state.Zoom)
        changeState {
                Center = state.Center + state.Velocity * scale * time
                Zoom = state.Zoom + state.ZoomVelocity * time
                Velocity = state.Velocity * dv
                ZoomVelocity = state.ZoomVelocity * dzv
            }
    let retractUpdate = Update.register update

    // Handles a scroll wheel event.
    let scroll (amount, position : Point) = 
        let newVelocity = state.Velocity + position * (0.7 * amount)
        let newZoomVelocity = state.ZoomVelocity - amount

        // Don't call changeState; we haven't changed the position so no bounds-checking is needed.
        state <- { state with Velocity = newVelocity; ZoomVelocity = newZoomVelocity }

    /// Registers a new controlling probe for the view.
    let registerProbe (probe : Probe) = 
        (Feed.tag probe.Scroll probe.Position).Register scroll
    let retractProbes = input.Probes.Register registerProbe

    /// Creates a new view with the given parameters.
    static member Create parameters = new View (parameters) |> Exclusive.custom (fun view -> view.Finish ())

    /// Gets the projection from viewspace to worldspace for the given view state.
    static member GetProjection (state : ViewState) =
        let center = state.Center
        let scale = Math.Pow (2.0, state.Zoom)
        new Transform (center, new Point(scale, 0.0), new Point (0.0, scale))

    /// Gets the projection feed for this view.
    member this.Projection = this :> ViewState signal |> Feed.maps View.GetProjection

    /// Releases all resources used by this view.
    member this.Finish () = 
        if retractChangeState <> null then retractChangeState.Invoke ()
        if retractUpdate <> null then retractUpdate.Invoke ()
        if retractProbes <> null then retractProbes.Invoke ()

    interface SignalFeed<ViewState> with
        member this.Current = state
        member this.Delta = None