using System;
using System.Collections.Generic;
using System.Linq;

namespace MD.Codec
{
    /// <summary>
    /// Multimedia content within a container format.
    /// </summary>
    public abstract class Content
    {
        /// <summary>
        /// Invalidates this content, marking it as unimportant.
        /// </summary>
        public virtual void Discard()
        {

        }
    }
}
