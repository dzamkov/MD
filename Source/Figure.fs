namespace MD

/// Describes a visual object on a two-dimensional plane.
type Figure =
    | Line of Point * Point * double * Paint
    | Transform of Figure * Transform
    | Composite of Figure * Figure

/// Contains functions for constructing and manipulating figures.
[<CompilationRepresentationAttribute(CompilationRepresentationFlags.ModuleSuffix)>]
module Figure =
    
    /// Constructs a transformed form of a figure.
    let transform transform figure = Figure.Transform (figure, transform)