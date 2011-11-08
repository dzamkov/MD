using System;
using System.Collections.Generic;
using System.Linq;

using MD.Data;

namespace MD.Codec
{
    /// <summary>
    /// Generic audio content.
    /// </summary>
    public abstract class AudioContent : Content
    {
        public AudioContent(double SampleRate, int Channels, AudioFormat Format, Stream<byte> Raw)
        {
            this.SampleRate = SampleRate;
            this.Channels = Channels;
            this.Format = Format;
            this.Raw = Raw;
        }

        /// <summary>
        /// The sample rate of this audio content.
        /// </summary>
        public readonly double SampleRate;

        /// <summary>
        /// The amount of channels in this audio content.
        /// </summary>
        public readonly int Channels;

        /// <summary>
        /// The format for the audio data.
        /// </summary>
        public readonly AudioFormat Format;

        /// <summary>
        /// A stream containing the raw audio data for the content.
        /// </summary>
        public Stream<byte> Raw;
    }

    /// <summary>
    /// Identifies an audio format for a sample of a single channel.
    /// </summary>
    public enum AudioFormat
    {
        PCM8,
        PCM16,
        PCM32,
        Float,
        Double
    }
}