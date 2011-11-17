using System;
using System.Collections.Generic;
using System.Linq;

namespace MD.UI.Input
{
    /// <summary>
    /// Represents an interface to user input.
    /// </summary>
    public class Interface
    {
        public Interface(CollectionFeed<Probe> Probes)
        {
            this.Probes = Probes;
        }

        /// <summary>
        /// The probes currently accessible by this interface.
        /// </summary>
        public readonly CollectionFeed<Probe> Probes;
    }
}
