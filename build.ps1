# Builds FireSweep.exe: one file with adb and the IsafellieOS launcher built in.
# Uses the C# compiler that ships with Windows (.NET Framework 4.x), so nothing needs installing.
# adb comes from %USERPROFILE%\platform-tools if it's there; otherwise Google's platform-tools
# are downloaded into build\ once.
param([string]$Out)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
if (-not $Out) { $Out = "$here\FireSweep.exe" }
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$apk  = "$here\launcher\IsafellieOS.apk"
$adb  = "$env:USERPROFILE\platform-tools"
if (-not (Test-Path "$adb\adb.exe")) {
    $adb = "$here\build\platform-tools"
    if (-not (Test-Path "$adb\adb.exe")) {
        New-Item -ItemType Directory -Force "$here\build" | Out-Null
        $zip = "$here\build\platform-tools.zip"
        Invoke-WebRequest 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' -OutFile $zip -UseBasicParsing
        Expand-Archive $zip "$here\build" -Force
    }
}

if (-not (Test-Path "$here\logo.png")) { & "$here\make-icon.ps1" }
$resources = @("$adb\adb.exe", "$adb\AdbWinApi.dll", "$adb\AdbWinUsbApi.dll", $apk, "$here\logo.png") | ForEach-Object {
    if (-not (Test-Path $_)) { throw "Missing $_" }
    "/resource:$_,$(Split-Path $_ -Leaf)"
}
# The home screen preview draws on one of the launcher's wallpapers.
$resources += "/resource:$here\launcher\assets\wallpapers\11.jpg,wallpaper.jpg"
$icon = Join-Path $here 'icon.ico'
if (-not (Test-Path $icon)) { & "$here\make-icon.ps1" }
$sources = Get-ChildItem "$here\src" -Filter *.cs | ForEach-Object FullName

& $csc /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu "/out:$Out" "/win32icon:$icon" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Net.Http.dll $resources $sources
if ($LASTEXITCODE) { throw 'csc failed' }
Get-Item $Out | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
