namespace Avalonia.FuncUI.LiveView.Core.Tests

module private Helper =
    open Xunit
    open FsUnit.Xunit
    open FsUnitTyped
    open System
    open System.IO
    open FSharp.Compiler.CodeAnalysis
    open FSharp.Compiler.EditorServices
    open FSharp.Compiler.Symbols
    open FSharp.Compiler.Text

    open Avalonia.FuncUI.LiveView

    /// F# インタラクティブに、このアプリのアセンブリと依存アセンブリを読み込むための引数。
    let references =
        let entryAsm = Reflection.Assembly.GetEntryAssembly()
        let asmPath = Directory.GetParent entryAsm.Location

        let getDeps asmName =

            let asm =
                Path.Combine(asmPath.FullName, asmName)
                |> Reflection.Assembly.LoadFile

            asm.GetReferencedAssemblies()
            |> Seq.map (fun asm -> $"{asm.Name}.dll")
            |> Seq.insertAt 0 asmName

        let deps =
            seq {
                "Avalonia.FuncUI.LiveView.Core.dll"
                "Avalonia.dll"
                "Avalonia.Desktop.dll"
                "Avalonia.Diagnostics.dll"
                "Avalonia.FuncUI.dll"
                "Avalonia.FuncUI.DSL.dll"
                "Avalonia.FuncUI.Elmish.dll"
            }
            |> Seq.map getDeps
            |> Seq.concat
            |> Seq.sort
            |> Seq.distinct
            |> Seq.filter (fun lib -> Path.Combine(asmPath.FullName, lib) |> File.Exists)

        deps
        |> Seq.map (fun p -> $"-r:{Path.GetFileName p}")
        |> Seq.append (Seq.singleton $"-I:{asmPath.FullName}")
        |> Seq.toArray

    let checker: FSharpChecker = FSharpChecker.Create(keepAssemblyContents = true)

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

        checker.ParseAndCheckProject projOptions
        |> Async.RunSynchronously

    let getDeclarations (results: FSharpCheckProjectResults) =
        results.Diagnostics |> shouldBeEmpty

        results.AssemblyContents.ImplementationFiles[0]
            .Declarations

    let runFuncUIAnalysis sourceCode =
        let livePreviewFuncs = ResizeArray()
        let invalidLivePreviewFuncs = ResizeArray()
        let invalidStringCalls = ResizeArray()

        sourceCode
        |> parseAndCheckSingleFile
        |> getDeclarations
        |> List.iter (
            FuncUIAnalysis.visitDeclaration
                { OnLivePreviewFunc = fun v vs -> livePreviewFuncs.Add(v, vs)
                  OnInvalidLivePreviewFunc = fun v vs -> invalidLivePreviewFuncs.Add(v, vs)
                  OnInvalidStringCall =
                    fun ex range m typeArgs argExprs -> invalidStringCalls.Add(ex, range, m, typeArgs, argExprs) }
        )

        {| livePreviewFuncs = livePreviewFuncs
           invalidLivePreviewFuncs = invalidLivePreviewFuncs
           invalidStringCalls = invalidStringCalls |}


module FuncUIAnalysisTests =

    open System
    open Xunit
    open FsUnit.Xunit
    open FsUnitTyped
    open Avalonia.FuncUI.LiveView

    [<Fact>]
    let ``Should work.`` () =
        // let
        let results =
            Helper.runFuncUIAnalysis
                """
open System
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.LiveView.Core.Types

[<RequireQualifiedAccess>]
module Store =
    let num = new State<_> 0

module Counter =
    open Avalonia.FuncUI
    open Avalonia.Controls
    open Avalonia.Media
    open Avalonia.FuncUI.DSL
    open Avalonia.Layout

    let view numState =

        Component.create (
            "Counter",
            fun ctx ->
                let state = ctx.usePassed numState

                Grid.create [
                    Grid.rowDefinitions "test,Auto"
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
"""
        results.livePreviewFuncs.Count |> shouldEqual 1
        results.invalidLivePreviewFuncs.Count |> shouldEqual 1
        results.invalidStringCalls.Count |> shouldEqual 1
