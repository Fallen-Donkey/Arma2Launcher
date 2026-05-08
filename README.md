# Backyard Esports Launcher

[![Build](https://github.com/Fallen-Donkey/Arma2Launcher/actions/workflows/build.yml/badge.svg)](https://github.com/Fallen-Donkey/Arma2Launcher/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A community launcher for **Arma 2 Operation Arrowhead** and **DayZ
Standalone**, built for the Backyard Esports community. One-click join for
BYES servers with auto mod-sync — no manual installs, no config hell.

> **Players: just want to play?** Grab the installer from
> **[byes.nz/launcher](https://byes.nz/launcher)**.
> The rest of this README is for developers and contributors.

## Features

- **Server browser** — every public Arma 2 OA + DayZ Standalone server (via
  the Steam master server, no API key needed). BYES community servers pinned
  to the top.
- **One-click join** — picks the right modpack, downloads only what's
  missing/changed (SHA256-verified, resumable, parallel), launches the game
  with the correct `-mod=` / `-connect=` arguments.
- **Filters that match what DayZ players actually use** — Hide
  Empty/Full/Password/Offline/Non-BattlEye, Map and Mod dropdowns, name
  search.
- **Profile management** — Arma 2 profile picker per join, per-profile mod
  toggles, BattlEye on/off.
- **Favorites + History tabs** — pinned servers and the last 25 you joined.
- **Player list popup** — A2S_PLAYER queries to see who's on a server before
  you commit.
- **Custom URL handler** — `byes://connect?ip=...&port=...&modpack=...` for
  one-click join links from the website.
- **Auto-update** via [Velopack](https://github.com/velopack/velopack).
  Releases ship as both an installer and a portable zip.

## Architecture

```
src/ByesLauncher/      # WPF launcher (.NET 8, MVVM via CommunityToolkit.Mvvm,
                       # styled with WPF-UI; Velopack for auto-update)
tools/manifest_gen.py  # Hash a folder of @ModName subfolders into a content-
                       # addressed manifest (used to publish modpacks server-side)
.github/workflows/     # CI (build + tests) and release (Velopack pack +
                       # GitHub Release) pipelines
```

The launcher talks to a backend at `https://byes.nz` (the Backyard Esports
website) for the curated BYES server list, modpack manifests, and content-
addressed mod blob storage. The backend is a separate project — this repo is
just the launcher.

## Building from source

Requirements: **.NET 8 SDK** on Windows.

```powershell
git clone https://github.com/Fallen-Donkey/Arma2Launcher.git
cd Arma2Launcher
dotnet run --project src/ByesLauncher
```

For a release build:

```powershell
dotnet publish src/ByesLauncher -c Release -r win-x64 --self-contained -o dist/publish
```

For the full Velopack release set (installer + portable + delta-update payload):

```powershell
dotnet tool install -g vpk
vpk pack -u ByesLauncher -v 0.1.0 -p dist/publish -e ByesLauncher.exe -o dist/releases `
  --packTitle "Backyard Esports Launcher" --packAuthors "Backyard Esports"
```

`vpk` produces:

| File | Purpose |
|---|---|
| `ByesLauncher-win-Setup.exe` | Public installer (what users download) |
| `ByesLauncher-win-Portable.zip` | Alternate, no-install download |
| `ByesLauncher-<ver>-full.nupkg` + `releases.win.json` | Auto-update feed payload |

## Contributing

PRs welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Areas where contributions
would be especially appreciated:

- DayZ Standalone Steam Workshop integration (currently the SA tab just
  steam-protocol-launches; full Workshop subscription management is open).
- Discord rich presence ("Playing on BYES Epoch").
- Crash telemetry hookup (Sentry or a simple `/api/crash` POST).
- Localization beyond English.

## Code signing

The launcher is currently **unsigned**, which means Windows SmartScreen
shows the "Windows protected your PC" dialog on first install. Click
**More info** → **Run anyway** to install. The warning goes away after
Windows builds a reputation for the binary.

A small minority of Win11 users on Smart App Control may see a stricter
block. See [byes.nz/launcher](https://byes.nz/launcher) for the workaround
documentation.

Code signing is on the roadmap once the project's user base grows enough
to justify the recurring cost of an EV/OV certificate. Geographic
restrictions on Microsoft's cheap Trusted Signing service (US/CA/EU/UK
only — we're Australia-based) ruled out the obvious budget option.

## Why is the source open?

A launcher with write access to your game folder should be auditable. You
can read every line and verify it does what it says before installing.

## License

[MIT](LICENSE) — do whatever you want with it, just don't blame us if your
mods break.

## Related projects

- The [Backyard Esports website](https://byes.nz) — server browser, events,
  community Discord, and where the launcher pulls modpacks from.
- [Velopack](https://github.com/velopack/velopack) — auto-update framework
  this launcher uses.
- [WPF-UI](https://github.com/lepoco/wpfui) — Fluent design controls for WPF.
