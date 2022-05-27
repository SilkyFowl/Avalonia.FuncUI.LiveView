namespace Sample

open System
open Avalonia.FuncUI


[<RequireQualifiedAccess>]
module Store =
    let num = new State<_> 0