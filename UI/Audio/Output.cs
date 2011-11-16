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
    /// A method of providing real-time audio output to the user.
    /// </summary>
    public interface AudioOutput
    {
        /// <summary>
        /// Tries creating an audio output source with the given parameters.
        /// </summary>
        /// <param name="Stream">The data stream used to retrieve audio data. The stream must support multithreading and must not end
        /// before the audio source stops playing.</param>
        /// <param name="Control">The event feed to receive control events from.</param>
        /// <param name="Pitch">A signal feed that provides a play-rate multiplier for the audio output source, or null if that 
        /// functionality is not needed.</param>
        /// <param name="Position">A signal feed that provides the current position (in samples) of the audio output source with 0 being the start
        /// of the data stream.</param>
        bool Begin(
            Stream<byte> Stream, 
            int SampleRate, int Channels, AudioFormat Format,
            EventFeed<AudioOutputControl> Control, 
            SignalFeed<double> Pitch, 
            out SignalFeed<long> Position);
    }

    /// <summary>
    /// Identifies a control event for an audio output source.
    /// </summary>
    public enum AudioOutputControl
    {
        /// <summary>
        /// Resumes playing the audio source after it is paused.
        /// </summary>
        Play,

        /// <summary>
        /// Temporarily stops the audio source.
        /// </summary>
        Pause,

        /// <summary>
        /// Permanently stops the audio source.
        /// </summary>
        Stop,
    }
}
