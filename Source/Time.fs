namespace MD

open System
open System.Collections.Generic

/// Contains functions for tracking the passage of time.
module Time =

    /// An action that is invoked at discrete time steps with the time (in seconds) that has passed
    /// since the previous update.
    let mutable update : Action<double> = null
    
    /// Registers an action to be called as time passes.
    let register (callback : Action<double>) = update <- Delegate.Combine (update, callback) :?> Action<double>