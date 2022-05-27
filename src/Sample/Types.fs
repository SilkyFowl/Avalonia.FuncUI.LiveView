namespace Sample

open System
open Avalonia.FuncUI


[<RequireQualifiedAccess>]
module Store =
    let mun = new State<_> 0