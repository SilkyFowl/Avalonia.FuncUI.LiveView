namespace Avalonia.FuncUI.LiveView

open Avalonia.FuncUI.LiveView.Types.PreviewApp

module MSBuildLocator =
    open Microsoft.Build.Locator

    let registerInstanceMaxVersion () =
        MSBuildLocator.QueryVisualStudioInstances()
        |> Seq.maxBy (fun i -> i.Version)
        |> MSBuildLocator.RegisterInstance

module MSBuildBinLog =
    type private OptionBuilder() =
        member __.Bind(x, f) = Option.bind f x
        member __.Return(x) = Some x

    let private option = OptionBuilder()

    module Helper =
        open System
        open System.Diagnostics
        open System.IO

        let runProc onStdOut onStdErr startDir filename args env =

            let procStartInfo =
                ProcessStartInfo(
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    FileName = filename
                )

            args |> Seq.iter procStartInfo.ArgumentList.Add

            match startDir with
            | Some d -> procStartInfo.WorkingDirectory <- d
            | _ -> ()

            env |> Map.iter (fun k v -> procStartInfo.Environment.Add(k, v))

            let outputHandler f (_sender: obj) (args: DataReceivedEventArgs) = f args.Data
            use p = new Process(StartInfo = procStartInfo)
            p.OutputDataReceived.AddHandler(outputHandler onStdOut)
            p.ErrorDataReceived.AddHandler(outputHandler onStdErr)

            let started =
                try
                    p.Start()
                with ex ->
                    ex.Data.Add("filename", filename)
                    reraise ()

            if not started then
                failwithf "Failed to start process %s" filename

            p.BeginOutputReadLine()
            p.BeginErrorReadLine()
            p.WaitForExit()
            p.ExitCode

    open Microsoft.Build.Logging.StructuredLogger
    open System.IO
    open System
    open Microsoft.Build.Construction


    (* 
        CustomBeforeMicrosoftCommonTargets
        必要な情報を得るには
        Target。。。-t:ResolveReferencesでいける？

            DebugType = portable
        define
            DefineConstants = TRACE;DEBUG;NET;NET6_0;NETCOREAPP
        
    *)
    let resolveReferencesWithBinlog onStdOut onStdErr path binlogPath =
        let env = Map [ "DOTNET_CLI_UI_LANGUAGE", "en-us" ]

        let msbuildPath =
            let fi = FileInfo typeof<ProjectElement>.Assembly.Location
            Path.Combine [| fi.Directory.FullName; "MSBuild.dll" |]

        let fileInfo = FileInfo path

        let binlog = FileInfo binlogPath

        let property =
            let combine fileName =
                Path.Combine [| AppDomain.CurrentDomain.BaseDirectory; fileName |]

            let targets = combine "CaptureResolveReferences.targets"

            let props = combine "CaptureResolveReferences.props"

            $"-p:CustomAfterMicrosoftCommonProps={props};CustomAfterMicrosoftCommonTargets={targets}"

        let args = [
            msbuildPath
            fileInfo.FullName
            property
            "-t:CaptureResolveReferences"
            "-noConLog"
            "-nologo"
            "-maxcpucount"
            $"-bl:{binlog.FullName};ProjectImports=None"
        ]

        Helper.runProc onStdOut onStdErr (Some fileInfo.Directory.FullName) "dotnet" args env

    let findProperty propertyName (tn: TreeNode) =
        tn.FindChildrenRecursive<Property>(fun p -> p.Name = propertyName)
        |> Seq.tryHead
        |> Option.map (fun p -> p.Value)

    let getMetadataValue name (tn: TreeNode) =
        match tn.FindChild(fun (m: Metadata) -> m.Name = name) with
        | null -> None
        | m -> Some m.Value

    let findRefs (t: Target) =
        match t.FindChild("CaptureReferencePath") with
        | null -> None
        | nn when not nn.HasChildren -> None
        | nn ->
            nn.Children
            |> Seq.choose (function
                | :? Item as item ->
                    option {
                        let referenceSourceTarget = item |> getMetadataValue "ReferenceSourceTarget"
                        let fusionName = item |> getMetadataValue "FusionName"

                        return {
                            Path = item.Text
                            ReferenceSourceTarget = referenceSourceTarget
                            FusionName = fusionName
                        }
                    }
                | _ -> None)
            |> Seq.toList
            |> Some


    let getProjctInfo binlog =
        let build = BinaryLog.ReadBuild binlog

        build.FindChildrenRecursive<Target>(fun t -> t.Name = "CoreCompile" && t.Succeeded)
        |> Seq.choose (fun coreCompile ->
            option {
                let proj = coreCompile.Project
                let! evaluation = build.FindEvaluation proj.EvaluationId |> Option.ofObj
                let! targetPath = evaluation |> findProperty "TargetPath"
                let! targetFramework = evaluation |> findProperty "TargetFramework"
                let! referenceSources = findRefs coreCompile

                return {
                    Name = proj.Name
                    ProjectDirectory = proj.ProjectDirectory
                    TargetPath = targetPath
                    TargetFramework = targetFramework
                    ReferenceSources = referenceSources
                }
            })
        |> Seq.toList

