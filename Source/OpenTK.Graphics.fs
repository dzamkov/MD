namespace MD.OpenTK

/// An interface to an OpenGL graphics device that tracks resources and allows rendering.
type Graphics () =
    
    /// Renders the given figure using this graphics context.
    member this.Render figure =
        ()