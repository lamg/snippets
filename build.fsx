#r "nuget: Fake.Core.Target, 6.1.3"
#r "nuget: Fake.DotNet.Cli, 6.1.3"
#r "nuget: Fake.IO.FileSystem, 6.1.3"

open System
open System.IO
open System.Runtime.InteropServices
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO

if not (Context.isFakeContext ()) then
  let executionContext = Context.FakeExecutionContext.Create false "build.fsx" []
  Context.RuntimeContext.Fake executionContext |> Context.setExecutionContext

let rootDir = Path.GetFullPath __SOURCE_DIRECTORY__
let projectPath = Path.Combine(rootDir, "Snippets", "Snippets.fsproj")
let nupkgDir = Path.Combine(rootDir, "nupkg")
let packageId = "Snippets.Tool"
let fallbackRid = "any"

let cliArgs =
  let commandLineArgs = Environment.GetCommandLineArgs() |> Array.toList

  match commandLineArgs |> List.tryFindIndex ((=) "--") with
  | Some separatorIndex -> commandLineArgs |> List.skip (separatorIndex + 1)
  | None -> commandLineArgs |> List.skip 1

let requestedTarget =
  let rec loop args =
    match args with
    | "--target" :: target :: _ -> target
    | "-t" :: target :: _ -> target
    | arg :: _ when arg.StartsWith "--target=" -> arg.Substring "--target=".Length
    | _ :: rest -> loop rest
    | [] -> "InstallGlobal"

  loop cliArgs

let private runDotNetCommand command args =
  let result = DotNet.exec id command args

  if not result.OK then
    failwithf "dotnet %s failed with args: %s" command args

let private currentLocalRid =
  let arch = RuntimeInformation.ProcessArchitecture

  if RuntimeInformation.IsOSPlatform OSPlatform.Linux && arch = Architecture.X64 then
    Some "linux-x64"
  elif RuntimeInformation.IsOSPlatform OSPlatform.Windows && arch = Architecture.X64 then
    Some "win-x64"
  elif RuntimeInformation.IsOSPlatform OSPlatform.OSX && arch = Architecture.Arm64 then
    Some "osx-arm64"
  else
    None

let private installGlobalTool () =
  let installArgs =
    $"install --global {packageId} --add-source \"{nupkgDir}\" --ignore-failed-sources"

  let installResult = DotNet.exec id "tool" installArgs

  if not installResult.OK then
    failwithf "dotnet tool install failed for %s" packageId

  Trace.log $"Installed global %s{packageId}."

Target.create "Clean" (fun _ ->
  Shell.cleanDirs [ nupkgDir ]
  Directory.CreateDirectory nupkgDir |> ignore)

Target.create "Restore" (fun _ -> runDotNetCommand "restore" $"\"{projectPath}\"")

Target.create "Build" (fun _ -> runDotNetCommand "build" $"\"{projectPath}\" -c Release --no-restore")

Target.create "PackCurrentAot" (fun _ ->
  match currentLocalRid with
  | Some rid ->
    runDotNetCommand "pack" $"\"{projectPath}\" -c Release -r {rid} -o \"{nupkgDir}\""
    Trace.log $"Packed AOT %s{packageId} for %s{rid}."
  | None ->
    Trace.log
      $"Skipping AOT package for local RID %s{RuntimeInformation.RuntimeIdentifier}; no configured runtime match.")

Target.create "PackAnyFallback" (fun _ ->
  runDotNetCommand "pack" $"\"{projectPath}\" -c Release -r {fallbackRid} -p:PublishAot=false -o \"{nupkgDir}\""
  Trace.log $"Packed fallback %s{packageId} for %s{fallbackRid}.")

Target.create "PackPointer" (fun _ ->
  runDotNetCommand "pack" $"\"{projectPath}\" -c Release -o \"{nupkgDir}\""
  Trace.log $"Packed pointer %s{packageId} package.")

Target.create "Pack" (fun _ ->
  let localRidLabel =
    match currentLocalRid with
    | Some rid -> rid
    | None -> "none"

  Trace.log
    $"Packed %s{packageId} artifacts in %s{nupkgDir} (local AOT RID=%s{localRidLabel}, fallback=%s{fallbackRid}, pointer=enabled).")

Target.create "InstallGlobal" (fun _ ->
  let updateArgs =
    $"update --global {packageId} --add-source \"{nupkgDir}\" --ignore-failed-sources"

  let updateResult = DotNet.exec id "tool" updateArgs

  if updateResult.OK then
    Trace.log $"Updated global %s{packageId}."
  else
    installGlobalTool ())

Target.create "InstallGlobalDev" (fun _ ->
  let uninstallArgs = $"uninstall --global {packageId}"
  let uninstallResult = DotNet.exec id "tool" uninstallArgs

  if uninstallResult.OK then
    Trace.log $"Removed existing global %s{packageId} installation."
  else
    Trace.log $"No existing global %s{packageId} installation found; continuing."

  installGlobalTool ())

"Clean"
==> "Restore"
==> "Build"
==> "PackCurrentAot"
==> "PackAnyFallback"
==> "PackPointer"
==> "Pack"

"Pack" ==> "InstallGlobal"
"Pack" ==> "InstallGlobalDev"

Target.runOrDefault requestedTarget
