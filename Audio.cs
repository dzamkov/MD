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
        public static void Output<T, TCompound>(Feed<T> Feed, ALFormat Format)
            where TCompound : ICompound<T, byte>
        {
            _Source s = new _SignalFeedSource<T, TCompound>((SignalFeed<T>)Feed, Format, 65536 * 4);
            s.Initialize(2);
            s.Play();
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
            public _Source(ALFormat Format, int BufferSize)
            {
                this.ID = AL.GenSource();
                this.TempBuffer = new byte[BufferSize];
                this.Format = Format;
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
            /// Gets the buffer size (in bytes) for this source.
            /// </summary>
            public int BufferSize
            {
                get
                {
                    return this.TempBuffer.Length;
                }
            }

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
                this.Write(this.TempBuffer);
                AL.BufferData<byte>(Buffer, this.Format, this.TempBuffer, this.TempBuffer.Length, this.SampleRate);
                AL.SourceQueueBuffer(this.ID, Buffer);
            }

            /// <summary>
            /// Writes the next section of BufferSize samples to the given buffer; advances the read position.
            /// </summary>
            public abstract void Write(byte[] Buffer);

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
        private sealed class _SignalFeedSource<T, TCompound> : _Source
            where TCompound : ICompound<T, byte>
        {
            public _SignalFeedSource(SignalFeed<T> Feed, ALFormat Format, int BufferSize)
                : base(Format, BufferSize)
            {
                _AutoTimedSignalFeed.Remove(Feed);
                this.Feed = Feed;
                Signal<T> source = Feed.Source;

                // Deconstruct source signal to find a data array, sample rate, position and base rate
                this.BaseRate = 1.0f;
                this.Position = 0;
                DiscreteSignal<T> ds = source as DiscreteSignal<T>;
                if (ds != null)
                {
                    this.SampleRate = ds.Rate;
                    base.SampleRate = (int)ds.Rate;
                    this.Data = ds.Data.Split<byte, TCompound>();
                }
            }

            /// <summary>
            /// The feed for this audio source.
            /// </summary>
            public readonly SignalFeed<T> Feed;

            /// <summary>
            /// The actual sample rate for the stream (used for timing), takes into account base playback rate.
            /// </summary>
            public new double SampleRate;

            /// <summary>
            /// The base playback rate of the signal.
            /// </summary>
            public float BaseRate;

            /// <summary>
            /// The sample data array for the source.
            /// </summary>
            public Array<byte> Data;

            /// <summary>
            /// The position of the next byte in the data array to be written into a buffer.
            /// </summary>
            public int Position;

            /// <summary>
            /// The time in the signal the first buffer in the source starts at.
            /// </summary>
            public double StartTime;

            /// <summary>
            /// Gets the amount of bytes in a sample in this source.
            /// </summary>
            public int SampleSize
            {
                get
                {
                    return default(TCompound).Size;
                }
            }

            public override void Write(byte[] Buffer)
            {
                this.Data.Read(this.Position, Buffer.Length, Buffer, 0);
                this.Position += Buffer.Length;
            }

            public override void Update(double Time)
            {
                int bufferprocessed;
                AL.GetSource(this.ID, ALGetSourcei.BuffersProcessed, out bufferprocessed);

                if (bufferprocessed > 0)
                {
                    this.StartTime += (double)(this.BufferSize / this.SampleSize) / this.SampleRate;

                    int[] buffers = new int[bufferprocessed];
                    AL.SourceUnqueueBuffers(this.ID, bufferprocessed, buffers);
                    for (int t = 0; t < buffers.Length; t++)
                    {
                        this.Write(buffers[t]);
                    }

                    AL.SourcePlay(this.ID);
                }

                // Update feed time
                int sampleoffset;
                AL.GetSource(this.ID, ALGetSourcei.SampleOffset, out sampleoffset);
                this.Feed._Time = this.StartTime + (double)sampleoffset / this.SampleRate;

                // Update playback rate
                float nrate = (float)this.Feed.Rate.Current * this.BaseRate;
                AL.Source(this.ID, ALSourcef.Pitch, nrate);
            }
        }
    }
}
