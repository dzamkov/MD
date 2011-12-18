namespace MD.OpenTK

open MD
open MD.UI
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open MD.UI.Image

/// An interface to a OpenGL texture.
type Texture (id : int) =

    /// Creates and binds a 2d texture with no associated image data.
    static member Create () =
        let id = GL.GenTexture ()
        GL.BindTexture (TextureTarget.Texture2D, id)
        GL.TexEnv (TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, int TextureEnvMode.Modulate);
        new Texture (id)

    /// Creates and binds a 2d texture for the given image. This will not set any parameters or generate bitmaps.
    static member Create (image : Image<Paint>) =
        let tex = Texture.Create ()
        Texture.SetImage (image, 0)
        tex

    /// Sets the image for the given mipmap level of the currently-bound 2d texture.
    static member SetImage (image : Image<Paint>, level : int) =
        match image with
        | Opaque image -> 
            let data = image.Source |> Image.toArray |> Image.toBGR24
            GL.TexImage2D (TextureTarget.Texture2D, level, PixelInternalFormat.Rgb, image.Width, image.Height, 0, PixelFormat.Bgr, PixelType.UnsignedByte, data)
        | image ->
            let data = image |> Image.toArray |> Image.toBGRA32
            GL.TexImage2D (TextureTarget.Texture2D, level, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, data)

    /// Sets the wrap mode for the currently-bound texture.
    static member SetWrapMode (target, horizontal : TextureWrapMode, vertical : TextureWrapMode) =
        GL.TexParameter (target, TextureParameterName.TextureWrapS, int horizontal)
        GL.TexParameter (target, TextureParameterName.TextureWrapT, int vertical)

    /// Sets the filter mode for the currently-bound texture.
    static member SetFilterMode (target, min : TextureMinFilter, mag : TextureMagFilter) =
        GL.TexParameter (target, TextureParameterName.TextureMinFilter, int min)
        GL.TexParameter (target, TextureParameterName.TextureMagFilter, int mag)

    /// Creates a mipmap for the currently-bound texture.
    static member CreateMipmap (target) =
        GL.Ext.GenerateMipmap target

    /// Gets the ID for this texture.
    member this.ID = id
    
    /// Sets this as the current texture for the given texture target.
    member this.Bind target = GL.BindTexture (target, id)

    /// Sets this as the current texture for Texture2D target.
    member this.Bind2D () = GL.BindTexture (TextureTarget.Texture2D, id)

    /// Creates an exclusive handle to this texture that will delete it upon release.
    member this.MakeExclusive () = this |> Exclusive.custom (fun this -> this.Delete ())

    /// Deletes this texture.
    member this.Delete () = GL.DeleteTexture id