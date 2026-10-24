<div align="center">

<img src="docs/images/project-icon.png" alt="ReStreet" width="64">

# Proton/Wine Launcher documentation

**This file explains how the Proton/Wine launcher works.**

## Why this?

ReStreet uses, so to speak, a web app for its UI instead of a only a traditional .exe file. This web app has problems for rendering on Linux, specially on SteamOS, where, in case of not being able to open the launcher, it's fallback opens the default browser, causing a bug that ends with an invisible classic UI brwoser (user sees nothing, but when clicking any part of the screen, a dropdown menu appears like the launcher is there). On SteamOS Gamemode it's catastrophic, using Steam web browser, resulting in phantom windows, bugged game starts, problems to access to library.

The fix? instead of building a new launcher, just modify the existing launcher and replace the actual web wrapper with electron. This way, the user on Proton/Wine will see a normal window instead of the old web browser menu.

<div align="left">
  
## How it works

The new launcher uses a compiled web viewer using electron framework. A mini browser with only one window, so to speak. This launcher size is around 250MB, so it is not included in the original game setup to reduce installation size. Windows users should not see any changes at all, as the launcher relies on webview2, a dependency that is normally available on Windows. Proton/Wine users however, they should download an aditional 250MB for opening the new launcher. This isn't mandatory; there are commands users can type in their launch arguments to enable the old UI or to stop the download and just play right away.

## Setup

Users should only download the setup and install. 

This is an unofficial community project, with no affiliation to EA, Microsoft or Xbox.
