package tv.isafellie.os;

import android.app.Activity;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.GridLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import java.util.List;

/** Every installed TV app as a grid of banners, plus a way back to Amazon's home for profiles. */
public class AppsActivity extends Activity {
    private static final int TILE_DP = 150;
    private static final int COLUMNS = 5;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        List<Tiles.App> apps = Tiles.all(this);

        LinearLayout page = new LinearLayout(this);
        page.setOrientation(LinearLayout.VERTICAL);
        page.setBackgroundColor(0xF0101416);
        int pad = Tiles.dp(this, 40);
        page.setPadding(pad, Tiles.dp(this, 26), pad, 0);

        TextView title = new TextView(this);
        title.setText("Apps");
        title.setTextColor(0xFFFFFFFF);
        title.setTextSize(26);
        page.addView(title);

        TextView hint = new TextView(this);
        hint.setText("Press ☰ Menu on an app to add it to the dock or take it off");
        hint.setTextColor(0x99FFFFFF);
        hint.setTextSize(14);
        page.addView(hint);

        GridLayout grid = new GridLayout(this);
        grid.setColumnCount(COLUMNS);
        grid.setClipChildren(false);
        grid.setClipToPadding(false);
        int gp = Tiles.dp(this, 20);
        grid.setPadding(gp, gp, gp, gp);

        for (Tiles.App app : apps) {
            View tile = Tiles.appTile(this, app, TILE_DP);
            Tiles.editable(tile, app, false, () -> Toast.makeText(this,
                    Dock.contains(this, app.pkg) ? app.label + " added to the dock" : app.label + " removed from the dock",
                    Toast.LENGTH_SHORT).show());
            grid.addView(cell(tile, app.label));
        }
        String amazon = "Amazon Home (profiles)";
        View home = Tiles.appTile(this, new Tiles.App(Tiles.AMAZON_LAUNCHER, amazon, null,
                getDrawable(R.drawable.banner), true), TILE_DP);
        home.setOnClickListener(v -> Tiles.openAmazonHome(this));
        grid.addView(cell(home, amazon));

        ScrollView scroll = new ScrollView(this);
        scroll.setClipChildren(false);
        scroll.setFillViewport(true);
        scroll.addView(grid, new ScrollView.LayoutParams(-1, -2));
        page.addView(scroll, new LinearLayout.LayoutParams(-1, 0, 1f));
        setContentView(page);
        ((ViewGroup) grid.getChildAt(0)).getChildAt(0).requestFocus();
    }

    private View cell(View tile, String label) {
        LinearLayout cell = new LinearLayout(this);
        cell.setOrientation(LinearLayout.VERTICAL);
        cell.setGravity(Gravity.CENTER_HORIZONTAL);
        cell.setClipChildren(false);
        ViewGroup.LayoutParams t = tile.getLayoutParams();
        cell.addView(tile, new LinearLayout.LayoutParams(t.width, t.height));
        TextView name = new TextView(this);
        name.setText(label);
        name.setTextColor(0xCCFFFFFF);
        name.setTextSize(13);
        name.setSingleLine(true);
        name.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams nl = new LinearLayout.LayoutParams(t.width, -2);
        nl.topMargin = Tiles.dp(this, 10);
        cell.addView(name, nl);
        GridLayout.LayoutParams gl = new GridLayout.LayoutParams();
        int m = Tiles.dp(this, 12);
        gl.setMargins(m, m, m, Tiles.dp(this, 18));
        cell.setLayoutParams(gl);
        return cell;
    }
}
