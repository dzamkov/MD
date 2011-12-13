namespace MD.OpenTK

open MD
open MD.UI
open System
open System.Collections.Generic
open System.Threading
open OpenTK.Audio
open OpenTK.Audio.OpenAL

/// An interface to an audio output device managed by OpenAL.
type AudioOutput private (context : AudioContext) =
    let sources = new Dictionary<AudioOutputSource, Retract> ()
    let messages = new Queue<AudioOutputMessage> ()
    let wait = new ManualResetEvent (false)
    let mutable exit = false

    // Updates all sources for this audio output interface.
    let update () =
        context.MakeCurrent ()

        // Time to wait between update cycles
        let mutable waittime = 0
        
        // Process messages and update sources.
        while not exit do
            wait.WaitOne (waittime) |> ignore
            wait.Reset () |> ignore

            // Process messages
            Monitor.Enter messages
            while messages.Count > 0 do
                let message = messages.Dequeue ()
                match message with
                | New (source, retract) -> sources.Add (source, retract)
                | Control (source, AudioControl.Play) -> source.Play ()
                | Control (source, AudioControl.Pause) -> source.Pause ()
                | Control (source, AudioControl.Stop) ->
                    source.Stop ()
                    sources.[source].Invoke ()
                    sources.Remove source |> ignore
            Monitor.Exit messages

            // Update sources (keep track of buffers processed and amount of active sources).
            let mutable activecount = 0
            let mutable buffercount = 0
            for source in sources.Keys do
                if source.Playing then activecount <- activecount + 1
                buffercount <- buffercount + source.Update ()

            // Adjust wait time
            waittime <- 
            match (waittime, activecount, buffercount) with
            | (x, 0, _) -> -1
            | (-1, _, _) -> 0
            | (x, _, 0) -> min 500 (x + 5)
            | (x, _, _) -> max 0 (x - 1)

        // Clean up on exit
        for kvp in sources do
            kvp.Key.Stop ()
            kvp.Value.Invoke ()
        context.Dispose()
        wait.Close ()

    let updateThread = new Thread (update)
    do 
        updateThread.IsBackground <- true
        updateThread.Start ()

    /// Gets the OpenAL format for an audio stream with the given format and channel count.
    static let alformat channels format =
        match (channels, format) with
        | (1, AudioFormat.PCM8) -> Some (ALFormat.Mono8, 1)
        | (1, AudioFormat.PCM16) -> Some (ALFormat.Mono16, 2)
        | (1, AudioFormat.Float) -> Some (ALFormat.MonoFloat32Ext, 4)
        | (1, AudioFormat.Double) -> Some (ALFormat.MonoDoubleExt, 8)
        | (2, AudioFormat.PCM8) -> Some (ALFormat.Stereo8, 2)
        | (2, AudioFormat.PCM16) -> Some (ALFormat.Stereo16, 4)
        | (2, AudioFormat.Float) -> Some (ALFormat.StereoFloat32Ext, 8)
        | (2, AudioFormat.Double) -> Some (ALFormat.StereoDoubleExt, 16)
        | (4, AudioFormat.PCM8) -> Some (ALFormat.MultiQuad8Ext, 4)
        | (4, AudioFormat.PCM16) -> Some (ALFormat.MultiQuad16Ext, 8)
        | (4, AudioFormat.PCM32) -> Some (ALFormat.MultiQuad32Ext, 16)
        | (5, AudioFormat.PCM8) -> Some (ALFormat.Multi51Chn8Ext, 5)
        | (5, AudioFormat.PCM16) -> Some (ALFormat.Multi51Chn16Ext, 10)
        | (5, AudioFormat.PCM32) -> Some (ALFormat.Multi51Chn32Ext, 20)
        | (6, AudioFormat.PCM8) -> Some (ALFormat.Multi61Chn8Ext, 6)
        | (6, AudioFormat.PCM16) -> Some (ALFormat.Multi61Chn16Ext, 12)
        | (6, AudioFormat.PCM32) -> Some (ALFormat.Multi61Chn32Ext, 24)
        | (7, AudioFormat.PCM8) -> Some (ALFormat.Multi71Chn8Ext, 7)
        | (7, AudioFormat.PCM16) -> Some (ALFormat.Multi71Chn16Ext, 14)
        | (7, AudioFormat.PCM32) -> Some (ALFormat.Multi71Chn32Ext, 28)
        | _ -> None
    
    /// Tries creating a new audio output context for the default device.
    static member Create () =
        try
            Some (new AudioOutput (new AudioContext ()))
        with
        | x -> None

    /// Tries creating a new audio output context for the given device.
    static member Create (device : string) =
        try
            Some (new AudioOutput (new AudioContext (device)))
        with
        | x -> None
            
    /// Gets the names of the available devices on the current machine.
    static member AvailableDevices = AudioContext.AvailableDevices

    /// Makes the audio context for this output current on the current thread.
    member private this.MakeCurrent () =
        if AudioContext.CurrentContext <> context then
            context.MakeCurrent ()

    interface MD.UI.AudioOutput with
        member this.Begin p =
            match alformat p.Channels p.Format with
            | Some (format, bps) ->
                // Create source
                this.MakeCurrent ()
                let source = new AudioOutputSource (p, format, bps, 4096 * 4, 4)

                // Register control callback for message queue.
                let retract = p.Control.Register (fun x ->
                    Monitor.Enter messages
                    messages.Enqueue (Control (source, x))
                    Monitor.Exit messages
                    wait.Set() |> ignore
                )

                // Add source and signal update thread
                Monitor.Enter messages
                messages.Enqueue (New (source, retract))
                Monitor.Exit messages
                wait.Set () |> ignore

                Some { Position = source.Position } 
            | _ -> None

        member this.Finish () =
            wait.Set () |> ignore
            exit <- true

