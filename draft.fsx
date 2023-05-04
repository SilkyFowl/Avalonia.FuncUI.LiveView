// #r "nuget: Avalonia.Desktop"
// #r "nuget: FSharp.Compiler.Service"
#I "tests/Avalonia.FuncUI.LiveView.Core.Tests/bin/Debug/net6.0"
#r "Avalonia"
#r "Avalonia.Base"
#r "Avalonia.Desktop"
#r "Avalonia.Skia"
#r "Avalonia.Controls"
#r "Avalonia.FuncUI"
#r "FSharp.Compiler.Service"

#load "draft-init.fsx"
#load "src/Avalonia.FuncUI.LiveView.Core/Types.fs"
#load "src/Avalonia.FuncUI.LiveView.Core/FuncUIAnalysis.fs"

open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.Core.Types

#load "draft-input.fsx"
let t = ResizeArray()
open Draft
AnalysisDraft.references
AnalysisDraft.checkProjectResults
let d = AnalysisDraft.parseAndCheckSingleFile InputData.input
d.AssemblyContents.ImplementationFiles.Length
// let input = "DockPanel.background \"Fpp\""
// (AnalysisDraft.parseAndCheckSingleFile InputData.input).AssemblyContents.ImplementationFiles[1].Declarations
d.AssemblyContents.ImplementationFiles[0].Declarations
|> List.iter (
    FuncUIAnalysis.visitDeclaration
        { OnLivePreviewFunc =
            fun v vs ->
                printfn $"{nameof v}:{v}"
                printfn $"{nameof vs}:{vs}"
          OnInvalidLivePreviewFunc =
            fun v vs ->
                printfn $"{nameof v}:{v}"
                printfn $"{nameof vs}:{vs}"

          OnInvalidStringCall =
              fun ex range m typeArgs argExprs ->
                  [ $"{nameof range}: %A{range}"
                    $"{nameof m}: {m}"
                    $"{nameof m}: {m.FullName}"
                    $"{nameof m}: {m.ReturnParameter}"
                    $"{nameof typeArgs}: {typeArgs}"
                    $"{nameof argExprs}: {argExprs}" ]
                  |> List.iter (printfn "%s")
          OnNotSuppurtPattern =
            fun ex e ->
                () }
)
AnalysisDraft.declarations
|> List.iter (
    FuncUIAnalysis.visitDeclaration
        { OnLivePreviewFunc =
            fun v vs ->
                printfn $"{nameof v}:{v}"
                printfn $"{nameof vs}:{vs}"
                t.Add v
          OnInvalidLivePreviewFunc =
            fun v vs ->
                printfn $"{nameof v}:{v}"
                printfn $"{nameof vs}:{vs}"

          OnInvalidStringCall =
              fun ex range m typeArgs argExprs ->
                  [ $"{nameof range}: %A{range}"
                    $"{nameof m}: {m}"
                    $"{nameof m}: {m.FullName}"
                    $"{nameof m}: {m.ReturnParameter}"
                    $"{nameof typeArgs}: {typeArgs}"
                    $"{nameof argExprs}: {argExprs}" ]
                  |> List.iter (printfn "%s")
          OnNotSuppurtPattern = 
            fun ex e ->
                () }
)

t[0].DisplayName

let session = AnalysisDraft.createSession ()
session.DynamicAssemblies.Length
session.EvalExpression "10"
let r, w = session.EvalScriptNonThrowing "src/Sample/draft.fsx"
let r', w' = session.EvalInteractionNonThrowing "Draft.Counter.view"
r'
open type System.Reflection.BindingFlags

let ty =
    session.DynamicAssemblies
    |> Array.last
    |> (fun asm -> asm.GetExportedTypes())
    |> Array.map (fun ty -> ty, ty.GetMethods(Public ||| Static))

open System
open System.Reflection
let t', ms = ty[2]
let m = ms[2]
let rec getFriendlyName (ty: System.Type) path =
    match ty.DeclaringType with
    | null -> [ ty.Name ]
    | ty' -> ty.Name :: (path |> getFriendlyName ty')

// fullname
m
m.DeclaringType
|> Seq.unfold (function
    | null -> None
    | ty -> Some(ty.Name, ty.DeclaringType))
|> Seq.append [$"{m.Name}()"]
|> Seq.rev
|> String.concat "."
open System
m.ReturnType

m.GetParameters()
|> Array.isEmpty

getFriendlyName ms[2].DeclaringType []
ms[2].DeclaringType.DeclaringType.DeclaringType
$"{ms[2]}"
ms[ 2 ].Invoke((), [||]) :?> Avalonia.FuncUI.Types.IView

session.CurrentPartialAssemblySignature.Entities[0]
    .NestedEntities[1]
    .MembersFunctionsAndValues[0]

session.DynamicAssemblies[ 0 ].GetTypes()
|> Seq.iter (fun t -> t.FullName |> printfn "%s")
System
    .Reflection
    .Assembly
    .Load("Avalonia.FuncUI")
    .GetReferencedAssemblies()
|> Seq.iter (fun asm -> printfn $"{asm.Name}")

$"{t[0]}"
let assemblies =System.AppDomain.CurrentDomain.GetAssemblies()
let allTypeInfo =
    assemblies
    |> Array.map(fun asm ->
        asm.GetExportedTypes()
        |> Array.map (fun ty -> ty, ty.GetMethods(Public ||| Static))
    )
    |> Array.concat

let tInfo,_= 
    allTypeInfo
    |> Array.find(fun (ty,_) ->
        let rec loop (t:Type) =
            if t.FullName = "Avalonia.AvaloniaObject" then
                true
            else
                match t.BaseType with
                | null -> false
                | b -> loop b
        loop ty
    )
tInfo.FullName.Contains "Ava"

let a =
    [|

        let rec containBase (t:Type) =
            if t.FullName = "Avalonia.AvaloniaObject" then
                true
            else
                match t.BaseType with
                | null -> false
                | b -> containBase b

        for asm in System.AppDomain.CurrentDomain.GetAssemblies() do
            for ty in asm.ExportedTypes do
                if containBase ty then
                    ty
    |]
    |> Array.groupBy (fun t -> t.FullName)
    |> Array.filter (fun (_,arr) -> arr.Length >= 2)
    // // |> Array.head
    // |> Array.iter (fun (_,ts) ->
    //     Array.iter (printfn "%A") ts)
a[0]
assemblies
|> Array.map(fun asm ->
    asm.GetExportedTypes()
    |> Array.filter(fun (ty) ->
        let rec loop (t:Type) =
            if t.FullName = "Avalonia.AvaloniaObject" then
                true
            else
                match t.BaseType with
                | null -> false
                | b -> loop b
        loop ty
    )
)
|> Array.concat
|> Array.iter (fun (ty) ->
    printfn $"{ty.FullName}"
)