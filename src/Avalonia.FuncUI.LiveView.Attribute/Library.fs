namespace Avalonia.FuncUI.LiveView.Attribute
open System

[<AttributeUsage(AttributeTargets.Property)>]
type LivePreviewAttribute([<ParamArray>] tags: string []) =
    inherit Attribute()
    member _.Tags = tags