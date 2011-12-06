namespace MD.OpenTK

open MD
open Util
open System
open Microsoft.FSharp.NativeInterop
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

    let image = Image.load (Path.WorkingDirectory + "Resources" + "Images" + "Test.png") |> Option.get
    let fig = new ControlSignalFeed<Figure> (Figure.``null``)

    do 
        this.MakeCurrent ()
        this.VSync <- VSyncMode.On
        Graphics.Initialize ()

        let music = new Path (@"N:\Music\Me\57.mp3")
        let container, context = (Container.Load music).Value
        let audiocontent = context.Object.Content.[0] :?> AudioContent
        let control = new ControlEventFeed<AudioControl> ()

        let stream = 
            context 
            |> Exclusive.bind (fun context -> 
                Stream.chunk 1 () (fun () -> 
                    let mutable index = 0
                    if context.NextFrame (&index)
                    then Some (Data.lock audiocontent.Data.Value, ())
                    else None)) 
            |> Exclusive.map Stream.cast
        let shortData : int16 data = Data.make 65536 stream
        let floatData = Data.map (fun x -> float x / 32768.0) shortData
        let monoFloatData = Data.combine 2 (fun (x, o) -> x.[o]) floatData

        let mouse = Input.probe this.Mouse
        let mouseView = Probe.windowToView size mouse

        let audioparams = {
                Stream = shortData.Lock () |> Exclusive.map Stream.cast
                SampleRate = int audiocontent.SampleRate
                Channels = audiocontent.Channels
                Format = audiocontent.Format
                Control = control
                Volume = mouseView.Position |> Feed.maps (fun x -> Math.Exp x.Y) 
                Pitch = mouseView.Position |> Feed.maps (fun x -> Math.Exp x.X)
            }
        audiooutput.Begin audioparams |> ignore
        control.Fire AudioControl.Play

        let gradient = 
            new Gradient [|
                { Value = 0.0; Color = Color.RGB(1.0, 1.0, 1.0) };
                { Value = 0.35; Color = Color.RGB(0.0, 1.0, 1.0) };
                { Value = 0.5; Color = Color.RGB(0.0, 1.0, 0.0) };
                { Value = 0.6; Color = Color.RGB(1.0, 1.0, 0.0) };
                { Value = 0.85; Color = Color.RGB(1.0, 0.0, 0.0) };
                { Value = 1.0; Color = Color.RGB(0.5, 0.0, 0.0) };
            |]

        let timeResolution = 1024
        let windowDelta = 2048
        let freqResolution = 2048
        let colorBuffer = Array2D.zeroCreate timeResolution freqResolution

        let parameters = new FFTParameters (freqResolution)
        let window : float[] = Array.zeroCreate freqResolution
        let output : Complex[] = Array.zeroCreate freqResolution
        for x = 0 to timeResolution - 1 do
            let start = uint64 windowDelta * uint64 x
            monoFloatData.Read (start, window, 0, freqResolution)
            pin window (fun windowPtr -> pin output (fun outputPtr -> DFT.computeReal (NativePtr.ofNativeInt windowPtr) (NativePtr.ofNativeInt outputPtr) parameters))
            for y = 0 to freqResolution - 1 do
                colorBuffer.[x, freqResolution - y - 1] <- gradient.GetColor (output.[y].Abs * 0.1)

        let image = Image.colorBuffer colorBuffer

        fig.Current <- Figure.image image ImageInterpolation.Linear (new Rectangle (-1.0, 7.0, 1.0, -1.0))

    /// Gets a feed that gives the size of the client area of this window in pixels.
    member this.Size = size

    override this.OnRenderFrame args =
        Graphics.Setup (Transform.Identity, this.Width, this.Height, false)
        Graphics.Clear (Color.RGB (1.0, 1.0, 1.0))

        graphics.Render fig.Current

        this.SwapBuffers ()

    override this.OnUpdateFrame args =
        // Update program time
        Update.invoke args.Time

    override this.OnResize args =
        size.Current <- new Point (double this.Width, double this.Height)