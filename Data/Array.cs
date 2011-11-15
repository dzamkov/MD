using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace MD.Data
{
    /// <summary>
    /// Contains array related functions.
    /// </summary>
    public static class Array
    {
        /// <summary>
        /// Creates a stream to read this array starting at the given index.
        /// </summary>
        public static Stream<T> Read<T>(this Array<T> Array, int Index)
        {
            return Array.Read(Index, Array.Size - Index);
        }

        /// <summary>
        /// Constructs a mapped form of this array based on the given mapping function.
        /// </summary>
        public static Array<T> Map<TSource, T>(this Array<TSource> Source, Func<TSource, T> Map)
        {
            return new MapArray<TSource, T>(Source, Map);
        }
    }

    /// <summary>
    /// A collection of data indexed by an integer. Unless stated otherwise, an array can be
    /// assumed to be mutable.
    /// </summary>
    public interface Array<T>
    {
        /// <summary>
        /// Gets the current size of the array.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Gets the current value of the array at the given index.
        /// </summary>
        T this[int Index] { get; }

        /// <summary>
        /// Creates a stream to read this array starting at the given index. Note that the returned stream may be larger than the requested
        /// Size (or even the size of the array).
        /// </summary>
        /// <param name="Size">The maximum amount of data that will be read from the stream.</param>
        Stream<T> Read(int Index, int Size);
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

        public int Size
        {
            get
            {
                return this.Source.Size;
            }
        }

        public T this[int Index]
        {
            get
            {
                return this.Map(this.Source[Index]);
            }
        }

        public Stream<T> Read(int Index, int Size)
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

        public int Size
        {
            get
            {
                return this._Size;
            }
        }

        public T this[int Index]
        {
            get
            {
                return this.Source[this.Offset + Index];
            }
            set
            {
                this.Source[this.Offset + Index] = value;
            }
        }

        public Stream<T> Read(int Index, int Size)
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

        public int Size
        {
            get
            {
                return this._Size;
            }
        }

        public T this[int Index]
        {
            get
            {
                Chunk chunk = default(Chunk);
                int ci = this._FindChunk(Index, ref chunk);
                return chunk.Source[Index - chunk.Position];
            }
        }

        public Stream<T> Read(int Index, int Size)
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

        public unsafe int Size
        {
            get
            {
                return (int)(this.End - this.Start);
            }
        }

        public unsafe byte this[int Index]
        {
            get
            {
                return this.Start[Index];
            }
            set
            {
                this.Start[Index] = value;
            }
        }

        public unsafe Stream<byte> Read(int Index, int Size)
        {
            return new UnsafeStream(this.Start + Index, this.End);
        }
    }
}
