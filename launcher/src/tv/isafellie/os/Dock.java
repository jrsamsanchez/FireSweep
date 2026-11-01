package tv.isafellie.os;

import android.app.AlertDialog;
import android.content.Context;
import android.content.SharedPreferences;
import android.text.TextUtils;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/** The dock's apps, in order, saved on the TV so they can be changed from the remote. */
final class Dock {
    /** First-run dock, in this order: Netflix, YouTube, Prime Video, Max, Apple TV, Disney+. */
    private static final String[] DEFAULT = {
            "com.netflix.ninja",
            "com.amazon.firetv.youtube",
            "com.amazon.firebat",
            "com.hbo.hbonow",
            "com.apple.atve.amazon.appletv",
            "com.disney.disneyplus",
    };
    private static final String KEY = "dock";

    private Dock() {}

    private static SharedPreferences prefs(Context c) {
        return c.getSharedPreferences("home", Context.MODE_PRIVATE);
    }

    static List<String> load(Context c) {
        String saved = prefs(c).getString(KEY, null);
        if (saved == null) return new ArrayList<>(Arrays.asList(DEFAULT));
        if (saved.isEmpty()) return new ArrayList<>();
        return new ArrayList<>(Arrays.asList(saved.split(",")));
    }

    static void save(Context c, List<String> pkgs) {
        prefs(c).edit().putString(KEY, TextUtils.join(",", pkgs)).apply();
    }

    static boolean contains(Context c, String pkg) {
        return load(c).contains(pkg);
    }

    /**
     * Shows the edit menu for an app. On the dock it offers moving and removing; in the app
     * grid it offers adding or removing. Calls {@code changed} after any edit.
     */
    static void edit(Context c, Tiles.App app, boolean onDock, Runnable changed) {
        List<String> dock = load(c);
        int at = dock.indexOf(app.pkg);
        List<String> labels = new ArrayList<>();
        List<Runnable> actions = new ArrayList<>();
        if (onDock && at > 0) {
            labels.add("Move left");
            actions.add(() -> { dock.remove(at); dock.add(at - 1, app.pkg); });
        }
        if (onDock && at >= 0 && at < dock.size() - 1) {
            labels.add("Move right");
            actions.add(() -> { dock.remove(at); dock.add(at + 1, app.pkg); });
        }
        if (at >= 0) {
            labels.add("Remove from dock");
            actions.add(() -> dock.remove(app.pkg));
        } else {
            labels.add("Add to dock");
            actions.add(() -> dock.add(app.pkg));
        }
        new AlertDialog.Builder(c, android.R.style.Theme_DeviceDefault_Dialog_Alert)
                .setTitle(app.label)
                .setItems(labels.toArray(new String[0]), (d, which) -> {
                    actions.get(which).run();
                    save(c, dock);
                    if (changed != null) changed.run();
                })
                .setNegativeButton("Cancel", null)
                .show();
    }
}
