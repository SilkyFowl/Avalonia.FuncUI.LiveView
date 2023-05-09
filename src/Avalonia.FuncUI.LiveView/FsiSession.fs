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

        let deps = asm.GetReferencedAssemblies() |> Seq.map (fun asm -> asm.Name)

        let path = (Directory.GetParent asm.Location).FullName

        Directory.EnumerateFiles(path, "*.dll")
        |> Seq.filter (fun p -> Seq.contains (Path.GetFileNameWithoutExtension p) deps)
        |> Seq.map (fun p -> $"-r:{Path.GetFileName p}")
        |> Seq.append (Seq.singleton $"-r:{asm.GetName().Name}")

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
            yield "-d:LIVEPREVIEW"
            yield! references
        |],
        inStream,
        outStream,
        errStream
    )


let getLivePreviews (assembly: Assembly) =

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

    assembly.GetExportedTypes()
    |> Array.map (fun ty ->
        ty.GetMethods(Public ||| Static)
        |> Array.choose (fun m ->
            if isLivePreviewFunc m then
                let fullName = getFullName m

                let content =
                    try
                        match m.Invoke((), [||]) with
                        | :? IView as view -> view
                        | :? Control as view -> ContentControl.create [ ContentControl.content view ]
                        | other -> TextBlock.create [ TextBlock.text $"%A{other}" ]
                        |> VirtualDom.create
                    with :? TargetInvocationException as e ->
                        StackPanel.create [
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.foreground Brushes.Red
                                    TextBlock.text $"%A{e.InnerException.GetType()}"
                                ]
                                TextBlock.create [
                                    TextBlock.foreground Brushes.Red
                                    TextBlock.text $"%s{e.InnerException.Message}"
                                ]
                                TextBlock.create [
                                    TextBlock.text $"%s{e.InnerException.StackTrace}"
                                    TextBlock.textWrapping TextWrapping.WrapWithOverflow
                                ]
                            ]
                        ]
                        |> VirtualDom.create

                Some(fullName, content)
            else
                None))
    |> Array.concat
    |> Array.toList

/// Evalを行う。
let evalInteraction
    (fsiSession: FsiEvaluationSession)
    (log: Logger)
    (tempFile: FileInfo)
    { Msg.Content = content }
    (evalWarings: IWritable<_>)
    (evalResult: IWritable<_>)
    =
    task {
        let time = DateTime.Now.ToString "T"
        (LogInfo >> log) $"{time} Eval Start..."

        // Fsiの改行コードは、どのOSでも\n固定。
        do! File.WriteAllTextAsync(tempFile.FullName, content.Replace(Environment.NewLine, "\n"))

        let res, warnings = fsiSession.EvalScriptNonThrowing tempFile.FullName

        match res with
        | Choice1Of2() ->
            warnings
            |> Array.map (fun w -> box $"Warning {w.Message} at {w.StartLine},{w.StartColumn}")
            |> evalWarings.Set

            let control = fsiSession.DynamicAssemblies |> Array.last |> getLivePreviews

            evalResult.Set control


            (LogInfo >> log) $"{time} Eval Success."
        | Choice2Of2(exn: exn) ->
            (LogError >> log) $"{time} Eval Failed."

            [|
                yield!
                    warnings
                    |> Array.map (fun w -> box $"Warning {w.Message} at {w.StartLine},{w.StartColumn}")
                box $"exception %s{exn.Message}"
            |]
            |> evalWarings.Set
    }