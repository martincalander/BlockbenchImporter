# Releasing

Only a maintainer with repository release access can publish an official version.

## Checklist

1. Confirm the working tree contains only intended changes.
2. Run Unity EditMode tests and confirm a clean compilation.
3. Run `python3 .github/scripts/validate_package.py`.
4. Update `package.json` using semantic versioning.
5. Move relevant notes into a dated `CHANGELOG.md` section with the same version.
6. Merge the release-ready changes to `main`.
7. Create and push an annotated tag named `v<package-version>`.

The release workflow verifies the tag/version match, packages the UPM repository, generates a SHA-256 checksum, and creates the GitHub release. Inspect the archive and checksum before announcing the release.

Never retag or replace a published version. Correct a bad release with a new patch version and explain the issue in the changelog.
