# ReStreet viewer (for Linux: Wine / Proton / Steam Deck)

On Wine/Proton the launcher cannot use WebView2, so it shows its new page in a browser. Inside Steam's Proton
container that browser is often not reachable, and nothing appears. This viewer is a small Windows (Electron)
program that runs in the SAME Wine prefix as the launcher, so it needs no Linux browser and no Flatpak.

**Windows is not affected.** The launcher only looks for the viewer when it detects Wine, and Windows installs do not
need this folder. If the viewer is missing or fails, the launcher falls back to a Chromium browser and then to the
classic launcher window, so Linux never ends up with a blank screen.

## For the maintainer: make the viewer package

On Windows, with internet:

    powershell -ExecutionPolicy Bypass -File Viewer\build-viewer.ps1

This downloads the current Electron (Windows x64), renames `electron.exe` to `ReStreetViewer.exe`, adds
`resources\app` (main.js + package.json) and writes `ReStreet-Viewer.zip`. Attach it to the release as an
optional Linux download. Do not add it to the Windows installer.

## For Linux players

1. Unzip `ReStreet-Viewer.zip` into the game folder, next to `FifaStreetLauncher.exe`, so you get
   `FIFA Street PC/Viewer/ReStreetViewer.exe`.
2. Run the launcher with Proton as usual (Proton-GE recommended; it needs a prefix that reports Windows 10+).

If the window does not appear, read `browser-ui.log` next to the launcher. Steam launch options to try:

- `RESTREET_VIEWER_ARGS="--in-process-gpu"` (extra switches for the viewer)
- `RESTREET_VIEWER_GPU=1` (hardware acceleration is off in the viewer by default)
- `RESTREET_CLASSIC_UI=1` (always use the classic launcher window instead)
