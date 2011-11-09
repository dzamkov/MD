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
        /// Indicates wether this content is ignored in the reading context. If so, frames for this content will still be read, but will not be
        /// interpreted.
        /// </summary>
        public bool Ignore;
    }

    /// <summary>
    /// A context that allows content data to be read.
    /// </summary>
    public abstract class Context
    {
        public Context(Content[] Content)
        {
            this.Content = Content;
        }

        /// <summary>
        /// The content for this context.
        /// </summary>
        public readonly Content[] Content;

        /// <summary>
        /// Advances to the next frame of the context. Updates content data for one of the contents of 
        /// this context (given by ContentIndex). Returns false if there are no more frames to read.
        /// </summary>
        /// <param name="ContentIndex">The index of the content of the frame to read. If this is modified by the method, then the
        /// given value is the actual index of the content read.</param>
        public abstract bool NextFrame(ref int ContentIndex);
    }
}
