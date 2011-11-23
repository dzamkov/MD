namespace MD

open System
open OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open OpenTK.Input

/// Main program window
type Window () =
    inherit GameWindow (640, 480, GraphicsMode.Default, "MD")

    let audiooutput = new OpenALOutput ()
    let song = new Path ("N:\\Music\\Me\\57.mp3")
    let container, context = (Container.Load song).Value
    let audiocontent = context.Content.[0] :?> AudioContent
    let audiostream = Stream.chunk () (fun () -> if context.NextFrame (ref 0) then Some (Data.read audiocontent.Data.Value, ()) else None)
    let controlfeed = new ControlEventFeed<AudioControl> ()
    let pitchfeed = new ControlSignalFeed<double> (1.0)
    let audioparam = { 
        Stream = audiostream
        SampleRate = int audiocontent.SampleRate;
        Channels = audiocontent.Channels
        Format = audiocontent.Format
        Control = controlfeed :> EventFeed<AudioControl>
        Pitch = pitchfeed :> SignalFeed<double>
        }
    do
        (audiooutput :> AudioOutput).Begin audioparam |> ignore
        controlfeed.Fire AudioControl.Play


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