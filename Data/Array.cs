using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace MD.Data
{
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
        /// Gets the current value of the array at the given index.
        /// </summary>
        public virtual T this[int Index]
        {
            get
            {
                Stream<T> str = this.Read(Index, 1);
                T val = default(T);
                str.Read(ref val);
                return val;
            }
        }

        /// <summary>
        /// Creates a stream to read this array starting at the given index. Note that the returned stream may be larger than the requested
        /// Size (or even the size of the array).
        /// </summary>
        /// <param name="Size">The maximum amount of data that will be read from the stream.</param>
        public abstract Stream<T> Read(int Index, int Size);

        /// <summary>
        /// Creates a stream to read this array starting at the given index.
        /// </summary>
        public Disposable<Stream<T>> Read(int Index)
        {
            return this.Read(Index, this.Size - Index);
        }

        /// <summary>
        /// Constructs a mapped version of this array using the given mapping function.
        /// </summary>
        public Array<F> Map<F>(Expression<Func<T, F>> Map)
        {
            return new MapArray<T, F>(this, Map.Compile());
        }

        /// <summary>
        /// Combines items in this array to form an array of compounds.
        /// </summary>
        public Array<F> Combine<F, TCompound>()
            where TCompound : ICompound<F, T>
        {
            SplitArray<F, T, TCompound> sa = this as SplitArray<F, T, TCompound>;
            if (sa != null)
                return sa.Source;

            return new CombineArray<T, F, TCompound>(this);
        }

        /// <summary>
        /// Splits compounds in this array to form an array of source items.
        /// </summary>
        public Array<F> Split<F, TCompound>()
            where TCompound : ICompound<T, F>
        {
            CombineArray<F, T, TCompound> ca = this as CombineArray<F, T, TCompound>;
            if (ca != null)
                return ca.Source;

            return new SplitArray<T, F, TCompound>(this);
        }
    }

    /// <summary>
    /// An array created by mapping values from a source array.
    /// </summary>
    public sealed class MapArray<TSource, T> : Array<T>
    {
        public MapArray(Array<TSource> Source, Func<TSource, T> Map)
        {
            this.Source = Source;
            this.Map = Map;
        }

        /// <summary>
        /// The source for this array.
        /// </summary>
        public readonly Array<TSource> Source;

        /// <summary>
        /// The mapping function for the array.
        /// </summary>
        public readonly Func<TSource, T> Map;

        public override int Size
        {
            get
            {
                return this.Source.Size;
            }
        }

        public override Stream<T> Read(int Index, int Size)
        {
            return new MapStream<TSource, T>(this.Source.Read(Index, Size), this.Map);
        }
    }

    /// <summary>
    /// An array that reads from a buffer (native array).
    /// </summary>
    public sealed class BufferArray<T> : Array<T>
    {
        public BufferArray(T[] Source, int Size, int Offset)
        {
            this.Source = Source;
            this.Offset = Offset;
            this._Size = Size;
        }

        public BufferArray(T[] Source)
        {
            this.Source = Source;
            this.Offset = 0;
            this._Size = Source.Length;
        }

        /// <summary>
        /// The source buffer for this array.
        /// </summary>
        public readonly T[] Source;

        /// <summary>
        /// The offset of this array in the source buffer.
        /// </summary>
        public readonly int Offset;

        public override int Size
        {
            get
            {
                return this._Size;
            }
        }

        public override T this[int Index]
        {
            get
            {
                return this.Source[this.Offset + Index];
            }
        }

        public override Stream<T> Read(int Index, int Size)
        {
            return new BufferStream<T>(this.Source, this.Offset + Index);
        }

        private int _Size;
    }

    /// <summary>
    /// A mutable array that stores data with a extendable list of chunks (also given by arrays).
    /// </summary>
    public sealed class ChunkArray<T> : Array<T>
    {
        public ChunkArray()
        {
            this._Chunks = new List<Chunk>();
            this._Size = 0;
        }

        /// <summary>
        /// Appends a chunk to this array using data from the given array. The array will be referenced directly; making any changes 
        /// to the array will cause corresponding changes to be made in the chunk data.
        /// </summary>
        public void Append(Array<T> Source)
        {
            int nsize = this._Size + Source.Size;
            this._Chunks.Add(new Chunk
            {
                Source = Source,
                Position = this._Size
            });
            this._Size = nsize;
        }

        /// <summary>
        /// Clears the array (sets size to 0).
        /// </summary>
        public void Clear()
        {
            this._Chunks.Clear();
            this._Size = 0;
        }

        public override int Size
        {
            get
            {
                return this._Size;
            }
        }

        public override Stream<T> Read(int Index, int Size)
        {
            Chunk chunk = default(Chunk);
            int ci = this._FindChunk(Index, ref chunk);
            int coffset = Index - chunk.Position;
            if (chunk.Source.Size - coffset < Size)
            {
                return chunk.Source.Read(coffset, Size);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Describes a chunk in a memory array.
        /// </summary>
        public struct Chunk
        {
            /// <summary>
            /// The absolute position of this chunk in the memory array.
            /// </summary>
            public int Position;

            /// <summary>
            /// The array containing the source data for this chunk.
            /// </summary>
            public Array<T> Source;
        }

        /// <summary>
        /// Gets the index (and description) of the chunk that includes the given absolute position.
        /// </summary>
        private int _FindChunk(int Position, ref Chunk Chunk)
        {
            int l = 0;
            int h = this._Chunks.Count;
            while (true)
            {
                int s = (l + h) / 2;
                Chunk = this._Chunks[s];
                if (Position >= Chunk.Position)
                {
                    if (Position - Chunk.Position < Chunk.Source.Size)
                    {
                        return s;
                    }
                    l = s;
                }
                else
                {
                    h = s;
                }
            }
        }

        private int _Size;
        private List<Chunk> _Chunks;
    }

    /// <summary>
    /// An array that combines items from a source array to make an array of compounds.
    /// </summary>
    public sealed class CombineArray<TSource, T, TCompound> : Array<T>
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

        public override Stream<T> Read(int Index, int Size)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// An array that splits items from a source array of compounds to make an array of base items.
    /// </summary>
    public sealed class SplitArray<TSource, T, TCompound> : Array<T>
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

        public override Stream<T> Read(int Index, int Size)
        {
            TCompound compound = default(TCompound);
            int compoundsize = compound.Size;
            SplitStream<TSource, T, TCompound> str = new SplitStream<TSource, T, TCompound>(this.Source.Read(Index / compoundsize, (Size + compoundsize - 1) / compoundsize));
            int r = Index % compoundsize;
            if (r != 0)
            {
                if (str.AdvanceBuffer())
                {
                    str.Offset = r;
                }
            }
            return str;
        }
    }

    /// <summary>
    /// An array that reads raw data from a pointer location.
    /// </summary>
    public sealed class UnsafeArray : Array<byte>
    {
        public UnsafeArray()
        {

        }

        public unsafe UnsafeArray(byte* Start, byte* End)
        {
            this.Start = Start;
            this.End = End;
        }

        /// <summary>
        /// A pointer to the beginning of the array in memory.
        /// </summary>
        public unsafe byte* Start;

        /// <summary>
        /// A pointer to the end of the array in memory.
        /// </summary>
        public unsafe byte* End;

        public unsafe override int Size
        {
            get
            {
                return (int)(this.End - this.Start);
            }
        }

        public override unsafe Stream<byte> Read(int Index, int Size)
        {
            return new UnsafeStream(this.Start + Index, this.End);
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
