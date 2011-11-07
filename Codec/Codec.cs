using System;
using System.Collections.Generic;
using System.Linq;

using MD.Data;

namespace MD.Codec
{
    /// <summary>
    /// A codec for video or audio data of a certain format.
    /// </summary>
    public abstract class Codec
    {
        /// <summary>
        /// Indicates wether this codec can be used for encoding.
        /// </summary>
        public readonly bool CanEncode;

        /// <summary>
        /// Indicates wether this codec can be used for decoding.
        /// </summary>
        public readonly bool CanDecode;

        /// <summary>
        /// Tries encoding the given content into a stream. Returns false if not possible.
        /// </summary>
        public virtual bool Encode(AudioInfo Audio, VideoInfo Video, SubtitleInfo Subtitle, out Stream<byte> Stream)
        {
            Stream = null;
            return false;
        }

        /// <summary>
        /// Tries decoding content from the given stream. Returns false if not possible.
        /// </summary>
        public virtual bool Decode(Stream<byte> Stream, out AudioInfo Audio, out VideoInfo Video, out SubtitleInfo Subtitle)
        {
            Audio = null;
            Video = null;
            Subtitle = null;
            return false;
        }
    }

    /// <summary>
    /// Contains information about audio content.
    /// </summary>
    public class AudioInfo
    {
        public AudioInfo(AudioFormat Format, int Channels, double SampleRate, Stream<byte> Data)
        {
            this.Format = Format;
            this.Channels = Channels;
            this.SampleRate = SampleRate;
            this.Data = Data;
        }

        /// <summary>
        /// The format of the audio.
        /// </summary>
        public readonly AudioFormat Format;

        /// <summary>
        /// The channels in the audio.
        /// </summary>
        public readonly int Channels;

        /// <summary>
        /// The sample rate for the audio.
        /// </summary>
        public readonly double SampleRate;

        /// <summary>
        /// The audio data.
        /// </summary>
        public readonly Stream<byte> Data;
    }
    
    /// <summary>
    /// Contains information about video content.
    /// </summary>
    public class VideoInfo
    {

    }

    /// <summary>
    /// Contains information about subtitle content.
    /// </summary>
    public class SubtitleInfo
    {

    }
    
    /// <summary>
    /// Identifies an audio format for a single sample of a channel.
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