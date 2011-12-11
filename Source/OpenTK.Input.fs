namespace MD.OpenTK

open MD
open MD.UI
open System
open global.OpenTK
open OpenTK.Input

/// A signal feed for a mouse button state. Note that "MouseButton.LastButton" will be given a constant value of false.
type MouseButtonStateFeed (mouse : MouseDevice) =
    inherit CompoundSignalFeed<MouseButton, bool> ()
    static let buttonCount = int MouseButton.LastButton
    let buttonFeeds = (Array.create buttonCount None) : (ControlSignalFeed<bool> option)[] 

    /// Handles a button event.
    member this.ButtonEvent (args : MouseButtonEventArgs) =
        match buttonFeeds.[int args.Button] with
        | Some signal -> signal.Update args.IsPressed
        | None -> ()

    /// Updates the signals for all buttons in the feed.
    member this.Update () =
        for button = 0 to buttonCount - 1 do
            match buttonFeeds.[button] with
            | Some signal -> signal.Update mouse.[enum<MouseButton> button]
            | None -> ()

    override this.Current = 
        let buttonState = Array.zeroCreate buttonCount
        for button = 0 to buttonCount - 1 do
            buttonState.[button] <- mouse.[enum<MouseButton> button]
        fun x -> if x <> MouseButton.LastButton then buttonState.[int x] else false

    override this.GetElementSignal index =
        if index = MouseButton.LastButton then Feed.constant false
        else 
            match buttonFeeds.[int index] with
            | Some signal -> signal :> bool signal
            | None -> 
                let signal = new ControlSignalFeed<bool> (mouse.[index])
                buttonFeeds.[int index] <- Some signal
                signal :> bool signal

/// A feed that tracks the position of a mouse device in window coordinates.
type MousePositionFeed (mouse : MouseDevice) =
    inherit SignalFeed<Point> ()

    override this.Current = new Point (float mouse.X, float mouse.Y)

/// Contains functions related to OpenTK input.
module Input =

    /// Gets the OpenTK mouse button for the given input button.
    let getNativeButton (button : Button) =
        match button with
        | Primary -> MouseButton.Left
        | Secondary -> MouseButton.Right
        | Extra x -> if x >= 0 && x < 8 then enum<MouseButton> (x + 3) else MouseButton.LastButton

    /// Gets the input button for the given OpenTK mouse button.
    let getButton (button : MouseButton) =
        match button with
        | MouseButton.Left -> Button.Primary
        | MouseButton.Right -> Button.Secondary
        | x -> Button.Extra (int button - 3)

    /// Links an interface to a window, returning a retract action to later undo the link.
    let link (window : GameWindow) (source : Interface) =
        let mouse = window.Mouse
        let sourceButtonState = new MouseButtonStateFeed (mouse)
        let position = new MousePositionFeed (mouse) :> Point signal
        let buttonState = Feed.alias getNativeButton sourceButtonState
        let modifierState = Feed.constant (fun x -> false)
        let keyState = Feed.constant (fun x -> false)
        let unlock = ref (None : (MouseButton * Retract) option)
        let unfocus = ref (None : Retract option)

        // Gets the current mouse input context.
        let context () = (buttonState.Current, modifierState.Current)

        // Hook up mouse button events.
        let buttonEvent args = 
            sourceButtonState.ButtonEvent args
            if args.IsPressed then
                if (!unlock).IsNone then
                    match source.ButtonDown (context (), getButton args.Button, new Point (float args.X, float args.Y)) with
                    | Some lock -> unlock := Some (args.Button, lock position)
                    | None -> ()
            else
                match !unlock with
                | Some (button, runlock) -> 
                    if button = args.Button then
                        runlock.Invoke ()
                        unlock := None
                | None ->
                    match source.ButtonPress (context (), getButton args.Button, new Point (float args.X, float args.Y)) with
                    | Some focus ->
                        match !unfocus with
                        | Some runfocus -> runfocus.Invoke ()
                        | None -> ()
                        unfocus := Some (focus keyState)
                    | None -> ()

        let retract = (Feed.native mouse.ButtonUp).Register buttonEvent + (Feed.native mouse.ButtonDown).Register buttonEvent

        // Hook up scroll events.
        let scrollEvent (args : MouseWheelEventArgs) = 
            source.Scroll (context (), new Point (float args.X, float args.Y), float args.DeltaPrecise)

        let retract = retract + (Feed.native mouse.WheelChanged).Register scrollEvent

        // All done
        retract