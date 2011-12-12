namespace MD

open System
open System.Threading
open System.Threading.Tasks

/// Contains functions for creating and using multithread tasks.
module Task =

    /// Starts the given task, returning a retract action to cancel it if
    /// needed.
    let start (task : unit -> unit) =
        let cancelSource = new CancellationTokenSource ()
        let task = new Task (Action task, cancelSource.Token)
        task.Start ()
        Retract.Single cancelSource.Cancel