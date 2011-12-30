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
    | Left
    | Middle
    | Right
    | Grab
    | Number of int

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
    | Space
    | Modifier of Modifier
    | Function of int
    | Char of char

/// Encapsulates the state of all keyboard keys. Keys that are unavailable will have a state of "false".
type KeyState = Key -> bool

/// A function that responds to a "locked" mouse while a button is down. When locked, no other interface will
/// receive information from the mouse and its position will be reported in a signal feed, even if that position
/// is outside the bounds of the interface it was locked in. When the mouse button that started the lock is released,
/// the retract action for the corresponding lock will be called.
type Lock = Point signal -> RetractAction

/// A function that responds to a "focused" mouse after a button is pressed. When focused, keystate and typing
/// information will be reported. Focus is lost when another interface requests it, in which case the retract
/// action for the last focus function will be called.
type Focus = KeyState signal -> RetractAction

/// A possible response to an event.
type Response<'a> =
    | Handled of 'a
    | Unhandled

/// A two-dimensional interactive surface that accepts user input.
[<AbstractClass>]
type Interface () =

    /// The callback for a mouse button down event
    abstract member ButtonDown : ModifierState * ButtonState * Point -> Response<Lock option>
    default this.ButtonDown (_, _, _) = Unhandled

    /// The callback for a mouse button press event.
    abstract member ButtonPress : ModifierState * ButtonState * Point -> Response<Focus option>
    default this.ButtonPress (_, _, _) = Unhandled

    /// The callback for a mouse scroll event.
    abstract member Scroll : ModifierState * Point * float -> Response<unit>
    default this.Scroll (_, _, _) = Unhandled

/// A transformed form of a source interface.
type TransformInterface (source : Interface, transform : Transform signal) =
    inherit Interface ()

    override this.Scroll (modifier, point, amount) = source.Scroll (modifier, transform.Current.Apply point, amount)
    override this.ButtonPress (modifier, button, point) = source.ButtonPress (modifier, button, transform.Current.Apply point)
    override this.ButtonDown (modifier, button, point) =
        let newPointSignal pointSignal = pointSignal |> Feed.collate transform |> Feed.maps (fun (trans, point) -> trans.Apply point) 
        match source.ButtonDown (modifier, button, transform.Current.Apply point) with
        | Handled (Some lock) -> Handled (Some (newPointSignal >> lock))
        | x -> x

/// Contains functions for constructing and manipulating inputs and interfaces.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Input =

    /// Transforms an interface by applying the given transform to all spatial points it receives.
    let transformConstant transform source = new TransformInterface (source, Feed.constant transform) :> Interface

    /// Transforms an interface by applying the transform from the given signal to all spatial points it receives.
    let transform transform source = new TransformInterface (source, transform) :> Interface