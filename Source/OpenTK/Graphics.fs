namespace MD.OpenTK

open MD
open MD.UI
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

/// Defines useful extension functions for the GL class.
module GLExtensions =
    type GL with
        static member Vertex2 (point : Point) =
            GL.Vertex2 (point.X, point.Y)

        static member TexCoord2 (point : Point) =
            GL.TexCoord2 (point.X, point.Y)

        static member Color4 (paint : Paint) =
            let color = paint.Color
            GL.Color4 (color.R, color.G, color.B, paint.Alpha)

        static member BindTexture2D (texture : Texture) =
            GL.BindTexture (TextureTarget.Texture2D, texture.ID)

        static member UnbindTexture2D () =
            GL.BindTexture (TextureTarget.Texture2D, 0)

/// A context that gives information about the current render state and defines primitive rendering operations.
[<AbstractClass>]
type Context () =
    
    /// Pushes an effect that applies a transform to subsequent rendering operations.
    abstract member PushTransform : Transform -> unit

    /// Pushes an effect that applies color-modulation to subsequent rendering operations.
    abstract member PushModulate : Paint -> unit

    /// Pushes an effect that clips all subsequent rendering operations to the given rectangle (in worldspace).
    abstract member PushClip : Rectangle -> unit

    /// Pops the most recent effect on the effect stack.
    abstract member Pop : unit -> unit

    /// Gets the current transform from worldspace to viewspace.
    abstract member Transform : Transform

    /// Renders a line using this context.
    abstract member RenderLine : Line -> unit

    /// Renders a 2D texture to the unit square using this context.
    abstract member RenderTexture : Texture -> unit

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

/// An interface to an OpenGL graphics context that tracks resources and continuity when rendering.
[<AbstractClass>]
type Graphics () =
    let cache = new ManualCache<Figure, Procedure> (fun proc -> proc.Delete ())

    /// Initializes this graphics interface with the OpenGL context on the current thread.
    abstract member Initialize : unit -> unit
    default this.Initialize () = ()

    /// Sets up, or updates the viewport to have the given width and height for rendering.
    member this.Setup (width, height) =
        GL.Viewport (0, 0, width, height)

    /// Creates a context for this graphics interface.
    abstract member CreateContext : unit -> Context

    /// Creates a procedure to render the given figure.
    abstract member CreateProcedure : Figure -> Procedure

    /// Gets a procedure to render the given figure.
    member this.GetProcedure figure =
        match cache.Fetch figure with
        | Some procedure -> procedure
        | None -> 
            let procedure = this.CreateProcedure figure
            cache.Submit (figure, procedure)
            procedure

    /// Renders the given figure on the current graphics context.
    member this.Render figure =
        this.Render (this.CreateContext (), figure)

    /// Renders the given figure using the given context.
    member this.Render (context, figure) =
        match figure with
        | Null -> ()
        | Transform (transform, figure) ->
            context.PushTransform transform
            this.Render (context, figure)
            context.Pop ()
        | Modulate (paint, figure) ->
            context.PushModulate paint
            this.Render (context, figure)
            context.Pop ()
        | Clip (clip, figure) ->
            context.PushClip clip
            this.Render (context, figure)
            context.Pop ()
        | Composite (a, b) ->
            this.Render (context, a)
            this.Render (context, b)
        | Hint (Static, figure) ->
            (this.GetProcedure figure).Invoke context
        | Line line ->
            context.RenderLine line
        | complex -> this.RenderComplex (context, complex)

    /// Renders the given non-trivial figure using the given context.
    abstract member RenderComplex : Context * Figure -> unit
    default this.RenderComplex (context, figure) = (this.GetProcedure figure).Invoke context