package tv.isafellie.os;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.text.TextUtils;

import java.util.ArrayList;
import java.util.List;

/**
 * Lets the Fire TV Tool on the PC set the dock over adb. Only adb's shell can send this (the
 * manifest requires DUMP), and it answers with the dock as saved, so the PC can read it too:
 *   am broadcast -n tv.isafellie.os/.DockReceiver -a tv.isafellie.os.DOCK [--es dock a,b,c]
 */
public class DockReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context c, Intent intent) {
        String pkgs = intent.getStringExtra("dock");
        if (pkgs != null) {
            List<String> dock = new ArrayList<>();
            for (String p : pkgs.split(",")) {
                if (!p.trim().isEmpty() && !dock.contains(p.trim())) dock.add(p.trim());
            }
            Dock.save(c, dock);
        }
        setResultData(TextUtils.join(",", Dock.load(c)));
    }
}
