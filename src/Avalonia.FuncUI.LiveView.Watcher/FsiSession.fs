namespace Avalonia.FuncUI.LiveView

open System.Collections.Concurrent
open System.IO
open System.Reflection
open System.Text

open FSharp.Compiler.IO
open FSharp.Compiler.Interactive.Shell

open Avalonia.Controls
open Avalonia.Media
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.VirtualDom

open Avalonia.FuncUI.LiveView.Types

/// see
/// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html
/// https://github.com/fsprojects/Avalonia.FuncUI/issues/147
module FsiSession =
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
            ty.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
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

    let ofProjectInfo (projectInfo: ProjectInfo) =
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()

        let inStream = new StringReader("")
        let outStream = new StringWriter(new StringBuilder())
        let errStream = new StringWriter(new StringBuilder())

        let argv =
            [| "fsi.exe"
               "--noninteractive"
               "--nologo"
               "--gui-"
               "--debug+"
               "--shadowcopyreferences+"
               "-d:LIVEPREVIEW"
               for refSource in projectInfo.ReferenceSources do
                   $"-r:{refSource.Path}"
               $"-r:{projectInfo.TargetPath}" |]

        FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream, true)


type private LivePreviewFileSystem() =
    inherit DefaultFileSystem()

    let files = new ConcurrentDictionary<string, string>()

    let getStream (content: string) : Stream =
        new MemoryStream(Encoding.UTF8.GetBytes(content))

    member _.AddOrUpdateFsCode fileName content =
        files.AddOrUpdate(fileName, addValue = content, updateValueFactory = (fun _key _oldContent -> content))
        |> ignore

    override _.OpenFileForReadShim(fileName, ?useMemoryMappedFile: bool, ?shouldShadowCopy: bool) =
        match files.TryGetValue fileName with
        | true, text -> getStream text
        | _ when not (File.Exists fileName) && Path.GetFileName fileName = "unknown" -> getStream ""
        | _ ->
            base.OpenFileForReadShim(
                fileName,
                ?useMemoryMappedFile = useMemoryMappedFile,
                ?shouldShadowCopy = shouldShadowCopy
            )

    override _.FileExistsShim(fileName) =
        files.ContainsKey(fileName) || base.FileExistsShim(fileName)


module LivePreviewFileSystem =
    let private livePreviewFileSystem = new LivePreviewFileSystem()

    let setLivePreviewFileSystem () = FileSystem <- livePreviewFileSystem

    let resetFileSystem () = FileSystem <- DefaultFileSystem()

    let addOrUpdateFsCode fileName content =
        livePreviewFileSystem.AddOrUpdateFsCode fileName content