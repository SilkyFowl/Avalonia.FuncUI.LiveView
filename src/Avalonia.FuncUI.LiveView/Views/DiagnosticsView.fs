module Avalonia.FuncUI.LiveView.Views.DiagnosticsView

open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

open Avalonia.FuncUI.DSL

open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.Types.Watcher

let create (diagnostics: Diagnostic list) attrs =
    Expander.create [
        yield! attrs
        Expander.header (TextBlock.create [ TextBlock.text "Diagnostics"; TextBlock.fontWeight FontWeight.SemiBold ])
        let group =
            diagnostics
            |> List.groupBy (fun x -> x.Range.FileName)
            |> List.map (fun (fileName, diagnostics) -> fileName, diagnostics |> List.sortBy (fun x -> x.Range))

        let hasDiagnostic = diagnostics |> List.isEmpty |> not

        Expander.isExpanded hasDiagnostic

        Expander.content (
            TreeView.create [
                TreeView.isOpen hasDiagnostic
                TreeView.viewItems [
                    for (fileName, diagnostics) in group do
                        TreeViewItem.create [
                            TreeViewItem.isExpanded hasDiagnostic
                            TreeViewItem.header (
                                StackPanel.create [
                                    StackPanel.spacing 9
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.children [
                                        TextBlock.create [
                                            TextBlock.text fileName
                                            TextBlock.fontWeight FontWeight.SemiBold
                                        ]
                                        TextBlock.create [
                                            TextBlock.text $"%d{diagnostics.Length}"
                                            TextBlock.foreground Brushes.Gray
                                        ]
                                    ]
                                ]
                            )
                            TreeViewItem.viewItems [
                                for diagnostic in diagnostics do
                                    StackPanel.create [
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.spacing 8
                                        StackPanel.children [
                                            PathIcon.create [
                                                PathIcon.width 12
                                                match diagnostic.Severity with
                                                | DiagnosticSeverity.Error ->
                                                    PathIcon.data StreamGeometry.error
                                                    PathIcon.foreground Brushes.Red
                                                | DiagnosticSeverity.Warning ->
                                                    PathIcon.data StreamGeometry.warning
                                                    PathIcon.foreground Brushes.Orange
                                                | DiagnosticSeverity.Info ->
                                                    PathIcon.data StreamGeometry.info
                                                    PathIcon.foreground Brushes.Blue
                                                | DiagnosticSeverity.Hidden ->
                                                    PathIcon.data StreamGeometry.question
                                                    PathIcon.foreground Brushes.Gray
                                            ]
                                            TextBlock.create [

                                                TextBlock.text diagnostic.Message
                                                TextBlock.verticalAlignment VerticalAlignment.Center
                                                TextBlock.textWrapping TextWrapping.Wrap
                                            ]
                                            TextBlock.create [
                                                let errorNumber = diagnostic.ErrorNumber.ErrorNumberText

                                                let { Range = { Start = { Line = ln; Column = col } } } = diagnostic

                                                TextBlock.text $"{errorNumber} [Ln %d{ln}, Col %d{col}]"
                                                TextBlock.verticalAlignment VerticalAlignment.Center
                                                TextBlock.foreground Brushes.Gray
                                            ]
                                        ]
                                        StackPanel.onKeyDown (fun e ->
                                            match e.Source, e.Key, e.KeyModifiers with
                                            | :? Control as ct, Key.C, KeyModifiers.Control ->
                                                backgroundTask {
                                                    let clipboard = (TopLevel.GetTopLevel ct).Clipboard

                                                    do! clipboard.SetTextAsync diagnostic.Message
                                                }
                                                |> ignore

                                                e.Handled <- true

                                            | _ -> ())
                                    ]
                            ]
                        ]
                ]
            ]
        )
    ]

[<LivePreview>]
let preview () =
    create [
        { ErrorNumber =
            { ErrorNumber = 123
              ErrorNumberPrefix = "FC" }
          Range =
            { FileName = "test.fs"
              Start = { Line = 10; Column = 1 }
              End = { Line = 12; Column = 8 } }
          Severity = DiagnosticSeverity.Error
          Message =
            "This is a long test error message. The value 'foo' is not defined. Maybe you want one of the following: 'bar', 'baz', 'qux'."
          Subcategory = "TEST" }
        { ErrorNumber =
            { ErrorNumber = 456
              ErrorNumberPrefix = "FC" }
          Range =
            { FileName = "test.fs"
              Start = { Line = 14; Column = 1 }
              End = { Line = 16; Column = 8 } }
          Severity = DiagnosticSeverity.Warning
          Message = "This is a test warning message."
          Subcategory = "TEST" }
        { ErrorNumber =
            { ErrorNumber = 789
              ErrorNumberPrefix = "FC" }
          Range =
            { FileName = "test.fs"
              Start = { Line = 18; Column = 1 }
              End = { Line = 20; Column = 8 } }
          Severity = DiagnosticSeverity.Info
          Message = "This is a test info message."
          Subcategory = "TEST" }
        { ErrorNumber =
            { ErrorNumber = 123
              ErrorNumberPrefix = "FC" }
          Range =
            { FileName = "test.fs"
              Start = { Line = 22; Column = 1 }
              End = { Line = 24; Column = 8 } }
          Severity = DiagnosticSeverity.Hidden
          Message = "This is a test hidden message."
          Subcategory = "TEST" }
    ] []