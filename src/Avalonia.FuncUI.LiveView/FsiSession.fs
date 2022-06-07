/// 参考
/// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html
/// https://github.com/fsprojects/Avalonia.FuncUI/issues/147
module Avalonia.FuncUI.LiveView.FsiSession

open System
open System.IO
open System.Reflection

open type System.Reflection.BindingFlags

open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.IO

open Avalonia.Controls
open Avalonia.Media
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.VirtualDom
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
        let asm = Assembly.GetEntryAssembly()

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


let generateLivePreview (assembly: Assembly) =

    let isLivePreviewFunc (m: MethodInfo) =
        typeof<LivePreviewAttribute> |> m.IsDefined
        && m.GetParameters() |> Array.isEmpty

    let getFullName (m: MethodInfo) =
        m.DeclaringType
        |> Seq.unfold (function
            | null -> None
            | ty -> Some(ty.Name, ty.DeclaringType))
        |> Seq.append [ $"{m.Name}()" ]
        |> Seq.rev
        |> String.concat "."

    let childView attrs name contentFunc : IView =
        Grid.create [
            yield! attrs
            Grid.rowDefinitions "Auto,*"
            Grid.children [
                TextBlock.create [
                    TextBlock.row 0
                    TextBlock.text name
                ]
                Border.create [
                    Border.row 1
                    contentFunc ()
                ]
            ]
        ]

    let children =
        assembly.GetExportedTypes()
        |> Array.map (fun ty ->
            ty.GetMethods(Public ||| Static)
            |> Array.choose (fun m ->
                if isLivePreviewFunc m then
                    fun () ->
                        match m.Invoke((), [||]) with
                        | :? IView as view -> Border.child view
                        | :? IControl as view -> Border.child view
                        | other ->
                            TextBlock.create [
                                TextBlock.text $"%A{other}"
                            ]
                            |> Border.child
                    |> childView [ Grid.dock Dock.Top ] (getFullName m)
                    |> Some
                else
                    None))
        |> Array.concat
        |> Array.toList

    DockPanel.create [
        DockPanel.lastChildFill false
        DockPanel.children children
    ]
    |> VirtualDom.create

/// Evalを行う。
let evalInteraction
    (fsiSession: FsiEvaluationSession)
    (log: Logger)
    (tempFile: FileInfo)
    { Msg.Content = content }
    (evalWarings: IWritable<_>)
    (evalResult: IWritable<obj>)
    =
    task {
        let time = DateTime.Now.ToString "T"
        do! File.WriteAllTextAsync(tempFile.FullName, content)

        let res, warnings = fsiSession.EvalScriptNonThrowing tempFile.FullName

        match res with
        | Choice1Of2 () ->
            warnings
            |> Array.map (fun w -> box $"Warning {w.Message} at {w.StartLine},{w.StartColumn}")
            |> evalWarings.Set

            let control =
                fsiSession.DynamicAssemblies
                |> Array.last
                |> generateLivePreview

            do!
                fun () -> evalResult.Set control
                |> Avalonia.Threading.Dispatcher.UIThread.InvokeAsync

            (LogInfo >> log) $"{time} Eval Success."
        | Choice2Of2 (exn: exn) ->
            (LogError >> log) $"{time} Eval Failed."

            [| yield!
                   warnings
                   |> Array.map (fun w -> box $"Warning {w.Message} at {w.StartLine},{w.StartColumn}")
               box $"exception %s{exn.Message}" |]
            |> evalWarings.Set
    }