namespace MD.OpenTK

open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

open MD
open MD.UI
open MD.OpenTK

/// Describes a rendering procedure that manipulates a graphics context in order to produce a visual object or
/// effect. Procedures are used for rendering operations that occur often, are complex, or require continuity.
[<AbstractClass>]
type Procedure () =

    /// Invokes the given procedure using the given context.
    static member Invoke (procedure : Procedure byref, context) = procedure.Invoke (context, &procedure)
    
    /// Invokes this procedure using the given context. A reference to the procedure to be used for the next
    /// invokation is provided to allow the procedure to defer to another.
    abstract member Invoke : Context * Procedure byref -> unit

    /// Invokes this procedure using the given context.
    member this.Invoke context =
        let mutable dummy = this
        this.Invoke (context, &dummy)

/// A procedure that does nothing.
type NullProcedure private () =
    inherit Procedure ()
    static let instance = new NullProcedure ()

    /// Gets the only instance of this class.
    static member Instance = instance

    override this.Invoke (_, _) = ()

/// A procedure that applies a transform to an inner procedure.
type TransformProcedure (inner : Procedure, transform : Transform) =
    inherit Procedure ()
    let mutable inner = inner

    /// Gets the inner procedure for this procedure.
    member this.Inner = inner

    /// Gets the transform applied by this procedure.
    member this.Transform = transform

    override this.Invoke (context, _) =
        context.PushTransform transform
        Procedure.Invoke (&inner, context)
        context.Pop ()

/// A procedure that draws a line.
type LineProcedure (line : Line) =
    inherit Procedure ()
    override this.Invoke (context, _) = context.RenderLine line

/// A procedure that renders a full 2D texture to the unit square.
type TextureProcedure (texture : Texture exclusive) =
    inherit Procedure ()

    /// Gets the texture for this procedure.
    member this.Texture = texture.Object

    override this.Invoke (context, _) = context.RenderTexture texture.Object

/// A procedure that invokes a sequence of component procedures sequentially.
type SequentialProcedure (procedures : Procedure[]) =
    inherit Procedure ()

    /// Gets the component procedures of this procedure.
    member this.Procedures = procedures

    override this.Invoke (context, _) =
        let mutable index = 0
        while index < procedures.Length do
            Procedure.Invoke (&procedures.[index], context)
            index <- index + 1