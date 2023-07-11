module Sample.FilePickerSample


module Menu =
    open System.IO
    open Avalonia.Controls
    open Avalonia.FuncUI
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.LiveView.Attribute
    open Avalonia.Layout
    open Avalonia.Platform.Storage
    open Avalonia.Media

    let openTextPicker ctr =
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
                                "Text",
                                Patterns = [ "*.txt" ],
                                MimeTypes = [ "text/plain" ],
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
                let textPath = ctx.useState @""

                let textContent = ctx.useState ""

                let buttonWidth = 100

                let centerText text =
                    TextBlock.create [ TextBlock.textAlignment TextAlignment.Center; TextBlock.text text ]

                ctx.attrs [
                    Component.width 600
                    Component.maxHeight 200
                    Component.verticalAlignment VerticalAlignment.Stretch
                ]

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
                                    Button.content (centerText "Load Text")
                                    Button.width buttonWidth
                                    Button.onClick (fun e ->
                                        task {
                                            match! openTextPicker ctx.control with
                                            | Some picked ->
                                                textPath.Set(picked.Path.AbsolutePath)

                                                backgroundTask {
                                                    use! stream = picked.OpenReadAsync()
                                                    use reader = new StreamReader(stream)
                                                    let! content = reader.ReadToEndAsync()

                                                    textContent.Set content
                                                }
                                                |> ignore
                                            | None -> ()
                                        }
                                        |> ignore)
                                ]
                            ]
                        ]
                        ScrollViewer.create [
                            ScrollViewer.maxHeight 400
                            ScrollViewer.content (
                                TextBox.create [
                                    TextBox.text textContent.Current
                                    TextBox.verticalAlignment VerticalAlignment.Stretch
                                    TextBox.textWrapping TextWrapping.WrapWithOverflow
                                ]
                            )
                        ]
                    ]

                ]
        )

    [<LivePreview>]
    let preview () = create "draft"