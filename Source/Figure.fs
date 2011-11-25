namespace MD

/// Describes a visual object on a two-dimensional plane.
type Figure =
    | Line of Point * Point * double * Paint
    | Transform of Figure * Transform
    | Composite of Figure * Figure