﻿namespace MD.OpenTK

open MD
open System
open System.Collections.Generic
open OpenTK.Audio
open OpenTK.Audio.OpenAL

/// An interface to an audio output device managed by OpenAL.
type AudioOutput private (context : AudioContext) =
    let sources = new HashSet<AudioOutputSource> ()

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
    
    /// Updates all sources for this audio output.
    member this.Update () =
        this.MakeCurrent ()
        for source in sources do
            source.Update ()

    interface MD.AudioOutput with
        member this.Begin p =
            match alformat p.Channels p.Format with
            | Some (format, bps) ->
                let source = new AudioOutputSource (p, format, bps, 4096 * 4, 4)
                sources.Add source |> ignore
                p.Control.Register (Action<AudioControl> (fun x ->
                    match x with
                    | AudioControl.Play -> source.Play ()
                    | AudioControl.Pause -> source.Pause ()
                    | AudioControl.Stop -> source.Stop (); sources.Remove source |> ignore
                    | _ -> ()
                )) |> ignore
                Some source.Position
            | _ -> None

        member this.Finish () =
            for source in sources do
                source.Stop ()
            context.Dispose ()

/// An interface to an OpenAL audio output source.
and private AudioOutputSource (parameters : AudioOutputParameters, format : ALFormat, bytesPerSample : int, bufferSize : int,  bufferCount : int) =
    let stream = parameters.Stream
    let sampleRate = parameters.SampleRate
    let pitch = parameters.Pitch
    let volume = parameters.Volume

    let mutable startPosition = 0
    let mutable playing = false
    let position = new ControlSignalFeed<int> (0)
    let buffer = Array.create bufferSize 0uy
    let sid = AL.GenSource ()

    /// Writes the next set of data from the stream into the OpenAL buffer with the given id. Returns false if there is no
    /// more data to be written.
    let write bid =
        let readsize = stream.Read (buffer, bufferSize, 0)
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
    member this.Position = position :> SignalFeed<int>

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
        stream.Finish ()
        AL.SourceStop sid

        let mutable bufferamount = 0
        AL.GetSource (sid, ALGetSourcei.BuffersQueued, &bufferamount)

        let buffers = AL.SourceUnqueueBuffers (sid, bufferamount)
        AL.DeleteBuffers buffers
        AL.DeleteSource sid
        playing <- false

    /// Updates the state of this source and ensures play buffers are queued. Returns false if the stream for the source is finished
    /// and the source can be stopped.
    member this.Update () =
        let mutable buffersprocessed = 0
        AL.GetSource (sid, ALGetSourcei.BuffersProcessed, &buffersprocessed)

        // Refill processed buffers
        if buffersprocessed > 0 then
            startPosition <- startPosition + buffersprocessed * bufferSize / bytesPerSample

            let buffers = AL.SourceUnqueueBuffers (sid, buffersprocessed)
            for buffer in buffers do
                write buffer |> ignore

        if playing && AL.GetSourceState sid <> ALSourceState.Playing then
            AL.SourcePlay sid

        // pdate play position
        let mutable sampleoffset = 0
        AL.GetSource (sid, ALGetSourcei.SampleOffset, &sampleoffset)
        position.Current <- startPosition + sampleoffset

        // Update gain
        let nvol = volume.Current
        AL.Source (sid, ALSourcef.Gain, float32 nvol)

        // Update pitch
        let nrate = pitch.Current
        AL.Source (sid, ALSourcef.Pitch, float32 nrate)