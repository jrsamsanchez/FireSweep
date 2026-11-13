package tv.isafellie.os;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ActivityInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.graphics.Outline;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.GradientDrawable;
import android.view.Gravity;
import android.view.View;
import android.view.ViewOutlineProvider;
import android.widget.FrameLayout;
import android.widget.ImageView;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** Builds the rounded, focus-zooming tiles used by the dock and the app grid. */
final class Tiles {
    static final String AMAZON_LAUNCHER = "com.amazon.tv.launcher";

    private Tiles() {}

    static int dp(Context c, float v) {
        return Math.round(v * c.getResources().getDisplayMetrics().density);
    }

    /** One launchable app: its banner (or icon) and how to open it. */
    static final class App {
        final String pkg;
        final String label;
        final Intent intent;
        final Drawable banner;
        final boolean hasBanner;

        App(String pkg, String label, Intent intent, Drawable banner, boolean hasBanner) {
            this.pkg = pkg;
            this.label = label;
            this.intent = intent;
            this.banner = banner;
            this.hasBanner = hasBanner;
        }
    }

    /** Finds the TV launch activity for a package, or null when it isn't installed or is disabled. */
    static App find(Context c, String pkg) {
        PackageManager pm = c.getPackageManager();
        Intent i = pm.getLeanbackLaunchIntentForPackage(pkg);
        if (i == null) i = pm.getLaunchIntentForPackage(pkg);
        if (i == null) return null;
        try {
            ActivityInfo ai = pm.getActivityInfo(i.getComponent(), 0);
            return fromActivity(pm, ai, i);
        } catch (PackageManager.NameNotFoundException e) {
            return null;
        }
    }

