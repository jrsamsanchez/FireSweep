# Builds IsafellieOS.apk without Gradle: aapt2 -> javac -> d8 -> zipalign -> apksigner.
$ErrorActionPreference = 'Stop'
# Needs a JDK 17 folder (jdk-17*) and an Android SDK with build-tools 34.0.0 and platform 34 in one folder.
$tc   = if ($env:ISAFELLIEOS_TOOLCHAIN) { $env:ISAFELLIEOS_TOOLCHAIN } else { "$env:USERPROFILE\isafellieos-toolchain" }
$jdk  = (Get-ChildItem $tc -Directory -Filter 'jdk-17*').FullName
$bt   = "$tc\sdk\build-tools\34.0.0"
$jar  = "$tc\sdk\platforms\android-34\android.jar"
$here = $PSScriptRoot
$out  = Join-Path $here 'build'
$env:JAVA_HOME = $jdk; $env:Path = "$jdk\bin;$env:Path"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force "$out\gen", "$out\classes", "$out\dex" | Out-Null

& "$bt\aapt2.exe" compile --dir "$here\res" -o "$out\res.zip"
if ($LASTEXITCODE) { throw 'aapt2 compile failed' }
# Assets are added below: aapt2 -A on Windows stores them with backslash paths the TV can't find.
& "$bt\aapt2.exe" link -I $jar --manifest "$here\AndroidManifest.xml" `
    --java "$out\gen" -o "$out\unsigned.apk" "$out\res.zip"
if ($LASTEXITCODE) { throw 'aapt2 link failed' }

$sources = Get-ChildItem "$here\src", "$out\gen" -Recurse -Filter *.java | ForEach-Object FullName
& javac --release 8 -encoding UTF-8 -nowarn -classpath $jar -d "$out\classes" @sources
if ($LASTEXITCODE) { throw 'javac failed' }

$classes = Get-ChildItem "$out\classes" -Recurse -Filter *.class | ForEach-Object FullName
& "$bt\d8.bat" --release --lib $jar --min-api 25 --output "$out\dex" @classes
if ($LASTEXITCODE) { throw 'd8 failed' }

# Add classes.dex and the assets (stored uncompressed, forward-slash paths) to the APK.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open("$out\unsigned.apk", 'Update')
[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, "$out\dex\classes.dex", 'classes.dex') | Out-Null
foreach ($f in Get-ChildItem "$here\assets" -Recurse -File) {
    $name = 'assets/' + $f.FullName.Substring("$here\assets\".Length).Replace('\', '/')
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $name, 'NoCompression') | Out-Null
}
$zip.Dispose()

& "$bt\zipalign.exe" -f -p 4 "$out\unsigned.apk" "$out\aligned.apk"
if ($LASTEXITCODE) { throw 'zipalign failed' }

$ks = Join-Path $here 'isafellieos.keystore'
if (-not (Test-Path $ks)) {
    cmd /c "keytool -genkeypair -keystore `"$ks`" -storepass isafellie -keypass isafellie -alias isafellieos -keyalg RSA -keysize 2048 -validity 10000 -dname CN=IsafellieOS 2>nul"
}
& "$bt\apksigner.bat" sign --ks $ks --ks-pass pass:isafellie --key-pass pass:isafellie `
    --out "$here\IsafellieOS.apk" "$out\aligned.apk"
if ($LASTEXITCODE) { throw 'apksigner failed' }
Get-Item "$here\IsafellieOS.apk" | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
