﻿namespace MD.OpenTK

open MD
open MD.UI
open Util
open System
open Microsoft.FSharp.NativeInterop
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open OpenTK.Input

/// Main program window
type Window () =
    inherit GameWindow (640, 480, GraphicsMode.Default, "MD")
    let audiooutput = AudioOutput.Create () |> Option.get :> MD.UI.AudioOutput
    let graphics = Graphics.Create ()
    let size = new ControlSignalFeed<Point> (new Point (float 640, float 480))
    let programTime = Feed.time

    let mutable figure = Unchecked.defaultof<Figure signal>
    let mutable projection = Unchecked.defaultof<Transform signal>

    /// Gets a feed that gives the size of the client area of this window in pixels.
    member this.Size = size

    /// Gets a feed that gives the projection from window coordinates to viewspace.
    member this.ViewspaceProjection = 
        let getTransform (size : Point) = new Transform (new Point (-1.0, 1.0), new Point (2.0 / size.X, 0.0), new Point (0.0, -2.0 / size.Y))
        size |> Feed.maps getTransform

    override this.OnLoad args =
        this.MakeCurrent ()
        this.VSync <- VSyncMode.Off
        Graphics.Initialize ()

        // Sinc
        let sincArray = Array.zeroCreate<float> 10
        sincArray.[0] <- 1.0
        let sinc x = sin (Math.PI * x) / (Math.PI * x)
        let mutable total = 0.5
        for index = 1 to sincArray.Length - 1 do
            let value = sinc (float index * 0.5)
            sincArray.[index] <- value
            total <- total + value
        let mutable values = ""
        for index = 0 to sincArray.Length - 1 do
            let value = sincArray.[index] * total
            values <- values + "\n" + value.ToString ()


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

        // Spectrogram parameters
        let parameters = {
                Samples = floatData
                Window = Window.hamming
                WindowSize = 4096.0 * 4.0
                Scaling = (fun x y -> y * 100.0)
                Gradient = gradient
            }

        // Spectrogram
        let area = new Rectangle (-1.0, 1.0, -1.0, 0.0) 
        let spectrogramTile = new SpectrogramTile (SpectrogramCache.Initialize parameters, 0.0, 1.0, 0, 0, area)

        let image = ref None
        let gotImage (im : Image exclusive) = image := Some im.Object
        spectrogramTile.RequestImage ((512, 512), gotImage) |> ignore

        // Figure
        let getFigure playSample =
            let image = 
                match !image with
                | Some image -> Figure.placeImage area image ImageInterpolation.Linear
                | None -> Figure.tile spectrogramTile
            let linex = 2.0 * float playSample / float floatData.Size - 1.0
            let line = Figure.line (new Point (linex, -1.0)) (new Point (linex, 1.0)) 0.002 (Paint.ARGB (1.0, 1.0, 0.3, 0.0))
            image + line

        // Start
        Input.link (this :> GameWindow) (view :> Interface |> Input.transform worldspaceProjection) |> ignore
        projection <- view.Projection
        figure <- playPosition |> Feed.maps getFigure
        control.Fire AudioControl.Play

    override this.OnRenderFrame args =
        graphics.Render (this.Width, this.Height, figure.Current * projection.Current.Inverse)
        this.SwapBuffers ()

    override this.OnUpdateFrame args =
        Update.invoke args.Time

    override this.OnResize args =
        size.Update (new Point (double this.Width, double this.Height))