namespace Avalonia.FuncUI.LiveView

open Avalonia.FuncUI.LiveView.Types.PreviewApp

module MSBuildLocator =
    open Microsoft.Build.Locator
    let registerInstanceMaxVersion() =
        MSBuildLocator.QueryVisualStudioInstances()
        |> Seq.maxBy(fun i -> i.Version)
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

        let runProc onStdOut onStdErr startDir filename args =

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

    let loggerAsmPath = typeof<BinaryLogger>.Assembly.Location

    let buildWithBinLog onStdOut onStdErr path binlogPath =
        let fileInfo = FileInfo path
        let binlog = FileInfo binlogPath

        Helper.runProc onStdOut onStdErr (Some fileInfo.Directory.FullName) "dotnet" [
            "build"
            "-t:Rebuild"
            "-v:diag"
            fileInfo.FullName
            $"-l:BinaryLogger,\"{loggerAsmPath}\";{binlog.FullName}"
        ]


    let findProperty propertyName (tn: TreeNode) =
        tn.FindChildrenRecursive<Property>(fun p -> p.Name = propertyName)
        |> Seq.tryHead
        |> Option.map (fun p -> p.Value)

    let getFscArgs binlog =
        let build = BinaryLog.ReadBuild binlog

        build.FindChildrenRecursive<FscTask>(fun _ -> true)
        |> Seq.choose (fun fscTask ->
            option {
                let! proj = fscTask.GetNearestParent<Project>() |> Option.ofObj
                let! evaluation = build.FindEvaluation proj.EvaluationId |> Option.ofObj

                let! targetPath = evaluation |> findProperty "TargetPath"
                let! targetFramework = evaluation |> findProperty "TargetFramework"
                let! dotnetHostPath = evaluation |> findProperty "DOTNET_HOST_PATH"
                let! dotnetFscCompilerPath = evaluation |> findProperty "DotnetFscCompilerPath"

                let! argsMsg =
                    fscTask.FindLastChild<Message>(fun m -> m.IsTextShortened && m.Text.Contains dotnetFscCompilerPath)
                    |> Option.ofObj
                    |> Option.map (fun m -> m.Text.ReplaceLineEndings().Split System.Environment.NewLine)

                return {
                    Name = proj.Name
                    ProjectDirectory = proj.ProjectDirectory
                    TargetPath = targetPath
                    TargetFramework = targetFramework
                    DotnetHostPath = dotnetHostPath
                    DotnetFscCompilerPath = dotnetFscCompilerPath
                    Args = argsMsg
                }
            })
        |> Seq.toList