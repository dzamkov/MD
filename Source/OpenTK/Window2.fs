namespace MD.OpenTK

open MD
open MD.UI
open MD.DSP
open Util
open System
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open OpenTK.Input

/// Main program window
type Window () =
    inherit GameWindow (640, 480, GraphicsMode.Default, "MD")
    let audiooutput = AudioOutput.Create () |> Option.get :> MD.UI.AudioOutput
    let size = new ControlSignalFeed<Point> (new Point (float 640, float 480))
    let programTime = Feed.time

    let mutable display = Unchecked.defaultof<Display>

    /// Gets a feed that gives the size of the client area of this window in pixels.
    member this.Size = size

    /// Gets a feed that gives the projection from window coordinates to viewspace.
    member this.ViewspaceProjection = 
        let getTransform (size : Point) = new Transform (new Point (-1.0, 1.0), new Point (2.0 / size.X, 0.0), new Point (0.0, -2.0 / size.Y))
        size |> Feed.maps getTransform

    override this.OnLoad args =
        this.MakeCurrent ()
        this.VSync <- VSyncMode.Off

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
                    then Some (audiocontent.Data.Value.Lock (), ())
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
        let initialViewState = {
                Center = new Point (0.0, 0.0)
                Velocity = new Point (0.0, 0.0)
                Zoom = 0.0
                ZoomVelocity = -0.2
            }
        let view, _ = View.Create {
                InitialState = initialViewState
                Bounds = new Rectangle (-1.0, 1.0, -1.0, 0.0)
                VelocityDamping = 0.1
                ZoomVelocityDamping = 0.1
            }
        let worldspaceProjection = Feed.collate this.ViewspaceProjection view.Projection |> Feed.maps (fun (a, b) -> a * b)

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

        // Spectrogram
        let frame = SpectrogramFrame.ConstantQ (Window.hann, 22.0, 0.004)
        let spectrogram = new Spectrogram (floatData, frame)
        let coloring = Map.func (fun (freq, value) -> value * (4.0e2 * freq)) |> Map.map gradient
        let area = new Rectangle (-1.0, 1.0, -1.0, 0.0) 
        let spectrogram = spectrogram.CreateFigure (coloring, area)

        // Figure
        let getLineFigure playSample =
            let linex = 2.0 * float playSample / float floatData.Size - 1.0
            let line = Figure.line { A = new Point (linex, -1.0); B = (new Point (linex, 1.0)); Weight = 0.002; Paint = Paint.ARGB (1.0, 1.0, 0.3, 0.0) }
            line
        let line = Figure.dynamic (playPosition |> Feed.maps getLineFigure)
        let figure = spectrogram + line

        // Start
        let graphics = new FixedGraphics ()
        graphics.Initialize ()
        display <- new Display (graphics, Figure.transformDynamic (view.Projection |> Feed.maps (fun transform -> transform.Inverse)) figure)
        Input.link (this :> GameWindow) (view :> Interface |> Input.transform worldspaceProjection) |> ignore
        control.Fire AudioControl.Play

    override this.OnRenderFrame args =
        GL.Clear ClearBufferMask.ColorBufferBit
        display.Render (new ImageSize (this.Width, this.Height))
        this.SwapBuffers ()

    override this.OnUpdateFrame args =
        Update.invoke args.Time

    override this.OnResize args =
        size.Update (new Point (double this.Width, double this.Height))