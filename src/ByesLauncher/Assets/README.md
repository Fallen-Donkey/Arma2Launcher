# Assets

Drop your final logo here as `logo.png` (recommended 256×256 transparent PNG).

The launcher's `MainWindow.xaml` already has a slot for it: an `<Image>` whose
`Source` points at `pack://application:,,,/Assets/logo.png`. When that file is
present, it replaces the "BYES" placeholder badge in the header band.

After adding the file, set its **Build Action** to `Resource` (right-click in
Visual Studio, or it's already wired by the `<Resource Include="Assets\logo.png" />`
glob in the csproj — no action needed if you just drop a `logo.png` here).

Optional sibling files the launcher will also pick up if present:
- `logo@2x.png` — high-DPI variant (512×512)
- `app.ico`     — used as the EXE icon (set `<ApplicationIcon>` in csproj)
