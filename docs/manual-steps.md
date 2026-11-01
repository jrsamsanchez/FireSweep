# Doing it by hand

Everything FireSweep does is ordinary `adb` commands. If you'd rather run them yourself, or want to see exactly what the app changes, here they are. Nothing here uninstalls an Amazon app. `pm disable-user` hides and stops a package, and `pm enable` brings it back.

## 1. Get adb and connect

1. Download Google's [SDK Platform-Tools for Windows](https://developer.android.com/tools/releases/platform-tools) and unzip it, for example to `C:\platform-tools`.
2. Turn on ADB debugging on the TV ([guide](enable-adb.md)).
3. Open PowerShell in that folder and connect, using your TV's address:

```powershell
.\adb connect 192.168.1.50:5555
.\adb devices          # the TV should say "device", not "unauthorized"
```

The examples below assume one TV is connected. With several, add `-s 192.168.1.50:5555` after `adb`.

## 2. Back up first

Save what's on the TV now so you can compare or undo later:

```powershell
mkdir backup
.\adb shell pm list packages -d        > backup\packages-disabled.txt
.\adb shell pm list packages -e        > backup\packages-enabled.txt
.\adb shell settings list secure       > backup\settings-secure.txt
.\adb shell settings list global       > backup\settings-global.txt
.\adb shell settings get secure enabled_accessibility_services > backup\accessibility.txt
```

Keep `packages-disabled.txt`: anything not in it that you disable later is yours to re-enable.

## 3. Switch off ads, tracking and clutter

Run `pm disable-user` for each package you want off. A package that isn't on your TV just prints an error, which is safe to ignore.

```powershell
.\adb shell pm disable-user --user 0 com.amazon.tv.acr
```

| Group | What | Packages |
|---|---|---|
| Ads and tracking | Screen content recognition (ACR) | `com.amazon.tv.acr` |
| | Advertising ID | `com.amazon.hybridadidservice` |
| | Ad click-through links | `com.amazon.ftvads.deeplinking` |
| | Usage and device metrics | `com.amazon.tv.fw.metrics` `com.amazon.device.metrics` `com.amazon.wirelessmetrics.service` `com.amazon.perfcollection` |
| | Crash and log uploads | `com.amazon.device.crashmanager` `com.amazon.device.logmanager` `com.amazon.dp.logger` `com.amazon.neo.minerva.logcat` |
| Clutter | Autoplaying featured previews | `com.amazon.sneakpeek` |
| | Shopping overlays | `com.amazon.shoptv.client` `com.amazon.shoptv.firetv.client` |
| | Amazon miniTV | `com.amazon.minitv.android.app` |
| | Store demo content | `com.amazon.tv.democontent.provider` |
| | Tutorials and "what's new" | `com.amazon.storm.lightning.tutorial` `com.amazon.tmm.tutorial` `com.amazon.tv.releasenotes` |
| Amazon features (only if unused) | Alexa (the remote's voice button stops working) | `com.amazon.vizzini` `com.amazon.vizzini.ftvcds` `com.amazon.alexamediaplayer.runtime.ftv` `com.amazon.alexa.externalmediaplayer.fireos` `com.amazon.tv.alexaalerts` `com.amazon.tv.alexanotifications` `com.ivona.orchestrator` |
| | Profiles picker | `com.amazon.ftv.profilepicker` |
| | Amazon Photos | `com.amazon.bueller.photos` |
| | Amazon Music | `com.amazon.bueller.music` |
| | Silk browser | `com.amazon.cloud9` |
| | Amazon Kids | `com.amazon.tahoe` |
| | Games hub | `com.amazon.gamehub` |
| | Matter smart-home | `com.amazon.tv.matter` `com.amazon.tv.mattercohost` |
| | Antenna live TV | `com.amazon.tv.livetv` `com.amazon.tv.channelscan` |
| | Ambient screensaver | `com.amazon.ftv.screensaver` |
| | Automatic Fire OS updates (not recommended) | `com.amazon.device.software.ota` `com.amazon.tv.forcedotaupdater.v2` |

**Leave these alone:** Amazon's home screen (`com.amazon.tv.launcher`), anything with `droidlogic`, `tvinput`, `inputpreference` or `tuner` in its name (HDMI inputs and the TV tuner), settings, the keyboard and the Appstore. Disabling them can break input switching or setup.

### Protected packages

Fire OS refuses to disable a few packages (the command prints an error instead of `new state: disabled-user`). For those, stop them running in the background instead:

```powershell
.\adb shell cmd appops set com.amazon.device.metrics RUN_IN_BACKGROUND ignore
```

## 4. Privacy settings

These are the same switches as **Settings → Preferences → Privacy settings** and **Data monitoring**. Note each value first (`settings get secure <name>`) so you can put it back.

```powershell
.\adb shell settings put secure limit_ad_tracking 1
.\adb shell settings put secure amz_limit_ad_tracking 1
.\adb shell settings put secure non_amz_limit_ad_tracking 1
.\adb shell settings put secure USAGE_METRICS_UPLOAD_ENABLED 0
.\adb shell settings put secure usage_metrics_marketing_enabled 0
.\adb shell settings put secure "advertisingIdApp:interestBasedAds:value" 0
.\adb shell settings put secure "settingsApp:appUsageData:value" 0
.\adb shell settings put secure "settingsApp:deviceUsageData:value" 0
```

## 5. Optional: the IsafellieOS home screen

Fire OS 7.7 won't let another app become the default home screen. IsafellieOS works around that with an accessibility service: when the Home button is pressed, or Amazon's home appears, it opens IsafellieOS instead. Amazon's home can flash for a moment.

```powershell
# Install it (the APK is in launcher\ in this repo, or built into FireSweep.exe)
.\adb install -r IsafellieOS.apk

# Turn on its Home-button redirect. If other accessibility services are already on,
# join them with ':' instead of replacing them.
.\adb shell settings put secure enabled_accessibility_services tv.isafellie.os/tv.isafellie.os.HomeRedirectService
.\adb shell settings put secure accessibility_enabled 1

# Set the dock, left to right (package names from "pm list packages")
.\adb shell am broadcast -n tv.isafellie.os/.DockReceiver -a tv.isafellie.os.DOCK --es dock com.netflix.ninja,com.amazon.firetv.youtube,com.amazon.firebat

# Open it
.\adb shell am start -n tv.isafellie.os/.HomeActivity
```

After reinstalling the APK, set `enabled_accessibility_services` to something else and back again. Otherwise Fire OS keeps the old copy unbound and the Home button stops redirecting.

You can also change the dock from the TV: highlight an app and press **Menu** (☰) on the remote.

## 6. Undo

```powershell
# Each package you disabled
.\adb shell pm enable com.amazon.tv.acr

# Each protected package you restricted
.\adb shell cmd appops set com.amazon.device.metrics RUN_IN_BACKGROUND default

# Each setting, back to the value you noted (or delete it if it was "null")
.\adb shell settings put secure limit_ad_tracking 0

# The home screen
.\adb shell settings delete secure enabled_accessibility_services
.\adb shell pm uninstall tv.isafellie.os
```

Then restart the TV: **Settings → My Fire TV → Restart**.
