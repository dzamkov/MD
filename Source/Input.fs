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

    /// Gets a feed for the state of a button on a mouse.
    let button (mouse : MouseDevice) (button : MouseButton) =
        let control = new ControlSignalFeed<bool> (mouse.[button])
        mouse.ButtonDown.Add (fun args -> if args.Button = button then control.Current <- true)
        mouse.ButtonUp.Add (fun args -> if args.Button = button then control.Current <- false)
        control :> SignalFeed<bool>

    /// Gets a feed for the scroll event on a mouse.
    let scroll (mouse : MouseDevice) =
        Feed.native mouse.WheelChanged
        |> Feed.mape (fun x -> double x.DeltaPrecise)

    /// Creates a probe for a mouse device.
    let probe (mouse : MouseDevice) = {
            Position = position mouse
            Primary = button mouse MouseButton.Left
            Secondary = button mouse MouseButton.Right
            Scroll = scroll mouse
        }