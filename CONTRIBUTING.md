# Contributing to the BYES Launcher

Thanks for considering a contribution. This is a community project for a
small Arma 2 / DayZ community, so we keep the bar pragmatic — clean code that
solves a real player problem is what we're after.

## Quick start

```powershell
git clone https://github.com/Fallen-Donkey/Arma2Launcher.git
cd Arma2Launcher
dotnet run --project src/ByesLauncher
```

Requirements: **.NET 8 SDK** on Windows. The launcher targets `net8.0-windows`
because it's WPF.

## Code style

- The codebase uses MVVM via [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/).
  Use `[ObservableProperty]` and `[RelayCommand]` source generators rather
  than hand-rolling `INotifyPropertyChanged`.
- Services live in `src/ByesLauncher/Services/`, view models in `ViewModels/`,
  views in `Views/`, dialogs in `Views/Dialogs/`.
- Comments are encouraged, but only where they add information beyond what
  the code already says — explain the "why," not the "what."
- File-scoped namespaces, nullable reference types on, implicit usings on.
  These are already configured in the csproj.

## Pull request checklist

Before opening a PR:

1. **Build cleanly** — `dotnet build src/ByesLauncher` should pass with
   zero warnings on your branch.
2. **Manual smoke test** — run the launcher, confirm the change you made
   actually works end-to-end (e.g. if you touched the download flow,
   actually sync a modpack).
3. **Don't commit `dist/` or `.user` files** — `.gitignore` excludes them
   already, but double-check.
4. **Keep PRs scoped** — one feature or fix per PR. Refactors that touch
   half the codebase are hard to review.

## Where to ask questions

- **Design questions / "should I build this?"** — open a GitHub Discussion
  before starting work, especially for bigger features.
- **Player-facing bugs** — open an Issue with: launcher version, what server
  you tried to join, any error message from `%APPDATA%\ByesLauncher\` logs.
- **Real-time** — the [BYES Discord](https://discord.gg/Kng9hBA9V8) has a
  `#launcher` channel for fast feedback.

## Releasing (maintainers only)

1. Bump `<Version>` in `src/ByesLauncher/ByesLauncher.csproj`.
2. `git tag v0.1.1 && git push --tags`.
3. The release workflow (`.github/workflows/release.yml`) runs `dotnet
   publish` + `vpk pack` and creates a GitHub Release with the artifacts
   attached. Once SignPath integration is live, those artifacts will be
   signed automatically.
4. SCP the artifacts (or have CI auto-deploy) to `byes.nz/launcher/`. Existing
   installed launchers pick up the update on next startup.

## Code of conduct

Be kind. Disagree about technical things, not about people. If a PR or comment
crosses a line, the maintainers will reach out privately first. Repeated bad
behavior gets you blocked from the repo — short version of the
[Contributor Covenant](https://www.contributor-covenant.org/).
