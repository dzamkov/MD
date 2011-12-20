module MD.Reflection

open System
open System.Reflection

/// Maps a source object of a generic type into to an object of the given target generic type by calling
/// its constructor with the downcasted form of the source object.
let mapType (sourceType : Type) (targetType : Type) (source : 'a) =

    let rec getArgumentsAndType (fullType : Type) (genericType : Type) =
        if fullType.IsGenericType && fullType.GetGenericTypeDefinition () = genericType then
            (fullType.GetGenericArguments (), fullType)
        else
            getArgumentsAndType fullType.BaseType genericType
    
    let arguments, sourceType = getArgumentsAndType (source.GetType ()) sourceType
    let targetType = targetType.MakeGenericType arguments
    (targetType.GetConstructor [| sourceType |]).Invoke [| source |] :?> 'b