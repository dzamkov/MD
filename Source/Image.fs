namespace MD

/// Identifies a possible image format.
type ImageFormat =
    | BGR24 = 0
    | BGR24Aligned = 1
    | ABGR32 = 2

/// Describes an immutable image, a two-dimensional array of pixels that contain color or paint information.
type Image = {
    
    /// The width of the image, in pixels
    Width : int

    /// The height of the image, in pixels
    Height : int

    /// The format for the image
    Format : ImageFormat

    /// The raw pixel data for the image
    Data : byte data
    }