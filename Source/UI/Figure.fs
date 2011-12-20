namespace MD.UI

open MD
open Util

/// Identifies an interpolation mode for an image.
type ImageInterpolation = 
    | Nearest
    | Linear
    | Cubic

/// A large or complex dynamically-loaded image made up of tiles containing images that can
/// be loaded upon request.
[<AbstractClass>]
type TileImage (area : Rectangle) =

    /// Gets the rectangular area this image occupies. Note that this can be infinite.
    member this.Area = area

/// A tile image with a specific tile type.
[<AbstractClass>]
type TileImage<'a when 'a : equality> (area : Rectangle) =
    inherit TileImage (area)

    /// Gets the tiles of the most appropriate resolution that intersect the given area with resolution
    /// given in pixels per unit along each axis. The returned tiles should not overlap each other and should
    /// collectively cover the entire area given.
    abstract member GetTiles : area : Rectangle * resolution : Point -> seq<'a>

    /// Gets the area occupied by the given tile.
    abstract member GetTileArea : tile : 'a -> Rectangle

    /// Requests the image for the given tile. When the image is available, the provided callback
    /// should be called with an exclusive handle to it. If the returned retract action is
    /// called, the image is no longer needed, but may still be provided.
    abstract member RequestTileImage : tile : 'a * callback : (Image exclusive -> unit) -> Retract

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
    | Modulate of Paint * Figure
    | Transform of Transform * Figure
    | Composite of Figure * Figure
    | Clip of Rectangle * Figure
    | Hint of RenderHint * Figure
    | Sample of Sample
    | Line of Line
    | Image of Image * ImageInterpolation
    | TileImage of TileImage

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
    let tileImage tile = Figure.TileImage tile

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