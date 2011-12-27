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
    | Image of Image * ImageSize * ImageInterpolation

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

    /// Constructs a clipped form of a figure. This will cause all paints of the figure outside the given area to
    /// be completely transparent.
    let clip area figure = Figure.Clip (area, figure)

    /// Provides a hint for the given figure.
    let hint hint figure = Figure.Hint (hint, figure)