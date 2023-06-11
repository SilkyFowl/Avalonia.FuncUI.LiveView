module Avalonia.FuncUI.LiveView.Views

open System
open System.IO
open System.Collections.Generic
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler
open FSharp.Compiler.Text
open FSharp.Compiler.CodeAnalysis

open FSharp.Analyzers.SDK

type LivePreviewAttribute = Core.Types.LivePreviewAttribute

module String =

    let removePrefix (prefix: string) (str: string) =
        if str.StartsWith(prefix, StringComparison.Ordinal) then
            str.Substring prefix.Length
        else
            str

    let deIndent (str: string) =
        let lines = str.Split(Environment.NewLine)

        let shortestRunOfSpacesForNonEmptyLines =
            lines
            |> Seq.filter (not << String.IsNullOrWhiteSpace)
            |> Seq.map (fun s -> s |> Seq.takeWhile ((=) ' ') |> Seq.length)
            |> Seq.min

        let spacesToRemove = String.replicate shortestRunOfSpacesForNonEmptyLines " "

        lines
        |> Seq.map (removePrefix spacesToRemove)
        |> String.concat Environment.NewLine

module Context =
    open FSharp.Compiler.Diagnostics
    open FSharp.Compiler.EditorServices

    let private filename = "script.fsx"


    let private parseFile source =
        async {
            let checker = FSharpChecker.Create()
            let sourceText = SourceText.ofString source
            let otherOpts = [| "--targetprofile:netstandard" |]

            let! projectOptions, _ =
                checker.GetProjectOptionsFromScript(
                    filename,
                    sourceText,
                    assumeDotNetFramework = false,
                    otherFlags = otherOpts
                )

            let parsingOptions, _diagnostics =
                checker.GetParsingOptionsFromProjectOptions(projectOptions)

            let! parseResults = checker.ParseFile(filename, sourceText, parsingOptions)

            let errs =
                parseResults.Diagnostics
                |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)

            if errs.Length > 0 then
                failwith $"Parse produced these errors: %A{parseResults.Diagnostics}"

            return parseResults
        }

    let private typeCheck parseResults source =
        async {
            let checker = FSharpChecker.Create(keepAssemblyContents = true)
            let sourceText = SourceText.ofString source
            let otherOpts = [| "--targetprofile:netstandard" |]

            let! projectOptions, _ =
                checker.GetProjectOptionsFromScript(
                    filename,
                    sourceText,
                    assumeDotNetFramework = false,
                    otherFlags = otherOpts
                )

            let! answer = checker.CheckFileInProject(parseResults, filename, 1, sourceText, projectOptions)

            return
                match answer with
                | FSharpCheckFileAnswer.Succeeded results ->
                    let errs =
                        results.Diagnostics
                        |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)

                    if errs.Length > 0 then
                        failwith $"Type check produced these errors: %A{errs}"

                    results
                | FSharpCheckFileAnswer.Aborted -> failwith "Type check abandoned"
        }


    let private entityCache = EntityCache()


    let private getAllEntities (checkResults: FSharpCheckFileResults) (publicOnly: bool) : AssemblySymbol list =
        try
            let res = [
                yield!
                    AssemblyContent.GetAssemblySignatureContent
                        AssemblyContentType.Full
                        checkResults.PartialAssemblySignature

                let ctx = checkResults.ProjectContext

                let assembliesByFileName =
                    ctx.GetReferencedAssemblies()
                    |> Seq.groupBy (fun assembly -> assembly.FileName)
                    |> Seq.map (fun (fileName, assemblies) -> fileName, List.ofSeq assemblies)
                    |> Seq.toList
                    |> List.rev // If mscorlib.dll is the first, then FSC raises exception when we try to get Content.Entities from it.

                for fileName, signatures in assembliesByFileName do
                    let contentType =
                        if publicOnly then
                            AssemblyContentType.Public
                        else
                            AssemblyContentType.Full

                    let content =
                        AssemblyContent.GetAssemblyContent entityCache.Locking contentType fileName signatures

                    yield! content
            ]

            res
        with _ -> []

    let processTestSource (source: string) =
        source.ReplaceLineEndings()
        |> String.removePrefix Environment.NewLine
        |> String.deIndent

    let fromProcessedTestSource source : Context =
        let parseResults = parseFile source |> Async.RunSynchronously

        let typeCheckResults = typeCheck parseResults source |> Async.RunSynchronously

        {
            FileName = filename
            Content = source.Split(Environment.NewLine)
            ParseTree = parseResults.ParseTree
            TypedTree = typeCheckResults.ImplementationFile.Value
            Symbols = typeCheckResults.PartialAssemblySignature.Entities |> Seq.toList
            GetAllEntities = getAllEntities typeCheckResults
        }


    let fromTestSource (source: string) : Context =
        source |> processTestSource |> fromProcessedTestSource

module ProjectLoader =
  [<Literal>]
  let ProduceReferenceAssembly = "ProduceReferenceAssembly"

  let globalProperties =
    [
      // For tooling we don't want to use Reference Assemblies as this doesn't play well with type checking across projects
      ProduceReferenceAssembly, "false" ]

