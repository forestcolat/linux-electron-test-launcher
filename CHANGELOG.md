# ReStreet - FIFA Street 2012 Recompiled: changelog

## v2.0.7 Online Beta

Keyboard fixes, and ReStreet on Linux. Offered automatically by the launcher.

### Fixed
- **Keyboard: A and B did nothing** (stuck on the "INFORMATION — autosave" pop-up). Buttons with two keys in the launcher's Controls page (A = J / Enter, B = K / Backspace, or the mouse buttons in the keyboard + mouse layout) were ignored by the game since 2.0. Both keys now work.
- **Keyboard: skill-move keys** (right stick, the arrow keys in the keyboard-only layout) now work, and turning off "Mouse for skill moves" really leaves the mouse free.

### New
- **Linux (beta):** a native Linux launcher that runs the game through Proton (**ReStreet-Linux-launcher.tar.gz** on the release). See "Linux (beta)" in the README.

## v2.0.6 Online Beta

Crash and save fixes. Offered automatically by the launcher.

### Fixed
- **Crash in the middle of a match** ("memory at 0x0000000100000000 could not be read"): when the game's own memory filled up, some requests got nothing. Every request is now covered, and borrowed memory is given back and reused.
- **Crash ~2 seconds after start** on PCs whose sound device refuses to be opened twice.
- **World Tour progress from 2.0.0 / 2.0.1** (saved under your online name) is loaded again.
- **"Save file corrupt" after a crash while saving**: the empty save is skipped, and a deleted save is kept in `_apagados` (last 10) instead of being destroyed.
- **Portuguese (PT-BR)** chosen in the launcher now really shows the game in Portuguese.
- **Achievements** load again when the Windows user name has accents or non-Latin letters.
- **Launcher rooms**: the "Loading game" page is now silent.

## v2.0.5 Online Beta

Crash and sign-in fixes. Offered automatically by the launcher.

### Fixed
- **Crash entering a match** (World Tour and others): the game ran out of its own GPU memory when loading players' heads.
- **Online sign-in** works again when the server is slow.
- **Crash ~30 s after start** when the save folder path has special characters.
- **Ultrawide (21:9) screens**: no more crash loading a match.
- **"Settings File damaged" / World Tour not saving** with OneDrive or a blocked Documents folder.
- **My Squad player faces** are no longer noise.
- **Launcher rooms** start the game on time, also with the launcher minimized.
- **Older CPUs without AVX2** (before ~2015): the launcher now uses a build made for them.

### Known
- On AMD graphics cards, use Direct3D 12 (Vulkan can crash in the AMD driver).

## v2.0.4 Online Beta

Sign-in fix. Offered automatically by the launcher.

### Fixed
- **"SIGN IN FAILED" / `OSDK_SIGNIN_REQUIRED` on 2.0.3.** The 2.0.3 game was built without its online sign-in part. 2.0.4 puts it back.

## v2.0.3 Online Beta

World Tour crash fixes. Offered automatically by the launcher.

### Fixed
- **World Tour closed the game when leaving a match.** A piece of the game's code that World Tour calls at the end of a match was missing from the 2.0 recompilation.
- **The game closed when saving** if Documents can't be used (for example a missing or blocked OneDrive folder). The error now goes back to the game, and saves fall back to `Binaries\UserData`.

### Known
- Player portraits in World Tour (My Squad) look scrambled on 2.0; being worked on.

## v2.0.2 Online Beta

World Tour progress restored. Offered automatically by the launcher.

### Fixed
- **World Tour started from zero on 2.0.** 2.0 looked for saves in a new, empty profile folder. It now loads the same saves as 1.5 (`Documents\fifastreet\B13EBABEBABEBABE`). Nothing had been deleted. Anything played on 2.0 / 2.0.1 before this update stays in its own folder and is not merged.

## v2.0.1 Online Beta

Sign-in fix. Offered automatically by the launcher.

### Fixed
- **"SIGN IN FAILED" on 2.0.** The game tried to reach the online service through a wrong local network address instead of 127.0.0.1, so it never reached the servers.

## v2.0 Online Beta

