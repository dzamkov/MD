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

    do
        let song = new Path (@"N:\Music\Me\19.mp3")
        let container, context = (Container.Load song).Value
        let audiocontent = context.Content.[0] :?> AudioContent
        let audiostream = Stream.chunk () (fun () -> if context.NextFrame (ref 0) then Some (Data.read audiocontent.Data.Value, ()) else None)

        let probe = Input.probe this.Mouse
        let control =
            let play = (Feed.falling probe.Primary).Value |> Feed.replace AudioControl.Play
            let pause = (Feed.falling probe.Secondary).Value |> Feed.replace AudioControl.Pause
            Feed.unione play pause
        let volume = probe.Position |> Feed.maps (fun x -> x.Y / 300.0)
        let pitch = probe.Position |> Feed.maps (fun x -> Math.Exp(x.X / 300.0 - 1.0))

        let audioparam = {
            Stream = audiostream
            SampleRate = int audiocontent.SampleRate;
            Channels = audiocontent.Channels
            Format = audiocontent.Format
            Control = control
            Volume = volume
            Pitch = pitch
            }
        (audiooutput :> AudioOutput).Begin audioparam |> ignore
        
    override this.OnRenderFrame args =
        this.MakeCurrent ()
        GL.ClearColor (0.5f, 0.5f, 0.5f, 1.0f)
        GL.Clear ClearBufferMask.ColorBufferBit

        this.SwapBuffers ()

    override this.OnUpdateFrame args =
        let updatetime = args.Time
        audiooutput.Update ()
        ()

    override this.OnResize args =
        this.MakeCurrent ()
        GL.Viewport (0, 0, this.Width, this.Height)

        ()