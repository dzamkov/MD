using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spectrogram
{
    /// <summary>
    /// Contains functions related to arrays.
    /// </summary>
    public static class Array
    {
        
    }

    /// <summary>
    /// A continous collection of data indexed by an integer. Unless stated otherwise, an array can be
    /// assumed to be mutable.
    /// </summary>
    public abstract class Array<T>
    {
        /// <summary>
        /// Gets the size of the array.
        /// </summary>
        public abstract int Size { get; }

        /// <summary>
        /// Reads a portion of this array into the given buffer.
        /// </summary>
        public abstract void Read(int Start, int Size, T[] Buffer, int Offset);

        /// <summary>
        /// Combines items in this array to form an array of compounds.
        /// </summary>
        public Array<F> Combine<F, TCompound>()
            where TCompound : ICompound<F, T>
        {
            SplitArray<T, F, TCompound> sa = this as SplitArray<T, F, TCompound>;
            if (sa != null)
                return sa.Source;

            return new CombineArray<F, T, TCompound>(this);
        }

        /// <summary>
        /// Splits compounds in this array to form an array of source items.
        /// </summary>
        public Array<F> Split<F, TCompound>()
            where TCompound : ICompound<T, F>
        {
            CombineArray<T, F, TCompound> ca = this as CombineArray<T, F, TCompound>;
            if (ca != null)
                return ca.Source;

            return new SplitArray<F, T, TCompound>(this);
        }
    }

    /// <summary>
    /// A mutable array that stores data with a extendable list of fixed-sized buffers.
    /// </summary>
    public sealed class MemoryArray<T> : Array<T>
    {
        public MemoryArray(int BufferSize)
        {
            this.BufferSize = BufferSize;
            this._Buffers = new List<T[]>();
        }

        /// <summary>
        /// The size of the buffers in this array.
        /// </summary>
        public readonly int BufferSize;

        /// <summary>
        /// Appends a buffer to this array. The buffer must have a size of the buffer size for this array. The buffer will
        /// be referenced directly; making any changes to the buffer will cause corresponding changes to be made in the array.
        /// </summary>
        public void Append(T[] Buffer)
        {
            this._Buffers.Add(Buffer);
        }

        /// <summary>
        /// Clears all buffers in this array.
        /// </summary>
        public void Clear()
        {
            this._Buffers.Clear();
        }

        public override int Size
        {
            get
            {
                return this.BufferSize * this._Buffers.Count;
            }
        }

        public override void Read(int Start, int Size, T[] Buffer, int Offset)
        {
            int sb = Start / this.BufferSize;
            int si = Start - (sb * this.BufferSize);
            while (Size > 0)
            {
                T[] buf = this._Buffers[sb];
                int tr = Math.Min(Size, this.BufferSize - si);
                for (int t = 0; t < tr; t++)
                {
                    Buffer[Offset++] = buf[si++];
                }

                Size -= tr;
                si = 0;
                sb++;
            }
        }

        private List<T[]> _Buffers;
    }

    /// <summary>
    /// An array that combines items from a source array to make an array of compounds.
    /// </summary>
    public sealed class CombineArray<T, TSource, TCompound> : Array<T>
        where TCompound : ICompound<T, TSource>
    {
        public CombineArray(Array<TSource> Source)
        {
            this.Source = Source;
        }
        
        /// <summary>
        /// The source data array for the compound array.
        /// </summary>
        public readonly Array<TSource> Source;

        public override int Size
        {
            get
            {
                return this.Source.Size / default(TCompound).Size;
            }
        }

        public override void Read(int Start, int Size, T[] Buffer, int Offset)
        {
            TCompound compound = default(TCompound);
            int compoundsize = compound.Size;
            TSource[] sourcebuf = new TSource[Size * compoundsize];
            this.Source.Read(Start * compoundsize, sourcebuf.Length, sourcebuf, 0);
            Start = 0;
            while (Size > 0)
            {
                Buffer[Offset] = compound.Combine(sourcebuf, Start);
                Start += compoundsize;
                Size--;
                Offset++;
            }
        }
    }

    /// <summary>
    /// An array that splits items from a source array of compounds to make an array of base items.
    /// </summary>
    public sealed class SplitArray<T, TSource, TCompound> : Array<T>
        where TCompound : ICompound<TSource, T>
    {
        public SplitArray(Array<TSource> Source)
        {
            this.Source = Source;
        }

        /// <summary>
        /// The source data array for the compound array.
        /// </summary>
        public readonly Array<TSource> Source;

        public override int Size
        {
            get
            {
                return this.Source.Size * default(TCompound).Size;
            }
        }

        public override void Read(int Start, int Size, T[] Buffer, int Offset)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Combines items of a certain type into a compound of another type.
    /// </summary>
    public interface ICompound<TCompound, TItem>
    {
        /// <summary>
        /// The amount of base items in a compound.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Combines items from the given array (starting at the given offset) into a compound.
        /// </summary>
        TCompound Combine(TItem[] Buffer, int Offset);

        /// <summary>
        /// Splits a compound and places the base items in the given buffer.
        /// </summary>
        void Split(TCompound Compound, TItem[] Buffer, int Offset);
    }
}
