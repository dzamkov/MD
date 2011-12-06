namespace MD

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

    /// The movement velocity of the view, in worldspace units per second.
    Velocity : Point

    /// The zoom level of the view, such that the ideal view will have
    /// a square worldspace area with edge length 2 * 2 ^ Zoom.
    Zoom : float

    /// The zoom velocity of the view, in zoom units per second.
    ZoomVelocity : float

    /// The aspect ratio of the view. This is the width divided by the height of
    /// the worldspace area for a square viewport.
    AspectRatio : float

    }

/// Contains parameters for a view.
type ViewParameters = {

    /// The initial view state for the view.
    InitialState : ViewState

    /// An event feed that will cause the view state to immediately change to recieved events.
    ChangeState : ViewState event

    /// The bounds of the viewing area. The view will never allow a projection to an area outside these bounds.
    Bounds : Rectangle
    
    /// The probe controlling the view, using viewport coordinates.
    Probe : Probe

    }

/// Contains information for a user-controlled zoomable axis-aligned view.
type View = {

    /// The current state of the view.
    State : ViewState signal

    /// The current transform from viewspace to worldspace.
    Projection : Transform signal

    }