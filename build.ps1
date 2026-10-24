param([switch]$Development,[switch]$RunSelfTest)
$buildDefine=if($Development){'/define:SENSEBRIDGE_DEVELOPMENT'}else{'/define:SENSEBRIDGE_RELEASE'}
$ErrorActionPreference='Stop'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo $buildDefine "/resource:$PSScriptRoot\build\runtime-bundle.zip,SenseBridge.Runtime.zip" /platform:x64 /target:winexe /optimize+ "/win32icon:$PSScriptRoot\SenseBridge.ico" "/out:$PSScriptRoot\SenseBridge.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$PSScriptRoot\source\AssemblyInfo.cs" "$PSScriptRoot\source\Localization.cs" "$PSScriptRoot\source\SettingsForm.cs" "$PSScriptRoot\source\SteamLibrary.cs" "$PSScriptRoot\source\GameGallery.cs" "$PSScriptRoot\source\ModernControls.cs" "$PSScriptRoot\source\ProfileRouter.cs" "$PSScriptRoot\source\SenseBridge.cs" "$PSScriptRoot\source\LiveAttach.cs" "$PSScriptRoot\source\GenericForm.cs" "$PSScriptRoot\source\ItemAudioForm.cs" "$PSScriptRoot\source\Presets.cs" "$PSScriptRoot\source\EventPacks.cs" "$PSScriptRoot\source\Ac6TriggerForm.cs" "$PSScriptRoot\source\Ac6Runtime.cs" "$PSScriptRoot\source\Ac6Data.cs" "$PSScriptRoot\source\RuntimeBundle.cs" "$PSScriptRoot\source\ac6\Ac6Compatibility.cs" "$PSScriptRoot\source\ac6\Ac6TraceConnection.cs" "$PSScriptRoot\source\PackDownloads.cs" "$PSScriptRoot\source\GameDataCatalog.cs" "$PSScriptRoot\source\AppUpdates.cs" "$PSScriptRoot\source\UpdatePackage.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed. Exit SenseBridge from its tray menu before rebuilding.'}
if($RunSelfTest){
 Start-Process -FilePath "$PSScriptRoot\SenseBridge.exe" -ArgumentList '--self-test' -WindowStyle Hidden -Wait
 Get-Content "$PSScriptRoot\self-test.txt"
}

& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /target:winexe /optimize+ "/out:$PSScriptRoot\SenseBridge.Updater.exe" /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$PSScriptRoot\source\UpdatePackage.cs" "$PSScriptRoot\source\updater\Program.cs"
if($LASTEXITCODE -ne 0){throw 'Updater build failed'}
