module Avalonia.FuncUI.LiveView.Views.SelectProjectInfoView

open Avalonia.Controls
open Avalonia.Layout

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL

open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.Types

let create (log: LogMessage -> unit) onOpenSelectProject =
    Component.create (
        "project-info-selecter",
        fun ctx ->

            let loadedProjects = ctx.useState<list<ProjectInfo>> []

            let projectInfo = ctx.useState<ProjectInfo option> None

            let projOrSlnPath = ctx.useState<string> ""

            let configFromEnv = ctx.useStateLazy Config.tryGetFromEnv

            ctx.useEffect (
                (fun () ->
                    match configFromEnv.Current with
                    | { WatichingProjectInfo = Some { Path = IO.FilePathString path & IO.MatchExtension @"\..+proj"
                                                      TargetFramework = tf } } ->

                        ProjectInfo.loadFromProjFile path
                        |> List.tryPick (function
                            | Ok x when x.TargetFramework = tf -> Some x
                            | _ -> None)
                        |> Option.iter onOpenSelectProject
                    | _ -> ()),
                [ EffectTrigger.AfterInit ]
            )

            ctx.useEffect (
                (fun () ->
                    backgroundTask {
                        log (LogInfo "Select a project or solution file to load.")

                        match projOrSlnPath.Current with
                        | IO.FilePathString path & IO.MatchExtension @"\.sln" -> ProjectInfo.loadFromSlnFile path
                        | IO.FilePathString path & IO.MatchExtension @"\..+proj" -> ProjectInfo.loadFromProjFile path
                        | _ -> []
                        |> List.ofSeq
                        |> function
                            | [ Ok info ] -> onOpenSelectProject info
                            | results ->
                                let filtered =
                                    results
                                    |> List.choose (function
                                        | Ok x -> Some x
                                        | Error _ -> None)

                                if List.isEmpty filtered then
                                    log (LogError "No valid Project in selected file.")
                                else
                                    log (LogInfo "Loaded projects from selected file.")

                                loadedProjects.Set filtered
                    }
                    |> ignore),
                [ EffectTrigger.AfterChange projOrSlnPath ]
            )

            let openFile _ =
                backgroundTask {
                    match! FilePicker.openProjectOrSolutionFileAsync ctx.control with
                    | Some picked -> projOrSlnPath.Set picked.Path.LocalPath
                    | None -> ()
                }
                |> ignore

            let selectedProjectView =
                StackPanel.create [
                    StackPanel.orientation Orientation.Horizontal
                    StackPanel.children [
                        Button.create [
                            Button.margin 4
                            Button.content "Open project or load solution."
                            Button.onClick openFile
                        ]
                    ]
                ]

            let loadedProjectsView =
                ListBox.create [
                    ListBox.margin 4
                    ListBox.isEnabled (List.isEmpty loadedProjects.Current |> not)
                    ListBox.viewItems [
                        match loadedProjects.Current with
                        | [] ->
                            ListBoxItem.create [ ListBoxItem.isEnabled false; ListBoxItem.content "No project loaded." ]
                        | loadedProjects ->
                            for x in loadedProjects do
                                ListBoxItem.create [
                                    ListBoxItem.content $"{x.Name} - {x.TargetFramework}"
                                    ListBoxItem.onDoubleTapped (fun _ -> onOpenSelectProject x)
                                ]
                    ]
                    ListBox.onSelectedIndexChanged (fun x ->
                        if x < 0 then
                            projectInfo.Set None
                        else
                            List.tryItem x loadedProjects.Current |> projectInfo.Set)
                ]

            let openSelectedProjectButton =
                Button.create [
                    Button.isEnabled (projectInfo.Current |> Option.isSome)
                    Button.margin 4
                    Button.content "Open selected project"
                    Button.onClick (fun _ -> Option.iter onOpenSelectProject projectInfo.Current)
                ]

            StackPanel.create [
                StackPanel.orientation Orientation.Vertical
                StackPanel.children [ selectedProjectView; loadedProjectsView; openSelectedProjectButton ]
            ]
    )

[<LivePreview>]
let filePickerView () = create ignore ignore