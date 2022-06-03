module Draft.AnalysisDraft

#r "nuget: FSharp.Compiler.Service"

open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

// Create an interactive checker instance
let checker: FSharpChecker = FSharpChecker.Create(keepAssemblyContents = true)

let parseAndCheckSingleFile (input) =
    let file = Path.ChangeExtension(System.IO.Path.GetTempFileName(), "fsx")
    File.WriteAllText(file, input)
    // Get context representing a stand-alone (script) file
    let projOptions, _errors =
        checker.GetProjectOptionsFromScript(file, SourceText.ofString input, assumeDotNetFramework = false)
        |> Async.RunSynchronously

    checker.ParseAndCheckProject(projOptions)
    |> Async.RunSynchronously

let input =
    """
#r "nuget: Avalonia.Desktop"
#r "nuget: JaggerJo.Avalonia.FuncUI"
#r "nuget: JaggerJo.Avalonia.FuncUI.DSL"
#r "nuget: JaggerJo.Avalonia.FuncUI.Elmish"

open System
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Controls

[<AttributeUsage(AttributeTargets.Property)>]
type LivePreviewAttribute () =
    inherit Attribute()

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

let checkProjectResults: FSharpCheckProjectResults = parseAndCheckSingleFile (input)
let declarations = checkProjectResults.AssemblyContents.ImplementationFiles[1].Declarations