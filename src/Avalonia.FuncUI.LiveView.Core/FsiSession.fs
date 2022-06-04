/// 参考
/// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html
/// https://github.com/fsprojects/Avalonia.FuncUI/issues/147
module Avalonia.FuncUI.LiveView.Core.FsiSession


open System
open System.IO
open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.IO

open Avalonia.FuncUI.LiveView.Core.Types

let create () =
    let argv = Environment.GetCommandLineArgs()
    let sbOut = new Text.StringBuilder()
    let sbErr = new Text.StringBuilder()
    let inStream = new StringReader("")
    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)
    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()

    /// F# インタラクティブに、このアプリのアセンブリと依存アセンブリを読み込むための引数。
    let references =
        let asm = Reflection.Assembly.GetEntryAssembly()

        let deps =
            asm.GetReferencedAssemblies()
            |> Seq.map (fun asm -> asm.Name)

        let path = (Directory.GetParent asm.Location).FullName

        Directory.EnumerateFiles(path, "*.dll")
        |> Seq.filter (fun p -> Seq.contains (Path.GetFileNameWithoutExtension p) deps)
        |> Seq.map (fun p -> $"-r:{Path.GetFileName p}")
        |> Seq.append (Seq.singleton $"-r:{asm.GetName().Name}")

    FsiEvaluationSession.Create(
        fsiConfig,
        [| yield "fsi.exe"
           yield! argv
           yield "--noninteractive"
           yield "--nologo"
           yield "--gui-"
           yield "--debug+"
           // 参照が F# インタラクティブ プロセスによってロックされないようにする。
           yield "--shadowcopyreferences+"
           yield "-d:PREVIEW"
           yield! references |],
        inStream,
        outStream,
        errStream
    )