using System;
using System.Collections.Generic;
using System.Linq;

using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

using MD.Data;
using MD.Codec;

namespace MD.UI.Audio
{
    /// <summary>
    /// An audio output method.
    /// </summary>
    public abstract class AudioOutput
    {
        /// <summary>
        /// Creates an audio source to play the given stream. Returns null if this is not possible (the stream is unsupported).
        /// </summary>
        public abstract AudioOutputSource Begin(Stream<byte> Stream, int SampleRate, int Channels, AudioFormat Format);

        /// <summary>
        /// Updates the state of the audio output by the given amount of time in seconds.
        /// </summary>
        public virtual void Update(double Time)
        {

        }
    }

    /// <summary>
    /// An interface to an audio output source.
    /// </summary>
    public abstract class AudioOutputSource
    {
        /// <summary>
        /// Gets or sets current position of the source (in samples) in its source stream.
        /// </summary>
        public abstract int Position { get; set; }

        /// <summary>
        /// Begins playing the source.
        /// </summary>
        public abstract void Play();

        /// <summary>
        /// Temporarily stops the audio source.
        /// </summary>
        public abstract void Pause();

        /// <summary>
        /// Permanently stops the audio source. Note that this will release the audio source and will prevent it
        /// from being used again.
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Links a feed to control the pitch (sample rate multiplier) of the output, or returns false if not possible.
        /// </summary>
        public virtual bool LinkPitch(SignalFeed<double> Feed)
        {
            return false;
        }
    }
}
