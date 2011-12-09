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

    /// The log2 of the vertical scale component for this viewspace.
    Zoom : float

    /// The change in the zoom level in units per second.
    ZoomVelocity : float

    } with

    /// Gets the positive difference between the center and any of the corners of the view in
    /// worldspace.
    member this.Scale = 
        let vertical = Math.Pow (2.0, this.Zoom)
        new Point (vertical, vertical)

    /// Updates this view state by the given amount of time in seconds, given the velocity damping factors.
    member this.Update (velocityDamping, zoomVelocityDamping) time = {
            Center = this.Center + Point.Scale (this.Velocity, this.Scale) * time
            Zoom = this.Zoom + this.ZoomVelocity * time
            Velocity = this.Velocity * Math.Pow (velocityDamping, time)
            ZoomVelocity = this.ZoomVelocity * Math.Pow (zoomVelocityDamping, time)
        }

    /// Gets the worldspace area seen with this view state.
    member this.Area = 
        let center = this.Center
        let scale = this.Scale
        new Rectangle (center.X - scale.X, center.X + scale.X, center.Y - scale.Y, center.Y + scale.Y)

    /// Checks that the area this view state is within the given rectangle, correcting any parameters
    /// (including center and zoom) if needed.
    member this.CheckBounds (bounds : Rectangle) =
        let center = this.Center
        let velocity = this.Velocity
        let scale = this.Scale

        // Check if scale is too big to fit in bounds. If so, reduce the size of both components.
        let widthRatio = (bounds.Right - bounds.Left) / (2.0 * scale.X)
        let heightRatio = (bounds.Top - bounds.Bottom) / (2.0 * scale.Y)
        let minRatio = min widthRatio heightRatio
        let scale = if minRatio > 1.0 then scale else scale * minRatio

        // Check if the view is outside the horizontal and vertical axis independently.
        let check min max value scale =
            if value - scale < min then min + scale
            elif value + scale > max then max - scale
            else value

        let centerX = check bounds.Left bounds.Right center.X scale.X
        let centerY = check bounds.Bottom bounds.Top center.Y scale.Y

        { this with Center = new Point (centerX, centerY); Zoom = Math.Log (scale.Y, 2.0) }


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
    let input = parameters.Input
    let bounds = parameters.Bounds
    let velocityDamping = parameters.VelocityDamping
    let zoomVelocityDamping = parameters.ZoomVelocityDamping
    let mutable state = parameters.InitialState.CheckBounds bounds

    // Changes the state of the view to the given state after checking if it is
    // within bounds and correcting if needed.
    let changeState (newState : ViewState) = state <- newState.CheckBounds bounds
    let retractChangeState = parameters.ChangeState.Register changeState

    // Updates the state of the view by the given amount of time in seconds.
    let update time = changeState (state.Update (velocityDamping, zoomVelocityDamping) time)
    let retractUpdate = Update.register update

    // Handles a scroll wheel event.
    let scroll (amount, position : Point) = 
        let newVelocity = state.Velocity + position * (0.7 * amount)
        let newZoomVelocity = state.ZoomVelocity - amount
        changeState { state with Velocity = newVelocity; ZoomVelocity = newZoomVelocity }

    /// Registers a new controlling probe for the view.
    let registerProbe (probe : Probe) = 
        (Feed.tag probe.Scroll probe.Position).Register scroll
    let retractProbes = input.Probes.Register registerProbe

    /// Creates a new view with the given parameters.
    static member Create parameters = new View (parameters) |> Exclusive.custom (fun view -> view.Finish ())

    /// Gets the projection from viewspace to worldspace for the given view state.
    static member GetProjection (state : ViewState) =
        let center = state.Center
        let scale = state.Scale
        new Transform (center, new Point(scale.X, 0.0), new Point (0.0, scale.Y))

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