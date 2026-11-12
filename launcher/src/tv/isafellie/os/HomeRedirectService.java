package tv.isafellie.os;

import android.accessibilityservice.AccessibilityService;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.util.Log;
import android.view.KeyEvent;
import android.view.accessibility.AccessibilityEvent;

import java.util.HashSet;
import java.util.Set;

/**
 * Fire OS 7.7 won't let another app be the default home, so the Home button always opens
 * Amazon's launcher. This catches the Home key first and opens IsafellieOS instead (a long
 * press opens Settings, since Amazon's quick menu can't be launched from outside). If Amazon's
 * home still comes up some other way, such as after a restart, it's replaced as soon as it
 * appears. Choosing "Amazon Home" in IsafellieOS lets it through until another app comes up.
 *
 * Amazon's home isn't the same activity on every Fire TV, so it's recognised as any screen that
 * answers the Home intent on this TV (other than IsafellieOS), plus Amazon's usual HomeActivity.
 */
public class HomeRedirectService extends AccessibilityService {
    private static final String PREFS = "redirect";
    private static final String ALLOW = "allowAmazonHome";
    private static final long LONG_PRESS_MS = 600;

    static final String TAG = "IsafellieOS";

    private long homeDownAt;
    /** "package/class" of every other home screen on this TV, and the packages they're in. */
    private final Set<String> homeScreens = new HashSet<>(), homePackages = new HashSet<>();

    @Override
    protected void onServiceConnected() {
        super.onServiceConnected();
        findHomeScreens();
        Log.i(TAG, "Home redirect running; other home screens: " + homeScreens);
    }

    private void findHomeScreens() {
        homeScreens.clear();
        homePackages.clear();
        homePackages.add(Tiles.AMAZON_LAUNCHER);
        Intent home = new Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME);
        for (ResolveInfo r : getPackageManager().queryIntentActivities(home, PackageManager.MATCH_ALL)) {
            String pkg = r.activityInfo.packageName;
            if (pkg.equals(getPackageName()) || pkg.equals("android") || pkg.equals("com.android.settings")) continue;
            homeScreens.add(pkg + "/" + r.activityInfo.name);
            homePackages.add(pkg);
        }
    }

    private boolean isOtherHome(CharSequence pkg, CharSequence cls) {
        if (cls == null) return false;
        String p = pkg.toString(), c = cls.toString();
        if (homeScreens.contains(p + "/" + c)) return true;
        return homePackages.contains(p) && c.contains("HomeActivity");
    }

    static void allowAmazonHome(Context c) {
        c.getSharedPreferences(PREFS, MODE_PRIVATE).edit().putBoolean(ALLOW, true).apply();
    }

    private void setAllowed(boolean allowed) {
        getSharedPreferences(PREFS, MODE_PRIVATE).edit().putBoolean(ALLOW, allowed).apply();
    }

    private void openIsafellie() {
        startActivity(new Intent(this, HomeActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_NO_ANIMATION
                        | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT));
    }

    @Override
    protected boolean onKeyEvent(KeyEvent e) {
        if (e.getKeyCode() != KeyEvent.KEYCODE_HOME) return false;
        if (e.getAction() == KeyEvent.ACTION_DOWN) {
            if (e.getRepeatCount() == 0) homeDownAt = e.getEventTime();
        } else if (e.getAction() == KeyEvent.ACTION_UP) {
            setAllowed(false);
            if (e.getEventTime() - homeDownAt >= LONG_PRESS_MS) {
                startActivity(new Intent(this, SettingsActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            } else {
                openIsafellie();
            }
        }
        return true;
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent e) {
        if (e.getEventType() != AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED) return;
        CharSequence pkg = e.getPackageName(), cls = e.getClassName();
        if (pkg == null) return;

        boolean amazonHome = isOtherHome(pkg, cls);
        boolean allowed = getSharedPreferences(PREFS, MODE_PRIVATE).getBoolean(ALLOW, false);

        if (amazonHome) {
            Log.i(TAG, (allowed ? "Letting Amazon home through: " : "Replacing Amazon home: ") + pkg + "/" + cls);
            if (!allowed) openIsafellie();
        } else if (allowed && !isOverlay(pkg) && !homePackages.contains(pkg.toString())) {
            setAllowed(false);
        }
    }

    /** System overlays and the keyboard pop up over Amazon home without leaving it. */
    private static boolean isOverlay(CharSequence pkg) {
        String p = pkg.toString();
        return p.equals("com.android.systemui") || p.equals("com.amazon.tv.ime")
                || p.startsWith("com.amazon.tv.quicksettings") || p.equals("com.amazon.tv.notificationcenter")
                || p.equals("android") || p.equals(Tiles.AMAZON_LAUNCHER);
    }

    @Override
    public void onInterrupt() {}
}
