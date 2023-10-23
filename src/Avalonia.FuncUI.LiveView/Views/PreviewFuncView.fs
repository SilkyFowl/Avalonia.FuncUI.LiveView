module Avalonia.FuncUI.LiveView.Views.PreviewFuncView

open System

open Avalonia.Controls
open Avalonia.Media

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.VirtualDom

open Avalonia.FuncUI.LiveView.Types.Watcher

let rec previewFailedView (ex: exn) =
    StackPanel.create [
        StackPanel.children [
            TextBlock.create [ TextBlock.foreground Brushes.Red; TextBlock.text $"%A{ex.GetType()}" ]
            TextBlock.create [ TextBlock.foreground Brushes.Red; TextBlock.text $"%s{ex.Message}" ]
            TextBlock.create [
                TextBlock.text $"%s{ex.StackTrace}"
                TextBlock.textWrapping TextWrapping.WrapWithOverflow
            ]
            match ex with
            | :? AggregateException as ae ->
                Expander.create [
                    Expander.header (TextBlock.create [ TextBlock.text "InnerExceptions" ])
                    Expander.content (
                        StackPanel.create [
                            StackPanel.children [
                                for ex in ae.InnerExceptions do
                                    previewFailedView ex
                            ]
                        ]
                    )
                ]
            | ex when not (isNull ex.InnerException) ->
                Expander.create [
                    Expander.header (TextBlock.create [ TextBlock.text "InnerException" ])
                    Expander.content (previewFailedView ex.InnerException)
                ]
            | _ -> ()
        ]
    ]

let evalPreviewFunc ((_, func): PreviewFunc) =
    try
        match func () with
        | :? IView as view -> view
        | :? Control as view -> ContentControl.create [ ContentControl.content view ]
        | other -> TextBlock.create [ TextBlock.text $"%A{other}" ]
    with ex ->
        previewFailedView ex
    |> VirtualDom.create

let create (previewFunc: PreviewFunc) attrs =
    Border.create [
        yield! attrs
        Border.child (
            Grid.create [
                Grid.rowDefinitions "Auto,Auto,Auto"
                Grid.children [
                    TextBlock.create [
                        TextBlock.row 0
                        TextBlock.fontSize 20
                        TextBlock.margin 4
                        TextBlock.fontWeight FontWeight.SemiBold
                        TextBlock.text (fst previewFunc)
                    ]
                    Border.create [ Border.row 1; Border.height 2 ]
                    Border.create [ Border.row 2; Border.child (evalPreviewFunc previewFunc) ]
                ]
            ]
        )
    ]