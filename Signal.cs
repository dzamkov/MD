using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spectrogram
{
    /// <summary>
    /// Represents a static, continous signal of values of the given type.
    /// </summary>
    public abstract class Signal<T>
    {
        public Signal(double Length)
        {
            this.Length = Length;
        }

        /// <summary>
        /// Begins playing this signal to a feed.
        /// </summary>
        public SignalFeed<T> Play(double Start)
        {
            return new SignalFeed<T>(this, Start);
        }

        /// <summary>
        /// Begins playing this signal to a feed.
        /// </summary>
        public SignalFeed<T> Play()
        {
            return new SignalFeed<T>(this, 0.0);
        }

        /// <summary>
        /// Gets a looped version of this signal.
        /// </summary>
        public Signal<T> Loop(int Times)
        {
            return new LoopSignal<T>(this, Times);
        }

        /// <summary>
        /// Gets a dilated version of this signal.
        /// </summary>
        public Signal<T> Dilate(double Factor)
        {
            return new DilateSignal<T>(this, Factor);
        }

        /// <summary>
        /// Gets the value of the signal at the given time in the interval [0, this.Length).
        /// </summary>
        public abstract T this[double Time] { get; }

        /// <summary>
        /// The length of the signal.
        /// </summary>
        public readonly double Length;
    }

    /// <summary>
    /// A signal created from uniformly-spaced samples.
    /// </summary>
    public abstract class DiscreteSignal<T> : Signal<T>
    {
        public DiscreteSignal(int Size, double Rate)
            : base(Size / Rate)
        {
            this.Size = Size;
            this.Rate = Rate;
        }

        public override T this[double Time]
        {
            get
            {
                return this.GetSample((int)(Time / this.Rate));
            }
        }

        /// <summary>
        /// Reads the data in this signal into the given buffer.
        /// </summary>
        public virtual void Read(T[] Buffer, int Start, int Size, int Offset)
        {
            while (Size-- > 0)
                Buffer[Offset++] = this.GetSample(Start++);
        }
        
        /// <summary>
        /// Gets the value of a certain sample in this signal.
        /// </summary>
        public abstract T GetSample(int Sample);

        /// <summary>
        /// The amount of samples in this signal.
        /// </summary>
        public readonly int Size;

        /// <summary>
        /// The amount of samples in a time unit in this signal.
        /// </summary>
        public readonly double Rate;
    }

    /// <summary>
    /// A signal that repeats a source signal a certain amount of times.
    /// </summary>
    public sealed class LoopSignal<T> : Signal<T>
    {
        public LoopSignal(Signal<T> Source, int Times)
            : base(Source.Length * Times)
        {
            this.Source = Source;
            this.Times = Times;
        }

        /// <summary>
        /// The source for the loop.
        /// </summary>
        public readonly Signal<T> Source;

        /// <summary>
        /// The amount of times the source signal is repeated.
        /// </summary>
        public readonly int Times;

        public override T this[double Time]
        {
            get
            {
                return this.Source[Time % this.Source.Length];
            }
        }
    }

    /// <summary>
    /// A signal that dilates (shrinks or enlarges) a source signal over time by a certain factor.
    /// </summary>
    public sealed class DilateSignal<T> : Signal<T>
    {
        public DilateSignal(Signal<T> Source, double Factor)
            : base(Source.Length * Factor)
        {
            this.Source = Source;
            this.Factor = Factor;
        }

        /// <summary>
        /// The source for this signal.
        /// </summary>
        public readonly Signal<T> Source;

        /// <summary>
        /// The time dilation factor.
        /// </summary>
        public readonly double Factor;

        public override T this[double Time]
        {
            get
            {
                return this.Source[Time * Factor];
            }
        }
    }

    /// <summary>
    /// A signal that starts at a certain offset in a source signal.
    /// </summary>
    public sealed class OffsetSignal<T> : Signal<T>
    {
        public OffsetSignal(Signal<T> Source, double Offset)
            : base(Source.Length - Offset)
        {
            this.Source = Source;
            this.Offset = Offset;
        }

        /// <summary>
        /// The source for this signal.
        /// </summary>
        public readonly Signal<T> Source;

        /// <summary>
        /// The offset in the source signal this signal starts at.
        /// </summary>
        public readonly double Offset;

        public override T this[double Time]
        {
            get
            {
                return this.Source[Time + this.Offset];
            }
        }
    }

    /// <summary>
    /// A signal that contains a sine wave with a period of one time unit.
    /// </summary>
    public sealed class SineSignal : Signal<double>
    {
        private SineSignal()
            : base(double.PositiveInfinity)
        {

        }

        /// <summary>
        /// The only instance of this class.
        /// </summary>
        public static readonly SineSignal Instance = new SineSignal();

        public override double this[double Time]
        {
            get
            {
                return Math.Sin(Time * 2.0 * Math.PI);
            }
        }
    }
}
