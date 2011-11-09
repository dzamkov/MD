using System;
using System.Collections.Generic;
using System.Linq;

using MD.Data;

namespace MD.Codec
{
    /// <summary>
    /// Generic audio content.
    /// </summary>
    public sealed class AudioContent : Content
    {
        public AudioContent(double SampleRate, int Channels, AudioFormat Format)
        {
            this.SampleRate = SampleRate;
            this.Channels = Channels;
            this.Format = Format;
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
        /// The audio data for the last frame.
        /// </summary>
        public Array<byte> Data;

        /// <summary>
        /// Gets the amount of bytes in a sample of the given format.
        /// </summary>
        public static int BytesPerSample(AudioFormat Format)
        {
            return _BPSTable[(int)Format];
        }

        /// <summary>
        /// Gets an audio stream for this audio content. This makes the context unusable for other purposes.
        /// </summary>
        public Disposable<AudioStream> GetStream(Disposable<Context> Context)
        {
            int ci = 0;
            for (int t = 0; t < (~Context).Content.Length; t++)
            {
                Content cont = (~Context).Content[t];
                if (cont == this)
                {
                    ci = t;
                }
                else
                {
                    cont.Ignore = true;
                }
            }
            return new AudioStream(Context, this, ci);
        }

        private static readonly int[] _BPSTable = new int[] { 1, 2, 4, 4, 8 };
    }

    /// <summary>
    /// A stream for audio data from a context.
    /// </summary>
    public class AudioStream : Stream<byte>, IDisposable
    {
        public AudioStream(Disposable<Context> Context, AudioContent Content, int ContentIndex)
        {
            this.Context = Context;
            this.Content = Content;
            this.ContentIndex = ContentIndex;
        }

        /// <summary>
        /// The context this stream is for.
        /// </summary>
        public readonly Disposable<Context> Context;

        /// <summary>
        /// The audio content this stream is for.
        /// </summary>
        public readonly AudioContent Content;

        /// <summary>
        /// The content index for the content this stream is for.
        /// </summary>
        public readonly int ContentIndex;

        /// <summary>
        /// The stream for the remainder of the current audio frame.
        /// </summary>
        public Stream<byte> Frame;

        public override int Read(byte[] Buffer, int Size, int Offset)
        {
            if (this.Frame == null)
                if (!this._AdvanceFrame())
                    return 0;
            int ar = 0;
            ar = this.Frame.Read(Buffer, Size, Offset);
            Size -= ar;
            Offset += ar;
            while (Size > 0 && this._AdvanceFrame())
            {
                int r = this.Frame.Read(Buffer, Size, Offset);
                Offset += r;
                Size -= r;
                ar += r;
            }
            return ar;
        }

        public override unsafe int Read(byte* Destination, int Size)
        {
            if (this.Frame == null)
                if (!this._AdvanceFrame())
                    return 0;
            int ar = 0;
            ar = this.Frame.Read(Destination, Size);
            Size -= ar;
            Destination += ar;
            while (Size > 0 && this._AdvanceFrame())
            {
                int r = this.Frame.Read(Destination, Size);
                Destination += r;
                Size -= r;
                ar += r;
            }
            return ar;
        }

        /// <summary>
        /// Tries advancing to the next audio frame.
        /// </summary>
        private bool _AdvanceFrame()
        {
            int ci = this.ContentIndex;
            while ((~this.Context).NextFrame(ref ci))
            {
                if (ci == this.ContentIndex)
                {
                    this.Frame = this.Content.Data.Read(0);
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            this.Context.Dispose();
        }
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