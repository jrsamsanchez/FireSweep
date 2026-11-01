package tv.isafellie.os;

import android.app.Activity;
import android.content.ComponentName;
import android.content.Intent;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.widget.GridLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

/**
 * Fire OS sends the standard "open Settings" request to an empty placeholder, and its real
 * Settings menu lives inside Amazon's home screen. This page links straight to each Fire TV
 * settings section instead.
 */
public class SettingsActivity extends Activity {
    private static final String PKG = "com.amazon.tv.settings.v2";
    private static final String[][] SECTIONS = {
            {"Network", ".tv.network.NetworkActivity"},
            {"Display & Sounds", ".tv.display_sounds.DisplayAndSoundsActivity"},
            {"Applications", ".tv.applications.ApplicationsActivity"},
            {"Remotes & Bluetooth", ".tv.controllers_bluetooth_devices.ControllersAndBluetoothActivity"},
            {"Preferences", ".tv.preferences.PreferencesActivity"},
            {"Device & Software", ".tv.device.DeviceActivity"},
            {"Accessibility", ".tv.accessibility.AccessibilityActivity"},
            {"Account & Profile", ".tv.my_account.MyAccountActivity"},
    };
    private static final int COLUMNS = 4;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        LinearLayout page = new LinearLayout(this);
        page.setOrientation(LinearLayout.VERTICAL);
        page.setBackgroundColor(0xF0101416);
        int pad = Tiles.dp(this, 40);
        page.setPadding(pad, Tiles.dp(this, 26), pad, 0);

        TextView title = new TextView(this);
        title.setText("Settings");
        title.setTextColor(0xFFFFFFFF);
        title.setTextSize(26);
        page.addView(title);

        GridLayout grid = new GridLayout(this);
        grid.setColumnCount(COLUMNS);
        grid.setClipChildren(false);
        grid.setClipToPadding(false);
        int gp = Tiles.dp(this, 24);
        grid.setPadding(0, gp, 0, gp);

        for (String[] s : SECTIONS) {
            Intent i = new Intent().setComponent(new ComponentName(PKG, PKG + s[1]))
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            grid.addView(tile(s[0], v -> Tiles.launch(this, i)));
        }
        // Everything else (Alexa, parental controls, profiles) is only in Amazon's own menu.
        grid.addView(tile("All Fire TV settings", v -> Tiles.openAmazonSettings(this)));

        LinearLayout.LayoutParams glp = new LinearLayout.LayoutParams(-2, -2);
        glp.gravity = Gravity.CENTER_HORIZONTAL;
        page.addView(grid, glp);
        setContentView(page);
        grid.getChildAt(0).requestFocus();
    }

    private View tile(String label, View.OnClickListener click) {
        TextView t = new TextView(this);
        t.setText(label);
        t.setTextColor(0xFFFFFFFF);
        t.setTextSize(17);
        t.setGravity(Gravity.CENTER);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(0x26FFFFFF);
        bg.setCornerRadius(Tiles.dp(this, 14));
        t.setBackground(bg);
        t.setFocusable(true);
        t.setOnClickListener(click);
        t.setOnFocusChangeListener((v, has) -> {
            bg.setColor(has ? 0xF2FFFFFF : 0x26FFFFFF);
            t.setTextColor(has ? 0xFF111416 : 0xFFFFFFFF);
            v.animate().scaleX(has ? 1.06f : 1f).scaleY(has ? 1.06f : 1f).setDuration(140).start();
        });
        GridLayout.LayoutParams lp = new GridLayout.LayoutParams();
        lp.width = Tiles.dp(this, 190);
        lp.height = Tiles.dp(this, 84);
        int m = Tiles.dp(this, 12);
        lp.setMargins(m, m, m, m);
        t.setLayoutParams(lp);
        return t;
    }
}
