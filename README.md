# DisplayPilot

Download DisplayMagician from its [official releases page](https://github.com/terrymacdonald/DisplayMagician/releases/latest). In Settings, use **Choose folder** to select its installation directory (usually `C:\Program Files\DisplayMagician`), or **Choose file** to select `DisplayMagicianConsole.exe`. Click **Save all settings** to persist the selection. **Open DisplayMagician** launches the main application from that folder so you can create or edit display layouts.

A Windows tray utility that maps input devices to DisplayMagician profiles.

## Install and use
Run DisplayPilot-Setup.exe. Installation is per user and includes a Start Menu shortcut, uninstaller, and optional desktop/startup shortcuts. The application includes the .NET runtime. DisplayMagician must be installed separately. Exit the old DeviceDetector/DisplaySwitcher tray application before using DisplayPilot so two applications do not compete to switch displays.

Dashboard shows the last successfully requested profile and last keyboard/mouse input. Closing the window keeps the tray application running; use tray > Exit to stop. Every launch assumes the configured PC profile. It never queries CurrentProfile or switches displays at startup. Changes made outside DisplayPilot are not tracked.

Devices: refresh to enumerate attached raw-input devices. Edit friendly names and select any saved profile or Ignore. Multiple devices may use the same profile. New devices default to Ignore. Add/remove rows using the grid; select a row header and press Delete to remove. An identifier can contain multiple alternatives separated by |. Matching is case-insensitive, first matching rule wins. Save mappings to activate edits.

Fresh installations start with no device assignments. Refresh Devices, assign the keyboards or mice you want to each profile, and save. Personal assignments remain in local settings and are not distributed in source or releases.

Profiles: Detect profiles reads %LOCALAPPDATA%\DisplayMagician\Profiles\DisplayProfiles.json without launching DisplayMagician. Add any exact saved profile name manually if detection is unavailable. Save profiles before assigning newly added profiles on Devices. These names refer to existing DisplayMagician layouts; adding a name does not create a display layout. If renaming a profile, update its device mappings and PC/TV button selections before saving.

Settings: select DisplayMagicianConsole.exe, choose the profiles used by the PC and TV buttons, set cooldown (default 5 seconds), and configure minimized/startup behavior. Dashboard and tray automatic-switch toggles save immediately. Other edits require Save. Start with Windows uses the current user's Run registry key.

HID devices are enumerated, with generic Sony/Microsoft labels for recognized vendor IDs, and can be assigned for future support. Controller input activation is intentionally not enabled: idle HID reports and controller-specific dead zones require validation first. Xbox devices exposed only through XInput may not appear. Keyboard and mouse raw input remain the active switching sources.

## Settings
%LOCALAPPDATA%\DisplayPilot\settings.json

Defaults are created on first save. Settings survive uninstall/reinstall. Invalid JSON is backed up as settings.json.invalid-TIMESTAMP before defaults are used. Profile failures retain the prior assumed state and display a tray error. A failed attempt also incurs cooldown to prevent rapid retries. Manual switching bypasses cooldown, but never repeats the current profile.

## Build
Requires Windows x64, .NET 10 SDK, and Inno Setup 6 or newer.

    dotnet run --project tests\DisplayPilot.Tests.csproj -c Release
    dotnet publish DeviceDetector.csproj -c Release -r win-x64 --self-contained true -o publish --source https://api.nuget.org/v3/index.json
    ISCC.exe DisplayPilot.iss

Executable: publish\DisplayPilot.exe (keep the entire publish directory together).
Installer: installer\DisplayPilot-Setup.exe.

The installer and application are not code-signed. The custom icon is original vector-style artwork rendered into an ICO.

## Validation
Regression checks cover default mappings, assumed-PC startup, no-op first desk events, K600 identifiers, cooldown, pause, manual override, arbitrary profiles, Ignore, failed switches, concurrent requests, and JSON round trip. --smoke-test renders the dashboard and exits without registering input or invoking DisplayMagician. Hardware display changes require a final hands-on check with the desk devices and K600; automated checks do not physically exercise those devices.


## Downloads and releases

Download installers and portable builds from [GitHub Releases](https://github.com/cmdsreedev/DisplayPilot/releases/latest). See [RELEASING.md](RELEASING.md) for the tag-based release workflow and local packaging command. Personal settings and compiled output are excluded from Git.

