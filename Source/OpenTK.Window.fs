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
    let size = new ControlSignalFeed<Point> (new Point (float 640, float 480))
    let input = Input.create this
    let programTime = Feed.time

    let mutable figure = Unchecked.defaultof<Figure signal>
    let mutable projection = Unchecked.defaultof<Transform signal>

    /// Gets a feed that gives the size of the client area of this window in pixels.
    member this.Size = size

    override this.OnLoad args =
        this.MakeCurrent ()
        this.VSync <- VSyncMode.Off
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

        let sampleRate = audiocontent.SampleRate
        let audioparams = {
                Stream = shortData.Lock () |> Exclusive.map Stream.cast
                SampleRate = int sampleRate
                Channels = audiocontent.Channels
                Format = audiocontent.Format
                Control = control
                Volume = Feed.constant 1.0
                Pitch = Feed.constant 1.0
            }
        let playPosition = (audiooutput.Begin audioparams).Value.Position

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
        let windowSize = 4096 * 2
        let downsampleCount = log2 (uint32 windowSize) - log2 (uint32 freqResolution)
        let colorBuffer = Array2D.zeroCreate timeResolution freqResolution

        let parameters = new FFTParameters (freqResolution, 8)

        let window : float[] = Array.zeroCreate windowSize
        let output : Complex[] = Array.zeroCreate freqResolution
        for x = 0 to timeResolution - 1 do
            let start = uint64 (windowDelta * x)
            monoFloatData.Read (start * 2UL, window, 0, windowSize)

            let windowHandle, windowPtr = pin window
            let outputHandle, outputPtr = pin output

            let mutable curSize = windowSize
            while curSize > freqResolution do
                DSignal.downsampleReal (NativePtr.ofNativeInt windowPtr) curSize
                curSize <- curSize / 2
            DFT.computeReal (NativePtr.ofNativeInt windowPtr) (NativePtr.ofNativeInt outputPtr) parameters

            unpin windowHandle
            unpin outputHandle

            for y = 0 to freqResolution - 1 do
                colorBuffer.[x, freqResolution - y - 1] <- gradient.GetColor (output.[y].Abs * 0.1)

        let image = Image.colorBuffer colorBuffer

        let mouse = Input.probe this.Mouse
        let initialViewState = {
                Center = new Point (0.0, 0.0)
                Velocity = new Point (0.0, 0.0)
                Zoom = 0.0
                ZoomVelocity = -0.2
            }
        let view = Exclusive.get (View.Create {
                InitialState = initialViewState
                ChangeState = Feed.nil
                Bounds = Rectangle.Unbound
                Input = Input.windowToView size input
                VelocityDamping = 0.1
                ZoomVelocityDamping = 0.1
            })

        let getFigure playSample =
            let image = Figure.image image ImageInterpolation.Nearest (new Rectangle (-1.0, 1.0, 1.0, -1.0))
            let linex = 2.0 * float playSample / float (timeResolution * windowDelta) - 1.0
            let line = Figure.line (new Point (linex, -1.0)) (new Point (linex, 1.0)) 0.002 (Paint.ARGB (1.0, 1.0, 0.3, 0.0))
            Figure.composite image line

        projection <- view.Projection
        figure <- playPosition |> Feed.maps getFigure
        control.Fire AudioControl.Play

    override this.OnRenderFrame args =
        Graphics.Setup (projection.Current, this.Width, this.Height, false)
        Graphics.Clear (Color.RGB (0.6, 0.8, 1.0))

        graphics.Render figure.Current

        this.SwapBuffers ()

    override this.OnUpdateFrame args =
        // Update program time
        Update.invoke args.Time

    override this.OnResize args =
        size.Current <- new Point (double this.Width, double this.Height)