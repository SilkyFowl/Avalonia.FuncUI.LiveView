namespace Avalonia.FuncUI.LiveView

module Disposable =
    open System
    let inline dispose (x: #IDisposable) = x.Dispose()

    let inline tryDispose (x: #IDisposable option) =
        match x with
        | Some x -> x.Dispose()
        | None -> ()

module MSBuildLocator =
    open Microsoft.Build.Locator
    /// Returns true if instance of MSBuild found on the machine is registered.
    let inline isRegistered () = MSBuildLocator.IsRegistered

    /// Registers the highest version instance of MSBuild found on the machine.
    let register () =
        let visualStudioInstances = MSBuildLocator.QueryVisualStudioInstances()
        let instance = visualStudioInstances |> Seq.maxBy (fun i -> i.Version)
        MSBuildLocator.RegisterInstance(instance)

    /// Registers the highest version instance of MSBuild found on the machine if it is not already registered.
    let registerIfNotRegistered () =
        if not <| isRegistered () then
            register ()

module ProjectInfo =
    open Microsoft.Build.Construction
    open Microsoft.Build.Evaluation
    open Microsoft.Build.Execution

    open Avalonia.FuncUI.LiveView.Types

    module private Solution =
        let parse (path: string) =
            SolutionFile.Parse(path).ProjectsInOrder
            |> Seq.map (fun p -> p.ProjectName, $"{p.ProjectType}", p.AbsolutePath)


    module private Project =
        open System.IO
        open Microsoft.Build.Definition

        let load globalProps (path: string) =
            Project.FromFile(path, ProjectOptions(GlobalProperties = Map globalProps))

        let captureCompile (projectPath: string) =
            let projectPath = Path.GetFullPath projectPath

            let targetFrameworks =
                List.distinct [
                    for p in ProjectRootElement.Open(projectPath).Properties do
                        if p.Name = "TargetFramework" && p.Value <> "" then
                            p.Value
                        else if p.Name = "TargetFrameworks" then
                            yield! p.Value.Split(';')
                ]

            let loadedProjSamePath =
                ProjectCollection.GlobalProjectCollection.LoadedProjects
                |> Seq.filter (fun p -> p.FullPath = projectPath)
                |> Seq.toList

            let baseGlobalProps =
                [ let combine fileName =
                      System.IO.Path.Combine [| System.AppDomain.CurrentDomain.BaseDirectory; fileName |]

                  "DOTNET_CLI_UI_LANGUAGE", "en-us"
                  "CustomAfterMicrosoftCommonProps", combine "CaptureCompile.props" ]

            let projs =
                [ for targetFramework in targetFrameworks do
                      let globalProps = [ yield! baseGlobalProps; "TargetFramework", targetFramework ]

                      loadedProjSamePath
                      |> List.tryFind (fun p ->
                          p.GlobalProperties.Count = List.length globalProps
                          && globalProps
                             |> List.forall (fun (key, value) ->
                                 p.GlobalProperties.ContainsKey key && p.GlobalProperties[key] = value))
                      |> Option.defaultWith (fun () -> load globalProps projectPath) ]

            [ for proj in projs do
                  let projInstance = proj.CreateProjectInstance()

                  if projInstance.Build(target = "Compile", loggers = []) then
                      Ok projInstance
                  else
                      Error "build failed" ]



    // Use Policy:ReferencePath to get the referenced assembly of the project.
    let private ofProject (proj: ProjectInstance) =
        let path = proj.FullPath
        let name = proj.GetPropertyValue("ProjectName")
        let targetPath = proj.GetPropertyValue("TargetPath")
        let targetFramework = proj.GetPropertyValue("TargetFramework")

        let referenceSources =
            proj.GetItems("ReferencePath")
            |> Seq.map (fun i ->
                let path = i.EvaluatedInclude
                let referenceSourceTarget = i.GetMetadataValue("ReferenceSourceTarget")
                let fusionName = i.GetMetadataValue("FusionName")

                { Path = path
                  ReferenceSourceTarget = referenceSourceTarget
                  FusionName = fusionName })
            |> Seq.toList

        { Name = name
          ProjectDirectory = path
          TargetPath = targetPath
          TargetFramework = targetFramework
          ReferenceSources = referenceSources }

    let loadFromProjFile (path: string) =
        Project.captureCompile path |> List.map (Result.map ofProject)

    let loadFromSlnFile (path: string) =
        Solution.parse path
        |> Seq.map (fun (name, projectType, path) ->
            match projectType with
            | "KnownToBeMSBuildFormat" -> Ok(path)
            | _ -> Error "not supported project type")
        |> Seq.collect (function
            | Ok path -> loadFromProjFile path
            | Error _ -> [])


module Watcher =
    open Avalonia.FuncUI.LiveView.Types
    open FSharp.Control
    open FSharp.Compiler.Interactive.Shell
    open System.IO
    open System.Text
    open System
    open FSharp.Compiler.IO
    open System.Collections.Concurrent
    open System.Text.RegularExpressions
    open System.Threading.Channels
    open System.Threading


    module private FsiSession =
        open System.Reflection
        open Avalonia.FuncUI.Types
        open Avalonia.Controls
        open Avalonia.FuncUI.DSL
        open Avalonia.FuncUI.VirtualDom
        open Avalonia.Media

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
                | _ when not (File.Exists fileName) && Path.GetFileName fileName = "unknown" ->
                    new MemoryStream(Encoding.UTF8.GetBytes("")) :> Stream
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

    type private WatcherMsg = EvalScript of path: string * constent: string[]

    type Service() =


        let cts = new CancellationTokenSource()
        let msgChannel = Channel.CreateUnbounded<WatcherMsg>()
        let evalResultEvent = new Event<_>()
        let logMsgEvent = new Event<LogMessage>()

        let logInfo, logErr, logDebug =

            let logger case msg =
                let timestamp = DateTimeOffset.Now.ToString "T"
                logMsgEvent.Trigger(case $"{timestamp} %s{msg}")

            logger LogInfo, logger LogError, logger LogDebug

        let mutable projectInfo: ProjectInfo option = None
        let mutable fsi: FsiEvaluationSession option = None


        let addOrUpdateFsCode path content =
            content
            // Fsi newline code for any OS is LF(\n).
            |> Array.map (fun s -> Regex.Replace(s, "\r?\n?$", "\n"))
            |> String.concat ""
            |> livePreviewFileSystem.AddOrUpdateFsCode path

        do FileSystem <- livePreviewFileSystem


        let runner =
            task {
                while not cts.IsCancellationRequested do
                    for msg in msgChannel.Reader.ReadAllAsync(cts.Token) do
                        try
                            match projectInfo, fsi, msg with
                            | None, _, _
                            | _, None, _ -> ()
                            | Some projectInfo, Some fsi, EvalScript(path, content) ->
                                logDebug $"eval {path}"
                                addOrUpdateFsCode path content

                                let res, warnings = fsi.EvalScriptNonThrowing path

                                match res with
                                | Choice1Of2() ->
                                    let control = fsi.DynamicAssemblies |> Array.last |> FsiSession.getLivePreviews
                                    logDebug $"eval {path} succeeded"
                                    Ok(path, control, warnings) |> evalResultEvent.Trigger
                                | Choice2Of2 ex ->
                                    logErr $"{typeof<Service>} failed: {ex}"
                                    Error(ex, warnings) |> evalResultEvent.Trigger

                        with ex ->
                            logErr $"{typeof<Service>} failed: {ex}"
            }

        let mutable isDisposed = false

        member _.WatchingProjectInfo = projectInfo

        member _.Watch(newProjectInfo: ProjectInfo) =
            match projectInfo with
            | Some current when current = newProjectInfo -> ()
            | _ ->
                Disposable.tryDispose fsi
                projectInfo <- Some newProjectInfo
                fsi <- Some(FsiSession.ofProjectInfo newProjectInfo)

        member _.UnWatch() =
            Disposable.tryDispose fsi
            projectInfo <- None
            fsi <- None


        member _.RequestEval(path, content) =
            let msg = EvalScript(path, content)

            while not (msgChannel.Writer.TryWrite msg) do
                ()

        [<CLIEvent>]
        member _.OnEvalResult = evalResultEvent.Publish

        [<CLIEvent>]
        member _.OnLogMsg = logMsgEvent.Publish

        interface IDisposable with
            member this.Dispose() =
                if not isDisposed then
                    isDisposed <- true

                    if
                        msgChannel.Writer.TryComplete()
                        && not runner.IsCompleted
                        && not (runner.Wait(1_000))
                    then
                        cts.Cancel()

                    cts.Dispose()
                    this.UnWatch()
                    FileSystem <- defaultFileSystem