A new graphics engine, a launcher with friends, chat and rooms, and online play from start to kick-off.

### New
- **Graphics engine** based on AnDr3zF's "Deck" port: sharper image and much higher frame rates (about 400 FPS in a match on the test PC, against about 165 on 1.5.7). Direct3D 12 and Vulkan.
- **Graphics options:** FSR 1 modes, internal resolution up to 4x, aspect ratio, anisotropic filtering, ambient occlusion (SSAO, HBAO, GTAO), MSAA, FXAA and an FPS limit.
- **Launcher:** social page with online players, friends, requests and chat; game rooms that bring both players to the lobby; performance, display, graphics and controls pages.
- **In-game friends Guide** on F6 or the controller's Xbox button, with friends list and game invites (accept with A / Space, decline with B / Shift). Quit an online match with START + B.
- **Brazilian Portuguese** translation by Eze_, with heights and weights in cm and kg.
- **One installer** (`ReStreet-2.0-Setup.exe`): installs from the ISO or updates an older ReStreet install.

### Known limits
- No mouse for skill moves yet; Vulkan can have small stutters; the game renders internally at 1280x720; START + B also shows the game's own question behind it; a launcher room does not pull a player out of another lobby; chat is only in the launcher; two games on the same PC can lose the connection after an invite.

## v1.5.7 Online Beta

### Fixed
- Matches from the **Matches** tab no longer get stuck on "loading" when a game was already open: the launcher restarts the game by itself when the match fills up.
- Friend invites always go to the game your friend has open now, not an old crashed one.

## v1.5.6 Online Beta

### Improved
- Smoother on 4-core CPUs and laptops: while the game waits for the graphics card, the CPU goes to the parts doing work (about 145 to 160-171 FPS on a 4-core test).
- Performance reports always record the graphics card really used and whether it is integrated.

## v1.5.5 Online Beta

### New
- Matches open by themselves: when someone joins, both games go straight to the match lobby. Stadiums picked by photo, with the game's own names.
- **Controls** page: play with keyboard + mouse or keyboard only, rebindable keys; a controller still works.
- New folder layout like the FIFA games on PC: `Game` (game files) and `Binaries` (program, files and Logs). Moved automatically on update.

### Fixed
- "Your Gamer Profile does not have the correct permissions" when a friend joined or after a match (server fix).
- Pink crowds with two copies on one PC; stutter in the first matches of each session (shaders now saved); FPS counter shows the slowest moment; antivirus warnings about `TracyClient.dll` (file removed).

## v1.5.4 Online Beta

Matches tab and a smoother launcher. Offered automatically by the v1.5.1+ launcher.

### New
- **Matches tab:** post a match (optional title, mode, stadium), all open matches as cards with Join, an open-matches badge in the menu, and who is searching in the game.

### Fixed
- The classic window no longer flashes at start: the launcher stays invisible until the new page has loaded.
- False "Server not reachable": requests retry once and background refreshes only report after three failures in a row.

## v1.5.3 Online Beta

Open games reach the match by themselves; the new launcher everywhere. Offered automatically by the v1.5.1/v1.5.2 launcher.

### New
- **Automatic match set-up for launcher rooms:** the runtime's room watcher is installed again (it stopped with the retired F6 panel); a full room passes the intro, title and autosave notice, then takes both games to the Friend Match set-up. The launcher shows a "Game ready" dialog with PLAY.
- **New launcher without WebView2:** on Wine/Proton or without the runtime, the page is served on 127.0.0.1 under a random path and opened as a browser app window (Chromium family) or in the default browser; the classic window is hidden; the update check also runs there.

### Fixed
- False "Chat is not available on this server yet".
- Red crosses in the classic launcher on Wine (GDI+ effects Wine lacks are skipped).

## v1.5.2 Online Beta

Open games in the launcher. Offered automatically by the v1.5.1 launcher; installs over v1.3, v1.4, v1.5 or v1.5.1.

### New
- **Open games (Social page):** create a game room (5-a-side, 6-a-side, Futsal; stadium) or join one; creating posts a message in the server chat; a full room uses the existing in-game room flow that takes both players to the match set-up.
- **Searching players:** the Online list shows the exact mode each player searches and the waiting time, plus a per-mode summary.

