namespace MD

/// Describes a visual object on a two-dimensional plane.
type Figure =
    | Line of Point * Point * double * Paint
    | Image of Image * Rectangle
    | Modulate of Paint * Figure
    | Transform of Transform * Figure
    | Composite of Figure * Figure

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
    
    /// Constructs a figure for a colored line.
    let line start stop weight paint = Figure.Line (start, stop, weight, paint)

    /// Constructs a figure for an imaged placed in a rectangular area. Note that this figure will respect 
    /// the transparency information encoded in the image, if any.
    let image image area = Figure.Image (image, area)

    /// Constructs a transformed form of a figure.
    let transform transform figure = Figure.Transform (transform, figure)

    /// Constructs a modulated form of a figure. This will cause all colors (and transparency) of the figure to
    /// be multiplied by the values in a paint.
    let modulate paint figure = Figure.Modulate (paint, figure)

    /// Constructs a composite figure.
    let composite bottom top = Figure.Composite (bottom, top)