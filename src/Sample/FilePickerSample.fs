module Sample.FilePickerSample


module Menu =
    open System.IO
    open Avalonia
    open Avalonia.Controls
    open Avalonia.FuncUI
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.LiveView.Attribute
    open Avalonia.Layout
    open Avalonia.Platform.Storage
    open Avalonia.Media
    open Avalonia.FuncUI.Types

    let openDllPicker ctr =
        task {
            let provier = TopLevel.GetTopLevel(ctr).StorageProvider
            let! location = provier.TryGetWellKnownFolderAsync(WellKnownFolder.Documents)

            let! result =
                provier.OpenFilePickerAsync(
                    FilePickerOpenOptions(
                        Title = "Open...",
                        SuggestedStartLocation = location,
                        AllowMultiple = false,
                        FileTypeFilter = [
                            FilePickerFileType(
                                "Binary Log",
                                Patterns = [ "*.binlog"; "*.buildlog" ],
                                MimeTypes = [ "application/binlog"; "application/buildlog" ],
                                AppleUniformTypeIdentifiers = [ "public.data" ]
                            )
                        ]
                    )
                )

            match List.ofSeq result with
            | [ picked ] -> return Some picked
            | _ -> return None
        }

    let create id =
        Component.create (
            $"live-view-menu-{id}",
            fun ctx ->
                let binlogPath = ctx.useState @""

                let loadProjInfo binlog () =
                    let file = FileInfo binlogPath.Current

                    if file.Exists && file.Extension = ".binlog" then [] else []

                let projs = loadProjInfo binlogPath.Current |> ctx.useStateLazy
                let selectedProj = ctx.useState None

                ctx.useEffect (
                    (fun () ->
                        let file = FileInfo binlogPath.Current

                        if file.Exists && file.Extension = ".binlog" then
                            ()),
                    [ EffectTrigger.AfterInit; EffectTrigger.AfterChange binlogPath ]
                )

                let buttonWidth = 100

                let centerText text =
                    TextBlock.create [ TextBlock.textAlignment TextAlignment.Center; TextBlock.text text ]

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
                                    Button.content (centerText "Load Binlog")
                                    Button.onClick (fun e ->
                                        task {
                                            match! openDllPicker ctx.control with
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
                                            match! openDllPicker ctx.control with
                                            | Some picked -> binlogPath.Set(picked.Path.AbsolutePath)
                                            | None -> ()
                                        }
                                        |> ignore)
                                ]
                            ]
                        ]
                    ]

                ]
        )

    [<LivePreview>]
    let preview () = create "draft"