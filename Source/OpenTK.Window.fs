namespace MD.OpenTK

open MD
open System
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open OpenTK.Input

/// Main program window
type Window () as this =
    inherit GameWindow (640, 480, GraphicsMode.Default, "MD")
    let audiooutput = AudioOutput.Create () |> Option.get
    let graphics = Graphics.Create ()
    let mutable delta = 0.0

    do 
        this.MakeCurrent ()
        this.VSync <- VSyncMode.On
        Graphics.Initialize ()

    override this.OnRenderFrame args =
        Graphics.Setup (Transform.Identity, this.Width, this.Height, false)
        Graphics.Clear (Color.RGB (1.0, 1.0, 1.0))

        let fig = 
            Figure.Line (new Point (-0.5, -0.5), new Point (0.5, 0.5), 0.1, Paint.ARGB (1.0, 0.0, 0.0, 0.5))
            |> Figure.transform (Transform.Rotate delta)
            |> Figure.transform (Transform.Scale (cos (delta * 3.7) * 0.3 + 0.7))
            |> Figure.transform (Transform.Translate (new Point (delta * 0.05, 0.0)))
        graphics.Render fig

        this.SwapBuffers ()

    override this.OnUpdateFrame args =
        let updatetime = args.Time
        delta <- delta + updatetime
        ()