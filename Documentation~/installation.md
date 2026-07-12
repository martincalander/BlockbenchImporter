# Installation

You need Unity `2021.3` or newer and Git installed on the same machine.

## Package Manager

Open **Window → Package Manager**, click **+ → Add package from git URL…**, and enter:

```text
https://github.com/martincalander/UnityBlockbenchImporter.git
```

There is no published version tag yet, so this URL currently follows `main`. After tagged releases begin, append `#vX.Y.Z` to pin a version.

## `manifest.json`

You can also edit `Packages/manifest.json` directly:

```json
"com.essentials.blockbenchimporter": "https://github.com/martincalander/UnityBlockbenchImporter.git"
```

Remember the comma between entries. Commit both `manifest.json` and `packages-lock.json`.

## Updating

Change the tag to the version you want, then let Unity resolve the package. Check the [changelog](../CHANGELOG.md) first when moving to a new minor or major version.

## Removing it

Remove the package in Package Manager or delete its line from `manifest.json`. Your `.bbmodel` files are not touched. Unity simply stops knowing how to import them.

If Unity cannot install the package, check that Git is available, the URL is correct, and the Editor is new enough. Closing and reopening Unity often clears a stale package-resolution error.
