# Changelog

All notable changes to the Backyard Esports Launcher are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
this project loosely follows [Semantic Versioning](https://semver.org/) — until
we hit 1.0, the minor number bumps for each release and the patch number
indicates an internal/unshipped iteration.

The launcher auto-updates via [Velopack](https://github.com/velopack/velopack);
existing installs pick up new releases on next launch.

## [0.1.18] — 2026-05-08

### Added
- Right-click context menus on Servers, Favorites, and History tabs (join,
  favorite/unfavorite, refresh, remove from history, etc.)
- Double-click any server row to join — same lifecycle as the Join button
- "JOINING…" button label state with a 5-second minimum-visible window so
  fast cache-hit launches still flash the feedback
- Selected-row visual highlight (cyan border, dark blue fill) on all three
  list tabs; wins over the BYES-server tint when both apply
- Right-pane details panel on Favorites and History tabs (mirrors the layout
  on the Servers tab so muscle memory transfers)
- Search box and refresh button on Favorites and History tabs
- Live favorites updates — starring a server in the Servers tab now appears
  in the Favorites tab instantly without a manual Refresh All
- Tab-activate auto-refresh — switching back to the Servers tab refreshes the
  data if it's more than 30 seconds stale (debounces tab-flipping)
- History bulk-clear: "Clear older than 7 / 30 / 90 days" plus existing
  "Clear all"
- History single-row remove via right-click "Remove from history" or the side
  button

### Fixed
- Map and Mod dropdown filters no longer reset when you click Refresh All
- Re-entrancy guard on the join command prevents double-click from launching
  twice while a join is in flight

### Internal
- New `ModpackJoinCoordinator.IsJoining` observable state shared across all
  three list-tab view models via singleton DI
- New `FavoritesService.Changed` event drives live Favorites tab updates
- New converters (`InverseBoolConverter`, `JoinButtonContentConverter`)
  registered in `App.xaml`
- Filter dropdown rebuild now preserves user selections through the
  `ObservableCollection` clear/refill cycle

## [0.1.17] — 2026-04-30

### Changed
- Windows 11 24H2 compatibility notice now uses a short URL
  (`byes.nz/win11fix`) instead of the full Discord channel link, so the
  redirect can be repointed without shipping a launcher update

### Added
- Cleaner BYES Discord callout in the 24H2 notice

## [0.1.16] — 2026-04-30 (internal)

### Changed
- Win11 24H2 notice text rewrite — clearer scope (auto-fixed vs.
  manual-fix-needed), explicit pointer to the Backyard Esports Discord
- Clicking "Yes" on the notice now opens the support URL in the user's
  browser

## [0.1.15] — 2026-04-29 (internal — packed but not deployed)

### Added
- `-nod3d9ex` launch parameter (default ON) — fixes the D3D9Ex Reset() crash
  introduced by Windows 11 24H2 affecting player death, server disconnect,
  and Abort-from-ESC scenarios
- One-time Windows 11 24H2 detection notice (build ≥ 26100) covering both
  known Arma 2 OA compatibility issues:
  - D3D9Ex crash (auto-fixed by `-nod3d9ex`)
  - BattlEye Error 577 (manual fix required — HVCI rejects the legacy BE
    driver; users replace community-patched files manually)
- New `WindowsCompatService` for OS-version detection and one-time notice
  display

## [0.1.14] — 2026-04-28

### Changed
- Steam-protocol launch (added in 0.1.13) made opt-in instead of default-on.
  Users now toggle `UseSteamLaunch` in Settings if they want the no-UAC
  Steam-handler launch path. Default off until the path is field-validated
  by power users.

## [0.1.13] — 2026-04-27 (internal — packed but not deployed)

### Added
- A2S keywords field parsing (the comma-separated `bt,r164,n144629,s1,...`
  tag string) — exposes the server's self-reported build number, BattlEye
  flag, signature verification mode, etc.
- Modpack matcher now uses the keywords string in its haystack — improves
  build-number-based modpack detection without requiring admin
  self-submission
- "Mods needed" dialog when the user joins a public (non-BYES) server with
  a recognized mod gametype but no registered modpack — gives the user a
  clear choice (continue with their own manual mods, or bail)
- Steam-protocol launch via `steam://run/33930//<args>` (made opt-in in
  0.1.14)

## [0.1.12] — 2026-04-26

### Fixed
- Sorting by Ping or Players column no longer causes "—" (unmeasured) rows
  to sort to the top. The custom sort handler now always preserves
  `HasPing` as a sort-tier above the user-clicked column, regardless of
  direction.

## [0.1.11] — 2026-04-25

### Added
- Clickable column-sort with BYES-server pinning preserved across all sorts
- `SortMemberPath` set explicitly on Players and Ping columns so they sort
  numerically (not alphabetically by display string)

## [0.1.10] — 2026-04-25

### Changed
- New default sort tier scheme on the Servers tab:
  1. BYES servers always pinned at top (`IsByes` desc)
  2. Measured servers above unmeasured ones (`HasPing` desc)
  3. Lowest ping first within the measured group (`Ping` asc)
  4. Most-populated wins as the unmeasured tiebreaker (`Players` desc)

## [0.1.9] — 2026-04-24

### Fixed
- `ByesServerEntry.QueryPort` field was missing from the C# DTO, so
  admin-set query_port values from the BYES backend were being silently
  dropped during deserialization (System.Text.Json default-tolerance). Now
  honored, with a `[JsonPropertyName("queryPort")]` annotation.

## [0.1.8] — 2026-04-23

### Added
- Multi-candidate query port probing for non-BYES servers — A2S queries
  now try `queryPort`, `gamePort`, and `gamePort+2` before giving up.
  Picks up servers using non-default port offsets (Arma 3-style configs,
  some shared hosts, etc.)

## [0.1.7] — 2026-04-22

### Added
- Two-pass A2S enrichment for ping discovery on public servers:
  1. Fast pass (parallelism 192, 2.5s timeout) — most responsive servers
  2. Slow retry pass (parallelism 48, 5s timeout) — under-load servers
  3. ICMP fallback for A2S-blocked hosts
- Click-time auto-probe — selecting an unmeasured row triggers a fresh
  ping probe in the background

### Why
- Steam Web API doesn't return latency, so Steam-discovered servers show
  ping as "—" until we measure it ourselves. The original Steam master
  server is offline; backend-side discovery via Steam Web API + BattleMetrics
  fills the gap but doesn't include ping.

## [0.1.6] — 2026-04-21

### Added
- BYES logo asset bundled into the launcher
- Multi-resolution `app.ico` baked into the EXE (16/32/48/256 px)

## [0.1.5] — 2026-04-20

### Fixed
- Sauerland / Chernarus addon errors on launch caused by 0.1.3's
  `Expansion;ca` mod-list prepend. Combined Operations Steam installs
  don't have a `ca/` directory inside the OA folder; the Arma 2 base
  game lives in a sibling Steam directory.
- Now detects the sibling Arma 2 base path automatically (Steam install
  + registry fallback) and prepends `<arma2 base>;Expansion` to the mod
  list.

## [0.1.4] — 2026-04-20

### Added
- Smart fuzzy modpack matching — gametype string → modpack slug-prefix
  matcher catches common variations (e.g. "Epoch 1.0.7" → `epoch-1.0.7.1`)
  even when an admin-defined regex hasn't been written yet

## [0.1.3] — 2026-04-19 (internal — superseded by 0.1.5)

### Added
- `Expansion;ca` prepend to the mod list and `@server*` mod filter
- (`ca` doesn't exist on Combined Operations Steam installs — fixed in 0.1.5)

## [0.1.2] — 2026-04-19

### Changed
- `ServerInfo.ModpackId` (single string) → `ModpackIds` (list) to support
  servers requiring multiple stacked modpacks (Epoch + Overwatch + Sauerland
  scenarios)
- Backward-compat `ResolveModpackIds()` helper preserves single-value
  semantics for older API responses

## [0.1.1] — 2026-04-18

### Added
- Backend integration baseline against `https://byes.nz` API
- Multi-modpack DTO support in the BYES servers payload

## [0.1.0] — 2026-04-17

### Initial release

- WPF launcher (.NET 8, MVVM via CommunityToolkit.Mvvm, WPF-UI Fluent design)
- Server browser for Arma 2 OA + DayZ Standalone
  - BYES community servers pinned to top
  - Filters: Hide Empty / Full / Password / Offline / Non-BattlEye, Map +
    Mod dropdowns, name search
- One-click join for BYES servers with auto mod-sync (SHA256-verified,
  resumable, parallel downloads from a content-addressed blob store)
- Profile management — Arma 2 profile picker per join, per-profile mod
  toggles, BattlEye on/off
- Favorites + History tabs
- Player list popup via A2S_PLAYER queries
- Custom URL handler — `byes://connect?ip=...&port=...&modpack=...`
  for one-click join links from the website
- Auto-update via Velopack
- Distribution: installer (`Setup.exe`) and portable zip
