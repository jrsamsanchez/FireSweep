package tv.isafellie.os;

import android.animation.ObjectAnimator;
import android.animation.PropertyValuesHolder;
import android.app.Activity;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.animation.LinearInterpolator;
import android.widget.FrameLayout;
import android.widget.HorizontalScrollView;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextClock;
import android.widget.TextView;

import java.io.InputStream;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** The IsafellieOS home screen: a slowly drifting nature wallpaper, a clock and a dock. */
public class HomeActivity extends Activity {
    private static final long WALLPAPER_MS = 45_000;
    private static final long FADE_MS = 1_800;

    private final Handler ui = new Handler(Looper.getMainLooper());
    private final List<String> wallpapers = new ArrayList<>();
    private ImageView front, back;
    private LinearLayout dock;
    private int next;
    private ObjectAnimator drift;

    private final Runnable rotate = new Runnable() {
        @Override public void run() {
            showNextWallpaper();
            ui.postDelayed(this, WALLPAPER_MS);
        }
    };

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);

        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);

        FrameLayout wall = new FrameLayout(this);
        back = wallpaperView();
        front = wallpaperView();
        wall.addView(back);
        wall.addView(front);
        root.addView(wall, new FrameLayout.LayoutParams(-1, -1));

        View scrim = new View(this);
        scrim.setBackground(new GradientDrawable(GradientDrawable.Orientation.TOP_BOTTOM,
                new int[]{0x55000000, 0x00000000, 0x00000000, 0xAA000000}));
        root.addView(scrim, new FrameLayout.LayoutParams(-1, -1));

        int pad = Tiles.dp(this, 40);

        TextView name = new TextView(this);
        name.setText("IsafellieOS");
        name.setTextColor(0xE6FFFFFF);
        name.setTextSize(20);
        name.setLetterSpacing(0.04f);
        name.setShadowLayer(8, 0, 2, 0x88000000);
        FrameLayout.LayoutParams nlp = new FrameLayout.LayoutParams(-2, -2, Gravity.TOP | Gravity.START);
        nlp.setMargins(pad, Tiles.dp(this, 28), 0, 0);
        root.addView(name, nlp);

        TextClock clock = new TextClock(this);
        clock.setFormat12Hour("h:mm");
        clock.setFormat24Hour("H:mm");
        clock.setTextColor(Color.WHITE);
        clock.setTextSize(30);
        clock.setShadowLayer(8, 0, 2, 0x88000000);
        FrameLayout.LayoutParams clp = new FrameLayout.LayoutParams(-2, -2, Gravity.TOP | Gravity.END);
        clp.setMargins(0, Tiles.dp(this, 22), pad, 0);
        root.addView(clock, clp);

        dock = new LinearLayout(this);
        dock.setOrientation(LinearLayout.HORIZONTAL);
        dock.setGravity(Gravity.CENTER_VERTICAL);
        dock.setClipChildren(false);
        dock.setClipToPadding(false);
        int dpad = Tiles.dp(this, 22);
        dock.setPadding(dpad, dpad, dpad, dpad);

        // The shelf is as wide as its tiles up to the screen's width (less the margins), and
        // scrolls sideways as focus moves once more apps are added than fit across the screen.
        final int maxShelf = getResources().getDisplayMetrics().widthPixels - 2 * pad;
        HorizontalScrollView shelfView = new HorizontalScrollView(this) {
            @Override
            protected void onMeasure(int widthSpec, int heightSpec) {
                int limit = MeasureSpec.getMode(widthSpec) == MeasureSpec.UNSPECIFIED
                        ? maxShelf : Math.min(maxShelf, MeasureSpec.getSize(widthSpec));
                super.onMeasure(MeasureSpec.makeMeasureSpec(limit, MeasureSpec.AT_MOST), heightSpec);
            }
        };
        shelfView.setHorizontalScrollBarEnabled(false);
        shelfView.setHorizontalFadingEdgeEnabled(true);
        shelfView.setFadingEdgeLength(Tiles.dp(this, 48));
        shelfView.setSmoothScrollingEnabled(true);
        GradientDrawable shelf = new GradientDrawable();
        shelf.setColor(0x33FFFFFF);
        shelf.setCornerRadius(Tiles.dp(this, 26));
        shelfView.setBackground(shelf);
        shelfView.setClipToOutline(true);
        shelfView.addView(dock, new FrameLayout.LayoutParams(-2, -2));
        FrameLayout.LayoutParams dlp = new FrameLayout.LayoutParams(-2, -2,
                Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL);
        dlp.bottomMargin = Tiles.dp(this, 44);
        root.setClipChildren(false);
        root.addView(shelfView, dlp);

        setContentView(root);
        loadWallpaperList();
        HomeWatchService.startIfNeeded(this);
    }

    private ImageView wallpaperView() {
        ImageView v = new ImageView(this);
        v.setScaleType(ImageView.ScaleType.CENTER_CROP);
        v.setLayoutParams(new FrameLayout.LayoutParams(-1, -1));
        v.setAlpha(0f);
        return v;
    }

    /** Rebuilt on every resume so newly installed or re-enabled apps appear. */
    private void buildDock() {
        View focused = dock.getFocusedChild();
        int focusIndex = focused == null ? 0 : dock.indexOfChild(focused);
        dock.removeAllViews();
        int gap = Tiles.dp(this, 18);
        for (String pkg : Dock.load(this)) {
            Tiles.App app = Tiles.find(this, pkg);
            if (app == null) continue;
            View tile = Tiles.appTile(this, app, 100);
            Tiles.editable(tile, app, true, this::buildDock);
            addToDock(tile, gap);
        }
        addToDock(Tiles.iconTile(this, R.drawable.ic_apps, "All apps", 63,
                v -> startActivity(new Intent(this, AppsActivity.class))), gap);
        addToDock(Tiles.iconTile(this, R.drawable.ic_settings, "Settings", 63,
                v -> startActivity(new Intent(this, SettingsActivity.class))), 0);
        View f = dock.getChildAt(Math.min(focusIndex, dock.getChildCount() - 1));
        if (f != null) f.requestFocus();
    }

    private void addToDock(View tile, int gapAfter) {
        ViewGroup.LayoutParams base = tile.getLayoutParams();
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(base.width, base.height);
        lp.rightMargin = gapAfter;
        dock.addView(tile, lp);
    }

    private void loadWallpaperList() {
        try {
            String[] files = getAssets().list("wallpapers");
            if (files != null) {
                for (String f : files) if (f.endsWith(".jpg")) wallpapers.add("wallpapers/" + f);
            }
        } catch (Exception ignored) {
        }
        Collections.shuffle(wallpapers);
    }

    private void showNextWallpaper() {
        if (wallpapers.isEmpty()) return;
        final String path = wallpapers.get(next++ % wallpapers.size());
        new Thread(() -> {
            Bitmap bmp = decode(path);
            if (bmp == null) return;
            ui.post(() -> crossfade(bmp));
        }).start();
    }

    private Bitmap decode(String path) {
        BitmapFactory.Options o = new BitmapFactory.Options();
        o.inPreferredConfig = Bitmap.Config.RGB_565;
        try (InputStream in = getAssets().open(path)) {
            return BitmapFactory.decodeStream(in, null, o);
        } catch (Exception e) {
            return null;
        }
    }

    private void crossfade(Bitmap bmp) {
        ImageView incoming = back, outgoing = front;
        if (drift != null) drift.cancel();
        incoming.animate().cancel();
        incoming.setImageBitmap(bmp);
        incoming.setAlpha(0f);
        incoming.setScaleX(1.0f);
        incoming.setScaleY(1.0f);
        incoming.bringToFront(); // only reorders within the wallpaper layer
        incoming.animate().alpha(1f).setDuration(FADE_MS).withEndAction(() -> {
            outgoing.setAlpha(0f);
            outgoing.setImageDrawable(null);
        }).start();
        // Slow Ken Burns drift across the wallpaper's time on screen.
        drift = ObjectAnimator.ofPropertyValuesHolder(incoming,
                PropertyValuesHolder.ofFloat(View.SCALE_X, 1f, 1.08f),
                PropertyValuesHolder.ofFloat(View.SCALE_Y, 1f, 1.08f));
        drift.setDuration(WALLPAPER_MS + FADE_MS);
        drift.setInterpolator(new LinearInterpolator());
        drift.start();
        front = incoming;
        back = outgoing;
    }

    @Override
    protected void onResume() {
        super.onResume();
        HomeGuard.stopAllowing(this); // back on IsafellieOS, so Amazon's home is redirected again
        buildDock();
        ui.removeCallbacks(rotate);
        ui.post(rotate);
    }

    @Override
    protected void onPause() {
        ui.removeCallbacks(rotate);
        super.onPause();
    }

    @Override
    public void onBackPressed() {
        // Home is the bottom of the stack; Back does nothing here.
    }

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event) {
        if (keyCode == KeyEvent.KEYCODE_MENU) {
            startActivity(new Intent(this, AppsActivity.class));
            return true;
        }
        return super.onKeyDown(keyCode, event);
    }
}
