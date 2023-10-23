namespace Avalonia.FuncUI.LiveView

open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.LiveView.Views

type LiveViewWindow() =
    inherit HostWindow(Title = "LiveView", Width = 800, Height = 600)


    do base.Content <- LiveView.create ()