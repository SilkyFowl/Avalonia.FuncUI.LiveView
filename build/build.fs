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

module Paket =
    open PaketTemplate

    let private baseParams =
        { DefaultPaketTemplateParams with
            TemplateType = Project
            Version = Some release.NugetVersion
            ReleaseNotes = release.Notes
            Description = projectDescription
            Authors = authors
            ProjectUrl = Some projectUrl
            Files = [ Include(".." </> ".." </> "LICENSE.md", "") ] }

    let settings =
        let toTemplateFilePath projectPath = Some(projectPath @@ "paket.template")

        [ {| projSetting = analyzerProjSetting
             templateParams =
              { baseParams with
                  TemplateFilePath = toTemplateFilePath analyzerProjSetting.Path
                  Id = Some analyzerProjSetting.PackageId
                  Files =
                      baseParams.Files
                      @ [ Include("bin" </> "Release" </> "net6.0" </> "publish", "lib" </> "net6.0") ] } |}
          {| projSetting = liveViewProjSetting
             templateParams =
              { baseParams with
                  TemplateFilePath = toTemplateFilePath liveViewProjSetting.Path
                  Id = Some liveViewProjSetting.PackageId } |} ]

module Nuspec =
    open System.Xml.Linq
    open System.IO.Compression

    let addLicense (proj: ProjSetting) =
        let xn = XName.Get

        let ns localName =
            XName.Get(localName, "http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd")

        let unzipedPath =
            outputPath
            </> $"{proj.PackageId}.{release.NugetVersion}"

        let nupkgPath = $"{unzipedPath}.nupkg"

        let nuspecPath = unzipedPath </> $"{proj.PackageId}.nuspec"

        Zip.unzip unzipedPath nupkgPath

        let nupkgDoc = XDocument.Load nuspecPath

        nupkgDoc.Descendants(ns "authors")
        |> Seq.iter (fun metadata ->
            metadata.AddAfterSelf(
                XElement(ns "requireLicenseAcceptance", "false"),
                XElement(ns "license", XAttribute(xn "type", "file"), "LICENSE.md"),
                XElement(ns "licenseUrl", "https://aka.ms/deprecateLicenseUrl")
            ))

        if proj = analyzerProjSetting then
            nupkgDoc.Descendants(ns "dependency")
            |> Seq.filter (fun dependency -> dependency.Attribute(xn "id").Value <> "FSharp.Analyzers.SDK")
            |> Seq.toList
            |> List.iter (fun dependency -> dependency.Remove())

        nupkgDoc.Save nuspecPath
        Shell.rm nupkgPath

        ZipFile.CreateFromDirectory(unzipedPath, nupkgPath)
        Shell.deleteDir unzipedPath

#nowarn "20"
let initTargets () =
    Target.initEnvironment ()

    // ****************************************************************************************************
    // --------------------------------------------- Targets ---------------------------------------------
    // ****************************************************************************************************

    Target.create "CleanDebug" (fun _ -> !! "src/**/Debug" ++ outputPath |> Shell.cleanDirs)

    Target.create "CleanRelease" (fun _ ->
        !! "src/**/Release" ++ outputPath
        |> Shell.cleanDirs)


    Target.create "BuildDebug" (fun _ ->
        slnPath
        |> DotNet.build (fun setParams -> { setParams with Configuration = DotNet.Debug }))

    Target.create "BuildRelease" (fun _ ->
        slnPath
        |> DotNet.build (fun setParams -> { setParams with Configuration = DotNet.Release }))

    Target.create "Pack" (fun _ ->
        for setting in Paket.settings do

            PaketTemplate.create (fun _ -> setting.templateParams)

            setting.projSetting.Path
            |> DotNet.publish (fun opts ->
                { opts with
                    Configuration = DotNet.Release
                    SelfContained = Some false
                    Framework = Some "net6.0" })

            Paket.pack (fun p ->
                { p with
                    ToolType = ToolType.CreateLocalTool()
                    TemplateFile = Option.toObj setting.templateParams.TemplateFilePath
                    OutputPath = outputPath
                    MinimumFromLockFile = true
                    IncludeReferencedProjects = true })

            Nuspec.addLicense setting.projSetting)

    Target.create "ClearLocalAnalyzer" (fun _ ->
        !!outputPath ++ localanalyzerPath
        |> Shell.cleanDirs)

    Target.create "SetLocalAnalyzer" (fun _ ->
        let analyzerId = analyzerProjSetting.PackageId

        outputPath
        |> Directory.findFirstMatchingFile $"{analyzerId}.*"
        |> Zip.unzip (localanalyzerPath </> analyzerId))

    Target.create "Default" ignore

    // ****************************************************************************************************
    // --------------------------------------- Targets Dependencies ---------------------------------------
    // ****************************************************************************************************

    "CleanDebug" ==> "BuildDebug" ==> "Default"

    "CleanRelease"
    ==> "ClearLocalAnalyzer"
    ==> "BuildRelease"
    ==> "Pack"
    ==> "SetLocalAnalyzer"

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