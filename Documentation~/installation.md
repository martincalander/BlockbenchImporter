# Installation and upgrades

## Requirements

- Unity `2021.3` or newer
- A Git client available to Unity when installing from Git
- Blockbench projects saved as `.bbmodel`

## Install with Package Manager

In Unity, open **Window → Package Manager**, select **+ → Add package from git URL…**, and enter:

```text
https://github.com/martincalander/UnityBlockbenchImporter.git
```

For a reproducible project, pin a release tag:

```text
https://github.com/martincalander/UnityBlockbenchImporter.git#v1.0.0
```

## Install through `manifest.json`

Add this entry to your project's `Packages/manifest.json` dependencies:

```json
"com.essentials.blockbenchimporter": "https://github.com/martincalander/UnityBlockbenchImporter.git#v1.0.0"
```

Keep the surrounding JSON valid and retain the comma required between dependency entries.

## Upgrade

Change the tag in `Packages/manifest.json`, then allow Unity to resolve packages. Read the [changelog](../CHANGELOG.md) before moving across minor or major versions. Commit both `manifest.json` and `packages-lock.json` so collaborators receive the same package version.

## Remove

Remove `com.essentials.blockbenchimporter` in Package Manager or delete its entry from `Packages/manifest.json`. Existing `.bbmodel` source files remain untouched, but their imported Unity representations will no longer be available until an importer for that extension is installed.

## Troubleshooting installation

- **Git executable not found:** install Git and restart Unity Hub and the Editor.
- **Repository not found:** verify the URL and network access.
- **Package resolution failed:** remove a stale package lock entry only after closing Unity, then reopen the project.
- **Unsupported Editor:** use Unity `2021.3` or later.

See [troubleshooting](troubleshooting.md) for import-specific help.
