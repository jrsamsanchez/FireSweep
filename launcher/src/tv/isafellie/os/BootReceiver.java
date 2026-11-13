package tv.isafellie.os;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/** Starts the backup Home watcher after a restart, on TVs that need it. */
public class BootReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context c, Intent intent) {
        HomeWatchService.startIfNeeded(c);
    }
}
