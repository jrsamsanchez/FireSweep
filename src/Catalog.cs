using System.Collections.Generic;

enum Kind { Packages, Settings, Launcher }

/// <summary>One checkbox in the list: a change the tool can make, and undo, on the TV.</summary>
class Item
{
    public string Group, Label, Note;
    public string[] Packages;
    public bool Recommended;
    public Kind Kind;

    public Item(string group, string label, string note, bool recommended, params string[] packages)
    {
        Group = group; Label = label; Note = note; Recommended = recommended; Packages = packages;
        Kind = Kind.Packages;
    }
}

static class Catalog
{
    public const string Ads = "Ads and tracking (recommended)";
    public const string Junk = "Clutter (recommended)";
    public const string Features = "Amazon features: turn off only the ones nobody in the house uses";
    public const string Home = "Home screen";

    public const string LauncherPackage = "tv.isafellie.os";
    public const string LauncherService = "tv.isafellie.os/tv.isafellie.os.HomeRedirectService";
    public const int LauncherVersion = 4;

    /// <summary>Privacy settings in the secure namespace, and the value that turns each off.</summary>
    public static readonly KeyValuePair<string, string>[] PrivacySettings =
    {
        new KeyValuePair<string, string>("limit_ad_tracking", "1"),
        new KeyValuePair<string, string>("amz_limit_ad_tracking", "1"),
        new KeyValuePair<string, string>("non_amz_limit_ad_tracking", "1"),
        new KeyValuePair<string, string>("USAGE_METRICS_UPLOAD_ENABLED", "0"),
        new KeyValuePair<string, string>("usage_metrics_marketing_enabled", "0"),
        new KeyValuePair<string, string>("advertisingIdApp:interestBasedAds:value", "0"),
        new KeyValuePair<string, string>("settingsApp:appUsageData:value", "0"),
        new KeyValuePair<string, string>("settingsApp:deviceUsageData:value", "0"),
    };

    public static readonly Item[] Items =
    {
        new Item(Ads, "Stop screen content recognition (ACR)", "Watches what's on screen, HDMI inputs included", true, "com.amazon.tv.acr"),
        new Item(Ads, "Turn off the advertising ID", "Ad profile service", true, "com.amazon.hybridadidservice"),
        new Item(Ads, "Turn off ad click-through links", "Opens ads from the home screen", true, "com.amazon.ftvads.deeplinking"),
        new Item(Ads, "Stop usage and device metrics", "Uploads what you watch and how the TV is used", true,
            "com.amazon.tv.fw.metrics", "com.amazon.device.metrics", "com.amazon.wirelessmetrics.service", "com.amazon.perfcollection"),
        new Item(Ads, "Stop crash and log uploads", "Sends logs to Amazon", true,
            "com.amazon.device.crashmanager", "com.amazon.device.logmanager", "com.amazon.dp.logger", "com.amazon.neo.minerva.logcat"),
        new Item(Ads, "Privacy settings: limit ad tracking, no usage data sharing", "The same switches as Settings > Preferences > Privacy", true)
            { Kind = Kind.Settings },

        new Item(Junk, "Remove autoplaying featured previews", "Sneak peek videos on the home screen", true, "com.amazon.sneakpeek"),
        new Item(Junk, "Remove Amazon shopping overlays", "Shop TV", true, "com.amazon.shoptv.client", "com.amazon.shoptv.firetv.client"),
        new Item(Junk, "Remove Amazon miniTV", "Ad-supported video app", true, "com.amazon.minitv.android.app"),
        new Item(Junk, "Remove store demo content", "", true, "com.amazon.tv.democontent.provider"),
        new Item(Junk, "Remove tutorials and \"what's new\" pop-ups", "", true,
            "com.amazon.storm.lightning.tutorial", "com.amazon.tmm.tutorial", "com.amazon.tv.releasenotes"),

        new Item(Features, "Turn off Alexa", "The voice button on the remote stops working", false,
            "com.amazon.vizzini", "com.amazon.vizzini.ftvcds", "com.amazon.alexamediaplayer.runtime.ftv",
            "com.amazon.alexa.externalmediaplayer.fireos", "com.amazon.tv.alexaalerts",
            "com.amazon.tv.alexanotifications", "com.ivona.orchestrator"),
        new Item(Features, "Turn off profiles", "The \"Who's watching?\" picker", false, "com.amazon.ftv.profilepicker"),
        new Item(Features, "Turn off Amazon Photos", "", false, "com.amazon.bueller.photos"),
        new Item(Features, "Turn off Amazon Music", "", false, "com.amazon.bueller.music"),
        new Item(Features, "Turn off the Silk browser", "", false, "com.amazon.cloud9"),
        new Item(Features, "Turn off Amazon Kids", "", false, "com.amazon.tahoe"),
        new Item(Features, "Turn off the Amazon games hub", "", false, "com.amazon.gamehub"),
        new Item(Features, "Turn off Matter smart-home controls", "", false, "com.amazon.tv.matter", "com.amazon.tv.mattercohost"),
        new Item(Features, "Turn off antenna channel scan", "Live TV itself stays on because HDMI inputs need it", false, "com.amazon.tv.channelscan"),
        new Item(Features, "Turn off Amazon's ambient screensaver", "", false, "com.amazon.ftv.screensaver"),
        new Item(Features, "Turn off automatic Fire OS updates", "Not recommended: updates carry security fixes", false,
            "com.amazon.device.software.ota", "com.amazon.tv.forcedotaupdater.v2"),

        new Item(Home, "Use IsafellieOS as the home screen", "Pick its dock apps on the Home screen dock tab", false, LauncherPackage)
            { Kind = Kind.Launcher },
    };

