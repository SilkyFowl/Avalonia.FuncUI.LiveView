open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open System.Runtime.InteropServices
open System.Text.RegularExpressions

// ****************************************************************************************************
// ------------------------------------------- Definitions -------------------------------------------
// ****************************************************************************************************

let srcPath = "./src"
let outputPath = "./dist"
let slnPath = "./Avalonia.FuncUI.LiveView.sln"
let localanalyzerPath = "./localanalyzers"

// properties
let projectDescription = [ "Live fs/fsx previewer for Avalonia.FuncUI." ]
let gitUserName = "SilkyFowl"
let authors = [ gitUserName ]
let projectUrl = $"https://github.com/{gitUserName}/Avalonia.FuncUI.LiveView"

let release = ReleaseNotes.load "RELEASE_NOTES.md"

type ProjSetting =
    { Name: string }

    member this.Path = srcPath @@ this.Name
    member this.PackageId = $"{gitUserName}.{this.Name}"

let analyzerProjSetting = { Name = "Avalonia.FuncUI.LiveView.Analyzer" }
let liveViewProjSetting = { Name = "Avalonia.FuncUI.LiveView" }


module Proc =
    let runSimple path args =
        CreateProcess.fromRawCommand path args
        |> CreateProcess.redirectOutput
        |> CreateProcess.ensureExitCode
        |> Proc.run
        |> fun result -> result.Result.Output

module String =
    let splitByLinebreak (str: string) = str.Split System.Environment.NewLine

module DotNet =
    let isIntalledLocalTool toolPackageId version toolPath =
        let regex = String.getRegEx $"^{String.toLower toolPackageId}\s+%s{version}"

        let args =
            let baseArgs = [ "tool"; "list" ]

            match toolPath with
            | Some toolPath -> baseArgs @ [ "--tool-path"; toolPath ]
            | None -> baseArgs

        Proc.runSimple "dotnet" args
        |> fun output -> output.Split System.Environment.NewLine
        |> Array.exists regex.IsMatch

    let installTool toolId version toolPath =
        let msg, args =
            let baseMsg = $"dotnet tool install {toolId} --version {version}"
            let baseArgs = [ "tool"; "install"; toolId; "--version"; version ]

            match toolPath with
            | Some toolPath -> $"{baseMsg} --tool-path {toolPath}", baseArgs @ [ "--tool-path"; toolPath ]
            | None -> baseMsg, baseArgs

        Trace.logfn "installTool: %s" msg

        Proc.runSimple "dotnet" args |> Trace.logfn "installTool result: %s"

    let uninstallTool toolId toolPath =
        let msg, args =
            let baseMsg = $"dotnet tool uninstall {toolId}"
            let baseArgs = [ "tool"; "uninstall"; toolId ]

            match toolPath with
            | Some toolPath -> $"{baseMsg} --tool-path {toolPath}", baseArgs @ [ "--tool-path"; toolPath ]
            | None -> baseMsg, baseArgs

        Trace.logfn "uninstallTool: %s" msg

        Proc.runSimple "dotnet" args |> Trace.logfn "uninstallTool result: %s"

module Nuget =

    /// Get nuget's global-packages path
    let getGlobalPackagesPath () =
        let header = "global-packages: "
        let regex = Regex $"(?<=({header}))([^\r\n]*)"

        let result =
            Proc.runSimple "dotnet" [ "nuget"; "locals"; "global-packages"; "--list" ]

        regex.Match(result).Value

    /// Get library's directory path from nuget's global-packages cache
    let tryGetGlobalPackagesCacheDir version packageId =
        let globalPackagesPath = getGlobalPackagesPath ()
        let cachePath = globalPackagesPath </> packageId </> version

        Trace.logfn "chchePath: %s" cachePath

        if Shell.testDir cachePath then Some cachePath else None

    /// If library's directory path exists in nuget's global-packages cache, delete it
    let tryClearGlobalPackagesCacheDir version packageId =
        let cachePath = tryGetGlobalPackagesCacheDir version packageId

        match cachePath with
        | Some path -> Shell.deleteDir path
        | None -> ()

