namespace MD

/// An interface to a mouse-like user input source on a two-dimensional coordinate system.
type Probe = {
    /// The position of this probe.
    Position : SignalFeed<Point>

    /// The state of the primary button.
    Primary : SignalFeed<bool>

    /// The state of the secondary button.
    Secondary : SignalFeed<bool>

    /// Fires an event whenever the scroll wheel is used, with the amount that was scrolled by.
    Scroll : EventFeed<double>
    }

/// An interface for user input.
type Input = {

    /// A collection of probes present in this input interface.
    Probes : CollectionFeed<Probe>
    }