## v1.5.1 Online Beta

Automatic updates and a Performance page in the launcher. Installs over v1.3, v1.4 or v1.5.

### New
- **Automatic updates:** at start the launcher checks the latest GitHub release; "Update now" downloads the patch and its .sha256 from GitHub only, verifies SHA-256, and the patch installs itself with the saved name, key and server, then reopens the launcher.
- **Performance page:** last 30 sessions on this PC (FPS average and 1% low per scene, crashes), the graphics card used, this PC, and tips (integrated GPU on a PC with a faster card, FPS below the refresh rate, stutters, repeated crashes).
- Performance reports also carry the game exit code and whether the integrated GPU was used.

### Fixed
- Performance reports had no graphics card name (the launcher read an earlier session from the log).

## v1.5 Online Beta

A new launcher with a social hub, the dedicated graphics card on hybrid laptops, and anonymous performance reports. Installs over v1.3 or over the v1.4 Online Beta Test.

### New
- **New launcher design** (glass interface, own title bar) with live server ping and player counts and a server switch.
- **Social page:** online players and what they are doing, friends and requests, add a friend by name, server chat and private chats with friends, block and report.
- **Performance reports:** FPS average, 1% low and stutters per scene (menus, matches, replays) shown after each session; sent anonymously to the server only if the player agrees (asked once, toggle in Advanced).

### Fixed
- **Hybrid laptops used the integrated GPU:** automatic selection now prefers the high-performance adapter on Direct3D 12 and discrete GPUs on Vulkan.

### Known issues
- Same as v1.4: no Rematch yet, Street Network videos off, rare Online Team Play loading hang, idle players are removed by the game.

## v1.4 Online Beta Test

Online play is back as a free open beta, installed as a patch over v1.3, on community servers in Europe and South America.

### New
- **Online modes:** Street Season Division Match (10-game seasons with promotion and relegation), Cup Match (a new cup every week with a trophy), Friend Match, Online Team Play and World Tour Play Online (Gold).
- **Friends** from the Xbox Guide (Guide button or F6): friends list, add friends, gamer cards and invites.
- **EA SPORTS Hub:** News Feed, leaderboards (Top 100, My 100, Friends and Country) and EAS FC levels.
- **Two servers:** Europe and South America, chosen when you install the patch.
- **Discord Rich Presence:** your profile shows *Playing ReStreet PC* and what you are doing. Contributed by **Emran_Ahm3d**.
- **New launcher design.**

### Known issues
- No Rematch at the end of a match yet. Street Network video uploads are off.
- Online Team Play: the second player's game can rarely stay on the loading screen.
- An idle player is removed from the match by the game.

## v1.3

True VSync on Direct3D 12 and Vulkan: no tearing, a new frame on every refresh at any refresh rate (also with Adaptive Sync / FreeSync), an FPS counter that matches GPU overlays, and clean stadium floors on every graphics card.

### New
- **True VSync.** With VSync on, the screen shows a new frame on every refresh, at whatever refresh rate you pick in the launcher (60, 120, 144, 165 Hz and beyond), with no tearing. Each refresh shows the game's newest frame, so motion is smooth: no more half-rate drops, uneven pacing or stutter when the game window has focus. Same on Direct3D 12 and Vulkan.
- **Works with Adaptive Sync / FreeSync.** With Adaptive Sync on, frames are handed to the display on an even clock, so the monitor refreshes exactly once per frame instead of holding frames for 2 or 3 refreshes.
- **The in-game FPS counter shows what you see.** With VSync on, it counts the frames that actually reach the screen, the same number AMD, NVIDIA and other overlays show.
- **VSync off = no frame limit.** With VSync on, the game is capped at twice the refresh rate so a fresh frame is always ready without wasting power.
- **The launcher's refresh rate change now sticks.** The chosen refresh rate is applied so Windows keeps it while the game is in front, and the refresh rate saved in Windows is restored when the game closes.

