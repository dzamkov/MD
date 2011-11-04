using System;
using System.Collections.Generic;
using System.Linq;

using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

using MD.Data;

namespace MD
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
        /// Outputs the given feed to the primary audio device. Returns a feed that gives the current play position (in samples) in the stream.
        /// </summary>
        public static Feed<long> Output(Stream<byte> Stream, ALFormat Format, int SampleRate, Feed<double> Pitch)
        {
            _StreamSource s = new _StreamSource(Stream, Format, SampleRate, Pitch, 4096 * 8);
            s.Initialize(4);
            s.Play();
            return s.Position;
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
        private sealed class _StreamSource : _Source
        {
            public _StreamSource(Stream<byte> Stream, ALFormat Format, int SampleRate, Feed<double> Pitch, int BufferSize)
                : base(Format, BufferSize)
            {
                this.Stream = Stream;
                this.SampleRate = SampleRate;
                this.Pitch = Pitch;
                this.Position = new ControlFeed<long>(0);
                this.StartPosition = 0;
            }

            /// <summary>
            /// The sample data stream for the source.
            /// </summary>
            public readonly Stream<byte> Stream;

            /// <summary>
            /// The pitch for the source.
            /// </summary>
            public readonly Feed<double> Pitch;

            /// <summary>
            /// The feed for the play position of this source.
            /// </summary>
            public readonly ControlFeed<long> Position;

            /// <summary>
            /// The position of the first sample currently in the buffer.
            /// </summary>
            public long StartPosition;

            public override void Write(byte[] Buffer)
            {
                this.Stream.Read(Buffer.Length, Buffer, 0);
            }

            public override void Update(double Time)
            {
                int bufferprocessed;
                AL.GetSource(this.ID, ALGetSourcei.BuffersProcessed, out bufferprocessed);

                if (bufferprocessed > 0)
                {
                    this.StartPosition += this.BufferSize * bufferprocessed;

                    int[] buffers = new int[bufferprocessed];
                    AL.SourceUnqueueBuffers(this.ID, bufferprocessed, buffers);
                    for (int t = 0; t < buffers.Length; t++)
                    {
                        this.Write(buffers[t]);
                    }
                    AL.SourcePlay(this.ID);
                }

                // Update play position
                int sampleoffset;
                AL.GetSource(this.ID, ALGetSourcei.SampleOffset, out sampleoffset);
                this.Position.Set(this.StartPosition + sampleoffset);

                // Update playback rate
                float nrate = (float)this.Pitch.Current;
                AL.Source(this.ID, ALSourcef.Pitch, nrate);
            }
        }
    }
}
