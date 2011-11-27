namespace MD

/// Describes a visual object on a two-dimensional plane.
type Figure =
    | Line of Point * Point * double * Paint
    | Transform of Figure * Transform
    | Composite of Figure * Figure

    /// Constructs a transformed figure.
    static member (*) (a : Figure, b : Transform) =
        Figure.Transform (a, b)

    /// Constructs a composite figure.
    static member (+) (a : Figure, b : Figure) =
        Figure.Composite (a, b)

/// Contains functions for constructing and manipulating figures.
[<CompilationRepresentationAttribute(CompilationRepresentationFlags.ModuleSuffix)>]
module Figure =
    
    /// Constructs a figure for a colored line.
    let line start stop weight paint = Figure.Line (start, stop, weight, paint)

    /// Constructs a transformed form of a figure.
    let transform transform figure = Figure.Transform (figure, transform)

    /// Constructs a composite figure.
    let composite bottom top = Figure.Composite (bottom, top)