let initTargets () =
    Target.initEnvironment ()

    // ****************************************************************************************************
    // --------------------------------------------- Targets ---------------------------------------------
    // ****************************************************************************************************

    Target.create "CleanDebug" (fun _ ->
        slnPath
        |> DotNet.msbuild (fun p ->
            { p with
                MSBuildParams =
                    { p.MSBuildParams with
                        Targets = [ "Clean" ]
                        Properties = [ "Configuration", "Debug" ] } }))

    Target.create "CleanRelease" (fun _ ->
        slnPath
        |> DotNet.msbuild (fun p ->
            { p with
                MSBuildParams =
                    { p.MSBuildParams with
                        Targets = [ "Clean" ]
                        Properties = [ "Configuration", "Release" ] } }))

    Target.create "CleanOutput" (fun _ -> !!outputPath |> Shell.cleanDirs)


    Target.create "BuildDebug" (fun _ ->
        slnPath
        |> DotNet.build (fun setParams ->
            { setParams with
                Configuration = DotNet.Debug }))

    Target.create "BuildRelease" (fun _ ->
        slnPath
        |> DotNet.build (fun setParams ->
            { setParams with
                Configuration = DotNet.Release }))

    Target.create "TestRelease" (fun _ ->
        slnPath
        |> DotNet.test (fun p ->
            { p with
                NoBuild = true
                Configuration = DotNet.BuildConfiguration.Release }))

    Target.create "UninstallAnalyerAsLocalTool" (fun _ ->
        let packageId = analyzerProjSetting.PackageId
        let version = release.NugetVersion

        if DotNet.isIntalledLocalTool packageId version None then
            DotNet.uninstallTool packageId None)

    Target.create "ClearAnalyerNugetGlobalPackagesCache" (fun _ ->

        match Nuget.tryGetGlobalPackagesCacheDir release.NugetVersion analyzerProjSetting.PackageId with
        | Some path ->
            Trace.logfn "Nuget global-packages cache found: %s" path
            Nuget.tryClearGlobalPackagesCacheDir release.NugetVersion analyzerProjSetting.PackageId
            Trace.logfn "Nuget global-packages cache cleared: %s" path
        | None ->
            Trace.logfn
                "Nuget global-packages cache not found: %s %s"
                analyzerProjSetting.PackageId
                release.NugetVersion)

    Target.create "InstallAnalyerAsLocalTool" (fun _ ->
        DotNet.installTool analyzerProjSetting.PackageId release.NugetVersion None)

    Target.create "BuildReleaseWithPack" (fun _ ->
        slnPath
        |> DotNet.build (fun p ->
            { p with
                Configuration = DotNet.Release
                MSBuildParams =
                    { p.MSBuildParams with
                        Properties = [ "GeneratePackageOnBuild", "true" ] } }))

    Target.create "ClearLocalAnalyzer" (fun _ ->
        let analyzaer =
            if Environment.isWindows then
                "funcui-analyser.exe"
            else
                "funcui-analyser"

        let analyserPath = localanalyzerPath </> analyzaer

        if Shell.testFile analyserPath then
            Some localanalyzerPath |> DotNet.uninstallTool analyzerProjSetting.PackageId)

    Target.create "SetLocalAnalyzer" (fun _ ->
        Some localanalyzerPath
        |> DotNet.installTool analyzerProjSetting.PackageId release.NugetVersion)

    Target.create "RebuildDebug" ignore
    Target.create "RebuildRelease" ignore
    Target.create "RebuildAll" ignore
    Target.create "RebuildReleaseWithPack" ignore
    Target.create "ReInstallAnalyerAsLocalTool" ignore
    Target.create "Pack" ignore
    Target.create "PackAndSetLocalAnalyzer" ignore
    Target.create "Default" ignore

    // ****************************************************************************************************
    // --------------------------------------- Targets Dependencies ---------------------------------------
    // ****************************************************************************************************

    "ClearLocalAnalyzer" ?=> "SetLocalAnalyzer" |> ignore

    "UninstallAnalyerAsLocalTool"
    ?=> "ClearAnalyerNugetGlobalPackagesCache"
    ?=> "InstallAnalyerAsLocalTool"
    |> ignore

    "CleanDebug" ?=> "BuildDebug" |> ignore
    "CleanRelease" ?=> "BuildRelease" |> ignore

    "BuildRelease" ?=> "TestRelease" |> ignore

    "CleanRelease" ?=> "BuildReleaseWithPack" |> ignore
    "CleanOutput" ?=> "BuildReleaseWithPack" |> ignore

    "BuildRelease" ?=> "TestRelease" |> ignore
    "BuildReleaseWithPack" ?=> "TestRelease" |> ignore

    "Pack" ?=> "SetLocalAnalyzer" |> ignore
    "Pack" ?=> "InstallAnalyerAsLocalTool" |> ignore

    "RebuildDebug" <== [ "CleanDebug"; "BuildDebug" ]

    "RebuildRelease" <== [ "CleanRelease"; "BuildRelease" ]

    "RebuildAll" <== [ "RebuildDebug"; "RebuildRelease" ]

    "RebuildReleaseWithPack"
    <== [ "BuildReleaseWithPack"; "CleanRelease"; "CleanOutput" ]

    "Pack" <== [ "TestRelease"; "RebuildReleaseWithPack" ]

    "SetLocalAnalyzer" <== [ "ClearLocalAnalyzer" ]

    "ReInstallAnalyerAsLocalTool"
    <== [ "InstallAnalyerAsLocalTool"
          "UninstallAnalyerAsLocalTool"
          "ClearAnalyerNugetGlobalPackagesCache" ]

    "PackAndSetLocalAnalyzer" <== [ "Pack"; "SetLocalAnalyzer" ]

//-----------------------------------------------------------------------------
// Target Start
//-----------------------------------------------------------------------------

[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> Context.FakeExecutionContext.Create false "build.fsx"
    |> Context.RuntimeContext.Fake
    |> Context.setExecutionContext

    initTargets ()
    Target.runOrDefaultWithArguments "Default"

    0 // return an integer exit code