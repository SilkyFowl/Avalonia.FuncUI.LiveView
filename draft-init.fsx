module Draft.AnalysisDraft

#I "tests/Avalonia.FuncUI.LiveView.Core.Tests/bin/Debug/net6.0"
#r "FSharp.Compiler.Service.dll"

open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

// Create an interactive checker instance
let checker: FSharpChecker = FSharpChecker.Create(keepAssemblyContents = true)

let references =
    let rootProjAsm = "Avalonia.FuncUI.LiveView.Core.Tests.dll"

    let asmDirctoryPath =
        Path.Combine [|
            __SOURCE_DIRECTORY__
            "tests"
            "Avalonia.FuncUI.LiveView.Core.Tests"
            "bin"
            "Debug"
            "net6.0"
        |]

    let getDeps asmName =

        let asm = Path.Combine(asmDirctoryPath, asmName) |> Reflection.Assembly.LoadFile

        asm.GetReferencedAssemblies()
        |> Seq.map (fun asm -> $"{asm.Name}.dll")
        |> Seq.insertAt 0 asmName

    let deps =
        seq {
            rootProjAsm
            "Avalonia.dll"
            "Avalonia.Desktop.dll"
            "Avalonia.Diagnostics.dll"
            "Avalonia.FuncUI.dll"
            "Avalonia.FuncUI.Elmish.dll"
        }
        |> Seq.map getDeps
        |> Seq.concat
        |> Seq.sort
        |> Seq.distinct
        |> Seq.filter (fun lib -> Path.Combine(asmDirctoryPath, lib) |> File.Exists)

    deps
    |> Seq.map (fun p -> $"-r:{Path.GetFileName p}")
    |> Seq.append (Seq.singleton $"-r:{rootProjAsm}")
    |> Seq.append (Seq.singleton $"-I:{asmDirctoryPath}")
    |> Seq.toArray

open FSharp.Compiler.Interactive.Shell

let createSession () =
    let argv = Environment.GetCommandLineArgs()
    let sbOut = new Text.StringBuilder()
    let sbErr = new Text.StringBuilder()
    let inStream = new StringReader("")
    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)
    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()

    FsiEvaluationSession.Create(
        fsiConfig,
        [|
            yield "fsi.exe"
            yield! argv
            yield "--noninteractive"
            yield "--nologo"
            yield "--gui-"
            yield "--debug+"
            // 参照が F# インタラクティブ プロセスによってロックされないようにする。
            yield "--shadowcopyreferences+"
            //    yield "-d:PREVIEW"
            yield! references
        |],
        inStream,
        outStream,
        errStream
    )

let parseAndCheckSingleFile (input) =
    let file = Path.ChangeExtension(Path.GetTempFileName(), "fsx")
    File.WriteAllText(file, input)
    // Get context representing a stand-alone (script) file
    let projOptions, _errors =
        checker.GetProjectOptionsFromScript(
            file,
            SourceText.ofString input,
            assumeDotNetFramework = false,
            otherFlags = references
        )
        |> Async.RunSynchronously

    checker.ParseAndCheckProject(projOptions) |> Async.RunSynchronously

let input =
    """
open System
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.LiveView.Core.Types

[<RequireQualifiedAccess>]
module Store =
    let num = new State<_> 0

module Counter =

    let view numState =

        Component.create (
            "Counter",
            fun ctx ->
                let state = ctx.usePassed numState

                Grid.create [
                    Grid.rowDefinitions "Auto"
                    Grid.children [
                        TextBlock.create [
                            TextBlock.text "Foo!!"
                        ]
                    ]
                ]
        )

    [<LivePreview>]
    let preview () =
        view Store.num

    [<LivePreview>]
    let preview2 =
        let n = 1
        view Store.num
    [<LivePreview>]
    let preview3() =
        DockPanel.create [ ]
      """

let checkProjectResults: FSharpCheckProjectResults = parseAndCheckSingleFile (input)

let declarations =
    checkProjectResults.AssemblyContents.ImplementationFiles[0].Declarations