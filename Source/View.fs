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
type View private (parameters : ViewParameters, retract : RetractAction byref) =
    let input = parameters.Input
    let bounds = parameters.Bounds
    let velocityDamping = parameters.VelocityDamping
    let zoomVelocityDamping = parameters.ZoomVelocityDamping
    let mutable state = parameters.InitialState.CheckBounds bounds
    let mutable drag : (Probe * Point * RetractAction) option = None

    // Changes the state of the view to the given state after checking if it is
    // within bounds and correcting if needed.
    let changeState (newState : ViewState) = state <- newState.CheckBounds bounds
    do retract <- Retract.combine retract (parameters.ChangeState.Register changeState)

    // Updates the state of the view by the given amount of time in seconds.
    let update time = 
        match drag with
        | Some (probe, startPosition, retractLock) ->
            if probe.Primary.Current then
                let scale = state.Scale
                let currentPosition = Point.Scale (probe.Position.Current, scale) + state.Center
                let offset = currentPosition - startPosition
                let pullCenter = state.Center - offset
                let centerSmooth = 6.0
                let newCenter = (state.Center * centerSmooth + pullCenter) / (centerSmooth + 1.0)
                let pullVelocity = -new Point (offset.X / scale.X, offset.Y / scale.Y) / time
                let velocitySmooth = 200.0
                let newVelocity = (state.Velocity * velocitySmooth + pullVelocity) / (velocitySmooth + 1.0) 

                let state = { state with Center = newCenter; Velocity = newVelocity; }
                changeState (state.Update (velocityDamping, zoomVelocityDamping) time)
            else 
                Retract.invoke retractLock
                drag <- None
                changeState (state.Update (velocityDamping, zoomVelocityDamping) time)
        | _ ->  changeState (state.Update (velocityDamping, zoomVelocityDamping) time)
    do retract <- Retract.combine retract (Update.register update)

    // Handles a scroll wheel event.
    let scroll (amount, position : Point) = 
        if Option.isNone drag then
            let newVelocity = state.Velocity + position * (0.7 * amount)
            let newZoomVelocity = state.ZoomVelocity - amount
            changeState { state with Velocity = newVelocity; ZoomVelocity = newZoomVelocity }

    // Handles a probe grab event.
    let grab probe identifier () = 
        
        // Start dragging
        if Option.isNone drag then
            let retractLock = input.Lock identifier
            drag <- Some (probe, state.Projection.Apply probe.Position.Current, retractLock)

    /// Registers a new controlling probe for the view.
    let registerProbe (probe : Probe, identifier) = 
        (Feed.tag probe.Scroll probe.Position).Register scroll 
        |> Retract.combine ((Feed.rising probe.Primary).Register (grab probe identifier))
    do retract <- Retract.combine retract (input.Probes.Register registerProbe)

    /// Creates a new view with the given parameters.
    static member Create parameters = 
        let mutable retract = Retract.nil
        (new View (parameters, &retract), retract)

    /// Gets the projection feed for this view.
    member this.Projection = this :> ViewState signal |> Feed.maps (fun vs -> vs.Projection)

    interface SignalFeed<ViewState> with
        member this.Current = state
        member this.Delta = None