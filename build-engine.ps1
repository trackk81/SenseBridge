param([string]$Toolchain = $env:SENSEBRIDGE_TOOLCHAIN)
$ErrorActionPreference='Stop'
if (-not $Toolchain) { $Toolchain='C:\msys64\clang64' }
$env:PATH=(Join-Path $Toolchain 'bin')+';'+$env:PATH
& (Join-Path $Toolchain 'bin\clang++.exe') -std=c++17 -O2 "$PSScriptRoot\source\generic_engine.cpp" "-I$Toolchain\include" "-L$Toolchain\lib" -lSDL3 -lole32 -luuid -lpropsys -lksuser -o "$PSScriptRoot\SenseBridge.Engine.exe"
if ($LASTEXITCODE -ne 0) { throw 'Engine build failed' }
& "$PSScriptRoot\SenseBridge.Engine.exe" --self-test
if ($LASTEXITCODE -ne 0) { throw 'Engine self-test failed' }
