namespace MD.OpenTK

open MD
open System
open global.OpenTK
open OpenTK.Input

/// Contains functions related to OpenTK input.
module Input =

    /// A feed that tracks the position of a mouse device in window coordinates.
    type MousePositionFeed (mouse : MouseDevice) =
        interface SignalFeed<Point> with
            member this.Current = new Point (float mouse.X, float mouse.Y)   
            member this.Delta = None
    
    /// Gets a feed for the position of a mouse.
    let position (mouse : MouseDevice) = new MousePositionFeed (mouse) :> Point signal
    
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
            | Some feed -> feed :> bool signal
            | None ->
                let newfeed = new ControlSignalFeed<bool> (mouse.[button])
                buttonFeeds.[int button] <- Some newfeed
                newfeed :> bool signal

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

    /// Creates a full input context for a window, in window coordinates.
    let create (window : GameWindow) =

        // Create mouse probe.
        let mouse = probe window.Mouse

        // Setup mouse enter/leave and lock/unlock controls.
        let probes = new ControlCollectionFeed<Probe * identifier> ()
        let mouseAvailable = new ControlSignalFeed<bool> (false)
        let mouseLocked = new ControlSignalFeed<bool> (false)
        let mouseReady = Feed.collate mouseAvailable mouseLocked |> Feed.maps (fun (x, y) -> x && not y)
        let retractMouse = ref null
        (Feed.delta mouseReady).Register (fun change ->
            if change.New && !retractMouse = null then retractMouse := probes.Add (mouse, Identifier.Create mouse)
            elif not change.New && !retractMouse <> null then Retract.invoke !retractMouse; retractMouse := null) |> ignore

        let retractMouse = ref null
        let mouseEnter args = mouseAvailable.Current <- true
        let mouseExit args = mouseAvailable.Current <- false
        let lock args =
            mouseLocked.Current <- true
            RetractAction (fun () -> mouseLocked.Current <- false)
        window.MouseEnter.Add mouseEnter
        window.MouseLeave.Add mouseExit

        // Create input context.
        {
            Probes = probes
            Lock = lock
        }