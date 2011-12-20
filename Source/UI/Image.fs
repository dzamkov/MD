namespace MD.UI

open MD
open MD.Util
open System
open System.Drawing
open System.Drawing.Imaging
open Microsoft.FSharp.NativeInterop

open MD.UI

/// A two-dimensional array of items of a certain type.
[<AbstractClass>]
type Image<'a when 'a : unmanaged> (width : int, height : int) =

    /// Gets the width of the image, in pixels.
    member this.Width = width

    /// Gets the height of the image, in pixels.
    member this.Height = height

    /// Reads the entirety of this image into the given row-major array, starting at the given offset.
    abstract member Read : array : 'a[] * offset : int -> unit

/// Create type abbreviation for image (since an "image" is usually an image of paints).
type Image = Image<Paint>

/// A color image from a System.Drawing.Bitmap.
[<Sealed>]
type BitmapColorImage (bitmap : Bitmap) =
    inherit Image<Color> (bitmap.Width, bitmap.Height)

    override this.Read (array, offset) =
        new NotImplementedException () |> raise

/// A paint image from a System.Drawing.Bitmap.
[<Sealed>]
type BitmapPaintImage (bitmap : Bitmap) =
    inherit Image<Paint> (bitmap.Width, bitmap.Height)

    override this.Read (array, offset) =
        new NotImplementedException () |> raise

/// An opaque paint image from a color image.
[<Sealed>]
type OpaqueImage (source : Image<Color>) =
    inherit Image<Paint> (source.Width, source.Height)

    /// Gets the source color image for this image.
    member this.Source = source

    override this.Read (array, offset) =
        let tempArray = Array.zeroCreate (this.Width * this.Height)
        source.Read (tempArray, 0)
        for index = 0 to tempArray.Length - 1 do
            array.[index + offset] <- Paint.RGB tempArray.[index]

/// An image that takes data from a row-major array.
[<Sealed>]
type ArrayImage<'a when 'a : unmanaged> (width : int, height : int, array : 'a[]) =
    inherit Image<'a> (width, height)
    new (width, height) = new ArrayImage<'a> (width, height, Array.zeroCreate (width * height))

    /// Gets the array for this image.
    member this.Array = array

    /// Gets or sets a pixel in this image.
    member this.Item 
        with get (x, y) = array.[x + (width * y)]
        and set (x, y) value = array.[x + (width * y)] <- value

    override this.Read (targetArray, targetOffset) =
        let size = this.Width * this.Height
        Array.blit array 0 targetArray targetOffset size 

/// An image that applies a gradient to a source image of floats.
type GradientImage (source : Image<float>, gradient : Gradient) =
    inherit Image<Color> (source.Width, source.Height)

    override this.Read (array, offset) =
        let tempArray = Array.zeroCreate (this.Width * this.Height)
        source.Read (tempArray, 0)
        for index = 0 to tempArray.Length - 1 do
            array.[index + offset] <- gradient.GetColor tempArray.[index]

/// Contains functions for constructing and manipulating images.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Image =

    /// Creates a color image from a System.Drawing.Bitmap representation.
    let colorBitmap bitmap = new BitmapColorImage (bitmap) :> Image<Color>

    /// Creates a paint image from a System.Drawing.Bitmap representation.
    let paintBitmap bitmap = new BitmapPaintImage (bitmap) :> Image<Paint>

    /// Creates an image from an array of items.
    let array array width height = new ArrayImage<'a> (width, height, array) :> Image<'a>

    /// Creates a color image from a gradient and a float image.
    let gradient gradient source = new GradientImage (source, gradient) :> Image<Color>

    /// Converts an image of colors into an opaque image of paints.
    let opaque image = new OpaqueImage (image) :> Image<Paint>

    /// Tries loading an image from the given path.
    let load (file : Path) =
        try
            let bd = new Bitmap (file.Source)
            Exclusive.dispose bd |> Exclusive.map (if (bd.Flags &&& int ImageFlags.HasAlpha) <> 0 then paintBitmap else colorBitmap >> opaque) |> Some
        with
            | _ -> None

    /// Converts an image into an array image.
    let toArray (image : Image<'a>) =
        match image with
        | :? ArrayImage<'a> as image -> image
        | _ ->
            let width = image.Width
            let height = image.Height
            let array = Array.zeroCreate (width * height)
            image.Read (array, 0)
            new ArrayImage<'a> (width, height, array)

    /// Converts an array image of color data into a byte array in the BGR24 format.
    let toBGR24 (image : ArrayImage<Color>) =
        let width = image.Width
        let height = image.Height
        let array = image.Array
        let stride = round (width * 3) 4
        let lineOffset = stride - width * 3
        let outputSize = height * stride
        let output = Array.zeroCreate outputSize
        let mutable pos = 0
        let mutable offset = 0
        let mutable y = 0
        while y < height do
            let mutable x = 0
            while x < width do
                let color = array.[offset + x]
                output.[pos + 0] <- color.BByte
                output.[pos + 1] <- color.GByte
                output.[pos + 2] <- color.RByte
                pos <- pos + 3
                x <- x + 1
            offset <- offset + width
            pos <- pos + lineOffset
            y <- y + 1
        output

    /// Converts an array image of paint data into a byte array in the BGRA32 format.
    let toBGRA32 (image : ArrayImage<Paint>) =
        let width = image.Width
        let height = image.Height
        let array = image.Array
        let outputSize = height * width
        let output = Array.zeroCreate outputSize
        let mutable pos = 0
        let mutable offset = 0
        let mutable y = 0
        while y < height do
            let mutable x = 0
            while x < width do
                let paint = array.[offset + x]
                let color = paint.Color
                output.[pos + 0] <- color.BByte
                output.[pos + 1] <- color.GByte
                output.[pos + 2] <- color.RByte
                output.[pos + 2] <- paint.AlphaByte
                pos <- pos + 3
                x <- x + 1
            offset <- offset + width
            y <- y + 1
        output

    /// Matches a paint image for an opaque representation, if possible.
    let (|Opaque|_|) (image : Image<Paint>) = 
        match image with
        | :? OpaqueImage as image -> Some image
        | _ -> None

    /// Matches a paint image for a bitmap representation, if possible.
    let (|PaintBitmap|_|) (image : Image<Paint>) = 
        match image with
        | :? BitmapPaintImage as image -> Some image
        | _ -> None

    /// Matches a color image for a bitmap representation, if possible.
    let (|ColorBitmap|_|) (image : Image<Color>) = 
        match image with
        | :? BitmapColorImage as image -> Some image
        | _ -> None