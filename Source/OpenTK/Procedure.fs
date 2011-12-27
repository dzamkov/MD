namespace MD.OpenTK

open MD
open MD.UI
open MD.OpenTK
open System
open System.Collections.Generic

/// Describes a rendering procedure that manipulates a graphics context in order to produce a visual object or
/// effect. Procedures are used for rendering operations that occur often, are complex, or require continuity.
[<AbstractClass>]
type Procedure () =
    
    /// Invokes this procedure using the given context.
    abstract member Invoke : Context -> unit

    /// Deletes this procedure and all resources it uses.
    abstract member Delete : unit -> unit
    default this.Delete () = ()

/// A procedure that does nothing.
type NullProcedure private () =
    inherit Procedure ()
    static let instance = new NullProcedure ()

    /// Gets the only instance of this class.
    static member Instance = instance

    override this.Invoke _ = ()

/// A procedure that applies a transform to an inner procedure.
type TransformProcedure (inner : Procedure, transform : Transform) =
    inherit Procedure ()

    /// Gets the inner procedure for this procedure.
    member this.Inner = inner

    /// Gets the transform applied by this procedure.
    member this.Transform = transform

    override this.Invoke context =
        context.PushTransform transform
        inner.Invoke context
        context.Pop ()

    override this.Delete () = inner.Delete ()

/// A procedure that draws a line.
type LineProcedure (line : Line) =
    inherit Procedure ()

    override this.Invoke context = context.RenderLine line

/// A procedure that renders a full 2D texture to the unit square.
type TextureProcedure (texture : Texture exclusive) =
    inherit Procedure ()

    /// Gets the texture for this procedure.
    member this.Texture = texture.Object

    override this.Invoke context = context.RenderTexture texture.Object
    override this.Delete () = texture.Finish ()

/// A procedure that invokes a sequence of component procedures sequentially.
type SequentialProcedure (procedures : seq<Procedure>) =
    inherit Procedure ()

    /// Gets the component procedures of this procedure.
    member this.Procedures = procedures

    override this.Invoke context =
        for procedure in procedures do procedure.Invoke context

    override this.Delete () =
        for procedure in procedures do procedure.Delete ()