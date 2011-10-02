using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spectrogram
{
    /// <summary>
    /// An array of samples.
    /// </summary>
    public interface ISampleSource<T>
    {
        /// <summary>
        /// Reads the samples in this source to the given buffer.
        /// </summary>
        void Read(int Start, int Size, T[] Buffer, int Offset);

        /// <summary>
        /// Creates a stream for this source starting at the given position.
        /// </summary>
        ISampleStream<T> Stream(int Start);
    }

    /// <summary>
    /// A stream of samples.
    /// </summary>
    public interface ISampleStream<T>
    {
        /// <summary>
        /// Reads the next set of samples in this stream to the given buffer.
        /// </summary>
        void Read(int Size, T[] Buffer, int Offset);
    }

    /// <summary>
    /// A sample source produced by sampling a signal.
    /// </summary>
    public class SignalSampleSource<T> : ISampleSource<T>
    {
        public SignalSampleSource(Signal<T> Source, double Rate)
        {
            this.Source = Source;
            this.Rate = Rate;
        }

        /// <summary>
        /// The source signal for this sampling.
        /// </summary>
        public readonly Signal<T> Source;

        /// <summary>
        /// The sample rate in samples per time unit.
        /// </summary>
        public readonly double Rate;

        public void Read(int Start, int Size, T[] Buffer, int Offset)
        {
            double c = ((double)Start + 0.5) / this.Rate;
            double d = 1.0 / this.Rate;
            while (Size-- > 0)
            {
                Buffer[Offset++] = this.Source[c];
                c += d;
            }
        }

        public ISampleStream<T> Stream(int Start)
        {
            return new DefaultSampleStream<T>(this, Start);
        }
    }

    /// <summary>
    /// A sample source created by infinitely repeating another sample source.
    /// </summary>
    public class RepeatSampleSource<T> : ISampleSource<T>
    {
        public RepeatSampleSource(ISampleSource<T> Source, int Size)
        {
            this.Source = Source;
            this.Size = Size;
        }

        /// <summary>
        /// The source for this sample source.
        /// </summary>
        public readonly ISampleSource<T> Source;

        /// <summary>
        /// The size of the source.
        /// </summary>
        public readonly int Size;

        public void Read(int Start, int Size, T[] Buffer, int Offset)
        {
            int avail;
            while ((avail = this.Size - Start) < Size)
            {
                this.Source.Read(Start, avail, Buffer, Offset);
                Offset += avail;
                Size -= avail;
            }
            this.Source.Read(Start, Size, Buffer, Offset);
        }

        public ISampleStream<T> Stream(int Start)
        {
            return new DefaultSampleStream<T>(this, Start);
        }
    }

    /// <summary>
    /// A sample stream created from a sample source.
    /// </summary>
    public class DefaultSampleStream<T> : ISampleStream<T>
    {
        public DefaultSampleStream(ISampleSource<T> Source, int Start)
        {
            this.Source = Source;
            this.Position = Start;
        }

        /// <summary>
        /// The source for this sample stream.
        /// </summary>
        public readonly ISampleSource<T> Source;

        /// <summary>
        /// The current position of the stream in the source.
        /// </summary>
        public int Position;

        public void Read(int Size, T[] Buffer, int Offset)
        {
            this.Source.Read(this.Position, Size, Buffer, Offset);
            this.Position += Size;
        }
    }
}
