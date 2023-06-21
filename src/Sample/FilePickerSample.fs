module Sample.FilePickerSample


module Menu =
    open System.IO
    open Avalonia
    open Avalonia.Controls
    open Avalonia.FuncUI
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.LiveView
    open Avalonia.FuncUI.LiveView.Core.Types
    open Avalonia.Layout
    open Avalonia.Platform.Storage
    open Avalonia.Media
    open Avalonia.FuncUI.Types
 
    let create id =
        Component.create (
            $"live-view-menu-{id}",
            fun ctx ->
                let binlogPath = ctx.useState @"C:\workspace\work\Avalonia.FuncUI.LiveView\o2.binlog"
                let loadProjInfo binlog ()=
                    let file = FileInfo binlogPath.Current
                    if file.Exists && file.Extension = ".binlog" then
                        MSBuildBinLog.getFscArgs file.FullName
                        |> Seq.toList
                    else []

                let projs =
                    loadProjInfo binlogPath.Current 
                    |>ctx.useStateLazy
                let selectedProj = ctx.useState None

                ctx.useEffect (
                    (fun () ->
                        let file = FileInfo binlogPath.Current

                        if file.Exists && file.Extension = ".binlog" then
                            MSBuildBinLog.getFscArgs file.FullName |> Seq.toList |> projs.Set),
                    [ EffectTrigger.AfterInit; EffectTrigger.AfterChange binlogPath ]
                )


                let buttonWidth = 100
                let centerText text =
                    TextBlock.create  [
                                        TextBlock.textAlignment TextAlignment.Center
                                        TextBlock.text text]

                DockPanel.create [
                    DockPanel.margin 8
                    DockPanel.children [
                        StackPanel.create [
                            StackPanel.margin 8
                            StackPanel.dock Dock.Top
                            StackPanel.spacing 4
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                Button.create [
                                    Button.width buttonWidth
                                    Button.content (centerText"Load Binlog")
                                    Button.onClick (fun e ->
                                        task {
                                            match! LiveViewMenu.openDllPicker ctx.control with
                                            | Some picked -> binlogPath.Set(picked.Path.AbsolutePath)
                                            | None -> ()
                                        }
                                        |> ignore)
                                ]
                                TextBox.create [ TextBox.text binlogPath.Current ]
                            ]
                        ]
                        StackPanel.create [
                            StackPanel.margin 8
                            StackPanel.dock Dock.Top
                            StackPanel.spacing 4
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                Button.create [
                                    
                                    Button.content (centerText "Load Project")
                                    Button.width buttonWidth
                                    Button.isEnabled (Option.isSome selectedProj.Current)
                                    Button.onClick (fun e ->
                                        task {
                                            match! LiveViewMenu.openDllPicker ctx.control with
                                            | Some picked -> binlogPath.Set(picked.Path.AbsolutePath)
                                            | None -> ()
                                        }
                                        |> ignore)
                                ]
                                ComboBox.create [
                                    ComboBox.dock Dock.Top
                                    ComboBox.dataItems projs.Current
                                    ComboBox.itemTemplate (
                                        DataTemplateView<ProjArgsInfo>.create (fun p ->
                                            TextBlock.create [ TextBlock.text p.Name ])
                                    )
                                    ComboBox.onSelectedItemChanged(function
                                    | :? ProjArgsInfo as p -> selectedProj.Set (Some p)
                                    | _ -> ()
                                    )
                                ]
                            ]
                        ]
                    ]

                ]
        )

    [<LivePreview>]
    let preview () = create "draft"