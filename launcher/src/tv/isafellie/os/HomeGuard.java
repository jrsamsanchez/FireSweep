package tv.isafellie.os;

import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.util.Log;

import java.util.HashSet;
import java.util.Set;

/**
 * Decides when Amazon's home has come up and should be swapped for IsafellieOS. Shared by the
 * two ways IsafellieOS watches the screen: the accessibility service ({@link HomeRedirectService})
 * and, on Fire TVs that won't run it, {@link HomeWatchService}.
 *
 * Amazon's home isn't the same activity on every Fire TV, so it's recognised as any screen that
 * answers the Home intent on this TV (other than IsafellieOS), plus Amazon's usual HomeActivity.
 * Choosing "Amazon Home" in IsafellieOS lets it through until another app comes up.
 */
final class HomeGuard {
    static final String TAG = "IsafellieOS";
    private static final String PREFS = "redirect";
    private static final String ALLOW = "allowAmazonHome";

    private final Context c;
    /** "package/class" of every other home screen on this TV, and the packages they're in. */
    private final Set<String> homeScreens = new HashSet<>(), homePackages = new HashSet<>();

    HomeGuard(Context c) {
        this.c = c;
        homePackages.add(Tiles.AMAZON_LAUNCHER);
        Intent home = new Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME);
        for (ResolveInfo r : c.getPackageManager().queryIntentActivities(home, PackageManager.MATCH_ALL)) {
            String pkg = r.activityInfo.packageName;
            if (pkg.equals(c.getPackageName()) || pkg.equals("android") || pkg.equals("com.android.settings")) continue;
            homeScreens.add(pkg + "/" + r.activityInfo.name);
            homePackages.add(pkg);
        }
        Log.i(TAG, "Other home screens: " + homeScreens);
    }

    private static SharedPreferences prefs(Context c) {
        return c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    /** Lets Amazon's home stay up until another app comes to the front. */
    static void allowAmazonHome(Context c) {
        prefs(c).edit().putBoolean(ALLOW, true).apply();
    }

    static void stopAllowing(Context c) {
        prefs(c).edit().putBoolean(ALLOW, false).apply();
    }

    void openIsafellie() {
        c.startActivity(new Intent(c, HomeActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_NO_ANIMATION
                        | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT));
    }

    private boolean isOtherHome(String pkg, String cls) {
        if (cls == null) return false;
        return homeScreens.contains(pkg + "/" + cls) || (homePackages.contains(pkg) && cls.contains("HomeActivity"));
    }

    /** Call when an activity comes to the front. Opens IsafellieOS if it's Amazon's home and that isn't allowed. */
    void onScreen(String pkg, String cls) {
        if (pkg == null) return;
        boolean allowed = prefs(c).getBoolean(ALLOW, false);
        if (isOtherHome(pkg, cls)) {
            Log.i(TAG, (allowed ? "Letting Amazon home through: " : "Replacing Amazon home: ") + pkg + "/" + cls);
            if (!allowed) openIsafellie();
        } else if (allowed && !isOverlay(pkg) && !homePackages.contains(pkg) && !pkg.equals(c.getPackageName())) {
            stopAllowing(c);
        }
    }

    /** System overlays and the keyboard pop up over Amazon home without leaving it. */
    private static boolean isOverlay(String p) {
        return p.equals("com.android.systemui") || p.equals("com.amazon.tv.ime")
                || p.startsWith("com.amazon.tv.quicksettings") || p.equals("com.amazon.tv.notificationcenter")
                || p.equals("android");
    }
}
