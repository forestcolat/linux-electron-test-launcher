# Validation and known limitations

## ReStreet 2.0.7 (current)

Tested by the developer on Windows 11 (AMD Radeon RX 9070 XT, Ryzen 5 7600), with two game copies on the live-like server path (about 180 ms ping):

| Check | Result |
|---|---|
| Sign-in and News Feed on the live servers | Passed (2.0.1 fix) |
| Friend requests, game invites (accept / decline), friends Guide on F6 | Passed |
| Launcher rooms: both games reach the lobby and kick off | Passed |
| START + B to leave an online match | Passed |
| Launcher language and monitor reach the game | Passed |
| Vulkan starts and signs in | Passed |
| World Tour: leaving a match no longer closes the game | Passed (2.0.3) |
| Online room to a full match between two games (synced, no disconnect) | Passed (2.0.6) |
| Portuguese (PT-BR) menus and pop-ups, Direct3D 12 and Vulkan | Passed (2.0.6) |
| World Tour progress from 2.0.0 / 2.0.1 brought back | Passed (2.0.6) |
| Auto-update 2.0.5 to 2.0.6 keeps online name, server and key | Passed (2.0.6) |
| Update from 1.5.7 / 2.0 / 2.0.1 / 2.0.2 keeps region, online name, settings and World Tour saves | Passed |

### Linux (beta, 2.0.7)

Tested by the developer on CachyOS (AMD, RADV), with the native Linux launcher:

| Check | Result |
|---|---|
| Install from the ISO in the Linux launcher (setup downloaded and checked, headless install through Proton) | Passed |
| PLAY through umu-run (Proton-CachyOS) | Passed |
| PLAY through Proton directly, without umu-launcher | Passed |
| Online: the game connects to the servers | Passed |

**Not yet tested on Linux:** the Steam Deck / SteamOS, Valve's Proton from a Steam library and the Steam Linux Runtime, NVIDIA and Intel graphics, launcher rooms and friend invites, and a system without WebKitGTK (the page then opens in the browser).

**Not yet tested on 2.0:** two separate real PCs by the team, NVIDIA and Intel graphics on Windows.

**Known issues:** the phone messages in a new World Tour can look wrong; plus the limits listed in the [README](../README.md#known-limits-20).

---

## Older: ReStreet v1.3

Last updated: **14 October 2026**, for **ReStreet v1.3** (offline only). This page separates what was tested from what was not.

## What was tested for v1.3

All of this was tested by the developer on one Windows 11 PC with an AMD Radeon RX 9070 XT, a Ryzen 5 7600 and a 165 Hz monitor with Adaptive Sync, at 100% display scale.

| Check | Result |
|---|---|
| Direct3D 12, VSync on at 60 Hz: menus, match, goals and replays at 60 FPS, one new image per refresh (DXGI frame statistics) | Passed |
| Same with Adaptive Sync / FreeSync on | Passed |
| Vulkan, VSync on at 60 Hz, Adaptive Sync on: smooth, 99% low about 57-58 FPS | Passed |
| In-game FPS counter equals the AMD overlay with VSync on | Passed |
| Court floors without graphical garbage in the two venues where it was seen | Passed |
| Main menu options open without the Direct3D 12 crash (Direct3D 12 and Vulkan) | Passed |
| Update over v1.2 (installer `--update-test` on a copy): all files replaced, settings kept | Passed |

**Not yet tested on v1.3:** NVIDIA and Intel graphics, refresh rates the game cannot sustain in a match (above about 90 FPS on the test PC), a long play session.

The game module is unchanged from v1.2; only the runtime, the launcher and the installer changed. World Tour and Practice were validated on earlier releases and not repeated.

## Graphics backends

The package contains two validated backend folders, `Game/Backends/D3D12` and `Game/Backends/Vulkan`. The launcher copies the selected backend's `rexruntime.dll` and `rexgpu-xenos.dll` beside `fifastreet.exe` before launch. Direct3D 12 is the default.

### SHA-256 of the backend files in v1.3

| Backend | `rexruntime.dll` | `rexgpu-xenos.dll` |
|---|---|---|
| Direct3D 12 | `D74A137AC448D82381FF408AFDB92E4FCE449F3E96E8977A108AC3EA5AB4F041` | `D6068770C9644184B6BC9E0BE6DE3D2521EF542E36C5DF4DD527544FDA289D18` |
| Vulkan | `EF2E7F7AD33DDB950FC98CCDA238FEF40491D34D889BA7C57C5DBFE37523BC07` | `ADBF3CB83E5CB18C41C83E60ED59DD4DEC38A330E8E3D0B6127926F30460C5C3` |

The installer checks its payload against the hashes in its own manifest before installing.

## Performance options

- **Profiles:** Balanced (the launcher defaults), Quality (2x internal resolution, FXAA Extreme, MSAA), Performance (native resolution, no post-processing, Fast readback, occlusion queries off) and Custom. They only set options the launcher already has.
- **Readback Resolve:** Some is the default in the Balanced and Quality profiles since v1.1: it was reworked to avoid the graphical garbage it used to cause, and it was tested on one GPU only. Full (compatible) is the old default. The Performance profile uses Fast, which gives more FPS under heavy GPU load but has not been validated on other hardware.
- **Shader cache:** a small set of pre-built shaders ships in `Game/shader-seed` and is copied into the player's cache on the first run. It never replaces files the player already has.

## Resolution model

Output resolution and internal rendering scale are independent. Internal resolution is a whole-number multiple of the game's 1280x720 base: 1x is 1280x720, 2x is 2560x1440, 3x is 3840x2160 and 4x is 5120x2880. Higher settings are available for testing and are not a performance guarantee.

## Not tested

- Any GPU other than the AMD Radeon RX 9070 XT (an RX 6700 was used on earlier releases). NVIDIA and Intel graphics have not been tested.
- Weak or old PCs. The system requirements in the README are estimates.
- The Performance profile on a PC where the GPU is the limit.
- Other ISO regions or revisions: the installer accepts only builds that match the three verified module hashes.
- Long World Tour careers, all venues, all controller types and long play sessions.
- Windows 10.

## Known limitations

- No universal hardware, driver or frame-rate guarantee. 3x/4x internal rendering and 4K output are not guaranteed 60 FPS modes.
- No online play in v1.3.
- The window title of the game itself still reads "FIFA Street PC"; the launcher and installer use the ReStreet name.
- The exit prompt only opens with the START + B (START + Circle) shortcut, not from the main menu.
- The installer expands its details panel by 180 px, which does not fit on screens 768 px high or less.
- Free-space requirements depend on extraction and temporary files.

## Development method

SamuelitoDaVila directed and tested the project with AI assistance from ChatGPT, OpenAI Codex and Anthropic's Claude (Claude Code). The statements above separate what was actually tested from what still needs broader coverage.
