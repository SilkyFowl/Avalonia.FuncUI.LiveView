module Avalonia.FuncUI.LiveView.FuncUIAnalysis

open System.IO
open System.Reflection
open type System.Reflection.BindingFlags

open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text

open Avalonia.FuncUI.Types

type FuncUIAnalysisHander =
    { OnLivePreviewFunc: FSharpMemberOrFunctionOrValue -> list<list<FSharpMemberOrFunctionOrValue>> -> unit
      OnInvalidLivePreviewFunc: FSharpMemberOrFunctionOrValue -> list<list<FSharpMemberOrFunctionOrValue>> -> unit
      OnInvalidStringCall: exn -> range -> FSharpMemberOrFunctionOrValue -> list<FSharpType> -> list<FSharpExpr> -> unit
      OnNotSuppurtPattern: exn -> FSharpExpr -> unit }

let (|String|_|) (o: obj) =
    match o with
    | :? string as s -> Some s
    | _ -> None

let (|FSharpString|_|) t =
    let stringType = "type Microsoft.FSharp.Core.string"

    if $"{t}" = stringType then Some() else None


let stringArgDSLFuncMap =
    let (|FsStaticMember|_|) (m: MethodInfo) =
        match m.Name.Split(".") with
        | [| _; name; _ |] -> Some name
        | _ -> None

    let (|StringParam|_|) (m: MethodInfo) =
        match m.GetParameters() with
        | [| p |] when p.ParameterType = typeof<string> -> Some()
        | _ -> None

    let (|ReturnIAttr|_|) (m: MethodInfo) =
        match m.ReturnType.GetInterfaces() with
        | [| t |] when t = typeof<IAttr> -> Some()
        | _ -> None

    let funcUIAssembly = Assembly.Load "Avalonia.FuncUI"

    do
        FileInfo(Assembly.GetExecutingAssembly().Location).Directory.EnumerateFiles "Avalonia.*.dll"
        |> Seq.toArray
        |> Seq.iter (fun fi -> Assembly.LoadFile fi.FullName |> ignore)

    funcUIAssembly.GetExportedTypes()
    |> Array.choose (fun ty ->
        let memberMap =
            ty.GetMethods(Public ||| Static)
            |> Array.choose (function
                | FsStaticMember name & StringParam & ReturnIAttr as m ->
                    let typeParameters =
                        m.GetGenericArguments()
                        |> Array.map (fun t ->
                            t.GetGenericParameterConstraints()
                            |> Array.tryHead
                            |> Option.defaultWith (fun () ->
                                failwith $"{m.ReflectedType}.{name}: Faild to find getGeneric type tarameter."))

                    let m' = m.MakeGenericMethod typeParameters

                    let invoke (str: string) = m'.Invoke((), [| box str |]) |> ignore

                    Some(name, invoke)
                | _ -> None)
            |> Map.ofArray

        if Map.isEmpty memberMap then
            None
        else
            Some(ty.Name, memberMap))
    |> Map.ofArray


let (|StringArgDSLFunc|_|) (m: FSharpMemberOrFunctionOrValue) =
    let signature =
        "type Microsoft.FSharp.Core.string -> Avalonia.FuncUI.Types.IAttr<'t>"

    match m.FullTypeSafe, m.DeclaringEntity with
    | Some ty, Some d when $"{ty}" = signature ->
        Map.tryFind d.CompiledName stringArgDSLFuncMap
        |> Option.bind (Map.tryFind m.DisplayName)
    | _ -> None


let (|InvalidStringCall|_|) =

    let (|TargetInvocationExceptionInner|_|) (ex: exn) =
        match ex with
        | :? TargetInvocationException as t -> Some t.InnerException
        | _ -> None

    function
    | Call(objExprOpt, memberOrFunc, typeArgs1, typeArgs2, ([ Const(String arg, FSharpString) ] as argExprs)) ->
        let validate func (arg: string) =
            try
                func arg
                None
            with
            | TargetInvocationExceptionInner ex
            | ex -> Some(ex, objExprOpt, memberOrFunc, typeArgs1, typeArgs2, argExprs)

        match memberOrFunc with
        | StringArgDSLFunc invoke -> validate invoke arg
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
            && not args[1].IsFunctionType

    let hasLivePreviewAttribute (m: FSharpMemberOrFunctionOrValue) =
        m.Attributes
        |> Seq.exists (fun attr ->
            let livePreviewAttr = typeof<LivePreviewAttribute>

            attr.AttributeType.BasicQualifiedName = livePreviewAttr.FullName
            && attr.AttributeType.Assembly.QualifiedName = livePreviewAttr.Assembly.FullName)

    match m with
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, vs, e) when hasLivePreviewAttribute v ->
        if isLiveViewSignatue v then
            Ok(v, vs, e) |> Some
        else
            Error(v, vs, e) |> Some
    | _ -> None

