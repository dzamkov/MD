namespace MD.UI

open MD
open MD.Util

/// Identifies a possible modifier key.
type Modifier =
    | Control
    | Shift
    | Alt

/// Encapsulates the state of all modifiers.
type ModifierState = Modifier -> bool

/// Identifies a possible mouse button.
type Button = 
    | Primary
    | Secondary
    | Extra of int

/// Encapsulates the state of all mouse buttons. Buttons that are unavailable will have a state of "false".
type ButtonState = Button -> bool

/// Identifies a possible keyboard key.
type Key =
    | LeftArrow
    | RightArrow
    | UpArrow
    | DownArrow
    | Backspace
    | Delete
    | Insert
    | Home
    | End
    | PageUp
    | PageDown
    | Control
    | Shift
    | Alt
    | Enter
    | Escape
    | Function of int
    | Char of char

/// Encapsulates the state of all keyboard keys. Keys that are unavailable will have a state of "false".
type KeyState = Key -> bool

/// A function that responds to a "locked" mouse while a button is down. When locked, no other interface will
/// receive information from the mouse and its position will be reported in a signal feed, even if that position
/// is outside the bounds of the interface it was locked in. When the mouse button that started the lock is released,
/// the retract action for the corresponding lock will be called.
type Lock = Point signal -> Retract

/// A function that responds to a "focused" mouse after a button is pressed. When focused, keystate and typing
/// information will be reported. Focus is lost when another interface requests it, in which case the retract
/// action for the last focus function will be called.
type Focus = KeyState signal -> Retract

/// Context information for a mouse event.
type Context = ButtonState * ModifierState

/// A two-dimensional interactive surface that accepts user input.
[<AbstractClass>]
type Interface () =

    /// The callback for a mouse button down event
    abstract member ButtonDown : Context * Button * Point -> Lock option
    default this.ButtonDown (_, _, _) = None

    /// The callback for a mouse button press event.
    abstract member ButtonPress : Context * Button * Point -> Focus option
    default this.ButtonPress (_, _, _) = None

    /// The callback for a mouse scroll event.
    abstract member Scroll : Context * Point * float -> unit
    default this.Scroll (_, _, _) = ()

/// A transformed form of a source interface.
type TransformInterface (source : Interface, transform : Transform signal) =
    inherit Interface ()

    override this.Scroll (context, point, amount) = source.Scroll (context, transform.Current.Apply point, amount)
    override this.ButtonPress (context, button, point) = source.ButtonPress (context, button, transform.Current.Apply point)
    override this.ButtonDown (context, button, point) =
        let newPointSignal pointSignal = pointSignal |> Feed.collate transform |> Feed.maps (fun (trans, point) -> trans.Apply point) 
        match source.ButtonDown (context, button, transform.Current.Apply point) with
        | Some lock -> Some (newPointSignal >> lock)
        | None -> None

/// Contains functions for constructing and manipulating inputs and interfaces.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Input =

    /// Transforms an interface by applying the given transform to all spatial points it receives.
    let transformConstant transform source = new TransformInterface (source, Feed.constant transform) :> Interface

    /// Transforms an interface by applying the transform from the given signal to all spatial points it receives.
    let transform transform source = new TransformInterface (source, transform) :> Interface