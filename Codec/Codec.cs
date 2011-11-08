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
        public Codec(string Name, string Hint, bool CanEncode, bool CanDecode)
        {
            this.Name = Name;
            this.Hint = Hint;
            this.CanEncode = CanEncode;
            this.CanDecode = CanDecode;
        }

        /// <summary>
        /// The user-friendly name of this codec. This should reflect the origin of the codec and the type of streams it
        /// can use.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The file extension this codec is intended for.
        /// </summary>
        public readonly string Hint;

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

        /// <summary>
        /// Registers a codec.
        /// </summary>
        public static RetractHandler Register(Codec Codec)
        {
            List<Codec> hc;
            if (!_Codecs.TryGetValue(Codec.Hint, out hc))
            {
                hc = new List<Codec>();
                _Codecs[Codec.Hint] = hc;
            }
            hc.Add(Codec);
            return delegate
            {
                hc.Remove(Codec);
            };
        }

        /// <summary>
        /// Gets all codecs that are currently registered.
        /// </summary>
        public static IEnumerable<Codec> Codecs
        {
            get
            {
                return
                    from hc in _Codecs
                    from c in hc.Value
                    select c;
            }
        }

        /// <summary>
        /// Gets all registered codecs with the given hint string.
        /// </summary>
        public static IEnumerable<Codec> GetCodecsByHint(string Hint)
        {
            List<Codec> hc;
            if (_Codecs.TryGetValue(Hint, out hc))
            {
                return hc;
            }
            else
            {
                return new Codec[0];
            }
        }

        /// <summary>
        /// Gets all registered codecs with the given name.
        /// </summary>
        public static IEnumerable<Codec> GetCodecsByName(string Name)
        {
            return
                from c in Codecs
                where c.Name == Name
                select c;
        }

        private static Dictionary<string, List<Codec>> _Codecs = new Dictionary<string, List<Codec>>();
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