let (|NotSuppurtPatternMessage|_|) (ex: exn) =
    if ex.Message.Contains "FSharp.Compiler.Service cannot yet return this kind of pattern match" then
        Some ex
    else
        None

let rec visitExpr (memberCallHandler: FuncUIAnalysisHander) (e: FSharpExpr) =
    try
        match e with
        // FuncUI Analysis
        | InvalidStringCall(ex, objExprOpt, memberOrFunc, typeArgs1, typeArgs2, argExprs) as call ->
            memberCallHandler.OnInvalidStringCall ex e.Range memberOrFunc typeArgs2 argExprs
            visitObjArg memberCallHandler objExprOpt
            visitExprs memberCallHandler argExprs

        // Others
        | AddressOf(lvalueExpr) -> visitExpr memberCallHandler lvalueExpr
        | AddressSet(lvalueExpr, rvalueExpr) ->
            visitExpr memberCallHandler lvalueExpr
            visitExpr memberCallHandler rvalueExpr
        | Application(funcExpr, typeArgs, argExprs) ->
            visitExpr memberCallHandler funcExpr
            visitExprs memberCallHandler argExprs

        | Call(objExprOpt, memberOrFunc, typeArgs1, typeArgs2, argExprs) as call ->
            visitObjArg memberCallHandler objExprOpt
            visitExprs memberCallHandler argExprs
        | Coerce(targetType, inpExpr) -> visitExpr memberCallHandler inpExpr
        | FastIntegerForLoop(startExpr, limitExpr, consumeExpr, isUp, debugPointAtFor, debugPointAtInOrTo) ->
            visitExpr memberCallHandler startExpr
            visitExpr memberCallHandler limitExpr
            visitExpr memberCallHandler consumeExpr
        | ILAsm(asmCode, typeArgs, argExprs) -> visitExprs memberCallHandler argExprs
        | ILFieldGet(objExprOpt, fieldType, fieldName) -> visitObjArg memberCallHandler objExprOpt
        | ILFieldSet(objExprOpt, fieldType, fieldName, valueExpr) -> visitObjArg memberCallHandler objExprOpt
        | IfThenElse(guardExpr, thenExpr, elseExpr) ->
            visitExpr memberCallHandler guardExpr
            visitExpr memberCallHandler thenExpr
            visitExpr memberCallHandler elseExpr
        | Lambda(lambdaVar, bodyExpr) -> visitExpr memberCallHandler bodyExpr
        | Let((bindingVar, bindingExpr, debugPointAtBinding), bodyExpr) ->
            visitExpr memberCallHandler bindingExpr
            visitExpr memberCallHandler bodyExpr
        | LetRec(recursiveBindings, bodyExpr) ->
            let recursiveBindings' =
                recursiveBindings |> List.map (fun (mfv, expr, dp) -> (mfv, expr))

            List.iter (snd >> visitExpr memberCallHandler) recursiveBindings'
            visitExpr memberCallHandler bodyExpr
        | NewArray(arrayType, argExprs) -> visitExprs memberCallHandler argExprs
        | NewDelegate(delegateType, delegateBodyExpr) -> visitExpr memberCallHandler delegateBodyExpr
        | NewObject(objType, typeArgs, argExprs) -> visitExprs memberCallHandler argExprs
        | NewRecord(recordType, argExprs) -> visitExprs memberCallHandler argExprs
        | NewTuple(tupleType, argExprs) -> visitExprs memberCallHandler argExprs
        | NewUnionCase(unionType, unionCase, argExprs) -> visitExprs memberCallHandler argExprs
        | Quote(quotedExpr) -> visitExpr memberCallHandler quotedExpr
        | FSharpFieldGet(objExprOpt, recordOrClassType, fieldInfo) -> visitObjArg memberCallHandler objExprOpt
        | FSharpFieldSet(objExprOpt, recordOrClassType, fieldInfo, argExpr) ->
            visitObjArg memberCallHandler objExprOpt
            visitExpr memberCallHandler argExpr
        | Sequential(firstExpr, secondExpr) ->
            visitExpr memberCallHandler firstExpr
            visitExpr memberCallHandler secondExpr
        | TryFinally(bodyExpr, finalizeExpr, debugPointAtTry, debugPointAtFinally) ->
            visitExpr memberCallHandler bodyExpr
            visitExpr memberCallHandler finalizeExpr
        | TryWith(bodyExpr, _, _, catchVar, catchExpr, debugPointAtTry, debugPointAtWith) ->
            visitExpr memberCallHandler bodyExpr
            visitExpr memberCallHandler catchExpr
        | TupleGet(tupleType, tupleElemIndex, tupleExpr) -> visitExpr memberCallHandler tupleExpr
        | DecisionTree(decisionExpr, decisionTargets) ->
            visitExpr memberCallHandler decisionExpr
            List.iter (snd >> visitExpr memberCallHandler) decisionTargets
        | DecisionTreeSuccess(decisionTargetIdx, decisionTargetExprs) ->
            visitExprs memberCallHandler decisionTargetExprs
        | TypeLambda(genericParam, bodyExpr) -> visitExpr memberCallHandler bodyExpr
        | TypeTest(ty, inpExpr) -> visitExpr memberCallHandler inpExpr
        | UnionCaseSet(unionExpr, unionType, unionCase, unionCaseField, valueExpr) ->
            visitExpr memberCallHandler unionExpr
            visitExpr memberCallHandler valueExpr
        | UnionCaseGet(unionExpr, unionType, unionCase, unionCaseField) -> visitExpr memberCallHandler unionExpr
        | UnionCaseTest(unionExpr, unionType, unionCase) -> visitExpr memberCallHandler unionExpr
        | UnionCaseTag(unionExpr, unionType) -> visitExpr memberCallHandler unionExpr
        | ObjectExpr(objType, baseCallExpr, overrides, interfaceImplementations) ->
            visitExpr memberCallHandler baseCallExpr
            List.iter (visitObjMember memberCallHandler) overrides

            List.iter (snd >> List.iter (visitObjMember memberCallHandler)) interfaceImplementations
        | TraitCall(sourceTypes, traitName, typeArgs, typeInstantiation, argTypes, argExprs) ->
            visitExprs memberCallHandler argExprs
        | ValueSet(valToSet, valueExpr) -> visitExpr memberCallHandler valueExpr
        | WhileLoop(guardExpr, bodyExpr, debugPointAtWhile) ->
            visitExpr memberCallHandler guardExpr
            visitExpr memberCallHandler bodyExpr
        | BaseValue baseType -> ()
        | DefaultValue defaultType -> ()
        | ThisValue thisType -> ()
        | Const(constValueObj, constType) -> ()
        | Value(valueToGet) -> ()
        | _ -> ()
    with NotSuppurtPatternMessage ex ->
        memberCallHandler.OnNotSuppurtPattern ex e

