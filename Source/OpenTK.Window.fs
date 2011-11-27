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
    let audiooutput = AudioOutput.Create () |> Option.get :> MD.AudioOutput
    let graphics = Graphics.Create ()
    let size = new ControlSignalFeed<Point> (new Point (double this.Width, double this.Height))
    let programTime = Feed.time

    let fig = programTime |> Feed.maps (fun time ->
        Figure.Line (new Point (-0.5, -0.5), new Point (0.5, 0.5), 0.1, Paint.ARGB (cos time * 0.5 + 0.5, sin time * 0.5 + 0.5, 1.0, 0.5))
        |> Figure.transform (Transform.Rotate time)
        |> Figure.transform (Transform.Scale (cos (time * 3.7) * 0.3 + 0.7))
        |> Figure.transform (Transform.Translate (new Point (time * 0.05, 0.0)))
        |> Temp.``static``)

    do 
        this.MakeCurrent ()
        this.VSync <- VSyncMode.On
        Graphics.Initialize ()

        let music = new Path (@"N:\Music\Me\57.mp3")
        let container, context = (Container.Load music).Value
        let audiocontent = context.Object.Content.[0] :?> AudioContent
        let control = new ControlEventFeed<AudioControl> ()
        let audioparams = {
                Stream = context |> Exclusive.bind (fun context -> 
                    Stream.chunk () (fun () -> 
                        let mutable index = 0
                        if context.NextFrame (&index)
                        then Some (Data.read audiocontent.Data.Value, ())
                        else None))
                SampleRate = int audiocontent.SampleRate
                Channels = audiocontent.Channels
                Format = audiocontent.Format
                Control = control
                Volume = Feed.``const`` 1.0
                Pitch = Feed.``const`` 1.0
            }
        audiooutput.Begin audioparams |> ignore
        control.Fire AudioControl.Play
        

    /// Gets a feed that gives the size of the client area of this window in pixels.
    member this.Size = size

    override this.OnRenderFrame args =
        Graphics.Setup (Transform.Identity, this.Width, this.Height, false)
        Graphics.Clear (Color.RGB (1.0, 1.0, 1.0))

        fig.Current |> Temp.tryUse graphics.Render

        this.SwapBuffers ()

    override this.OnUpdateFrame args =
        // Update program time
        Update.invoke args.Time

    override this.OnResize args =
        size.Current <- new Point (double this.Width, double this.Height)