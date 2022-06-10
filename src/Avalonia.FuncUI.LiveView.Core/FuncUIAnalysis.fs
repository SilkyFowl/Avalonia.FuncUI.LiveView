module Avalonia.FuncUI.LiveView.FuncUIAnalysis

open System
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open Avalonia
open Avalonia.Controls
open Avalonia.Skia
open Avalonia.Media
open Avalonia.Platform
open Avalonia.Controls.Shapes
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL

type FuncUIAnalysisHander =
    { OnLivePreviewFunc: FSharpMemberOrFunctionOrValue -> list<list<FSharpMemberOrFunctionOrValue>> -> unit
      OnInvalidLivePreviewFunc: FSharpMemberOrFunctionOrValue -> list<list<FSharpMemberOrFunctionOrValue>> -> unit
      OnInvalidStringCall: exn -> range -> FSharpMemberOrFunctionOrValue -> list<FSharpType> -> list<FSharpExpr> -> unit }

let (|String|_|) (o: obj) =
    match o with
    | :? string as s -> Some s
    | _ -> None

let (|FSharpString|_|) t =
    let stringType = "type Microsoft.FSharp.Core.string"

    if $"{t}" = stringType then
        Some()
    else
        None

let (|StringArgDSLFunc|_|) (controlTypeName) methodName (m: FSharpMemberOrFunctionOrValue) =
    let signature =
        "type Microsoft.FSharp.Core.string -> Avalonia.FuncUI.Types.IAttr<'t>"

    match m.FullTypeSafe, m.DeclaringEntity with
    | Some ty, Some d when
        $"{ty}" = signature
        && m.DisplayName = methodName
        && d.CompiledName = controlTypeName
        ->
        Some()
    | _ -> None