and visitExprs f exprs = List.iter (visitExpr f) exprs

and visitObjArg f objOpt = Option.iter (visitExpr f) objOpt

and visitObjMember f memb = visitExpr f memb.Body

let rec visitDeclaration (f: FuncUIAnalysisHander) d =
    match d with
    // FuncUI Analysis
    | LivePreviewFunc(Ok(v, vs, e)) ->
        f.OnLivePreviewFunc v vs
        visitExpr f e
    | LivePreviewFunc(Error(v, vs, e)) ->
        f.OnInvalidLivePreviewFunc v vs
        visitExpr f e

    // Others
    | FSharpImplementationFileDeclaration.Entity(e, subDecls) ->
        for subDecl in subDecls do
            visitDeclaration f subDecl
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, vs, e) ->
        match v.DeclaringEntity with
        | Some declaringEntity when declaringEntity.IsFSharpUnion && v.IsCompilerGenerated ->
            try
                /// https://github.com/SilkyFowl/Avalonia.FuncUI.LiveView/issues/5
                /// 
                /// This error seems to occur when accessing `e.ImmediateSubExpressions` with `MemberOrFunctionOrValue(v, vs, e)` of an automatically generated member of DU that satisfies certain conditions.
                /// Condition (tentative):DU with no Case that satisfies the following conditions
                /// - Case with no value
                /// - Case where the value is a function value (such as `Case of (int -> unit)`)
                let _tryAccessImmediateSubExpressions = e.ImmediateSubExpressions

                // if no error, visit sub expressions.
                visitExpr f e
            with NotSuppurtPatternMessage _ ->
                // If an error occurs, skip visiting sub expressions.
                ()
        | _ -> visitExpr f e

    | FSharpImplementationFileDeclaration.InitAction(e) -> visitExpr f e