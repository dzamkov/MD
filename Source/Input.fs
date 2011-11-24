namespace MD

open OpenTK.Input

/// An interface to a mouse-like user input source on a two-dimensional coordinate system.
type Probe = {
    /// The position of this probe.
    Position : SignalFeed<Point>

    /// The state of the primary button.
    Primary : SignalFeed<bool>

    /// The state of the secondary button.
    Secondary : SignalFeed<bool>

    /// Fires an event whenever the scroll wheel is used, with the amount that was scrolled by.
    Scroll : EventFeed<double>
    }

/// An interface for user input.
type Input = {

    /// A collection of probes present in this input interface.
    Probes : CollectionFeed<Probe>
    }


/// Contains functions related to OpenTK input.
module OpenTKInput =

    /// A feed that tracks the position of a mouse device in window coordinates.
    type MousePositionFeed (mouse : MouseDevice) =
        interface SignalFeed<Point> with
            member this.Current = new Point (double mouse.X, double mouse.Y)   
            member this.Delta = None
    
    /// Gets a feed for the position of a mouse.
    let position (mouse : MouseDevice) = new MousePositionFeed (mouse)
    
    /// Encapsulates the complete button state for a mouse device.
    type MouseButtonState (mouse : MouseDevice) =
        let buttonCount = int MouseButton.LastButton
        let buttonFeeds : ControlSignalFeed<bool> option [] = Array.create buttonCount None

        // Register events
        let buttonChange (args : MouseButtonEventArgs) =
            match buttonFeeds.[int args.Button] with
            | Some feed -> feed.Current <- args.IsPressed
            | None -> ()

        do
            mouse.ButtonDown.Add buttonChange
            mouse.ButtonUp.Add buttonChange

        /// Gets the signal feed for the given mouse button.
        member this.GetFeed (button : MouseButton) =
            match buttonFeeds.[int button] with
            | Some feed -> feed :> SignalFeed<bool>
            | None ->
                let newfeed = new ControlSignalFeed<bool> (mouse.[button])
                buttonFeeds.[int button] <- Some newfeed
                newfeed :> SignalFeed<bool>

    /// Gets an interface to the button state of a mouse. This contains feeds for each button of the mouse.
    let buttonState (mouse : MouseDevice) = new MouseButtonState (mouse)

    /// Gets a feed for the scroll event on a mouse.
    let scroll (mouse : MouseDevice) =
        Feed.native mouse.WheelChanged
        |> Feed.mape (fun x -> double x.DeltaPrecise)

    /// Creates a probe for a mouse device.
    let probe (mouse : MouseDevice) = 
        let buttonstate = buttonState mouse
        {   
            Position = position mouse
            Primary = buttonstate.GetFeed MouseButton.Left
            Secondary = buttonstate.GetFeed MouseButton.Right
            Scroll = scroll mouse
        }