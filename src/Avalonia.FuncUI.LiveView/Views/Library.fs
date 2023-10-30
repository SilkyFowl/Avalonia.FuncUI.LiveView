namespace Avalonia.FuncUI.LiveView.Views

open Avalonia
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL

[<AutoOpen>]
module StyledElement =
    open Avalonia.Styling

    type StyledElement with

        static member styles(styleSeq: list<(Selector -> Selector) * list<IAttr<'a>>>) =
            let styles = Styles()

            for (selector, setters) in styleSeq do
                let s = Style(fun x -> selector x)

                for attr in setters do
                    match attr.Property with
                    | ValueSome p ->
                        match p.Accessor with
                        | InstanceProperty x -> failwith "Can't support instance property"
                        | AvaloniaProperty x -> s.Setters.Add(Setter(x, p.Value))
                    | ValueNone -> ()

                styles.Add s

            StyledElement.styles styles


module FilePicker =
    open Avalonia.Controls
    open Avalonia.Platform.Storage

    let openSimgleFileAsync ctr title filters =
        task {
            let provier = TopLevel.GetTopLevel(ctr).StorageProvider
            let! location = provier.TryGetWellKnownFolderAsync(WellKnownFolder.Documents)

            let! result =
                provier.OpenFilePickerAsync(
                    FilePickerOpenOptions(
                        Title = title,
                        SuggestedStartLocation = location,
                        AllowMultiple = false,
                        FileTypeFilter = filters
                    )
                )

            match List.ofSeq result with
            | [ picked ] -> return Some picked
            | _ -> return None
        }

    let openProjectOrSolutionFileAsync ctr =
        openSimgleFileAsync ctr "Open Project or Solution" [
            FilePickerFileType(
                "MsBuild Project Or Solution File",
                Patterns = [ "*.fsproj"; "*.csproj"; "*.sln" ],
                AppleUniformTypeIdentifiers = [ "public.data" ]
            )
        ]


[<AutoOpen>]
module CustomHooks =
    open Avalonia.FuncUI

    [<RequireQualifiedAccess>]
    type Deferred<'t> =
        | NotStartedYet
        | Pending
        | Resolved of 't
        | Failed of exn


    module Deferred =
        let setAsync (state: IWritable<Deferred<'t>>) (computation: Async<'t>) =
            async {
                try
                    state.Set Deferred.Pending
                    let! result = computation
                    state.Set(Deferred.Resolved result)
                with exn ->
                    state.Set(Deferred.Failed exn)
            }
            |> Async.StartImmediate

        let setTask (state: IWritable<Deferred<'t>>) (t) =
            backgroundTask {
                try
                    state.Set Deferred.Pending
                    let! result = t
                    state.Set(Deferred.Resolved result)
                with exn ->
                    state.Set(Deferred.Failed exn)
            }
            |> ignore

    type IComponentContext with

        member this.useAsync<'t>(computation: Async<'t>) =
            let state = this.useState Deferred.NotStartedYet

            this.useEffect (
                (fun _ ->
                    match state.Current with
                    | Deferred.NotStartedYet ->
                        state.Set Deferred.Pending

                        async {
                            try
                                let! result = computation
                                state.Set(Deferred.Resolved result)
                            with exn ->
                                state.Set(Deferred.Failed exn)
                        }
                        |> Async.StartImmediate
                    | _ -> ()),
                [ EffectTrigger.AfterInit ]
            )

            state


[<AutoOpen>]
module PathIconExt =
    open Avalonia.Media
    open Avalonia.Controls.Shapes
    open Avalonia.FuncUI.Builder
    open Avalonia.FuncUI.DSL
    open Avalonia.Controls

    type PathIcon with
        static member create (attrs: IAttr<PathIcon> list) : IView<PathIcon> = ViewBuilder.Create<PathIcon>(attrs)


        static member data<'t when 't :> PathIcon>(streamGeometry: StreamGeometry) =
            AttrBuilder<'t>.CreateProperty(PathIcon.DataProperty, streamGeometry, ValueNone)

        static member data<'t when 't :> PathIcon>(dataStr: string) =
            StreamGeometry.Parse dataStr |> PathIcon.data
        


module StreamGeometry =
    open Avalonia.Media

    let info =
        StreamGeometry.Parse
            "M14,2 C20.6274,2 26,7.37258 26,14 C26,20.6274 20.6274,26 14,26 C7.37258,26 2,20.6274 2,14 C2,7.37258 7.37258,2 14,2 Z M14,3.5 C8.20101,3.5 3.5,8.20101 3.5,14 C3.5,19.799 8.20101,24.5 14,24.5 C19.799,24.5 24.5,19.799 24.5,14 C24.5,8.20101 19.799,3.5 14,3.5 Z M14,11 C14.3796833,11 14.6934889,11.2821653 14.7431531,11.6482323 L14.75,11.75 L14.75,19.25 C14.75,19.6642 14.4142,20 14,20 C13.6203167,20 13.3065111,19.7178347 13.2568469,19.3517677 L13.25,19.25 L13.25,11.75 C13.25,11.3358 13.5858,11 14,11 Z M14,7 C14.5523,7 15,7.44772 15,8 C15,8.55228 14.5523,9 14,9 C13.4477,9 13,8.55228 13,8 C13,7.44772 13.4477,7 14,7 Z"

    let warning =
        StreamGeometry.Parse
            "M10.9093922,2.78216375 C11.9491636,2.20625071 13.2471955,2.54089334 13.8850247,3.52240345 L13.9678229,3.66023048 L21.7267791,17.6684928 C21.9115773,18.0021332 22.0085303,18.3772743 22.0085303,18.7586748 C22.0085303,19.9495388 21.0833687,20.9243197 19.9125791,21.003484 L19.7585303,21.0086748 L4.24277801,21.0086748 C3.86146742,21.0086748 3.48641186,20.9117674 3.15282824,20.7270522 C2.11298886,20.1512618 1.7079483,18.8734454 2.20150311,17.8120352 L2.27440063,17.668725 L10.0311968,3.66046274 C10.2357246,3.291099 10.5400526,2.98673515 10.9093922,2.78216375 Z M20.4146132,18.3952808 L12.6556571,4.3870185 C12.4549601,4.02467391 11.9985248,3.89363262 11.6361802,4.09432959 C11.5438453,4.14547244 11.4637001,4.21532637 11.4006367,4.29899869 L11.3434484,4.38709592 L3.58665221,18.3953582 C3.385998,18.7577265 3.51709315,19.2141464 3.87946142,19.4148006 C3.96285732,19.4609794 4.05402922,19.4906942 4.14802472,19.5026655 L4.24277801,19.5086748 L19.7585303,19.5086748 C20.1727439,19.5086748 20.5085303,19.1728883 20.5085303,18.7586748 C20.5085303,18.6633247 20.4903516,18.5691482 20.455275,18.4811011 L20.4146132,18.3952808 L12.6556571,4.3870185 L20.4146132,18.3952808 Z M12.0004478,16.0017852 C12.5519939,16.0017852 12.9991104,16.4489016 12.9991104,17.0004478 C12.9991104,17.5519939 12.5519939,17.9991104 12.0004478,17.9991104 C11.4489016,17.9991104 11.0017852,17.5519939 11.0017852,17.0004478 C11.0017852,16.4489016 11.4489016,16.0017852 12.0004478,16.0017852 Z M11.9962476,8.49954934 C12.3759432,8.49924613 12.689964,8.78114897 12.7399193,9.14718469 L12.7468472,9.24894974 L12.750448,13.7505438 C12.7507788,14.1647572 12.4152611,14.5008121 12.0010476,14.5011439 C11.621352,14.5014471 11.3073312,14.2195442 11.257376,13.8535085 L11.250448,13.7517435 L11.2468472,9.25014944 C11.2465164,8.83593601 11.5820341,8.49988112 11.9962476,8.49954934 Z"

    let error =
        StreamGeometry.Parse
            "M12.45 2.15C14.992 4.05652 17.5866 5 20.25 5C20.6642 5 21 5.33579 21 5.75V11C21 16.0012 18.0424 19.6757 12.2749 21.9478C12.0982 22.0174 11.9018 22.0174 11.7251 21.9478C5.95756 19.6757 3 16.0012 3 11V5.75C3 5.33579 3.33579 5 3.75 5C6.41341 5 9.00797 4.05652 11.55 2.15C11.8167 1.95 12.1833 1.95 12.45 2.15ZM12 3.67782C9.58084 5.38829 7.07735 6.32585 4.5 6.47793V11C4.5 15.2556 6.95337 18.3789 12 20.4419C17.0466 18.3789 19.5 15.2556 19.5 11V6.47793C16.9227 6.32585 14.4192 5.38829 12 3.67782ZM12 16C12.4142 16 12.75 16.3358 12.75 16.75C12.75 17.1642 12.4142 17.5 12 17.5C11.5858 17.5 11.25 17.1642 11.25 16.75C11.25 16.3358 11.5858 16 12 16ZM12 7.00356C12.3797 7.00356 12.6935 7.28572 12.7432 7.65179L12.75 7.75356V14.2523C12.75 14.6665 12.4142 15.0023 12 15.0023C11.6203 15.0023 11.3065 14.7201 11.2568 14.3541L11.25 14.2523V7.75356C11.25 7.33935 11.5858 7.00356 12 7.00356Z"


    let question =
        StreamGeometry.Parse
            "M24 4C35.0457 4 44 12.9543 44 24C44 35.0457 35.0457 44 24 44C12.9543 44 4 35.0457 4 24C4 12.9543 12.9543 4 24 4ZM24 6.5C14.335 6.5 6.5 14.335 6.5 24C6.5 33.665 14.335 41.5 24 41.5C33.665 41.5 41.5 33.665 41.5 24C41.5 14.335 33.665 6.5 24 6.5ZM24.25 32C25.0784 32 25.75 32.6716 25.75 33.5C25.75 34.3284 25.0784 35 24.25 35C23.4216 35 22.75 34.3284 22.75 33.5C22.75 32.6716 23.4216 32 24.25 32ZM24.25 13C27.6147 13 30.5 15.8821 30.5 19.2488C30.502 21.3691 29.7314 22.7192 27.8216 24.7772L26.8066 25.8638C25.7842 27.0028 25.3794 27.7252 25.3409 28.5793L25.3379 28.7411L25.3323 28.8689L25.3143 28.9932C25.2018 29.5636 24.7009 29.9957 24.0968 30.0001C23.4065 30.0049 22.8428 29.4493 22.8379 28.7589C22.8251 26.9703 23.5147 25.7467 25.1461 23.9739L26.1734 22.8762C27.5312 21.3837 28.0012 20.503 28 19.25C28 17.2634 26.2346 15.5 24.25 15.5C22.3307 15.5 20.6142 17.1536 20.5055 19.0587L20.4935 19.3778C20.4295 20.0081 19.8972 20.5 19.25 20.5C18.5596 20.5 18 19.9404 18 19.25C18 15.8846 20.8864 13 24.25 13Z"