# NohBoard

NohBoard is a lightweight, fully customisable keyboard (and mouse) visualizer for Windows. It
is designed to be captured in OBS / streaming software with a simple window capture, no fancy
graphics, and no game-capture quirks. Useful for streaming, tutorials, recording, or just
seeing what you're pressing.

This fork modernises the original 2016-2020 codebase so it builds and runs cleanly on
**Windows 11** with the current .NET tooling.

## What changed in this revival

The project had been dormant since 2020 and would silently fail to register key presses on
modern 64-bit Windows 11 installations. The root cause was a set of incorrect P/Invoke
signatures in the global-hook layer:

- `SetWindowsHookEx` / `CallNextHookEx` / `UnhookWindowsHookEx` declared the hook handle as
  `int` instead of `IntPtr` (`HHOOK` is pointer-sized on x64).
- The hook procedure delegate marshalled `wParam` as `int` instead of `IntPtr`.
- `MSLLHOOKSTRUCT.dwExtraInfo` and `KBDLLHOOKSTRUCT.dwExtraInfo` are `ULONG_PTR` (8 bytes on
  x64), but were declared as `int` (4 bytes), making the managed structs 4-8 bytes too
  small and corrupting the struct unmarshalling.

In addition:

- The whole solution has been retargeted from EOL `netcoreapp3.1` / `netstandard2.1` to
  **.NET 10** (LTS).
- The external `gotri.exe` build-time versioning tool has been removed; the version is now
  set inline in `NohBoard.csproj`.
- A proper Windows 10/11 application manifest with `asInvoker` and supported-OS GUIDs is
  shipped, and Per-Monitor V2 DPI awareness is configured via `<ApplicationHighDpiMode>` so
  the keyboard stays sharp on 4K HiDPI screens.
- New `xUnit` test project (`NohBoard.Tests`) with 138+ unit tests covering the state
  machines, the low-level hook dispatch, JSON settings round-trip, geometry helpers, every
  bundled keyboard definition, and the new path / logging / error-reporter helpers.
