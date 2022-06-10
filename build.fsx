#if FAKE
#r "paket:
nuget Fake.Core.ReleaseNotes
nuget Fake.DotNet.Cli
nuget Fake.DotNet.Paket
nuget Fake.IO.FileSystem
nuget Fake.IO.Zip
nuget Fake.Core.Target //"
#endif
#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

// ****************************************************************************************************
// ------------------------------------------- Definitions -------------------------------------------
// ****************************************************************************************************

let srcPath = "./src"
let outputPath = "./dist"

// properties
let projectDescription = [ "Live fs/fsx previewer for Avalonia.FuncUI." ]
let gitUserName = "SilkyFowl"
let authors = [ gitUserName ]
let projectUrl = $"https://github.com/{gitUserName}/Avalonia.FuncUI.LiveView"

let release = ReleaseNotes.load "RELEASE_NOTES.md"


module AnalyzerProjInfo =
    let name = "Avalonia.FuncUI.LiveView.Analyzer"
    let path = srcPath @@ name
    let localanalyzerPath = "./localanalyzers"

module LiveViewProjInfo =
    let name = "Avalonia.FuncUI.LiveView"
    let path = srcPath @@ name

let toTemplateFilePath projectPath = Some(projectPath @@ "paket.template")

let idWithGitUserName projectName = $"{gitUserName}.{projectName}"

module Paket =
    open PaketTemplate

    let private baseParams =
        { DefaultPaketTemplateParams with
            TemplateType = Project
            Version = Some release.NugetVersion
            ReleaseNotes = release.Notes
            Description = projectDescription
            Authors = authors
            ProjectUrl = Some projectUrl }

    let settings =
        [ {| path = AnalyzerProjInfo.path
             templateParams =
              { baseParams with
                  TemplateFilePath = toTemplateFilePath AnalyzerProjInfo.path
                  Id = (idWithGitUserName >> Some) AnalyzerProjInfo.name
                  Files = [ Include("bin" </> "Release" </> "net6.0" </> "publish", "lib" </> "net6.0") ] } |}
          {| path = LiveViewProjInfo.path
             templateParams =
              { baseParams with
                  TemplateFilePath = toTemplateFilePath LiveViewProjInfo.path
                  Id = (idWithGitUserName >> Some) LiveViewProjInfo.name } |} ]


module PaketTemplate =
    let createWithLicenseExpression licenseExpression (paketTemplateParams: PaketTemplate.PaketTemplateParams) =
        match paketTemplateParams.TemplateFilePath with
        | None -> invalidArg "paketTemplateParams" "must set TemplateFilePath."
        | Some templateFilePath ->
            PaketTemplate.create (fun _ -> paketTemplateParams)

            File.append templateFilePath [ $"licenseExpression {licenseExpression}" ]

    let createWithLicenseMIT = createWithLicenseExpression "MIT"

Target.initEnvironment ()

// ****************************************************************************************************
// --------------------------------------------- Targets ---------------------------------------------
// ****************************************************************************************************

Target.create "Clean" (fun _ ->
    !! "src/**/bin" ++ "src/**/obj" ++ outputPath
    |> Shell.cleanDirs)


Target.create "Build" (fun _ -> DotNet.build id "./Avalonia.FuncUI.LiveView.sln")

Target.create "Pack" (fun _ ->
    for setting in Paket.settings do

        setting.templateParams
        |> PaketTemplate.createWithLicenseMIT

        setting.path
        |> DotNet.publish (fun opts ->
            { opts with
                Configuration = DotNet.Release
                SelfContained = Some false
                Framework = Some "net6.0" })

        let isAvalyzer = setting.path = AnalyzerProjInfo.path
        Paket.pack (fun p ->
            { p with
                ToolType = ToolType.CreateLocalTool()
                TemplateFile = Option.toObj setting.templateParams.TemplateFilePath
                OutputPath = outputPath
                MinimumFromLockFile = not isAvalyzer
                IncludeReferencedProjects = not isAvalyzer
                }))

Target.create "ClearLocalAnalyzer" (fun _ ->
    !!outputPath ++ AnalyzerProjInfo.localanalyzerPath
    |> Shell.cleanDirs)

Target.create "SetLocalAnalyzer" (fun _ ->
    let analyzerId = idWithGitUserName AnalyzerProjInfo.name

    outputPath
    |> Directory.findFirstMatchingFile $"{analyzerId}.*"
    |> Zip.unzip (AnalyzerProjInfo.localanalyzerPath </> analyzerId))

Target.create "Default" ignore

// ****************************************************************************************************
// --------------------------------------- Targets Dependencies ---------------------------------------
// ****************************************************************************************************

"Clean" ==> "Build" ==> "Default"

"ClearLocalAnalyzer"
==> "Pack"
==> "SetLocalAnalyzer"

Target.runOrDefault "Default"