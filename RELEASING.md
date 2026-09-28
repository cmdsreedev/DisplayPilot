# Releasing DisplayPilot

## Automated release

1. Merge and verify changes on `main`. The Build and test workflow must pass.
2. Choose an unused semantic version, such as `1.0.1`.
3. Create and push an annotated tag:

   ```powershell
   git tag -a v1.0.1 -m "DisplayPilot 1.0.1"
   git push origin v1.0.1
   ```

The Release DisplayPilot workflow runs the regression checks, publishes a self-contained Windows x64 application, compiles Inno Setup, and publishes a GitHub release with:

- `DisplayPilot-Setup.exe`
- `DisplayPilot-win-x64.zip`
- `SHA256SUMS.txt`

The tag determines both executable and installer versions. Downloads are release attachments, never committed binaries. The workflow uses GitHub's built-in token; no personal token is needed. Inno Setup is fetched from a pinned official release and checked against its SHA-256. Installers are currently unsigned.

## Local release build

Install .NET 10 SDK and Inno Setup 6, then run:

```powershell
./scripts/Build-Release.ps1 -Version 1.0.1 -InnoCompiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

Outputs are placed under `artifacts/1.0.1/`. Intermediate build folders under `artifacts/` are also ignored. Each build uses a fresh staging directory and rejects personal settings/key files from publish output.

## Privacy before a commit

`git status --short` should contain source, documentation, icon assets, or workflow files only. `.gitignore` excludes settings JSON and backups, local environment files, IDE state, build folders, executables, libraries, archives, and release artifacts. User-specific device mappings belong only in `%LOCALAPPDATA%\DisplayPilot\settings.json`. The public app starts with no device assignments; tests use synthetic identifiers.