- A diagnostics overhaul: typed application paths (`%LOCALAPPDATA%\NohBoard\` by default
  with optional `--portable` mode), a rolling daily log, an empty-state hint on first launch
  so new users know what to click, a Help submenu (Open data folder / Open logs folder /
  About…), and an error reporter that always logs and offers "Copy details" / "Open log"
  actions.
- New GitHub Actions workflow builds, tests, and produces both a self-contained and a
  framework-dependent single-file `NohBoard.exe` on every tag.

## Running NohBoard

### Easiest: grab a release

Download the latest zip from the
[Releases page](https://github.com/ThoNohT/NohBoard/releases):

- **`NohBoard-X.Y.Z-win-x64-selfcontained.zip`** (~55 MB) - includes the .NET runtime.
  Just unzip and run `NohBoard.exe`.
- **`NohBoard-X.Y.Z-win-x64-framework-dependent.zip`** (~2 MB) - smaller, but requires
  the [.NET 10 Desktop Runtime](https://aka.ms/dotnet/download) to be installed.

Either way, keep `NohBoard.exe` and the `keyboards/` folder next to each other.

### Where does NohBoard store my data?

By default NohBoard splits its files between two locations:

| What                               | Where                                                                       |
| ---------------------------------- | --------------------------------------------------------------------------- |
| Bundled / read-only keyboard layouts | `<exe folder>\keyboards\`                                                  |
| User-edited keyboards & styles     | `%LOCALAPPDATA%\NohBoard\keyboards\`                                        |
| Settings (`NohBoard.json`)         | `%LOCALAPPDATA%\NohBoard\NohBoard.json`                                     |
| Logs                               | `%LOCALAPPDATA%\NohBoard\logs\nohboard-yyyyMMdd.log` (rolling, last 7 days) |

When you load a keyboard, NohBoard searches **all** of these in priority order:
1. `%LOCALAPPDATA%\NohBoard\keyboards\` (your edits win)
2. `<exe folder>\keyboards\` (the bundled defaults)
3. As a fallback for `dotnet run`-style development sessions, NohBoard walks up to 5 directories
   above the executable looking for a sibling `keyboards/` folder.

Saving a keyboard or style always writes into your user folder, never into the bundled set,
so installs in `Program Files\` work without UAC.

### Portable mode

Launch the executable with `--portable` (or `-portable` / `/portable`) and NohBoard reverts to
the old next-to-exe layout: settings, keyboards/, and `logs/` all live in the exe directory.
Useful for USB sticks, locked-down machines, or testing.

```pwsh
NohBoard.exe --portable
```

### Where are the logs?

A new rolling log file is created each day under `%LOCALAPPDATA%\NohBoard\logs\`. NohBoard
keeps the last 7 days of logs and prunes older ones automatically. You can open the log folder
straight from the app via **Right-click -> Help -> Open logs folder**, or copy the last 200
lines to the clipboard from **Help -> About...**. Errors also offer an "Open log" button
directly on the dialog.

### Adding NohBoard to OBS

1. Launch NohBoard, right-click to open the menu, and load a keyboard from `Keyboards`.
2. In OBS add a *Window Capture* source and pick the `NohBoard` window.
3. Optionally enable a chroma-key filter on the source to make the background transparent.

## Building from source

Requirements: **.NET 10 SDK** (or newer) on Windows. Visual Studio 2022/2024 with the
".NET desktop development" workload also works.

### One-liner via `build.bat`

The repo ships a `build.bat` wrapper at the root that uses only relative paths, so anyone
who clones the project can build, test, and produce release EXEs without remembering the
underlying `dotnet` commands:

```bat
git clone https://github.com/ThoNohT/NohBoard.git
cd NohBoard

build.bat                 :: restore + build + test + publish BOTH EXEs (default)
build.bat build           :: restore + Release build only
build.bat test            :: build and run the xUnit suite (138+ tests)
build.bat publish         :: publish both self-contained and framework-dependent EXEs
build.bat publish-sc      :: only the self-contained EXE (~70-90 MB, no .NET required)
build.bat publish-fd      :: only the framework-dependent EXE (~2 MB, needs .NET 10)
build.bat run             :: launch NohBoard via `dotnet run`
build.bat clean           :: remove every bin/ and obj/
build.bat help            :: show the usage banner
```

Outputs land under `NohBoard\NohBoard\bin\Release\publish\<profile>\NohBoard.exe`.

### Direct `dotnet` commands

If you prefer not to use the wrapper, the equivalent calls are:

```pwsh
# Build everything
dotnet build NohBoard/NohBoard.sln -c Release

# Run the test suite (138+ tests)
dotnet test NohBoard/NohBoard.Tests/NohBoard.Tests.csproj -c Release

# Run from source
dotnet run --project NohBoard/NohBoard/NohBoard.csproj -c Release

# Single-file self-contained .exe (no .NET install needed on the target machine)
dotnet publish NohBoard/NohBoard/NohBoard.csproj -p:PublishProfile=win-x64-selfcontained

# Single-file framework-dependent .exe (requires .NET 10 Desktop Runtime)
dotnet publish NohBoard/NohBoard/NohBoard.csproj -p:PublishProfile=win-x64-framework-dependent
```

Ship the produced exe alongside the `keyboards/` folder.

## Project layout

| Path                                 | Purpose                                                      |
| ------------------------------------ | ------------------------------------------------------------ |
| `NohBoard/NohBoard/`                 | Main WinForms application (`net10.0-windows`).               |
| `NohBoard/Hooking/`                  | Low-level keyboard/mouse hook library (64-bit P/Invoke).     |
| `NohBoard/clipper_library/`          | Polygon clipping library used for custom key boundaries.     |
| `NohBoard/NohBoard.Tests/`           | xUnit unit tests (state machines, hook dispatch, settings).  |
| `keyboards/`                         | Bundled keyboard definitions (`.json`) and styles.           |
| `.github/workflows/build.yml`        | CI: build + test + release.                                  |

## Contributors

**Maintainer / original author**
- Eric "ThoNohT" Bataille (e.c.p.bataille@gmail.com) - Original author

**Contributors**
- Marius "Buttercak3" Becker - Various bugfixes
- Ivan "YaLTeR" Molodetskikh - Added the scroll counter *(NohBoard classic)*
- Michal Mitter - Added button outline *(NohBoard classic)*

**Keyboard layouts**
- BaronBargy, Burning Fish, Cloudwolf, Daigtas, Floatingthru, HAJohnny, Helixia, joao7yt,
  kernel1337, Krazy, layarion, MCCrafterTV, MtB1980, TicTacFoe, ToxicMirror, WayZHC,
  wingsltd, zolia, SirDifferential, flyingmongoose, JapanYoshi, dchitra.

If you want to contribute, either with code, with keyboard definitions, or with keyboard
styles, feel free to fork this repository and open a pull request.

## Documentation

See the [Wiki](https://github.com/ThoNohT/NohBoard/wiki) for the full user documentation.

## Donations

Donations are neither required nor requested but are always appreciated. If you'd like to
donate, [PayPal is available](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=FFB9XFRWE5EK2).
Donations are made purely as appreciation of work performed and have no impact on the speed
or order in which features are implemented.

## License

NohBoard is licensed under the GPL version 2. The license agreement is attached in this
repository and can be found [here](https://github.com/ThoNohT/NohBoard/blob/master/LICENSE).
