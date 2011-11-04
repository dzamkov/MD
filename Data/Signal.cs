using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace MD.Data
{
    /// <summary>
    /// Contains functions and objects related to signals.
    /// </summary>
    public static class Signal
    {
        /// <summary>
        /// A signal for a sine wave.
        /// </summary>
        public static Signal<double> Sine = SineSignal.Instance;

        /// <summary>
        /// The time signal.
        /// </summary>
        public static Signal<double> Time = TimeSignal.Instance;
    }

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
        public SignalFeed<T> Play(double Start, Feed<double> Rate)
        {
            return new SignalFeed<T>(this, Rate, Start);
        }

        /// <summary>
        /// Begins playing this signal to a feed.
        /// </summary>
        public SignalFeed<T> Play(double Start)
        {
            return new SignalFeed<T>(this, new ControlFeed<double>(1.0), Start);
        }

        /// <summary>
        /// Begins playing this signal to a feed.
        /// </summary>
        public SignalFeed<T> Play()
        {
            return new SignalFeed<T>(this, new ControlFeed<double>(1.0), 0.0);
        }

        /// <summary>
        /// Constructs a looped version of this signal.
        /// </summary>
        public Signal<T> Loop(int Times)
        {
            return new LoopSignal<T>(this, Times);
        }

        /// <summary>
        /// Constructs a dilated version of this signal.
        /// </summary>
        public Signal<T> Dilate(double Factor)
        {
            return new DilateSignal<T>(this, Factor);
        }

        /// <summary>
        /// Constructs an offset version of this signal.
        /// </summary>
        public Signal<T> Offset(double Offset)
        {
            return new OffsetSignal<T>(this, Offset);
        }

        /// <summary>
        /// Constructs a mapped version of this signal using the given mapping function.
        /// </summary>
        public Signal<F> Map<F>(Expression<Func<T, F>> Map)
        {
            return new MapSignal<T, F>(this, Map.Compile());
        }

        /// <summary>
        /// Approximates the value of the signal at the given time in the interval [0, this.Length).
        /// </summary>
        public abstract T this[double Time] { get; }

        /// <summary>
        /// Creates a discrete sampling of this signal.
        /// </summary>
        /// <param name="PreferredRate">The preferred sample rate for the resulting signal. This may be ignored if it's quicker or more accurate to produce
        /// a signal with a different sample rate.</param>
        public virtual DiscreteSignal<T> Sample(double PreferredRate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The length of the signal.
        /// </summary>
        public readonly double Length;

        /// <summary>
        /// Gets wether this signal is bounded (has a finite length).
        /// </summary>
        public bool Bounded
        {
            get
            {
                return !double.IsPositiveInfinity(this.Length);
            }
        }
    }

    /// <summary>
    /// A signal created from a sample array.
    /// </summary>
    public sealed class DiscreteSignal<T> : Signal<T>
    {
        public DiscreteSignal(Array<T> Data, double Rate)
            : base(Data.Size / Rate)
        {
            this.Data = Data;
            this.Rate = Rate;
        }

        /// <summary>
        /// The (immutable) source sample data for this signal.
        /// </summary>
        public readonly Array<T> Data;

        /// <summary>
        /// The amount of samples in a time unit in this signal.
        /// </summary>
        public readonly double Rate;

        public override DiscreteSignal<T> Sample(double PreferredRate)
        {
            return this;
        }

        public override T this[double Time]
        {
            get
            {
                return this.Data[(int)(Time / this.Rate)];
            }
        }
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
            : base(Source.Length / Factor)
        {
            this.Source = Source;
            this.InverseFactor = 1.0 / Factor;
        }

        /// <summary>
        /// The source for this signal.
        /// </summary>
        public readonly Signal<T> Source;

        /// <summary>
        /// The time dilation factor.
        /// </summary>
        public double Factor
        {
            get
            {
                return 1.0 / this.InverseFactor;
            }
        }

        /// <summary>
        /// The inverse of the time dilation factor.
        /// </summary>
        public readonly double InverseFactor;

        public override DiscreteSignal<T> Sample(double PreferredRate)
        {
            DiscreteSignal<T> source = this.Source.Sample(PreferredRate);
            return new DiscreteSignal<T>(source.Data, source.Rate * this.InverseFactor);
        }

        public override T this[double Time]
        {
            get
            {
                return this.Source[Time * InverseFactor];
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
        public readonly new double Offset;

        public override T this[double Time]
        {
            get
            {
                return this.Source[Time + this.Offset];
            }
        }
    }

    /// <summary>
    /// A signal created by mapping values from a source signal.
    /// </summary>
    public sealed class MapSignal<TSource, T> : Signal<T>
    {
        public MapSignal(Signal<TSource> Source, Func<TSource, T> Map)
            : base(Source.Length)
        {
            this.Source = Source;
            this.Map = Map;
        }

        /// <summary>
        /// The source for this signal.
        /// </summary>
        public readonly Signal<TSource> Source;

        /// <summary>
        /// The mapping function for the signal.
        /// </summary>
        public readonly Func<TSource, T> Map;

        public override DiscreteSignal<T> Sample(double PreferredRate)
        {
            DiscreteSignal<TSource> source = this.Source.Sample(PreferredRate);
            return new DiscreteSignal<T>(new MapArray<TSource, T>(source.Data, this.Map), source.Rate);
        }

        public override T this[double Time]
        {
            get
            {
                return this.Map(this.Source[Time]);
            }
        }
    }


    /// <summary>
    /// An unbounded signal that contains a sine wave with a period of one time unit.
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

    /// <summary>
    /// An unbound signal whose value at any time is that time.
    /// </summary>
    public sealed class TimeSignal : Signal<double>
    {
        private TimeSignal()
            : base(double.PositiveInfinity)
        {

        }

        /// <summary>
        /// The only instance of this class.
        /// </summary>
        public static readonly TimeSignal Instance = new TimeSignal();

        public override double this[double Time]
        {
            get
            {
                return Time;
            }
        }
    }
}
