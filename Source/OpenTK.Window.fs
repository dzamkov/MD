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

        // Get audio container
        let music = new Path (@"N:\Music\Me\19.mp3")
        let container, context = (Container.Load music).Value
        let audiocontent = context.Object.Content.[0] :?> AudioContent
        let control = new ControlEventFeed<AudioControl> ()

        // Read audio data
        let stream = 
            context 
            |> Exclusive.bind (fun context -> 
                Stream.chunk 1 () (fun () -> 
                    let mutable index = 0
                    if context.NextFrame (&index)
                    then Some (Data.lock audiocontent.Data.Value, ())
                    else None)) 
        let data = Data.make 65536 stream
        let floatData = Data.combine 4 (fun (x, o) -> float (BitConverter.ToInt16 (x, o)) / 32768.0) data

        // Play audio
        let sampleRate = audiocontent.SampleRate
        let audioparams = {
                Stream = data.Lock ()
                SampleRate = int sampleRate
                Channels = audiocontent.Channels
                Format = audiocontent.Format
                Control = control
                Volume = Feed.constant 1.0
                Pitch = Feed.constant 1.0
            }
        let playPosition = (audiooutput.Begin audioparams).Value.Position

        // Setup mouse control and view
        let mouse = Input.probe this.Mouse
        let initialViewState = {
                Center = new Point (0.0, 0.0)
                Velocity = new Point (0.0, 0.0)
                Zoom = 0.0
                ZoomVelocity = -0.2
            }
        let view, _ = View.Create {
                InitialState = initialViewState
                ChangeState = Feed.nil
                Bounds = new Rectangle (-1.0, 1.0, -1.0, 0.0)
                Input = Input.windowToView size input
                VelocityDamping = 0.1
                ZoomVelocityDamping = 0.1
            }

        // Create spectrogram gradient
        let gradient = 
            new Gradient [|
                { Value = 0.0; Color = Color.RGB(1.0, 1.0, 1.0) };
                { Value = 0.35; Color = Color.RGB(0.0, 1.0, 1.0) };
                { Value = 0.5; Color = Color.RGB(0.0, 1.0, 0.0) };
                { Value = 0.6; Color = Color.RGB(1.0, 1.0, 0.0) };
                { Value = 0.85; Color = Color.RGB(1.0, 0.0, 0.0) };
                { Value = 1.0; Color = Color.RGB(0.5, 0.0, 0.0) };
            |]

        // Spectrogram parameters
        let timeResolution = 512
        let freqResolution = 512
        let windowSize = freqResolution * 2
        let inputSize = windowSize * 8
        let inputDelta = (int floatData.Size - inputSize) / timeResolution
        let downsampleCount = log2 (uint32 inputSize) - log2 (uint32 freqResolution) - 1
        let colorBuffer = Array2D.zeroCreate timeResolution freqResolution

        let parameters = new FFTParameters (freqResolution * 2, 8)

        // Create buffers
        let input : float[] = Array.zeroCreate inputSize
        let window : float[] = Array.zeroCreate windowSize
        let output : Complex[] = Array.zeroCreate windowSize
        let inputHandle, inputPtr = pin input
        let windowHandle, windowPtr = pin window
        let outputHandle, outputPtr = pin output

        // Construct window
        Window.construct Window.hamming (NativePtr.ofNativeInt windowPtr) windowSize

        // Make spectrogram
        for x = 0 to timeResolution - 1 do

            // Read input
            let start = uint64 (inputDelta * x)
            floatData.Read (start, input, 0, inputSize)

            // Downsample input
            let mutable curSize = inputSize
            while curSize > freqResolution * 2 do
                DSignal.downsampleReal (NativePtr.ofNativeInt inputPtr) curSize
                curSize <- curSize / 2

            // Apply window
            DSignal.windowReal (NativePtr.ofNativeInt windowPtr) (NativePtr.ofNativeInt inputPtr) windowSize

            // Compute DFT
            DFT.computeReal (NativePtr.ofNativeInt inputPtr) (NativePtr.ofNativeInt outputPtr) parameters

            // Write to image
            for y = 0 to freqResolution - 1 do
                colorBuffer.[x, freqResolution - y - 1] <- gradient.GetColor (output.[y].Abs * 100.0)

        unpin inputHandle
        unpin windowHandle
        unpin outputHandle
        let image = Image.colorBuffer colorBuffer

        // Figure
        let getFigure playSample =
            let image = Figure.image image ImageInterpolation.Nearest (new Rectangle (-1.0, 1.0, -1.0, 0.0))
            let linex = 2.0 * float (playSample - uint64 (inputSize / 2)) / float (timeResolution * inputDelta) - 1.0
            let line = Figure.line (new Point (linex, -1.0)) (new Point (linex, 1.0)) 0.002 (Paint.ARGB (1.0, 1.0, 0.3, 0.0))
            Figure.composite image line

        // Start
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