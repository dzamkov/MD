﻿namespace MD.DSP

open MD
open MD.Util
open System

/// A windowing function on the interval (-1/2, 1/2) for use in signal processing.
type Window = float -> float

/// Constains functions for constructing and manipulating windowing functions.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Window = 

    /// The rectangular window
    let rect : Window = fun x -> 1.0

    /// The Hann window
    let hann : Window = fun x -> 1.0 + cos (2.0 * Math.PI  * x)

    /// The Hamming window
    let hamming : Window = fun x -> 0.54 + 0.46 * cos (2.0 * Math.PI  * x)

    /// Creates a normalized instance of a window.
    let create (window : Window) (windowSize : float) bufferSize =
        let buffer = Array.zeroCreate bufferSize
        let start = max 0 (int (float bufferSize / 2.0 - windowSize / 2.0))
        let size = min (bufferSize - start) (int (ceil windowSize))
        let delta = 1.0 / windowSize
        let mutable parameter = (float bufferSize / 2.0 - float start) * delta + delta * 0.5
        let mutable index = 0
        let mutable total = 0.0
        while index < size do
            let value = window parameter
            buffer.[start + index] <- value
            total <- total + value
            parameter <- parameter + delta
            index <- index + 1

        // Normalize
        let mutable index = 0
        while index < size do
            buffer.[start + index] <- buffer.[start + index] / total
            index <- index + 1

        // Return
        buffer