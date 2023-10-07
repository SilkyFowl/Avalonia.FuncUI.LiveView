namespace Avalonia.FuncUI.LiveView

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

        let captureCompile (projectPath: string) =
            let proj = Project(projectPath)

            [ let combine fileName =
                  System.IO.Path.Combine [| System.AppDomain.CurrentDomain.BaseDirectory; fileName |]

              "DOTNET_CLI_UI_LANGUAGE", "en-us"
              "CustomAfterMicrosoftCommonProps", combine "CaptureCompile.props" ]
            |> List.iter (fun (k, v) -> proj.SetGlobalProperty(k, v) |> ignore)

            let projInstance = proj.CreateProjectInstance(ProjectInstanceSettings.None)

            if projInstance.Build(target = "Compile", loggers = []) then
                Ok projInstance
            else
                Error "build failed"


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
        Project.captureCompile path |> Result.map ofProject

    let loadFromSlnFile (path: string) =
        Solution.parse path
        |> Seq.map (fun (name, projectType, path) ->
            match projectType with
            | "KnownToBeMSBuildFormat" -> Project.captureCompile path |> Result.map ofProject
            | _ -> Error "not supported project type")