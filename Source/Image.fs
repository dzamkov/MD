namespace MD

open System
open System.Drawing
open System.Drawing.Imaging
open Microsoft.FSharp.NativeInterop

/// Identifies a possible image format.
type ImageFormat =
    | BGR24 = 0
    | BGR24Aligned = 1
    | BGRA32 = 2

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

/// Contains functions for constructing and manipulating images.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Image =

    /// Creates an image from a System.Drawing.Bitmap representation.
    let bitmap (bitmap : Bitmap) =
        let width = bitmap.Width
        let height = bitmap.Height
        let nformat = bitmap.PixelFormat
        let iformat, rformat = 
            match nformat with
            | PixelFormat.Format24bppRgb -> (ImageFormat.BGR24Aligned, PixelFormat.Format24bppRgb)
            | _ -> (ImageFormat.BGRA32, PixelFormat.Format32bppArgb)
        
        let bd = bitmap.LockBits (new Rectangle (0, 0, width, height), ImageLockMode.ReadOnly, rformat)
        let ds = height * bd.Stride
        let datastart = NativePtr.ofNativeInt bd.Scan0
        let data = Data.unsafe datastart (NativePtr.add datastart ds)
        let image = { Width = width; Height = height; Format = iformat; Data = data }
        Exclusive.custom (fun () -> bitmap.UnlockBits bd) image

    /// Tries loading an image from the given apth.
    let load (file : Path) =
        try
            let bd = new Bitmap (file.Source)
            Some (Exclusive.dispose bd |> Exclusive.bind bitmap)
        with
            | _ -> None