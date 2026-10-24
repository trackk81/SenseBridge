param(
 [Parameter(Mandatory=$true)][string]$UnityManaged,
 [Parameter(Mandatory=$true)][string]$BepInExCore,
 [string]$Toolchain=$env:SENSEBRIDGE_TOOLCHAIN
)
$ErrorActionPreference='Stop'
if(!$Toolchain){$Toolchain='C:\msys64\clang64'}
$output=Join-Path $PSScriptRoot 'UnityRuntime\BepInEx\plugins\SenseBridge'
New-Item -ItemType Directory -Force $output | Out-Null
$references=@('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.AudioModule.dll') | ForEach-Object { '/reference:'+(Join-Path $UnityManaged $_) }
$references+=@('/reference:'+(Join-Path $BepInExCore 'BepInEx.dll'),'/reference:'+(Join-Path $BepInExCore '0Harmony.dll'))
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /noconfig /nologo /nostdlib /target:library "/out:$output\SenseBridge.UnityBridge.dll" $references "$PSScriptRoot\source\unity\UnityBridge.cs" "$PSScriptRoot\source\unity\BindingResolver.cs"
if($LASTEXITCODE -ne 0){throw 'Unity bridge build failed'}
& (Join-Path $Toolchain 'bin\clang++.exe') -std=c++17 -O2 "-I$Toolchain\include" "$PSScriptRoot\source\unity\event_engine.cpp" "-L$Toolchain\lib" -lSDL3 -lws2_32 -o "$PSScriptRoot\SenseBridge.Events.exe"
if($LASTEXITCODE -ne 0){throw 'Audio event engine build failed'}
# The runtime distribution must separately include the official compatible BepInEx
# loader, its core dependencies and license notices. Never copy game assemblies.
