namespace Avalonia.FuncUI.LiveView

module MSBuildLocator =
    open Microsoft.Build.Locator
    open System.Runtime.CompilerServices

    /// Returns true if instance of MSBuild found on the machine is registered.
    let inline isRegistered () = MSBuildLocator.IsRegistered

    /// Registers the highest version instance of MSBuild found on the machine.
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let register () =
        let visualStudioInstances = MSBuildLocator.QueryVisualStudioInstances()
        let instance = visualStudioInstances |> Seq.maxBy (fun i -> i.Version)

        if MSBuildLocator.CanRegister then
            MSBuildLocator.RegisterInstance(instance)

    /// Registers the highest version instance of MSBuild found on the machine if it is not already registered.
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let registerIfNotRegistered () =
        if not MSBuildLocator.IsRegistered then
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