namespace MD

open System
open System.Drawing
open System.Drawing.Imaging
open Microsoft.FSharp.NativeInterop

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
    


/// Contains functions for constructing and manipulating images.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Image =

    /// Creates an image from a System.Drawing.Bitmap representation.
    let bitmap (bitmap : Bitmap) = new BitmapImage (bitmap) :> Image

    /// Tries loading an image from the given apth.
    let load (file : Path) =
        try
            let bd = new Bitmap (file.Source)
            Some (Exclusive.dispose bd |> Exclusive.map bitmap)
        with
            | _ -> None