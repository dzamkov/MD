namespace MD

open Util
open System
open System.Drawing
open System.Drawing.Imaging

/// Identifies a possible image format.
type ImageFormat =
    | BGR24
    | BGRA32

/// Contains raw image data and information needed to interpret it as a colored image.
type ImageData = {
    /// The width of the image in pixels.
    Width : int

    /// The height of the image in pixels.
    Height : int

    /// The format for the image.
    Format : ImageFormat

    /// The data for the image.
    Data : byte data
    }

/// Represents an immutable image, a two-dimensional array of pixels containing color or paint data.
[<AbstractClass>]
type Image (width : int, height : int, nativeFormat : ImageFormat) =

    /// Gets the width of the image, in pixels.
    member this.Width = width

    /// Gets the height of the image, in pixels.
    member this.Height = height

    /// Gets the native format of the image. This is the format that is most preferable to use
    /// when accessing this image.
    member this.NativeFormat = nativeFormat

    /// Locks a rectangular region of this image for reading.
    abstract member LockData : left : int * top : int * width : int * height : int * format : ImageFormat -> byte data exclusive

    /// Locks the entirety of this image for reading.
    member this.LockData format = this.LockData (0, 0, width, height, format)

    /// Locks a rectangular region of this image for reading.
    member this.Lock (left, top, width, height, format) = this.LockData (left, top, width, height, format) |> Exclusive.map (fun data ->
        { Width = width; Height = height; Format = format; Data = data })

    /// Locks the entirety of this image for reading.
    member this.Lock format = this.LockData (0, 0, width, height, format) |> Exclusive.map (fun data ->
        { Width = width; Height = height; Format = format; Data = data })

/// An image from a System.Drawing.Bitmap.
[<Sealed>]
type BitmapImage (bitmap : Bitmap) =
    inherit Image (bitmap.Width, bitmap.Height, 
        match bitmap.PixelFormat with
        | PixelFormat.Format24bppRgb -> ImageFormat.BGR24
        | PixelFormat.Format32bppArgb -> ImageFormat.BGRA32
        | _ -> ImageFormat.BGRA32)

    override this.LockData (left, top, width, height, format) =
        let rformat = 
            match format with
            | ImageFormat.BGR24 -> PixelFormat.Format24bppRgb
            | ImageFormat.BGRA32 -> PixelFormat.Format32bppArgb
        let bd = bitmap.LockBits (new Rectangle (left, top, width, height), ImageLockMode.ReadOnly, rformat)
        let ds = height * bd.Stride
        let data = Data.unsafe bd.Scan0 (bd.Scan0 + nativeint ds)
        Exclusive.custom (fun () -> bitmap.UnlockBits bd) data

/// An image from a two-dimensional array of colors.
type ColorBufferImage (buffer : MD.Color[,]) =
    inherit Image (buffer.GetLength 0, buffer.GetLength 1, ImageFormat.BGR24)

    override this.LockData (left, top, width, height, format) =
        match format with
        | ImageFormat.BGRA32 -> new NotImplementedException () |> raise
        | ImageFormat.BGR24 ->
            let stride = round (width * 3) 4
            let lineOffset = stride - width * 3
            let outputSize = height * stride
            let output = Array.zeroCreate outputSize
            let mutable pos = 0
            let mutable y = 0
            while y < height do
                let mutable x = 0
                while x < width do
                    let color = buffer.[left + x, top + y]
                    output.[pos + 0] <- color.BByte
                    output.[pos + 1] <- color.GByte
                    output.[pos + 2] <- color.RByte
                    pos <- pos + 3
                    x <- x + 1
                pos <- pos + lineOffset
                y <- y + 1
            Data.buffer output 0 outputSize |> Exclusive.``static``

/// An image from a two-dimensional array of paints.
type PaintBufferImage (buffer : Paint[,]) =
    inherit Image (buffer.GetLength 0, buffer.GetLength 1, ImageFormat.BGRA32)

    override this.LockData (left, top, width, height, format) =
        match format with
        | ImageFormat.BGRA32 ->
            let outputSize = width * height * 4
            let output = Array.zeroCreate outputSize
            let mutable pos = 0
            let mutable y = 0
            while y < height do
                let mutable x = 0
                while x < width do
                    let paint = buffer.[left + x, top + y]
                    let color = paint.Color
                    output.[pos + 0] <- color.BByte
                    output.[pos + 1] <- color.GByte
                    output.[pos + 2] <- color.RByte
                    output.[pos + 3] <- paint.AlphaByte
                    pos <- pos + 4
                    x <- x + 1
                y <- y + 1
            Data.buffer output 0 outputSize |> Exclusive.``static``
        | ImageFormat.BGR24 -> new NotImplementedException () |> raise

/// Contains functions for constructing and manipulating images.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Image =

    /// Creates an image from a System.Drawing.Bitmap representation.
    let bitmap bitmap = new BitmapImage (bitmap) :> Image

    /// Creates an image from a color buffer representation.
    let colorBuffer buffer = new ColorBufferImage (buffer) :> Image

    /// Creates an image from a paint buffer representation.
    let paintBuffer buffer = new PaintBufferImage (buffer) :> Image

    /// Tries loading an image from the given apth.
    let load (file : Path) =
        try
            let bd = new Bitmap (file.Source)
            Some (Exclusive.dispose bd |> Exclusive.map bitmap)
        with
            | _ -> None