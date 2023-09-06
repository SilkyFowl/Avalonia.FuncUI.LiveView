namespace Avalonia.FuncUI.LiveView

open System

[<AttributeUsage(AttributeTargets.Property)>]
type LivePreviewAttribute() =
    inherit Attribute()