using System;
using System.Collections.Generic;
using System.Linq;

using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace Spectrogram
{
    /// <summary>
    /// Contains functions related to audio output and input.
    /// </summary>
    public static class Audio
    {
        /// <summary>
        /// The audio context for the program.
        /// </summary>
        public static AudioContext Context;

        /// <summary>
        /// Initializes the audio context.
        /// </summary>
        public static void Initialize()
        {
            Context = new AudioContext();
        }

        /// <summary>
        /// Outputs the given feed to the primary audio device.
        /// </summary>
        public static void Output<T, TMode>(Feed<T> Feed, TMode Mode)
            where TMode : IAudioMode<T>
        {
            _Source s = new _SignalFeedSource<T, TMode>((SignalFeed<T>)Feed, Mode, 2048);
            s.Initialize(4);
            s.Play();
        }

        public static void Output(Feed<double> Feed)
        {
            Output(Feed, new Mono16Mode());
        }

        /// <summary>
        /// Updates the state of the program audio manager by the given amount of time.
        /// </summary>
        public static void Update(double Time)
        {
            foreach (_Source s in _Source.Active)
            {
                s.Update(Time);
            }
        }

        /// <summary>
        /// Represents an audio source.
        /// </summary>
        private abstract class _Source
        {
            public _Source(ALFormat Format, int SampleSize, int BufferSize)
            {
                this.ID = AL.GenSource();
                this.BufferSize = BufferSize;
                this.TempBuffer = new byte[this.BufferSize * SampleSize];
                this.Format = Format;
                this.SampleRate = SampleRate;
            }

            /// <summary>
            /// A list of all active sources.
            /// </summary>
            public static readonly List<_Source> Active = new List<_Source>();

            /// <summary>
            /// The ID of the source.
            /// </summary>
            public readonly int ID;

            /// <summary>
            /// The size, in samples, of the buffers in this source.
            /// </summary>
            public readonly int BufferSize;

            /// <summary>
            /// The audio format used by this source.
            /// </summary>
            public readonly ALFormat Format;

            /// <summary>
            /// The temporary buffer to use before sending data.
            /// </summary>
            public readonly byte[] TempBuffer;

            /// <summary>
            /// The sample rate of this source.
            /// </summary>
            public int SampleRate;

            /// <summary>
            /// Generates and writes the given amount of buffers for this source.
            /// </summary>
            public void Initialize(int Amount)
            {
                while (Amount-- > 0)
                {
                    this.Write(AL.GenBuffer());
                }
            }

            /// <summary>
            /// Writes the next samples of the source to the given buffer.
            /// </summary>
            public void Write(int Buffer)
            {
                unsafe
                {
                    fixed (byte* ptr = this.TempBuffer)
                    {
                        this.Write(ptr);
                    }
                }
                AL.BufferData<byte>(Buffer, this.Format, this.TempBuffer, this.TempBuffer.Length, this.SampleRate);
                AL.SourceQueueBuffer(this.ID, Buffer);
            }

            /// <summary>
            /// Writes the next section of BufferSize samples to the given buffer; advances the read position.
            /// </summary>
            public abstract unsafe void Write(byte* Buffer);

            /// <summary>
            /// Updates this source by the given amount of time in seconds.
            /// </summary>
            public virtual void Update(double Time)
            {
                int bufferprocessed;
                AL.GetSource(this.ID, ALGetSourcei.BuffersProcessed, out bufferprocessed);

                if (bufferprocessed > 0)
                {
                    int[] buffers = new int[bufferprocessed];
                    AL.SourceUnqueueBuffers(this.ID, bufferprocessed, buffers);
                    for (int t = 0; t < buffers.Length; t++)
                    {
                        this.Write(buffers[t]);
                    }
                }
            }

            /// <summary>
            /// Begins playing the source.
            /// </summary>
            public void Play()
            {
                AL.SourcePlay(this.ID);
                Active.Add(this);
            }

            /// <summary>
            /// Pauses the source.
            /// </summary>
            public void Pause()
            {
                AL.SourcePause(this.ID);
                Active.Remove(this);
            }

            /// <summary>
            /// Stops (and releases) the source.
            /// </summary>
            public void Stop()
            {
                AL.SourceStop(this.ID);
                Active.Remove(this);

                int bufferamount;
                AL.GetSource(this.ID, ALGetSourcei.BuffersQueued, out bufferamount);

                int[] buffers = new int[bufferamount];
                AL.SourceUnqueueBuffers(this.ID, bufferamount, buffers);
                AL.DeleteBuffers(buffers);

                AL.DeleteSource(this.ID);
            }
        }

        /// <summary>
        /// A source for a signal feed.
        /// </summary>
        private sealed class _SignalFeedSource<T, TMode> : _Source
            where TMode : IAudioMode<T>
        {
            public _SignalFeedSource(SignalFeed<T> Feed, TMode Mode, int BufferSize)
                : base(Mode.Format, Mode.BytesPerSample, BufferSize)
            {
                this.Feed = Feed;
                this.Mode = Mode;

                Signal<T> source = Feed.Source;
                DiscreteSignal<T> dissource = source as DiscreteSignal<T>;
                if (dissource != null)
                {
                    this.SampleRate = dissource.Rate;
                    base.SampleRate = (int)(this.SampleRate);
                    this.Stream = dissource.Source.Stream(0);
                }
                else
                {
                    base.SampleRate = 44100;
                    if (source.Bounded)
                    {
                        this.SampleRate = source.Length / ((int)(base.SampleRate * source.Length));
                    }
                    else
                    {
                        this.SampleRate = base.SampleRate;
                    }
                    this.Stream = new SignalSampleSource<T>(source, this.SampleRate).Stream(0);
                }
            }

            /// <summary>
            /// The feed for this audio source.
            /// </summary>
            public readonly SignalFeed<T> Feed;

            /// <summary>
            /// The audio mode for this source.
            /// </summary>
            public readonly TMode Mode;

            /// <summary>
            /// The actual sample rate for the stream.
            /// </summary>
            public new double SampleRate;

            /// <summary>
            /// The sample stream for this source. This stream should be unbounded (looping or infinite).
            /// </summary>
            public ISampleStream<T> Stream;

            /// <summary>
            /// The time in the signal the first buffer in the source starts at.
            /// </summary>
            public double StartTime;

            public override unsafe void Write(byte* Buffer)
            {
                T[] samps = new T[this.BufferSize];
                this.Stream.Read(this.BufferSize, samps, 0);

                int d = this.Mode.BytesPerSample;
                for (int t = 0; t < samps.Length; t++)
                {
                    this.Mode.Write(samps[t], Buffer);
                    Buffer += d;
                }
            }

            public override void Update(double Time)
            {
                int bufferprocessed;
                AL.GetSource(this.ID, ALGetSourcei.BuffersProcessed, out bufferprocessed);

                if (bufferprocessed > 0)
                {
                    this.StartTime += (double)this.BufferSize / this.SampleRate;

                    int[] buffers = new int[bufferprocessed];
                    AL.SourceUnqueueBuffers(this.ID, bufferprocessed, buffers);
                    for (int t = 0; t < buffers.Length; t++)
                    {
                        this.Write(buffers[t]);
                    }
                }

                int sampleoffset;
                AL.GetSource(this.ID, ALGetSourcei.SampleOffset, out sampleoffset);
                this.Feed._Time = this.StartTime + (double)sampleoffset / this.SampleRate;
            }
        }
    }

    /// <summary>
    /// Contains information that allows a signal to be interpreted as audio.
    /// </summary>
    public interface IAudioMode<T>
    {
        /// <summary>
        /// Gets the format of this audio mode.
        /// </summary>
        ALFormat Format { get; }

        /// <summary>
        /// Gets the size, in bytes, of a sample using this audio mode.
        /// </summary>
        int BytesPerSample { get; }

        /// <summary>
        /// Writes a sample to the given buffer.
        /// </summary>
        unsafe void Write(T Sample, byte* Buffer);
    }

    /// <summary>
    /// 16bit monochannel audio mode.
    /// </summary>
    public struct Mono16Mode : IAudioMode<double>, IAudioMode<short>
    {
        public ALFormat Format
        {
            get 
            {
                return ALFormat.Mono16;
            }
        }

        public int BytesPerSample
        {
            get
            {
                return 2;
            }
        }

        public unsafe void Write(double Sample, byte* Buffer)
        {
            short samp = (short)(Sample * 32767.0);
            Buffer[0] = (byte)samp;
            Buffer[1] = (byte)(samp >> 8);
        }

        public unsafe void Write(short Sample, byte* Buffer)
        {
            Buffer[0] = (byte)Sample;
            Buffer[1] = (byte)(Sample >> 8);
        }
    }
}