### Fixed
- **Graphical garbage on some court floors** (seen on slower graphics cards). The floor data the game reads back from the graphics card is always read for the current frame again.
- **Smoother render-to-texture images at higher internal resolutions** (for example player portraits drawn by the game): they are averaged instead of keeping one pixel out of each block when copied back at the original resolution.
- **Intro and cutscene videos** play at the correct speed with clean sound and steady frames, with VSync on or off.

### Notes
- With VSync on, the game cannot show more frames than it can produce: if a scene runs below your refresh rate, some frames stay on screen for two refreshes. 60 Hz is the safest choice for a perfectly steady match.
- The 3D portraits of players without a photo (generated World Tour squad players, created players) are drawn by the game at a small size; they look soft when stretched to large resolutions. This is how the game draws them.
- Installs over v1.2. Saves, game data and launcher settings are kept.

## v1.2

Menus, cutscenes, goals and replays now run at your refresh rate, Vulkan replays are sharp, Direct3D 12 no longer drops to 1 FPS with VSync on, and updating over an online-patched install removes the online patch.

### Improved
- **Cutscenes and goal replays are no longer capped at 40 FPS.** The game has its own frame limiter that held replays and cutscenes at 40 FPS. It now follows the refresh rate chosen in the launcher.
- **Goals and match setup no longer drop to 30-40 FPS.** A forced wait for the graphics card on every frame was removed.
- **Menus run at 60 FPS** instead of 30.

### Fixed
- **Vulkan: goal replays were blurred.** Replays are now as sharp as in Direct3D 12.
- **Direct3D 12 with VSync on could fall to 1 FPS** (seen on laptops, with the Full, Some and Fast readback modes, getting worse as the game asked for more graphics readbacks per frame). Each wait for the graphics card cost two screen refreshes. The swap chain now supports tearing even when VSync is on, so presenting no longer holds back those waits; the game clock stays paced to your refresh rate. Measured on an RTX 4060 Laptop: 0.6 FPS to a steady 60 FPS in the same scene.
- Updating works over v1.1 with or without the online patch. If the online patch is installed, the update removes it (online.json, online-patch.json, the OnlinePatch backup folder, tools\ReStreetAssetExplorer.exe and the stadium pictures). A copy of the replaced files is kept in `.updates` first. Saves, game data and launcher settings are kept.

### Notes
- With VSync on at refresh rates above 60 Hz, some scenes can run at half the refresh rate (for example 50 FPS at 100 Hz). Turn VSync off (ideally with VRR) for an unlocked frame rate.
- With VSync on in Direct3D 12 you may see tearing in fullscreen. Turn VSync off if you prefer VRR.
- Vulkan still runs at 30 FPS outside menus on some PCs; Direct3D 12 is recommended.

## v1.1

Smoother graphics and VSync, direct play, and a setup and launcher that fit any screen.

### New
- **New logo: ReStreet.** The main menu and the Press Start screen now show the ReStreet logo. It is applied by a small patch in the new `Mods` folder next to `Game` and `GameData`, which the game applies while reading its files; your game files are not modified. Delete the file in `Mods` to get the original logo back.
- **Improved "Some" graphics readback, now the default.** The Some readback mode was reworked to remove the graphical garbage it used to cause on some courts (for example floor reflections and flickering floor lines) while keeping its performance. It is now the default in the Balanced and Quality profiles. Occlusion queries are off by default in those profiles. If you prefer the old behaviour, pick Full (compatible) in the Compatibility tab. Settings you already saved are kept.
- **VSync now works smoothly.** The old VSync made the game run at only a few FPS on some PCs (every wait for the graphics card cost two screen refreshes). VSync now keeps the game locked to your refresh rate and shows each frame without blocking the graphics queue, with no tearing. Turning VSync off still gives an unlocked frame rate.
- **Direct play.** Start the game without opening the launcher window: run `FifaStreetLauncher.exe --play`, or click the new **Shortcut** button in the launcher to create a desktop shortcut that does exactly that. It uses the settings you last chose in the launcher and closes by itself after the game exits.

### Fixed
- **Setup and launcher layout on any resolution or Windows scale.** On some screens (for example a 4K TV set to 1080p with Windows scale at 150%) the controls could end up smaller than the window, overlapping the title and cut off. The window, controls and text are now laid out from one scale that follows your monitor's DPI and always fits the screen, including 100% to 200% scale and small laptop screens.
- The window now follows you when it is moved to a monitor with a different scale.

