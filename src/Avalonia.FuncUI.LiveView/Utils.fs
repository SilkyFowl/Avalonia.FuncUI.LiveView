namespace Avalonia.FuncUI.LiveView

module Disposable =
    open System
    let inline dispose (x: #IDisposable) = x.Dispose()

    let inline tryDispose (x: #IDisposable option) =
        match x with
        | Some x -> x.Dispose()
        | None -> ()

module Disposables =
    open System

    let inline create (xs: #IDisposable seq) =
        { new IDisposable with
            member _.Dispose() =
                for x in xs do
                    x.Dispose() }

module IO =
    open System.IO
    open System.Text.RegularExpressions

    let inline (|FilePathString|_|) (str: string) =
        if File.Exists str then Some(Path.GetFullPath str) else None

    let inline (|DirectoryPathString|_|) (str: string) =
        if Directory.Exists str then
            Some(Path.GetFullPath str)
        else
            None

    let inline (|MatchExtension|_|) (patten: string) (path: string) =
        if Regex.IsMatch(Path.GetExtension path, patten) then
            Some()
        else
            None

module Config =
    open Types

    module EnvironmentVariable =
        open System
        open System.Text

        let toEnvironmentVariableName (str: string) =
            let pairwised = Seq.toList str |> List.pairwise

            (new StringBuilder(), pairwised)
            ||> List.fold (fun sb (prev, curr) ->
                sb.Append [|
                    if sb.Length = 0 then
                        Char.ToUpper prev

                    if not (Char.IsUpper prev) && Char.IsUpper curr then
                        '_'

                    Char.ToUpper curr
                |])
            |> fun sb -> sb.ToString()


        let tryGet (key: string) =
            match Environment.GetEnvironmentVariable key with
            | null -> None
            | value -> Some value

    module EnvVarName =
        let private prefix = "FuncuiLiveview"

        module WatichingProjectInfo =
            let private prefix = $"{prefix}{nameof WatichingProjectInfo}"
            let private d = Unchecked.defaultof<WatichingProjectInfo>

            let path =
                $"{prefix}{nameof d.Path}" |> EnvironmentVariable.toEnvironmentVariableName

            let targetFramework =
                $"{prefix}{nameof d.TargetFramework}"
                |> EnvironmentVariable.toEnvironmentVariableName

    let tryGetFromEnv () : Config =
        let info =
            let pathEnvVar = EnvVarName.WatichingProjectInfo.path |> EnvironmentVariable.tryGet

            let targetFrameworkEnvVar =
                EnvVarName.WatichingProjectInfo.targetFramework |> EnvironmentVariable.tryGet

            match pathEnvVar, targetFrameworkEnvVar with
            | Some path, Some targetFramework ->
                Some
                    { Path = path
                      TargetFramework = targetFramework }
            | _ -> None

        { WatichingProjectInfo = info }

module Plugin =
    open System
    open System.IO
    open System.Reflection
    open System.Runtime.CompilerServices
    open System.Runtime.Loader

    type PluginLoadContext(pluginPath: string) =
        inherit AssemblyLoadContext("PluginLoadContext", true)
        let resolver = AssemblyDependencyResolver(pluginPath)

        member _.PluginAssemblyName =
            AssemblyName(Path.GetFileNameWithoutExtension(pluginPath))

        override this.Load(assemblyName: AssemblyName) =
            let assemblyPath = resolver.ResolveAssemblyToPath(assemblyName)

            if assemblyPath <> null then
                this.LoadFromAssemblyPath(assemblyPath)
            else
                null

        override this.LoadUnmanagedDll(unmanagedDllName: string) =
            let libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName)

            if libraryPath <> null then
                this.LoadUnmanagedDllFromPath(libraryPath)
            else
                IntPtr.Zero

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let load<'t> (ctx: PluginLoadContext) =
        ctx.LoadFromAssemblyName(ctx.PluginAssemblyName).GetTypes()
        |> Seq.find (typeof<'t>).IsAssignableFrom
        |> Activator.CreateInstance
        :?> 't

module Watcher =
    open System.IO
    open System.Runtime.CompilerServices
    open Avalonia.FuncUI.LiveView.Types.Watcher

    let watcherContext =
        let dllPath =
            Path.Combine [|
                Path.GetDirectoryName typeof<IWatcherService>.Assembly.Location
                "plugins"
                "watcher"
                "Avalonia.FuncUI.LiveView.Watcher.dll"
            |]

        new Plugin.PluginLoadContext(dllPath)

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let createService () =
        Plugin.load<IWatcherService> watcherContext