    /// <summary>Dock order the launcher starts with when nothing else is chosen.</summary>
    public static readonly string[] DefaultDock =
    {
        "com.netflix.ninja", "com.amazon.firetv.youtube", "com.amazon.firebat", "com.hbo.hbonow",
        "com.apple.atve.amazon.appletv", "com.disney.disneyplus",
    };

    /// <summary>Launchable apps that make no sense on the dock.</summary>
    public static bool HiddenFromDock(string pkg)
    {
        return pkg == LauncherPackage || pkg.StartsWith("com.apple.airplay") || pkg == "com.amazon.spiderpork";
    }

    static readonly Dictionary<string, string> names = new Dictionary<string, string>
    {
        { "com.netflix.ninja", "Netflix" }, { "com.amazon.firetv.youtube", "YouTube" },
        { "com.amazon.firetv.youtube.tv", "YouTube TV" }, { "com.amazon.firebat", "Prime Video" },
        { "com.hbo.hbonow", "Max" }, { "com.apple.atve.amazon.appletv", "Apple TV" },
        { "com.disney.disneyplus", "Disney+" }, { "com.hulu.plus", "Hulu" },
        { "com.peacock.peacockfiretv", "Peacock" }, { "com.cbs.ott", "Paramount+" },
        { "com.plexapp.android", "Plex" }, { "org.xbmc.kodi", "Kodi" }, { "tv.pluto.android", "Pluto TV" },
        { "com.tubitv", "Tubi" }, { "com.espn.gtv", "ESPN" }, { "com.fubo.firetv.screen", "Fubo" },
        { "com.spotify.tv.android", "Spotify" }, { "com.mxtech.videoplayer.pro", "MX Player Pro" },
        { "com.esaba.downloader", "Downloader" }, { "link.unlinked", "Unlinked" },
        { "com.volleygames.phoenix", "Volley games" }, { "com.sling", "Sling TV" },
        { "tv.twitch.android.viewer", "Twitch" }, { "com.crunchyroll.crunchyroid", "Crunchyroll" },
        { "com.philo.philo", "Philo" }, { "com.amazon.rialto", "Freevee" }, { "com.amazon.cloud9", "Silk browser" },
        { "com.amazon.bueller.photos", "Amazon Photos" }, { "com.amazon.bueller.music", "Amazon Music" },
    };

    public static string AppName(string pkg)
    {
        string n;
        if (names.TryGetValue(pkg, out n)) return n;
        return pkg;
    }
}