## v1.0

First stable release. This version is offline only: it has no online play.

**The project is now called ReStreet - FIFA Street 2012 Recompiled.** The launcher, the setup and the desktop shortcut use the new name. Your install folder, saves and settings are not affected.

### New
- **Performance profiles in the launcher.** Choose Balanced (the default), Quality for strong PCs, or Performance for weaker PCs. Performance uses native resolution, no post-processing, a faster graphics readback and no occlusion queries. If you change any single option by hand, the profile switches to Custom.
- **Shader cache included.** A set of pre-built shaders now ships with the game and is copied on the first run, so the first matches have fewer slow moments. Shaders you already have are never overwritten.
- **FPS counter.** Press Home to show or hide a small FPS number in the top-left corner. It scales with the game's resolution and changes colour: green from 55 FPS, yellow from 30, red below. It replaces the old statistics panel.
- **Exit shortcut.** Hold START and press B (Xbox layout) or START and Circle (PlayStation layout) on any screen to open "EXIT THE GAME?". A confirms, B cancels, and the game then closes cleanly. Available in English, German, French, Spanish, Italian and Portuguese.

### Unchanged
- The game files are the same as in v0.4.1, so World Tour, saves and your launcher settings keep working. Saves live in `Documents\fifastreet` and are not touched by the installer.

### Notes
- Performance still depends on your hardware and drivers. The profiles are starting points, not guarantees.
- The project's MIT licence does not establish rights to redistribute game-derived binaries; no legal clearance is claimed.

## v0.4.1 Experimental

Same precompiled game binaries as v0.4.0; the launcher and installer changed.


- Launcher: new Readback Resolve option (Full, Some, Fast, None) in the Compatibility panel. Full remains the default because it is the validated mode; the other modes are not validated and Fast previously caused graphical corruption.
- Lighter first-run defaults for better performance: 1x internal scale, FXAA, MSAA off and Memory Page State off. Existing launcher settings are not migrated; they keep their saved values.
- Frame stats logging is now optional ("Frame stats log") and off by default.
- The status bar warns when 3x or higher internal resolution is selected.
- Launcher validates the selected resolution and monitor, and falls back to defaults when settings cannot be read.
- Installer keeps an existing `fifastreet.toml` when installing over a previous copy, and reports an ISO with several `default.xex` files as unsupported.

## v0.4.0 Experimental

- Faster installation using bundled precompiled binaries for the verified game build; no Visual Studio, compiler download or local game compilation in this installer.
- Existing-installation updates: select the folder containing Game and GameData. Saves, extracted game data, launcher preferences and existing runtime configuration are preserved.
- Update integrity checks, backups of replaced files and restoration on handled errors or cancellation. Power-loss recovery is not automatic.
- Clear installation/update instructions and corrected folder browsing when the default installation path does not exist.
- Launcher language selection: English, Spanish, French, German and Italian. All five were tested by Samuel.
- First-run defaults: Direct3D 12, fullscreen, 2x internal scale, FXAA Extreme, native 2x MSAA, VSync off and D3D12 VRR/tearing on. Resolution and refresh rate follow the selected primary display; existing preferences are retained.
- Rendering, synchronization and frame-pacing corrections for the tested Direct3D 12 and Vulkan configurations, retaining Full Readback for compatibility. No universal FPS guarantee or claim of unlocked game simulation.
- World Tour FootballCompEng function-boundary override; local code-generation, copy-routine validation and Samuel's World Tour test passed.
- Contributor fixes for Windows patch line endings, module-path lookup and diagnostics, flexible data discovery and resource compilation. The latest source improvements to data-discovery error handling require a future main-executable rebuild; they are not claimed as changes in the bundled main executable.
- Credits to Emran_Ahm3d in the documentation, installer and launcher.

Limitations: a separate clean Windows installation/update test has not been confirmed. The project's MIT licence does not establish rights to redistribute game-derived binaries; no legal clearance is claimed. Applicable bundled notices are included.
