using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using MD.Data;

namespace MD.Codec
{
    /// <summary>
    /// Describes a multimedia container format that can store content within a stream.
    /// </summary>
    public abstract class Container
    {
        public Container(string Name)
        {
            this.Name = Name;
        }

        /// <summary>
        /// A user-friendly name of this container format.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Decodes a content context from a stream using this container format. Returns null if not possible.
        /// </summary>
        public abstract Disposable<Context> Decode(Disposable<Stream<byte>> Stream);

        /// <summary>
        /// Encodes a content context into a stream using this container format. Returns null if not possible.
        /// </summary>
        public abstract Disposable<Stream<byte>> Encode(Disposable<Context> Context);
        
        public override string ToString()
        {
            return this.Name;
        }

        /// <summary>
        /// Registers a container format.
        /// </summary>
        public static RetractAction Register(Container Container)
        {
            List<Container> cs;
            if (!_Registered.TryGetValue(Container.Name, out cs))
            {
                cs = new List<Container>();
                _Registered[Container.Name] = cs;
            }

            cs.Add(Container);
            return delegate
            {
                cs.Remove(Container);
            };
        }

        /// <summary>
        /// Loads a container context from the given file, or returns null if not possible.
        /// </summary>
        public static Disposable<Context> Load(Path File)
        {
            string ext = File.Extension;
            foreach (Container container in WithName(ext))
            {
                Disposable<Stream<byte>> str = File.Open();
                Disposable<Context> context = container.Decode(str);
                if (!context.IsNull)
                {
                    return context;
                }
                else
                {
                    str.Dispose();
                }
            }
            return null;
        }

        /// <summary>
        /// Gets all registered container formats with the given name.
        /// </summary>
        public static IEnumerable<Container> WithName(string Name)
        {
            List<Container> cs;
            if (_Registered.TryGetValue(Name, out cs))
            {
                return cs;
            }
            else
            {
                return new Container[0];
            }
        }

        /// <summary>
        /// Gets all registered container formats.
        /// </summary>
        public static IEnumerable<Container> Available
        {
            get
            {
                return 
                    from cs in _Registered.Values
                    from c in cs
                    select c;
            }
        }

        private static Dictionary<string, List<Container>> _Registered = new Dictionary<string, List<Container>>();
    }
}