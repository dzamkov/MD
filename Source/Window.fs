namespace MD

open System
open OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open OpenTK.Input

/// Main program window
type Window () =
    inherit GameWindow (640, 480, GraphicsMode.Default, "MD")

    override this.OnRenderFrame args =
        this.MakeCurrent ()
        GL.ClearColor (0.5f, 0.5f, 0.5f, 1.0f)
        GL.Clear ClearBufferMask.ColorBufferBit

        this.SwapBuffers ()

    override this.OnUpdateFrame args =
        let updatetime = args.Time
        ()

    override this.OnResize args =
        this.MakeCurrent ()
        GL.Viewport (0, 0, this.Width, this.Height)

        ()