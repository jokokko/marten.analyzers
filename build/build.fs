open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Tools

System.Environment.GetCommandLineArgs()
|> Array.skip 1
|> Array.toList
|> Context.FakeExecutionContext.Create false "build.fs"
|> Context.RuntimeContext.Fake
|> Context.setExecutionContext

// build -e buildMode=Debug
Target.initEnvironment ()

let buildMode = Environment.environVarOrDefault "buildMode" "Release"
let buildArtifactPath = Path.getFullName "./artifacts"
let solution = "src/Marten.Analyzers.sln"
let baseVersion = "2.0.0"

let envVersion = Environment.environVarOrDefault "APPVEYOR_BUILD_VERSION" (baseVersion + ".0")
let buildVersion = envVersion.Substring(0, envVersion.LastIndexOf('.'))

let Version = (SemVer.parse buildVersion).ToString()

let branch =
  match Environment.environVarOrNone "APPVEYOR_REPO_BRANCH" with
  | Some name -> name
  | None -> Git.Information.getBranchName "."

let FileVersion = Environment.environVarOrDefault "APPVEYOR_BUILD_VERSION" (Version + ".0")

let InfoVersion =
  let label = if branch = "master" then "" else " (" + branch + "/" + (Git.Information.getCurrentSHA1 ".").[0..7] + ")"
  FileVersion + label

let NuGetVersion =
  let label = if branch = "master" then "" else "-" + branch
  let version = if branch = "master" then Version else FileVersion
  version + label

let versionArgs = [ "-p:Version=" + NuGetVersion
                    "-p:AssemblyVersion=" + FileVersion
                    "-p:FileVersion=" + FileVersion
                    "-p:InformationalVersion=" + InfoVersion ]

let dotnet args =
  let result = CreateProcess.fromRawCommand "dotnet" args |> Proc.run

  if result.ExitCode <> 0 then failwithf "dotnet %s failed" (List.head args)

Trace.logfn "Using version: %s" Version

Target.create "Clean" (fun _ ->
  Directory.ensure buildArtifactPath

  Shell.cleanDir buildArtifactPath
)

Target.create "RestorePackages" (fun _ ->
  dotnet [ "restore"; solution ]
)

Target.create "Build" (fun _ ->
  dotnet ([ "build"; solution; "-c"; buildMode; "--no-restore" ] @ versionArgs)
)

Target.create "Test" (fun _ ->
  !! "src/*.Tests/*.csproj"
  |> Seq.iter (fun project -> dotnet [ "test"; project; "-c"; buildMode; "--no-build" ])
)

Target.create "Package" (fun _ ->
  !! "src/*/*.csproj" -- "src/*.Tests/*.csproj"
  |> Seq.iter (fun project -> dotnet ([ "pack"; project; "-c"; buildMode; "-o"; buildArtifactPath; "--no-build" ] @ versionArgs))
)

Target.create "Default" ignore

"Clean"
  ==> "RestorePackages"
  ==> "Build"
  ==> "Test"
  ==> "Package"
  ==> "Default"
  |> ignore

try
  Target.runOrDefault "Default"
with ex ->
  Trace.traceError ex.Message
  exit 1
