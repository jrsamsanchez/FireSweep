package tv.isafellie.os;

import android.accessibilityservice.AccessibilityService;
import android.content.Intent;
import android.view.KeyEvent;
import android.view.accessibility.AccessibilityEvent;

/**
 * Fire OS won't let another app be the default home, so the Home button always opens Amazon's
 * launcher. This catches the Home key first and opens IsafellieOS instead (a long press opens
 * Settings, since Amazon's quick menu can't be launched from outside). If Amazon's home still
 * comes up some other way, such as after a restart, it's replaced as soon as it appears.
 *
 * Some Fire TVs (Fire OS 6) won't run it; {@link HomeWatchService} takes over there.
 */
public class HomeRedirectService extends AccessibilityService {
    private static final long LONG_PRESS_MS = 600;

    private long homeDownAt;
    private HomeGuard guard;

    @Override
    protected void onServiceConnected() {
        super.onServiceConnected();
        guard = new HomeGuard(this);
        // The backup watcher isn't needed while this runs.
        stopService(new Intent(this, HomeWatchService.class));
    }

    @Override
    protected boolean onKeyEvent(KeyEvent e) {
        if (e.getKeyCode() != KeyEvent.KEYCODE_HOME) return false;
        if (e.getAction() == KeyEvent.ACTION_DOWN) {
            if (e.getRepeatCount() == 0) homeDownAt = e.getEventTime();
        } else if (e.getAction() == KeyEvent.ACTION_UP) {
            HomeGuard.stopAllowing(this);
            if (e.getEventTime() - homeDownAt >= LONG_PRESS_MS) {
                startActivity(new Intent(this, SettingsActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            } else if (guard != null) {
                guard.openIsafellie();
            }
        }
        return true;
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent e) {
        if (guard == null || e.getEventType() != AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED) return;
        CharSequence pkg = e.getPackageName(), cls = e.getClassName();
        if (pkg != null) guard.onScreen(pkg.toString(), cls == null ? null : cls.toString());
    }

    @Override
    public void onInterrupt() {}
}
