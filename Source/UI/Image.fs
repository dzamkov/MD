namespace MD.UI

open MD
open MD.Util
open System
open System.Drawing
open System.Drawing.Imaging
open Microsoft.FSharp.NativeInterop

open MD.UI

/// Identifies a pixel in an image.
type Pixel (x : int, y : int) =
    struct

        /// Gets the horizontal offset of this pixel from the left edge of the image.
        member this.X = x

        /// Gets the vertical offset of this pixel from the top edge of the image.
        member this.Y = y

    end

/// Gives a size for an image.
type ImageSize (width : int, height : int) =
    struct

        /// Gets the width, in pixels, of the image.
        member this.Width = width

        /// Gets the height, in pixels, of the image.
        member this.Height = height

        /// Gets the total amount of pixels in an image of this size.
        member this.Pixels = width * height

    end

/// A two-dimensional array (with an unspecified size) of items of a certain type.
type Image<'a> = Map<Pixel, 'a> 

/// Create type abbreviation for paint image (since an "image" is usually an image of paints).
type Image = Image<Paint>

/// A color image from a System.Drawing.Bitmap.
[<Sealed>]
type BitmapColorImage (bitmap : Bitmap) =
    inherit Image<Color> ()

    /// Gets the size of this image.
    member this.Size = new ImageSize (bitmap.Width, bitmap.Height)

    /// Gets the bitmap for this image.
    member this.Bitmap = bitmap

    override this.Get pixel = 
        let color = bitmap.GetPixel (pixel.X, pixel.Y)
        Color.RGB (color.R, color.G, color.B)

/// A paint image from a System.Drawing.Bitmap.
[<Sealed>]
type BitmapPaintImage (bitmap : Bitmap) =
    inherit Image<Paint> ()

    /// Gets the size of this image.
    member this.Size = new ImageSize (bitmap.Width, bitmap.Height)

    /// Gets the bitmap for this image.
    member this.Bitmap = bitmap

    override this.Get pixel =
        let color = bitmap.GetPixel (pixel.X, pixel.Y)
        Paint.ARGB (color.A, color.R, color.G, color.B)

/// An opaque paint image from a color image.
[<Sealed>]
type OpaqueImage (source : Image<Color>) =
    inherit Image<Paint> ()

    /// Gets the source color image for this image.
    member this.Source = source

    override this.Get pixel = Paint.Opaque source.[pixel]

/// An image that takes data from a row-major array.
[<Sealed>]
type ArrayImage<'a> (size : ImageSize, array : 'a[]) =
    inherit Image<'a> ()
    new (size : ImageSize) = new ArrayImage<'a> (size, Array.zeroCreate<'a> size.Pixels)
    new (width, height) = new ArrayImage<'a> (new ImageSize (width, height))

    /// Gets the size of this image.
    member this.Size = size

    /// Gets the array for this image.
    member this.Array = array

    /// Gets or sets the value of a pixel in this image.
    member this.Item
        with get (pixel : Pixel) = array.[pixel.X + (pixel.Y * size.Width)]
        and set (pixel : Pixel) value = array.[pixel.X + (pixel.Y * size.Width)] <- value

    /// Gets or sets the value of a pixel in this image.
    member this.Item
        with get (x, y) = array.[x + (y * size.Width)]
        and set (x, y) value = array.[x + (y * size.Width)] <- value

    override this.Get pixel = array.[pixel.X + (pixel.Y * size.Width)]

/// Contains functions for constructing and manipulating images.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Image =

    /// Constructs a color image from a System.Drawing.Bitmap representation.
    let colorBitmap bitmap = new BitmapColorImage (bitmap) :> Image<Color>

    /// Constructs a paint image from a System.Drawing.Bitmap representation.
    let paintBitmap bitmap = new BitmapPaintImage (bitmap) :> Image<Paint>

    /// Constructs an image from an array of items.
    let array array (size : ImageSize) = new ArrayImage<'a> (size, array) :> Image<'a>

    /// Converts an image of colors into an opaque image of paints.
    let opaque image = new OpaqueImage (image) :> Image<Paint>

    /// Tries loading an image from the given path.
    let load (file : Path) =
        try
            let bd = new Bitmap (file.Source)
            let hasAlpha = (bd.Flags &&& int ImageFlags.HasAlpha) <> 0
            let size = new ImageSize (bd.Width, bd.Height)
            let image = Exclusive.dispose bd |> Exclusive.map (if hasAlpha then paintBitmap else colorBitmap >> opaque)
            Some (image, size)
        with
            | _ -> None

    /// Converts an image of color data into a byte array in the BGR24 format.
    let toBGR24 (image : Image<Color>, size : ImageSize) =
        let stride = round (size.Width * 3) 4
        let lineOffset = stride - size.Width * 3
        let outputSize = size.Height * stride
        let output = Array.zeroCreate outputSize
        let mutable pos = 0
        let mutable y = 0
        while y < size.Height do
            let mutable x = 0
            while x < size.Width do
                let color = image.[new Pixel (x, y)]
                output.[pos + 0] <- color.BByte
                output.[pos + 1] <- color.GByte
                output.[pos + 2] <- color.RByte
                pos <- pos + 3
                x <- x + 1
            pos <- pos + lineOffset
            y <- y + 1
        output

    /// Converts an image of paint data into a byte array in the BGRA32 format.
    let toBGRA32 (image : Image<Paint>, size : ImageSize) =
        let outputSize = size.Width * size.Height * 4
        let output = Array.zeroCreate outputSize
        let mutable pos = 0
        let mutable y = 0
        while y < size.Height do
            let mutable x = 0
            while x < size.Width do
                let paint = image.[new Pixel (x, y)]
                let color = paint.Color
                output.[pos + 0] <- color.BByte
                output.[pos + 1] <- color.GByte
                output.[pos + 2] <- color.RByte
                output.[pos + 3] <- paint.AlphaByte
                pos <- pos + 4
                x <- x + 1
            y <- y + 1
        output

    /// Matches an image for an array representation.
    let (|Array|_|) (image : Image<'a>) =
        match image with
        | :? ArrayImage<'a> as image -> Some image
        | _ -> None

    /// Matches a paint image for an opaque color image representation.
    let (|Opaque|_|) (image : Image<Paint>) = 
        match image with
        | :? OpaqueImage as image -> Some image.Source
        | _ -> None

    /// Matches a paint image for a bitmap representation.
    let (|PaintBitmap|_|) (image : Image<Paint>) = 
        match image with
        | :? BitmapPaintImage as image -> Some image.Bitmap
        | _ -> None

    /// Matches a color image for a bitmap representation.
    let (|ColorBitmap|_|) (image : Image<Color>) = 
        match image with
        | :? BitmapColorImage as image -> Some image.Bitmap
        | _ -> None