let (|InvalidStringCall|_|) =
    function
    | Call (objExprOpt, memberOrFunc, typeArgs1, typeArgs2, ([ Const (String arg, FSharpString) ] as argExprs)) ->
        let validate parse (arg: string) =
            try
                parse arg |> ignore
                None
            with
            | ex -> Some(ex, objExprOpt, memberOrFunc, typeArgs1, typeArgs2, argExprs)

        match memberOrFunc with
        | StringArgDSLFunc (nameof Border) (nameof (Border.background: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof Border) (nameof (Border.borderBrush: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof SplitView) (nameof (SplitView.paneBackground: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof TextBlock) (nameof (TextBlock.background: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof TextBlock) (nameof (TextBlock.foreground: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof TextBox) (nameof (TextBox.selectionBrush: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof TextBox) (nameof (TextBox.selectionForegroundBrush: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof TextBox) (nameof (TextBox.caretBrush: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof TickBar) (nameof (TickBar.fill: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof Panel) (nameof (Panel.background: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof Calendar) (nameof (Calendar.headerBackground: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof Shape) (nameof (Shape.fill: string -> IAttr<'t>))
        | StringArgDSLFunc (nameof Shape) (nameof (Shape.stroke: string -> IAttr<'t>)) -> validate Color.Parse arg
        | StringArgDSLFunc (nameof Grid) (nameof (Grid.columnDefinitions: string -> IAttr<'t>)) ->
            validate ColumnDefinitions.Parse arg
        | StringArgDSLFunc (nameof Grid) (nameof (Grid.rowDefinitions: string -> IAttr<'t>)) ->
            validate RowDefinitions.Parse arg
        | StringArgDSLFunc (nameof Path) (nameof (Path.data: string -> IAttr<'t>)) ->
            if isNull <| AvaloniaLocator.Current.GetService<IPlatformRenderInterface>() then
                SkiaPlatform.Initialize()
            validate StreamGeometry.Parse arg
        | _ -> None
    | _ -> None

let (|LivePreviewFunc|_|) m =

    let isLiveViewSignatue (m: FSharpMemberOrFunctionOrValue) =

        match m.FullTypeSafe with
        | None -> false
        | Some ty ->
            let args = ty.GenericArguments

            args.Count = 2
            && $"{args[0]}" = "type Microsoft.FSharp.Core.unit"

    let hasLivePreviewAttribute (m: FSharpMemberOrFunctionOrValue) =
        m.Attributes
        |> Seq.exists (fun attr -> attr.AttributeType.CompiledName = "LivePreviewAttribute")

    match m with
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue (v, vs, e) when hasLivePreviewAttribute v ->
        if isLiveViewSignatue v then
            Ok(v, vs, e) |> Some
        else
            Error(v, vs, e) |> Some
    | _ -> None

let rec visitExpr (memberCallHandler: FuncUIAnalysisHander) (e: FSharpExpr) =
    match e with
    // FuncUI Analysis
    | InvalidStringCall (ex, objExprOpt, memberOrFunc, typeArgs1, typeArgs2, argExprs) as call ->
        memberCallHandler.OnInvalidStringCall ex e.Range memberOrFunc typeArgs2 argExprs
        visitObjArg memberCallHandler objExprOpt
        visitExprs memberCallHandler argExprs

    // Others
    | AddressOf (lvalueExpr) -> visitExpr memberCallHandler lvalueExpr
    | AddressSet (lvalueExpr, rvalueExpr) ->
        visitExpr memberCallHandler lvalueExpr
        visitExpr memberCallHandler rvalueExpr
    | Application (funcExpr, typeArgs, argExprs) ->
        visitExpr memberCallHandler funcExpr
        visitExprs memberCallHandler argExprs

    | Call (objExprOpt, memberOrFunc, typeArgs1, typeArgs2, argExprs) as call ->
        visitObjArg memberCallHandler objExprOpt
        visitExprs memberCallHandler argExprs
    | Coerce (targetType, inpExpr) -> visitExpr memberCallHandler inpExpr
    | FastIntegerForLoop (startExpr, limitExpr, consumeExpr, isUp, debugPointAtFor, debugPointAtInOrTo) ->
        visitExpr memberCallHandler startExpr
        visitExpr memberCallHandler limitExpr
        visitExpr memberCallHandler consumeExpr
    | ILAsm (asmCode, typeArgs, argExprs) -> visitExprs memberCallHandler argExprs
    | ILFieldGet (objExprOpt, fieldType, fieldName) -> visitObjArg memberCallHandler objExprOpt
    | ILFieldSet (objExprOpt, fieldType, fieldName, valueExpr) -> visitObjArg memberCallHandler objExprOpt
    | IfThenElse (guardExpr, thenExpr, elseExpr) ->
        visitExpr memberCallHandler guardExpr
        visitExpr memberCallHandler thenExpr
        visitExpr memberCallHandler elseExpr
    | Lambda (lambdaVar, bodyExpr) -> visitExpr memberCallHandler bodyExpr
    | Let ((bindingVar, bindingExpr, debugPointAtBinding), bodyExpr) ->
        visitExpr memberCallHandler bindingExpr
        visitExpr memberCallHandler bodyExpr
    | LetRec (recursiveBindings, bodyExpr) ->
        let recursiveBindings' =
            recursiveBindings
            |> List.map (fun (mfv, expr, dp) -> (mfv, expr))

        List.iter (snd >> visitExpr memberCallHandler) recursiveBindings'
        visitExpr memberCallHandler bodyExpr
    | NewArray (arrayType, argExprs) -> visitExprs memberCallHandler argExprs
    | NewDelegate (delegateType, delegateBodyExpr) -> visitExpr memberCallHandler delegateBodyExpr
    | NewObject (objType, typeArgs, argExprs) -> visitExprs memberCallHandler argExprs
    | NewRecord (recordType, argExprs) -> visitExprs memberCallHandler argExprs
    | NewTuple (tupleType, argExprs) -> visitExprs memberCallHandler argExprs
    | NewUnionCase (unionType, unionCase, argExprs) -> visitExprs memberCallHandler argExprs
    | Quote (quotedExpr) -> visitExpr memberCallHandler quotedExpr
    | FSharpFieldGet (objExprOpt, recordOrClassType, fieldInfo) -> visitObjArg memberCallHandler objExprOpt
    | FSharpFieldSet (objExprOpt, recordOrClassType, fieldInfo, argExpr) ->
        visitObjArg memberCallHandler objExprOpt
        visitExpr memberCallHandler argExpr
    | Sequential (firstExpr, secondExpr) ->
        visitExpr memberCallHandler firstExpr
        visitExpr memberCallHandler secondExpr
    | TryFinally (bodyExpr, finalizeExpr, debugPointAtTry, debugPointAtFinally) ->
        visitExpr memberCallHandler bodyExpr
        visitExpr memberCallHandler finalizeExpr
    | TryWith (bodyExpr, _, _, catchVar, catchExpr, debugPointAtTry, debugPointAtWith) ->
        visitExpr memberCallHandler bodyExpr
        visitExpr memberCallHandler catchExpr
    | TupleGet (tupleType, tupleElemIndex, tupleExpr) -> visitExpr memberCallHandler tupleExpr
    | DecisionTree (decisionExpr, decisionTargets) ->
        visitExpr memberCallHandler decisionExpr
        List.iter (snd >> visitExpr memberCallHandler) decisionTargets
    | DecisionTreeSuccess (decisionTargetIdx, decisionTargetExprs) -> visitExprs memberCallHandler decisionTargetExprs
    | TypeLambda (genericParam, bodyExpr) -> visitExpr memberCallHandler bodyExpr
    | TypeTest (ty, inpExpr) -> visitExpr memberCallHandler inpExpr
    | UnionCaseSet (unionExpr, unionType, unionCase, unionCaseField, valueExpr) ->
        visitExpr memberCallHandler unionExpr
        visitExpr memberCallHandler valueExpr
    | UnionCaseGet (unionExpr, unionType, unionCase, unionCaseField) -> visitExpr memberCallHandler unionExpr
    | UnionCaseTest (unionExpr, unionType, unionCase) -> visitExpr memberCallHandler unionExpr
    | UnionCaseTag (unionExpr, unionType) -> visitExpr memberCallHandler unionExpr
    | ObjectExpr (objType, baseCallExpr, overrides, interfaceImplementations) ->
        visitExpr memberCallHandler baseCallExpr
        List.iter (visitObjMember memberCallHandler) overrides

        List.iter
            (snd
             >> List.iter (visitObjMember memberCallHandler))
            interfaceImplementations
    | TraitCall (sourceTypes, traitName, typeArgs, typeInstantiation, argTypes, argExprs) ->
        visitExprs memberCallHandler argExprs
    | ValueSet (valToSet, valueExpr) -> visitExpr memberCallHandler valueExpr
    | WhileLoop (guardExpr, bodyExpr, debugPointAtWhile) ->
        visitExpr memberCallHandler guardExpr
        visitExpr memberCallHandler bodyExpr
    | BaseValue baseType -> ()
    | DefaultValue defaultType -> ()
    | ThisValue thisType -> ()
    | Const (constValueObj, constType) -> ()
    | Value (valueToGet) -> ()
    | _ -> ()

and visitExprs f exprs = List.iter (visitExpr f) exprs

and visitObjArg f objOpt = Option.iter (visitExpr f) objOpt

and visitObjMember f memb = visitExpr f memb.Body

let rec visitDeclaration (f: FuncUIAnalysisHander) d =
    match d with
    // FuncUI Analysis
    | LivePreviewFunc (Ok (v, vs, e)) ->
        f.OnLivePreviewFunc v vs
        visitExpr f e
    | LivePreviewFunc (Error (v, vs, e)) ->
        f.OnInvalidLivePreviewFunc v vs
        visitExpr f e

    // Others
    | FSharpImplementationFileDeclaration.Entity (e, subDecls) ->
        for subDecl in subDecls do
            visitDeclaration f subDecl
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue (v, vs, e) -> visitExpr f e
    | FSharpImplementationFileDeclaration.InitAction (e) -> visitExpr f e