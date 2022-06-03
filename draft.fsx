// #r "nuget: FSharp.Compiler.Service"
#load "draft-init.fsx"
#load "src/Avalonia.FuncUI.LiveView.Core/FuncUIAnalysis.fs"

open Draft
open Avalonia.FuncUI.LiveView

AnalysisDraft.declarations
|> List.iter (
    FuncUIAnalysis.visitDeclaration
        { OnLivePreviewFunc =
            fun v vs ->
                printfn $"{nameof v}:{v}"
                printfn $"{nameof vs}:{vs}"
          OnRowDefinitionsCall =
            fun range m typeArgs argExprs ->
                [ $"{nameof range}: %A{range}"
                  $"{nameof m}: {m}"
                  $"{nameof typeArgs}: {typeArgs}"
                  $"{nameof argExprs}: {argExprs}" ]
                |> List.iter (printfn "%s") }
)
