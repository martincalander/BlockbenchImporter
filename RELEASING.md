# Releasing

Official releases are published by the maintainer.

1. Start from a clean working tree.
2. Compile in Unity and run the EditMode tests.
3. Run `python3 .github/scripts/validate_package.py`.
4. Set the new version in `package.json`.
5. Add the same version and date to `CHANGELOG.md`.
6. Merge to `main`.
7. Create and push an annotated `v<version>` tag.

The release workflow checks the tag, builds the UPM archive, writes a SHA-256 checksum, and creates the GitHub release. Download the result and inspect it before announcing it.

Do not move a published tag. If a release is bad, publish a new patch version and explain what happened in the changelog.