/// Note: If 7.0.203, The SDK resolver "Microsoft.DotNet.MSBuildWorkloadSdkResolver" failed ,
/// ref: https://github.com/dotnet/core/issues/8395
module ProjInfo =
    open Ionide.ProjInfo
    open Ionide.ProjInfo.Types

    let init projectDirectory = 
        // Init.setupForSdkVersion
        //     (DirectoryInfo @"C:\Program Files\dotnet\sdk\6.0.311")
        //     (FileInfo @"C:\Program Files\dotnet\dotnet.exe")
        // MSBuildLocator
        // ProjectLoader.loadProject  
        (Init.init (DirectoryInfo Environment.CurrentDirectory) None) // |> ignore
        // ToolsPath @"C:\Program Files\dotnet\sdk\6.0.311\MSBuild.dll"


    let createWorkspaceLoaderViaProjectGraph toolPath =
        WorkspaceLoader.Create(toolPath, ProjectLoader.globalProperties)

let context =
    """
open System
[<AttributeUsage(AttributeTargets.Property)>]
type LivePreviewAttribute() =
    inherit Attribute()

type Foo = | Bar of int
let f a Foo c = 0
[<LivePreview>]
let preview() = 1
    """
    |> Context.fromTestSource

open Avalonia
open Avalonia.Layout
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Ionide.ProjInfo
open Ionide.ProjInfo.Types

[<LivePreview>]
let view () =

    Component(fun ctx ->
        let projectDirectory = ctx.useState @"C:\workspace\work\Avalonia.FuncUI.LiveView"
        let entities = context.GetAllEntities true
        let fooContent = entities |> List.find (fun s -> s.FullName.Contains "preview")

        let slnPath = ctx.useState ""

        let errors = ctx.useState<list<obj>> []
        let log = ctx.useState []
        let toolsPath = ctx.useState None
        let loader = ctx.useState<IWorkspaceLoader option> None

        ctx.useEffect (
            (fun () ->
                match loader.Current with
                | Some loader -> loader.Notifications.Subscribe(function
                    | WorkspaceProjectState.Loading e -> printfn $"%A{e}"
                    | WorkspaceProjectState.Loaded _ as e -> printfn $"%A{e}"
                    | WorkspaceProjectState.Failed (p,errs) -> printfn $"""
                    Failed: %A{p}
                    %A{errs}
                    """ )
                | None ->
                    { new IDisposable with
                        member _.Dispose() = ()
                    }),
            [ EffectTrigger.AfterChange loader ]
        )

        // let loaderNotifications = ctx.useState({new IDisposable with member _.Dispose() = ()},false)

        let projectOptions = ctx.useState Seq.empty

        ctx.useEffect (
            (fun () ->
                toolsPath.Current
                |> Option.map (fun tp -> ProjInfo.createWorkspaceLoaderViaProjectGraph tp)
                |> loader.Set),
            [ EffectTrigger.AfterChange toolsPath ]
        )

        let loadProject _ =
            try
                let result = ProjInfo.init (DirectoryInfo projectDirectory.Current)
                Some result |> toolsPath.Set
                errors.Set []
            with e ->
                errors.Set [ e ]

        DockPanel.create [

            DockPanel.children [
                StackPanel.create [
                    StackPanel.dock Dock.Top
                    StackPanel.spacing 8
                    StackPanel.orientation Orientation.Horizontal
                    StackPanel.margin (0, 0, 0, 4)
                    StackPanel.children [
                        Button.create [ Button.content "Load"; Button.onClick loadProject ]
                        TextBlock.create [
                            TextBlock.verticalAlignment VerticalAlignment.Center
                            TextBlock.text "Project Directory"
                        ]
                        TextBox.create [
                            TextBox.errors errors.Current
                            TextBox.text projectDirectory.Current
                            TextBox.onTextChanged projectDirectory.Set
                        ]
                    ]

                ]

                TextBox.create [
                    DockPanel.dock Dock.Bottom
                    TextBox.text $"%O{fooContent.Symbol.Attributes[0]}"
                ]
                TextBox.create [ DockPanel.dock Dock.Bottom; TextBox.text $"%A{toolsPath.Current}" ]
                StackPanel.create [
                    StackPanel.dock Dock.Top
                    StackPanel.spacing 8
                    StackPanel.orientation Orientation.Horizontal
                    StackPanel.margin (0, 0, 0, 4)
                    StackPanel.children [
                        Button.create [
                            Button.content "LoadSln"
                            Button.onClick (fun _ ->
                                try
                                    loader.Current
                                    |> Option.iter (fun loader ->

                                        let result = loader.LoadSln slnPath.Current |> Seq.toArray
                                        projectOptions.Set result)
                                with e ->
                                    errors.Set [ e ])
                        ]
                        TextBlock.create [
                            TextBlock.verticalAlignment VerticalAlignment.Center
                            TextBlock.text "Sln Path"
                        ]
                        TextBox.create [
                            TextBox.errors errors.Current
                            TextBox.text slnPath.Current
                            TextBox.onTextChanged slnPath.Set
                        ]
                    ]

                ]
                TextBox.create [
                    DockPanel.dock Dock.Bottom
                    TextBox.text $"%A{loader.Current}"
                    match loader.Current with
                    | Some v -> TextBox.text $"%A{v}"
                    | None -> ()
                ]
                for opt in projectOptions.Current do
                    TextBox.create [ DockPanel.dock Dock.Bottom; TextBox.text $"%A{opt}" ]
            ]
        ])