param([switch]$Development)
$buildDefine=if($Development){'/define:SENSEBRIDGE_DEVELOPMENT'}else{'/define:SENSEBRIDGE_RELEASE'}
$ErrorActionPreference='Stop'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo $buildDefine /platform:x64 /target:winexe /optimize+ "/win32icon:$PSScriptRoot\SenseBridge.ico" "/out:$PSScriptRoot\SenseBridge.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\source\AssemblyInfo.cs" "$PSScriptRoot\source\Localization.cs" "$PSScriptRoot\source\SettingsForm.cs" "$PSScriptRoot\source\SteamLibrary.cs" "$PSScriptRoot\source\GameGallery.cs" "$PSScriptRoot\source\ModernControls.cs" "$PSScriptRoot\source\ProfileRouter.cs" "$PSScriptRoot\source\SenseBridge.cs" "$PSScriptRoot\source\LiveAttach.cs" "$PSScriptRoot\source\GenericForm.cs" "$PSScriptRoot\source\ItemAudioForm.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed. Exit SenseBridge from its tray menu before rebuilding.'}
Start-Process -FilePath "$PSScriptRoot\SenseBridge.exe" -ArgumentList '--self-test' -Wait
Get-Content "$PSScriptRoot\self-test.txt"
