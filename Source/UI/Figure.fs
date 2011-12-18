namespace MD.UI

open MD

/// Identifies an interpolation mode for an image.
type ImageInterpolation = 
    | Nearest
    | Linear
    | Cubic

/// A tile in a dynamically-loaded image. Tile-based figures should be used when
/// there is a large amount of image data that isn't immediately available and needs
/// to be requested on-the-fly.
[<AbstractClass>]
type Tile (area : Rectangle) =

    /// Gets the rectangular area this tile occupies. Note that it is possible for the area to be
    /// infinite, in which case, requesting an image for the tile is not valid, but there may be
    /// a child tile with a finite area for which an image can be requested.
    member this.Area = area

    /// Requests an image for this tile. A suggested size for the image is given. When the image 
    /// is available, the provided callback should be called with it. If the returned retract action is
    /// called, the image is no longer needed, but the callback may still be called.
    abstract member RequestImage : suggestedSize : (int * int) * callback : (Image<Paint> exclusive -> unit) -> Retract

    /// A list of available non-trivial divisions of this tile such that the tiles in a division do not overlap
    /// and collectively occupy the entire area of the root tile. These tiles can be queried for more accurate images
    /// of their respective areas.
    abstract member Divisions : seq<Tile[]>

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

/// A sampled image that defines an infinitely detailed mapping of points to colors.
[<ReferenceEquality>]
type Sample = {

    /// The mapping function for the sample.
    Map : Point -> Paint

    /// The bounds of the sample, such that all points outside the bounds are transparent.
    Bounds : Rectangle

    }

/// Indentifies a hint for how a figure should be rendered or managed.
type RenderHint =

    /// Indicates that the figure is persistent, and will likely appear again.
    | Static

    /// Indicates that the figure is dynamic, and is not likely to appear again.
    | Dynamic

/// Describes a visual object on a two-dimensional plane.
type Figure =
    | Null
    | Solid of Color
    | Image of Image<Paint> * ImageInterpolation
    | Modulate of Paint * Figure
    | Transform of Transform * Figure
    | Composite of Figure * Figure
    | Clip of Rectangle * Figure
    | Hint of RenderHint * Figure
    | Sample of Sample
    | Line of Line
    | Tile of Tile

    /// Constructs a transformed figure.
    static member (*) (a : Figure, b : Transform) =
        Figure.Transform (b, a)

    /// Constructs a modulated figure
    static member (*) (a : Figure, b : Paint) =
        Figure.Modulate (b, a)

    /// Constructs a composite figure.
    static member (+) (a : Figure, b : Figure) =
        Figure.Composite (a, b)

/// Contains functions for constructing and manipulating figures.
[<CompilationRepresentationAttribute(CompilationRepresentationFlags.ModuleSuffix)>]
module Figure =

    /// Gets the null figure, a figure that is completely transparent.
    let ``null`` = Figure.Null

    /// Constructs a figure that has a solid color over the entire render plane.
    let solid color = Figure.Solid color
    
    /// Constructs a figure for a colored line.
    let line line = Figure.Line line

    /// Constructs a figure for an image placed in the unit square. Note that this figure will respect 
    /// the transparency information encoded in the image, if any.
    let image image interpolation = Figure.Image (image, interpolation)

    /// Constructs a figure for an image placed in the given rectangular area. Note that this figure will respect 
    /// the transparency information encoded in the image, if any.
    let placeImage area image interpolation = Figure.Transform (Transform.Place area, Figure.Image (image, interpolation))

    /// Constructs a figure for a tile image.
    let tile tile = Figure.Tile tile

    /// Constructs a transformed form of a figure.
    let transform transform figure = Figure.Transform (transform, figure)

    /// Constructs a modulated form of a figure. This will cause all colors (and transparency) of the figure to
    /// be multiplied by the values in a paint.
    let modulate paint figure = Figure.Modulate (paint, figure)

    /// Constructs a composite figure.
    let composite bottom top = Figure.Composite (bottom, top)

    /// Constructs a clipped form of a figure. This will cause all paints of the figure outside the given area to
    /// be completely transparent.
    let clip area figure = Figure.Clip (area, figure)

    /// Provides a hint for the given figure.
    let hint hint figure = Figure.Hint (hint, figure)