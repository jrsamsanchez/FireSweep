# Turn on ADB debugging on a Fire TV

FireSweep talks to the TV over your home network using ADB, Android's debugging connection. It's off by default. Turning it on takes about a minute with the remote, and you only do it once per TV.

These steps are for Fire TV Edition smart TVs and Fire TV Sticks/Cubes on Fire OS 7. On newer Fire OS 8 devices, **My Fire TV** is called **Device & Software**. Everything else is the same.

## 1. Unlock Developer options

1. On the TV, open **Settings** (the gear at the right end of the top menu).
2. Go to **My Fire TV** → **About**.
3. Highlight the first line, which is the TV's name (for example *Fire TV Edition* or *Fire TV Stick 4K*).
4. Press **Select** on the remote **7 times**. A message counts down and then says **"No need, you are already a developer"** or **"You are now a developer"**.

If **Developer options** is already in the My Fire TV menu, skip this step.

## 2. Turn on ADB debugging

1. Press **Back** once to return to **My Fire TV**.
2. Open **Developer options**.
3. Turn **ADB debugging** **ON**.

You don't need **Apps from Unknown Sources** (or **Install unknown apps**). FireSweep installs its home screen over ADB.

## 3. Find the TV's IP address

You only need this if the scan doesn't find the TV.

**Settings** → **My Fire TV** → **About** → **Network**. The **IP Address** looks like `192.168.1.50`.

Type it into **Or type its IP address** in FireSweep and choose **Connect**.

## 4. Allow your PC

The first time FireSweep connects, the TV shows **"Allow USB debugging?"** with your computer's key fingerprint.

1. Tick **Always allow from this computer**.
2. Choose **OK**.

If you miss the prompt, choose **Connect to this TV** again in FireSweep and it reappears.

## If FireSweep can't find the TV

| What you see | What to try |
|---|---|
| The TV isn't in the list after a scan | Check that ADB debugging is on and that the PC and the TV are on the same network. Guest Wi-Fi networks often block devices from seeing each other. Type the IP address in instead. |
| "Allow this PC on the TV" | The TV is waiting for step 4. Look at the TV screen. |
| "Not answering" | The TV may be asleep or on another network. Wake it with the remote, then scan again. |
| It worked before but stopped | A factory reset, and some Fire OS updates, turn ADB debugging back off. Repeat step 2. |
| The IP address keeps changing | Give the TV a fixed (reserved) address in your router's settings. |

## Turning it off again

When you're done, you can switch **ADB debugging** back off in **Developer options**. Your changes stay; FireSweep only needs ADB to make or undo changes. Turn it back on before you use **Restore everything**.
