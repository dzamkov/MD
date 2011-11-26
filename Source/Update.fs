namespace MD

open System
open System.Collections.Generic

/// Contains functions for registering and invoking program-wide time-dependant state updates.
module Update =

    /// The update callbacks that are currently registered.
    let callbacks = new Registry<double -> unit> ()

    /// Adds a callback to be called on each program-wide update with the time, in seconds, that
    /// has elapsed since the previous one.
    let register callback = callbacks.Add callback

    /// Invokes a program-wide update with the given time in seconds.
    let invoke time =
        for callback in callbacks do
            callback time