namespace MD.OpenTK

open MD
open MD.UI
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

/// A graphics interface that uses a programmable pipeline (OpenGL 3.0 and above).
type ProgrammableGraphics () =
    inherit Graphics ()

    override this.CreateContext () = new NotImplementedException () |> raise
    override this.CreateProcedure _ = new NotImplementedException () |> raise