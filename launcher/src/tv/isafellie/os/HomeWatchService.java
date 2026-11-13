package tv.isafellie.os;

import android.accessibilityservice.AccessibilityServiceInfo;
import android.app.AppOpsManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.app.usage.UsageEvents;
import android.app.usage.UsageStatsManager;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.os.PowerManager;
import android.os.Process;
import android.util.Log;
import android.view.accessibility.AccessibilityManager;

/**
 * The backup Home redirect, for Fire TVs that won't run the accessibility service (Fire OS 6
 * clears the setting that turns it on). It watches which app is in front through usage access,
 * which FireSweep grants over adb, and swaps Amazon's home for IsafellieOS whenever it comes up.
 * It can't catch the key itself, so Amazon's home may show for a moment first.
 */
public class HomeWatchService extends Service {
    private static final long POLL_MS = 250;
    private static final String CHANNEL = "home";

    private final Handler handler = new Handler(Looper.getMainLooper());
    private HomeGuard guard;
    private UsageStatsManager usage;
    private PowerManager power;
    private long since;

    /** Starts the watcher when it's needed and allowed: no accessibility service, usage access granted. */
    static void startIfNeeded(Context c) {
        Intent i = new Intent(c, HomeWatchService.class);
        if (accessibilityRunning(c) || !hasUsageAccess(c)) {
            c.stopService(i);
            return;
        }
        try {
            if (Build.VERSION.SDK_INT >= 26) c.startForegroundService(i);
            else c.startService(i);
        } catch (Exception e) {
            Log.w(HomeGuard.TAG, "Couldn't start the Home watcher", e);
        }
    }

    static boolean accessibilityRunning(Context c) {
        AccessibilityManager am = (AccessibilityManager) c.getSystemService(ACCESSIBILITY_SERVICE);
        if (am == null) return false;
        for (AccessibilityServiceInfo s : am.getEnabledAccessibilityServiceList(AccessibilityServiceInfo.FEEDBACK_ALL_MASK)) {
            if (s.getResolveInfo() != null && c.getPackageName().equals(s.getResolveInfo().serviceInfo.packageName)) return true;
        }
        return false;
    }

    static boolean hasUsageAccess(Context c) {
        AppOpsManager ops = (AppOpsManager) c.getSystemService(APP_OPS_SERVICE);
        return ops != null && ops.checkOpNoThrow(AppOpsManager.OPSTR_GET_USAGE_STATS,
                Process.myUid(), c.getPackageName()) == AppOpsManager.MODE_ALLOWED;
    }

    private final Runnable poll = new Runnable() {
        @Override public void run() {
            try {
                check();
            } catch (Exception e) {
                Log.w(HomeGuard.TAG, "Home watcher", e);
            }
            handler.postDelayed(this, POLL_MS);
        }
    };

    @Override
    public void onCreate() {
        super.onCreate();
        startForeground(1, notification());
        guard = new HomeGuard(this);
        usage = (UsageStatsManager) getSystemService(USAGE_STATS_SERVICE);
        power = (PowerManager) getSystemService(POWER_SERVICE);
        since = System.currentTimeMillis();
        handler.post(poll);
        Log.i(HomeGuard.TAG, "Home watcher running");
    }

    private void check() {
        if (power != null && !power.isInteractive()) {
            since = System.currentTimeMillis();
            return;
        }
        if (accessibilityRunning(this)) {
            stopSelf();
            return;
        }
        long now = System.currentTimeMillis();
        UsageEvents events = usage.queryEvents(since, now + 1);
        UsageEvents.Event e = new UsageEvents.Event();
        String pkg = null, cls = null;
        while (events.hasNextEvent()) {
            events.getNextEvent(e);
            if (e.getEventType() != UsageEvents.Event.MOVE_TO_FOREGROUND) continue;
            pkg = e.getPackageName();
            cls = e.getClassName();
            since = Math.max(since, e.getTimeStamp() + 1);
        }
        // Only the latest screen to come up matters.
        if (pkg != null) guard.onScreen(pkg, cls);
    }

    @SuppressWarnings("deprecation")
    private Notification notification() {
        Notification.Builder b;
        if (Build.VERSION.SDK_INT >= 26) {
            NotificationManager nm = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
            nm.createNotificationChannel(new NotificationChannel(CHANNEL, "Home button", NotificationManager.IMPORTANCE_MIN));
            b = new Notification.Builder(this, CHANNEL);
        } else {
            b = new Notification.Builder(this).setPriority(Notification.PRIORITY_MIN);
        }
        return b.setSmallIcon(R.drawable.icon).setContentTitle("IsafellieOS")
                .setContentText("Keeps the Home button on IsafellieOS").build();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        return START_STICKY;
    }

    @Override
    public void onDestroy() {
        handler.removeCallbacks(poll);
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }
}
