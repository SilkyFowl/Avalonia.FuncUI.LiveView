#r "paket:
nuget Fake.Core.ReleaseNotes
nuget Fake.DotNet.Cli
nuget Fake.DotNet.Paket
nuget Fake.IO.FileSystem
nuget Fake.IO.Zip
nuget Fake.Core.Target //"
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

let analyzerProjName = "Avalonia.FuncUI.LiveView.Analyzer"
let analyzerPath = srcPath @@ analyzerProjName
let locallocalanalyzerPath = "./localanalyzers"

// properties
let projectDescription = ["Live fs/fsx previewer for Avalonia.FuncUI."]
let gitUserName = "SilkyFowl"
let authors = [gitUserName]
let projectUrl = $"https://github.com/{gitUserName}/Avalonia.FuncUI.LiveView"

let release = ReleaseNotes.load "RELEASE_NOTES.md"


Target.initEnvironment ()

// ****************************************************************************************************
// --------------------------------------------- Targets ---------------------------------------------
// ****************************************************************************************************

Target.create "Clean" (fun _ ->
    !! "src/**/bin" ++ "src/**/obj" ++ outputPath
    |> Shell.cleanDirs)


Target.create "Build" (fun _ -> DotNet.build id "./Avalonia.FuncUI.LiveView.sln")

Target.create "PackAnalyzer" (fun _ ->

    let templateFilePath = analyzerPath @@ "paket.template"

    PaketTemplate.create (fun p ->
        { p with
            TemplateType = PaketTemplate.Project
            TemplateFilePath = Some templateFilePath
            Id = Some analyzerProjName
            Version = Some release.NugetVersion
            ReleaseNotes = release.Notes
            Description = projectDescription
            Authors = authors
            ProjectUrl = Some projectUrl
            Files = [ PaketTemplate.Include("bin" </> "Release" </> "net6.0" </> "publish", "lib" </> "net6.0") ] })
    
    ["licenseExpression MIT"]
    |> File.append templateFilePath

    analyzerPath
    |> DotNet.publish (fun opts ->
        { opts with
            Configuration = DotNet.Release
            Framework = Some "net6.0" })

    Paket.pack (fun p ->
        { p with
            ToolType = ToolType.CreateLocalTool()
            OutputPath = outputPath }))


Target.create "ClearLocalAnalyzer" (fun _ ->
    !!outputPath ++ locallocalanalyzerPath
    |> Shell.cleanDirs)

Target.create "SetLocalAnalyzer" (fun _ ->
    outputPath
    |> Directory.findFirstMatchingFile $"{analyzerProjName}.*"
    |> Zip.unzip (locallocalanalyzerPath </> analyzerProjName))

Target.create "Default" ignore

// ****************************************************************************************************
// --------------------------------------- Targets Dependencies ---------------------------------------
// ****************************************************************************************************

"Clean" ==> "Build" ==> "Default"

"ClearLocalAnalyzer"
==> "PackAnalyzer"
==> "SetLocalAnalyzer"

Target.runOrDefault "Default"