    /** Every app with a TV launcher entry, sorted by name, without IsafellieOS itself. */
    static List<App> all(Context c) {
        PackageManager pm = c.getPackageManager();
        List<ResolveInfo> found = new ArrayList<>();
        found.addAll(pm.queryIntentActivities(
                new Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_LEANBACK_LAUNCHER), 0));
        List<String> seen = new ArrayList<>();
        List<App> apps = new ArrayList<>();
        for (ResolveInfo r : found) {
            String pkg = r.activityInfo.packageName;
            if (pkg.equals(c.getPackageName()) || seen.contains(pkg)) continue;
            seen.add(pkg);
            Intent i = new Intent(Intent.ACTION_MAIN)
                    .addCategory(Intent.CATEGORY_LEANBACK_LAUNCHER)
                    .setComponent(new ComponentName(pkg, r.activityInfo.name));
            apps.add(fromActivity(pm, r.activityInfo, i));
        }
        Collections.sort(apps, (a, b) -> a.label.compareToIgnoreCase(b.label));
        return apps;
    }

    private static App fromActivity(PackageManager pm, ActivityInfo ai, Intent i) {
        i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED);
        Drawable banner = ai.loadBanner(pm);
        if (banner == null) banner = ai.applicationInfo.loadBanner(pm);
        boolean hasBanner = banner != null;
        if (!hasBanner) banner = ai.loadIcon(pm);
        return new App(ai.packageName, String.valueOf(ai.loadLabel(pm)), i, banner, hasBanner);
    }

    /** A 16:9 tile showing an app banner, zooming up when focused like tvOS. */
    static View appTile(Context c, App app, int widthDp) {
        int w = dp(c, widthDp), h = Math.round(w * 9f / 16f);
        FrameLayout tile = baseTile(c, w, h, 0xFF2A2F33);
        ImageView img = new ImageView(c);
        img.setImageDrawable(app.banner);
        if (app.hasBanner) {
            img.setScaleType(ImageView.ScaleType.CENTER_CROP);
            tile.addView(img, new FrameLayout.LayoutParams(-1, -1));
        } else {
            img.setScaleType(ImageView.ScaleType.FIT_CENTER);
            int s = Math.round(h * 0.55f);
            FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(s, s, Gravity.CENTER);
            tile.addView(img, lp);
        }
        tile.setContentDescription(app.label);
        tile.setOnClickListener(v -> launch(c, app.intent));
        return tile;
    }

    /** Menu (or a long press of Select) on the tile opens the dock edit menu for its app. */
    static void editable(View tile, Tiles.App app, boolean onDock, Runnable changed) {
        tile.setOnLongClickListener(v -> {
            Dock.edit(v.getContext(), app, onDock, changed);
            return true;
        });
        tile.setOnKeyListener((v, keyCode, e) -> {
            if (keyCode != android.view.KeyEvent.KEYCODE_MENU) return false;
            if (e.getAction() == android.view.KeyEvent.ACTION_UP) Dock.edit(v.getContext(), app, onDock, changed);
            return true;
        });
    }

    /** A small frosted square with a white icon, for Apps and Settings. */
    static View iconTile(Context c, int icon, String caption, int sizeDp, View.OnClickListener click) {
        int s = dp(c, sizeDp);
        FrameLayout tile = baseTile(c, s, s, 0x40FFFFFF);
        tile.setOutlineProvider(roundOutline(dp(c, 14), 0f)); // no shadow through the frosted fill
        ImageView g = new ImageView(c);
        g.setImageResource(icon);
        int is = Math.round(s * 0.46f);
        tile.addView(g, new FrameLayout.LayoutParams(is, is, Gravity.CENTER));
        // Frosted tiles barely change when zoomed, so focus also turns them solid white.
        final View.OnFocusChangeListener zoom = tile.getOnFocusChangeListener();
        tile.setOnFocusChangeListener((v, has) -> {
            zoom.onFocusChange(v, has);
            ((GradientDrawable) v.getBackground()).setColor(has ? 0xF2FFFFFF : 0x40FFFFFF);
            g.setColorFilter(has ? 0xFF111416 : 0xFFFFFFFF);
        });
        tile.setContentDescription(caption);
        tile.setOnClickListener(click);
        return tile;
    }

    private static FrameLayout baseTile(Context c, int w, int h, int fill) {
        final float radius = dp(c, 14);
        FrameLayout tile = new FrameLayout(c);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(fill);
        bg.setCornerRadius(radius);
        tile.setBackground(bg);
        tile.setLayoutParams(new FrameLayout.LayoutParams(w, h));
        tile.setClipToOutline(true);
        tile.setOutlineProvider(roundOutline(radius, 1f));
        tile.setFocusable(true);
        tile.setFocusableInTouchMode(true);
        tile.setElevation(dp(c, 4));
        tile.setOnFocusChangeListener((v, has) -> {
            float s = has ? 1.1f : 1f;
            v.animate().scaleX(s).scaleY(s).translationZ(has ? dp(c, 18) : 0)
                    .setDuration(160).start();
            v.setAlpha(1f);
        });
        return tile;
    }

    private static ViewOutlineProvider roundOutline(final float radius, final float shadowAlpha) {
        return new ViewOutlineProvider() {
            @Override public void getOutline(View v, Outline o) {
                o.setRoundRect(0, 0, v.getWidth(), v.getHeight(), radius);
                o.setAlpha(shadowAlpha);
            }
        };
    }

    static void launch(Context c, Intent i) {
        try {
            c.startActivity(i);
        } catch (Exception e) {
            android.widget.Toast.makeText(c, "Couldn't open that app", android.widget.Toast.LENGTH_SHORT).show();
        }
    }

    /** Opens Amazon's full Settings menu, which lives inside its home screen, without being redirected. */
    static void openAmazonSettings(Context c) {
        HomeGuard.allowAmazonHome(c);
        launch(c, new Intent().setComponent(new ComponentName(AMAZON_LAUNCHER, AMAZON_LAUNCHER + ".ui.SettingsActivity"))
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
    }

    /** Opens Amazon's own home (for profiles and anything else that lives there) without being redirected. */
    static void openAmazonHome(Context c) {
        HomeGuard.allowAmazonHome(c);
        Intent i = new Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME)
                .setPackage(AMAZON_LAUNCHER).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        launch(c, i);
    }
}