/// An interface to an OpenAL audio output source.
and private AudioOutputSource (parameters : AudioOutputSourceParameters, format : ALFormat, bytesPerSample : int, bufferSize : int,  bufferCount : int) =
    let stream = parameters.Stream
    let samplesPerBuffer = bufferSize / bytesPerSample
    let sampleRate = parameters.SampleRate
    let pitch = parameters.Pitch
    let volume = parameters.Volume

    let mutable startPosition = 0UL
    let mutable playing = false
    let position = new ControlSignalFeed<uint64> (0UL)
    let buffer = Array.create bufferSize 0uy
    let sid = AL.GenSource ()

    /// Writes the next set of data from the stream into the OpenAL buffer with the given id. Returns false if there is no
    /// more data to be written.
    let write bid =
        let readsize = stream.Object.ReadArray (buffer, 0, bufferSize)
        if readsize > 0 then
            AL.BufferData (bid, format, buffer, readsize, sampleRate)
            AL.SourceQueueBuffer (sid, bid)
            true
        else false

    /// Write initial buffers.
    do
        let mutable t = 0
        while t < bufferCount do
            write (AL.GenBuffer ()) |> ignore
            t <- t + 1

    /// Gets a signal feed that maintains the position of this source in its input stream.
    member this.Position = position :> SignalFeed<uint64>

    /// Gets wether this source is playing.
    member this.Playing = playing

    /// Plays this source.
    member this.Play () = 
        AL.SourcePlay sid
        playing <- true

    /// Pauses this source.
    member this.Pause () = 
        AL.SourcePause sid
        playing <- false

    /// Stops and disposes this source.
    member this.Stop () = 
        AL.SourcePause sid // Pausing before stoping makes it stop quicker, strange
        AL.SourceStop sid
        playing <- false
        
        let mutable bufferamount = 0
        AL.GetSource (sid, ALGetSourcei.BuffersQueued, &bufferamount)

        let buffers = AL.SourceUnqueueBuffers (sid, bufferamount)
        AL.DeleteBuffers buffers
        AL.DeleteSource sid
       
        stream.Finish ()

    /// Updates the state of this source and ensures play buffers are queued. Returns the amount of buffers processed since the last
    /// update.
    member this.Update () =
        let mutable buffersprocessed = 0
        AL.GetSource (sid, ALGetSourcei.BuffersProcessed, &buffersprocessed)

        // Refill processed buffers
        if buffersprocessed > 0 then
            startPosition <- startPosition + uint64 (buffersprocessed * samplesPerBuffer)

            let buffers = AL.SourceUnqueueBuffers (sid, buffersprocessed)
            for buffer in buffers do
                write buffer |> ignore

        if playing && AL.GetSourceState sid <> ALSourceState.Playing then
            AL.SourcePlay sid

        // Update play position
        let mutable sampleoffset = 0
        AL.GetSource (sid, ALGetSourcei.SampleOffset, &sampleoffset)
        position.Update (startPosition + uint64 sampleoffset)

        // Update gain
        let nvol = volume.Current
        AL.Source (sid, ALSourcef.Gain, float32 nvol)

        // Update pitch
        let nrate = pitch.Current
        AL.Source (sid, ALSourcef.Pitch, float32 nrate)

        // Return amount of buffers processed.
        buffersprocessed

/// A message for audio output.
and private AudioOutputMessage =
    | New of AudioOutputSource * Retract
    | Control of AudioOutputSource * AudioControl