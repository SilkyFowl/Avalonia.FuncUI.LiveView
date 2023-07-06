namespace Avalonia.FuncUI.LiveView

open System
open System.IO
open System.Text

open Avalonia.FuncUI.Types
open Avalonia.FuncUI.LiveView.Types.LiveView.PreviewService
open Avalonia.FuncUI.LiveView.Types.PreviewApp


module ProjArgsInfo =
    let getReferenceArgs info =
        info.Args |> Array.filter (fun s -> s.StartsWith "-r:")

/// Ref
/// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html
/// https://github.com/fsprojects/Avalonia.FuncUI/issues/147
module internal FsiSession =
    open System.Collections.Concurrent
    open System.Reflection
    open System.Text.RegularExpressions

    open FSharp.Compiler.IO
    open FSharp.Compiler.Interactive.Shell

    open Avalonia.FuncUI.LiveView.Attribute

    let defaultFileSystem = FileSystem

    type LivePreviewFileSystem() =

        let files = new ConcurrentDictionary<string, string>()

        member _.AddOrUpdateFsCode fileName content =
            files.AddOrUpdate(fileName, addValue = content, updateValueFactory = (fun _key _oldContent -> content))
            |> ignore


        interface IFileSystem with
            // Implement the service to open files for reading and writing
            member _.OpenFileForReadShim(fileName, ?useMemoryMappedFile: bool, ?shouldShadowCopy: bool) =
                match files.TryGetValue fileName with
                | true, text -> new MemoryStream(Encoding.UTF8.GetBytes(text)) :> Stream
                | _ ->
                    defaultFileSystem.OpenFileForReadShim(
                        fileName,
                        ?useMemoryMappedFile = useMemoryMappedFile,
                        ?shouldShadowCopy = shouldShadowCopy
                    )

            member _.OpenFileForWriteShim
                (
                    fileName,
                    ?fileMode: FileMode,
                    ?fileAccess: FileAccess,
                    ?fileShare: FileShare
                ) =
                defaultFileSystem.OpenFileForWriteShim(
                    fileName,
                    ?fileMode = fileMode,
                    ?fileAccess = fileAccess,
                    ?fileShare = fileShare
                )

            // Implement the service related to file existence and deletion
            member _.FileExistsShim(fileName) =
                files.ContainsKey(fileName) || defaultFileSystem.FileExistsShim(fileName)

            // Implement the service related to temporary paths and file time stamps
            member _.GetTempPathShim() = defaultFileSystem.GetTempPathShim()

            member _.GetLastWriteTimeShim(fileName) =
                defaultFileSystem.GetLastWriteTimeShim(fileName)

            member _.GetFullPathShim(fileName) =
                defaultFileSystem.GetFullPathShim(fileName)

            member _.IsInvalidPathShim(fileName) =
                defaultFileSystem.IsInvalidPathShim(fileName)

            member _.IsPathRootedShim(fileName) =
                defaultFileSystem.IsPathRootedShim(fileName)

            member _.FileDeleteShim(fileName) =
                defaultFileSystem.FileDeleteShim(fileName)

            member _.AssemblyLoader = defaultFileSystem.AssemblyLoader

            member _.GetFullFilePathInDirectoryShim dir fileName =
                defaultFileSystem.GetFullFilePathInDirectoryShim dir fileName

            member _.NormalizePathShim(path) =
                defaultFileSystem.NormalizePathShim(path)

            member _.GetDirectoryNameShim(path) =
                defaultFileSystem.GetDirectoryNameShim(path)

            member _.GetCreationTimeShim(path) =
                defaultFileSystem.GetCreationTimeShim(path)

            member _.CopyShim(src, dest, overwrite) =
                defaultFileSystem.CopyShim(src, dest, overwrite)

            member _.DirectoryCreateShim(path) =
                defaultFileSystem.DirectoryCreateShim(path)

            member _.DirectoryExistsShim(path) =
                defaultFileSystem.DirectoryExistsShim(path)

            member _.DirectoryDeleteShim(path) =
                defaultFileSystem.DirectoryDeleteShim(path)

            member _.EnumerateFilesShim(path, pattern) =
                defaultFileSystem.EnumerateFilesShim(path, pattern)

            member _.EnumerateDirectoriesShim(path) =
                defaultFileSystem.EnumerateDirectoriesShim(path)

            member _.IsStableFileHeuristic(path) =
                defaultFileSystem.IsStableFileHeuristic(path)

            member _.ChangeExtensionShim(path: string, extension: string) : string =
                defaultFileSystem.ChangeExtensionShim(path, extension)

    let livePreviewFileSystem = LivePreviewFileSystem()

    FileSystem <- livePreviewFileSystem


    let init inStream outStream errStream info =
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()

        let session =
            FsiEvaluationSession.Create(
                fsiConfig,
                [|
                    yield "fsi.exe"
                    yield "--noninteractive"
                    yield "--nologo"
                    yield "--gui-"
                    yield "--debug+"
                    // Ensure that references are not locked by the F# interactive process.
                    yield "--shadowcopyreferences+"
                    yield "-d:LIVEPREVIEW"
                |],
                inStream,
                outStream,
                errStream,
                true
            )

        String.concat "\n" [
            for r in ProjArgsInfo.getReferenceArgs info do
                let path = r.Replace("-r:", "")
                $"#r @\"{path}\""
            $"#r @\"{info.TargetPath}\""
        ]
        |> session.EvalInteraction

        session

    let getLivePreviews (fsiSession: FsiEvaluationSession) =
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

        let asm = Array.last fsiSession.DynamicAssemblies

        asm.GetExportedTypes()
        |> Array.collect (fun ty -> ty.GetMethods(BindingFlags.Public ||| BindingFlags.Static))
        |> Array.filter isLivePreviewFunc
        |> Array.map (fun m ->
            let fullName = getFullName m

            let extract fn onExn : Async<IView> =
                async {
                    try
                        return m.Invoke((), [||]) |> fn
                    with exn ->
                        return onExn exn
                }

            fullName, extract)

    let addOrUpdateFsCode path content =
        content
        // Fsi newline code for any OS is LF(\n).
        |> Array.map (fun s -> Regex.Replace(s, "\r?\n?$", "\n"))
        |> String.concat ""
        |> livePreviewFileSystem.AddOrUpdateFsCode path

    let evalScriptNonThrowing (fsiSession: FsiEvaluationSession) path content =
        let res, diagnostic = fsiSession.EvalScriptNonThrowing path

        match res with
        | Choice1Of2() ->
            let views = getLivePreviews fsiSession |> Map.ofArray

            Ok {
                views = views
                diagnostic = diagnostic
                content = content
                timestamp = DateTime.Now
            }
        | Choice2Of2(exn: exn) ->
            Error {
                exn = exn
                diagnostic = diagnostic
                content = content
                timestamp = DateTime.Now
            }

type FsiPreviewSession(info: ProjArgsInfo) =
    let sbOut = new StringBuilder()
    let sbErr = new StringBuilder()
    let inStream = new StringReader("")
    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)


    let session =

        FsiSession.init inStream outStream errStream info

    interface IPreviewSession with
        member _.addOrUpdateFsCode path content =
            FsiSession.addOrUpdateFsCode path content

        member _.evalScriptNonThrowing path content =
            FsiSession.evalScriptNonThrowing session path content

    interface IDisposable with
        member _.Dispose() = (session :> IDisposable).Dispose()