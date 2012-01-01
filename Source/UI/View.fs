namespace MD.UI

open MD
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

    /// Checks that the area this view state is within the given rectangle, correcting parameters
    /// as needed.
    member this.CheckBounds (bounds : Rectangle) =
        let center = this.Center
        let velocity = this.Velocity
        let scale = this.Scale
        let zoomVelocity = this.ZoomVelocity

        // Check if scale is too big to fit in bounds. If so, reduce the size of both components.
        let widthRatio = (bounds.Right - bounds.Left) / (2.0 * scale.X)
        let heightRatio = (bounds.Top - bounds.Bottom) / (2.0 * scale.Y)
        let minRatio = min widthRatio heightRatio
        let scale, zoomVelocity = if minRatio > 1.0 then (scale, zoomVelocity) else (scale * minRatio, min zoomVelocity 0.0)

        // Check if the view is outside the horizontal and vertical axis independently.
        let maxSideVelocity = -zoomVelocity * Math.Log 2.0
        let check low high value velocity scale =
            if value - scale < low then (low + scale, max -maxSideVelocity velocity)
            elif value + scale > high then (high - scale, min maxSideVelocity velocity)
            else (value, velocity)

        let centerX, velocityX = check bounds.Left bounds.Right center.X velocity.X scale.X
        let centerY, velocityY = check bounds.Bottom bounds.Top center.Y velocity.Y scale.Y

        { Center = new Point (centerX, centerY); Velocity = new Point (velocityX, velocityY); Zoom = Math.Log (scale.Y, 2.0); ZoomVelocity = zoomVelocity }

    /// Gets the projection for this view.
    member this.Projection = 
        let center = this.Center
        let scale = this.Scale
        new Transform (center, new Point(scale.X, 0.0), new Point (0.0, scale.Y))

/// Contains parameters for a view.
type ViewParameters = {

    /// The initial view state for the view.
    InitialState : ViewState

    /// The bounds of the viewing area. The view will never allow a projection to an area outside these bounds.
    Bounds : Rectangle

    /// The portion of velocity that is retained after each second.
    VelocityDamping : float

    /// The portion of zoom velocity that is retained after each second.
    ZoomVelocityDamping : float

    }

/// Contains information for a user-controlled, zoomable axis-aligned view. Note that a view acts as an interface in
/// worldspace.
type View (parameters : ViewParameters) =
    inherit Interface ()
    let bounds = parameters.Bounds
    let velocityDamping = parameters.VelocityDamping
    let zoomVelocityDamping = parameters.ZoomVelocityDamping
    let mutable state = parameters.InitialState.CheckBounds bounds
    let mutable drag = None : (Point signal * Point) option

    // Changes the state of the view to the given state after checking if it is
    // within bounds and correcting if needed.
    let changeState (newState : ViewState) = state <- newState.CheckBounds bounds

    // Handles a scroll wheel event.
    let scroll (amount, position : Point) = 
        let newVelocity = state.Velocity + position * (0.7 * amount)
        let newZoomVelocity = state.ZoomVelocity - amount
        changeState { state with Velocity = newVelocity; ZoomVelocity = newZoomVelocity }

    /// Creates a new view with the given parameters.
    static member Create parameters = 
        let view = new View (parameters)
        let retract = Update.register view.Update
        (view, retract)

    /// Gets or sets the current state of this view.
    member this.State 
        with get () = state
        and set state = changeState state

    /// Gets the projection feed for this view.
    member this.Projection = Feed.property (fun () -> state.Projection)

    /// Updates the state of this view by the given amount of time in seconds.
    member this.Update time = 
        match drag with
        | Some (positionFeed, startPosition) ->
            let scale = state.Scale
            let currentPosition = positionFeed.Current
            let offset = currentPosition - startPosition
            let pullCenter = state.Center - offset
            let centerSmooth = 6.0
            let newCenter = (state.Center * centerSmooth + pullCenter) / (centerSmooth + 1.0)
            let pullVelocity = -new Point (offset.X / scale.X, offset.Y / scale.Y) / time
            let velocitySmooth = 200.0
            let newVelocity = (state.Velocity * velocitySmooth + pullVelocity) / (velocitySmooth + 1.0) 
            state <- { state with Center = newCenter; Velocity = newVelocity; }
        | None -> ()
        changeState (state.Update (velocityDamping, zoomVelocityDamping) time)

    /// Handles a button down event for the view.
    override this.ButtonDown (_, button, position) = 
        if button Grab then
            // Begin dragging
            let lock positionFeed =
                drag <- Some (positionFeed, position)
                Action.Custom (fun () -> drag <- None)
            Handled (Some lock)
        else Unhandled

    /// Handles a scroll event for the view.
    override this.Scroll (_, position, amount) =
        let newVelocity = state.Velocity + (state.Projection.Inverse.Apply position) * (0.7 * amount)
        let newZoomVelocity = state.ZoomVelocity - amount
        changeState { state with Velocity = newVelocity; ZoomVelocity = newZoomVelocity }
        Handled ()