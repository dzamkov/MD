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
    abstract member RequestImage : suggestedSize : (int * int) * callback : (Image exclusive -> unit) -> Retract

    /// Subdivides this tile to get its children. The children collectively should occupy the same area as
    /// the parent, but they do not have to be placed in a regular pattern.
    abstract member Children : Tile[] option

/// Indentifies a hint for how a figure should be rendered or managed.
type RenderHint =

    /// Indicates that the figure is persistent, and will likely appear again.
    | Static

    /// Indicates that the figure is dynamic, and is not likely to appear again.
    | Dynamic

/// Describes a visual object on a two-dimensional plane.
[<ReferenceEquality>]
type Figure =
    | Null
    | Solid of Color
    | Line of Point * Point * double * Paint
    | Image of Image * ImageInterpolation
    | Modulate of Paint * Figure
    | Transform of Transform * Figure
    | Composite of Figure * Figure
    | Clip of Rectangle * Figure
    | Hint of RenderHint * Figure
    | Sample of (Point -> Paint)
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
    let line start stop weight paint = Figure.Line (start, stop, weight, paint)

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