namespace MD.OpenTK

open MD
open MD.UI
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

/// An interface to a OpenGL texture.
type Texture (id : int) =
    struct
        /// Gets the null texture (a texture with id 0).
        static member Null = new Texture (0)

        /// Creates and binds a 2d texture with no associated image data.
        static member Create () =
            let id = GL.GenTexture ()
            GL.BindTexture (TextureTarget.Texture2D, id)
            GL.TexEnv (TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, int TextureEnvMode.Modulate);
            new Texture (id)

        /// Creates and binds a 2d texture for the given image. This will not set any parameters or generate bitmaps.
        static member Create (image : Image) =
            let tex = Texture.Create ()
            Texture.SetImage (image, 0)
            tex

        /// Sets the image for the given mipmap level of the currently-bound 2d texture.
        static member SetImage (image : ImageData, level : int) =
            let pif, pf, pt = 
                match image.Format with
                | ImageFormat.BGR24 -> (PixelInternalFormat.Rgb, PixelFormat.Bgr, PixelType.UnsignedByte)
                | ImageFormat.BGRA32 -> (PixelInternalFormat.Rgba, PixelFormat.Bgra, PixelType.UnsignedByte)
            match image.Data with
            | Data.Buffer buffer when buffer.Stride = 1u -> GL.TexImage2D (TextureTarget.Texture2D, level, pif, image.Width, image.Height, 0, pf, pt, buffer.Start)
            | Data.ArrayComplete array -> GL.TexImage2D (TextureTarget.Texture2D, level, pif, image.Width, image.Height, 0, pf, pt, array)

        /// Sets the image for the given mipmap level of the currently-bound 2d texture.
        static member SetImage (image : Image, level : int) =
            let format = image.NativeFormat
            let edata = image.Lock format
            Texture.SetImage (edata.Object, level)
            edata.Finish ()

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
    
        /// Sets this as the current texture for the given texture target.
        member this.Bind target = GL.BindTexture (target, id)

        // Sets this as the current texture for Texture2D target.
        member this.Bind2D () = GL.BindTexture (TextureTarget.Texture2D, id)

        /// Deletes this texture.
        member this.Delete () = GL.DeleteTexture id
    end