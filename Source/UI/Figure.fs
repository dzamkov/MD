namespace MD.UI

open MD
open Util

/// Identifies an interpolation mode for an image.
type ImageInterpolation = 
    | Nearest
    | Linear
    | Cubic

/// A figure for a solid, colored line.
type Line = {

    /// One of the endpoints of the line.
    A : Point

    /// One of the endpoints of the line.
    B : Point

    /// The weight, or thickness of the line.
    Weight : float

    /// The paint for the line.
    Paint : Paint

    }

/// Describes a (possibly dynamic) visual object on a two-dimensional plane.
type Figure =
    | Nil
    | Solid of Color
    | Modulate of Paint * Figure
    | Transform of Transform * Figure
    | Composite of Figure * Figure
    | Clip of Rectangle * Figure
    | Sample of Map<Point, Paint>
    | Line of Line
    | Image of Image * ImageSize * ImageInterpolation

    | LOD of Figure * Figure * double
    | Bounded of Rectangle * Figure
    | Lazy of Lazy<Figure>
    | Query of Figure query
    
    | Dynamic of Figure signal
    | TransformDynamic of Transform signal * Figure

    /// Gets the bounding area of this figure.
    member this.Bounds =
        match this with
        | Nil -> Rectangle.Null
        | Solid _ -> Rectangle.Unbound
        | Modulate (_, figure) -> figure.Bounds
        | Transform (transform, figure) -> transform * figure.Bounds
        | Composite (a, b) -> a.Bounds ||| b.Bounds
        | Clip (area, figure) -> area &&& figure.Bounds
        | Sample _ -> Rectangle.Unbound
        | Line line -> new Rectangle (min line.A.X line.B.X, max line.A.X line.B.X, min line.A.Y line.B.Y, max line.A.Y line.B.Y)
        | Image (_, _, _) -> Rectangle.Unit
        | LOD (simple, _, _) -> simple.Bounds
        | Bounded (bounds, _) -> bounds
        | Lazy (figure) -> (figure.Force ()).Bounds
        | Query _ -> Rectangle.Unbound
        | Dynamic _ -> Rectangle.Unbound
        | TransformDynamic _ -> Rectangle.Unbound

    /// Constructs a transformed figure.
    static member (*) (a : Figure, b : Transform) =
        Figure.Transform (b, a)

    /// Constructs a transformed figure.
    static member (*) (a : Figure, b : Transform signal) =
        Figure.TransformDynamic (b, a)

    /// Constructs a modulated figure
    static member (*) (a : Figure, b : Paint) =
        Figure.Modulate (b, a)

    /// Constructs a composite figure.
    static member (+) (a : Figure, b : Figure) =
        Figure.Composite (a, b)

/// Contains functions for constructing and manipulating figures.
[<CompilationRepresentationAttribute(CompilationRepresentationFlags.ModuleSuffix)>]
module Figure =

    /// Gets the nil figure, a figure that is completely transparent.
    let nil = Figure.Nil

    /// Constructs a figure that has a solid color over the entire render plane.
    let solid color = Figure.Solid color
    
    /// Constructs a figure for a colored line.
    let line line = Figure.Line line

    /// Constructs a figure for an image placed in the unit square. Note that this figure will respect 
    /// the transparency information encoded in the image, if any.
    let image (image, size) interpolation = Figure.Image (image, size, interpolation)

    /// Constructs a figure for an image placed in the given rectangular area. Note that this figure will respect 
    /// the transparency information encoded in the image, if any.
    let placeImage area (image, size) interpolation = Figure.Transform (Transform.Place area, Figure.Image (image, size, interpolation))

    /// Constructs a transformed form of a figure.
    let transform transform figure = Figure.Transform (transform, figure)

    /// Constructs a modulated form of a figure. This will cause all colors (and transparency) of the figure to
    /// be multiplied by the values in a paint.
    let modulate paint figure = Figure.Modulate (paint, figure)

    /// Constructs a composite figure.
    let composite bottom top = Figure.Composite (bottom, top)

    /// Constructs a composite figure from a sequence of figures.
    let compositeMany figures =
        if Seq.isEmpty figures then Figure.Nil
        else Seq.reduce composite figures

    /// Constructs a clipped form of a figure. This will cause all paints of the figure outside the given area to
    /// be completely transparent.
    let clip area figure = Figure.Clip (area, figure)

    /// Constructs a level-of-detail hint figure. This type of figure supplies two figures which look similar, but one
    /// of them is more complex and provides more detail. The given resolution specifies the maximum viewing resolution 
    /// (in pixels per unit) for which the simple figure can be used in place of the complex figure.
    let lod simple complex resolution = Figure.LOD (simple, complex, resolution)

    /// Constructs a figure that is lazily evaluated.
    let ``lazy`` figure = Figure.Lazy figure

    /// Constructs a figure with an explicitly defined bounding area.
    let bounded bounds figure = Figure.Bounded (bounds, figure)

    /// Constructs a figure that is lazily and asynchronously loaded from a query.
    let query query = Figure.Query query

    /// Constructs a dynamic figure from a figure signal.
    let dynamic figure = Figure.Dynamic figure

    /// Constructs a dynamically-transformed form of a figure.
    let transformDynamic transform figure = Figure.TransformDynamic (transform, figure)