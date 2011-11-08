using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// A callback that can be used to retract a previously submitted object, modification or callback. If a method returns
    /// a retract handler of null, then no modifications were made.
    /// </summary>
    public delegate void RetractHandler();
}
