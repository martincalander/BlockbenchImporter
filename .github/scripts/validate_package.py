#!/usr/bin/env python3
"""Dependency-free structural validation for the UPM repository."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote


ROOT = Path(__file__).resolve().parents[2]
ERRORS: list[str] = []


def fail(message: str) -> None:
    ERRORS.append(message)


def load_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        fail(f"{path.relative_to(ROOT)}: invalid JSON: {error}")
        return {}


def validate_manifest() -> None:
    manifest = load_json(ROOT / "package.json")
    expected = {
        "name": "com.martincalander.blockbenchimporter",
        "displayName": "Blockbench Importer",
        "license": "Apache-2.0",
    }
    for key, value in expected.items():
        if manifest.get(key) != value:
            fail(f"package.json: {key!r} must equal {value!r}")

    version = str(manifest.get("version", ""))
    if not re.fullmatch(r"0|[1-9]\d*\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?", version):
        fail(f"package.json: invalid semantic version {version!r}")

    for key in ("description", "unity", "documentationUrl", "changelogUrl", "licensesUrl", "repository", "author"):
        if not manifest.get(key):
            fail(f"package.json: missing {key!r}")

    samples = manifest.get("samples", [])
    if not any(sample.get("path") == "Samples~/PostImportCustomization" for sample in samples
               if isinstance(sample, dict)):
        fail("package.json: missing Post-Import Customization sample declaration")

    changelog = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    if f"## [{version}]" not in changelog:
        fail(f"CHANGELOG.md: no release heading for package version {version}")

    citation = (ROOT / "CITATION.cff").read_text(encoding="utf-8")
    if f'version: "{version}"' not in citation:
        fail(f"CITATION.cff: version does not match package.json ({version})")


def validate_required_files() -> None:
    required = (
        "README.md",
        "CHANGELOG.md",
        "LICENSE.md",
        "NOTICE.md",
        ".github/CONTRIBUTING.md",
        ".github/CODE_OF_CONDUCT.md",
        ".github/SECURITY.md",
        ".github/SUPPORT.md",
        ".github/GOVERNANCE.md",
        ".github/MAINTAINERS.md",
        ".github/RELEASING.md",
        "package.json",
        "Documentation~/index.md",
        "Documentation~/installation.md",
        "Documentation~/importing-models.md",
        "Documentation~/format-support.md",
        "Documentation~/troubleshooting.md",
        "Documentation~/architecture.md",
        "Documentation~/roadmap.md",
        "Documentation~/Images/logo-mark.svg",
        "Samples~/PostImportCustomization/BlockbenchImportMarker.cs",
        "Samples~/PostImportCustomization/Editor/BlockbenchPostprocessorExample.cs",
        "Samples~/PostImportCustomization/SampleBird.postprocess.bbmodel",
        "Samples~/PostImportCustomization/README.md",
    )
    for relative in required:
        if not (ROOT / relative).is_file():
            fail(f"missing required file: {relative}")

    license_text = (ROOT / "LICENSE.md").read_text(encoding="utf-8")
    notice_text = (ROOT / "NOTICE.md").read_text(encoding="utf-8")
    if "Apache License" not in license_text or "Version 2.0" not in license_text:
        fail("LICENSE.md: expected Apache License 2.0 text")
    if "Martin Calander" not in notice_text:
        fail("NOTICE.md: maintainer attribution is missing")


LINK_PATTERN = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")


def validate_markdown_links() -> None:
    for markdown in ROOT.rglob("*.md"):
        if ".git" in markdown.parts:
            continue
        text = markdown.read_text(encoding="utf-8")
        for line_number, line in enumerate(text.splitlines(), start=1):
            if line.rstrip() != line and not line.endswith("  "):
                fail(f"{markdown.relative_to(ROOT)}:{line_number}: trailing whitespace")

        for raw_target in LINK_PATTERN.findall(text):
            target = raw_target.strip().split(maxsplit=1)[0].strip("<>")
            if not target or target.startswith(("#", "http://", "https://", "mailto:")):
                continue
            path_part = unquote(target.split("#", 1)[0])
            if not path_part:
                continue
            resolved = (markdown.parent / path_part).resolve()
            try:
                resolved.relative_to(ROOT.resolve())
            except ValueError:
                fail(f"{markdown.relative_to(ROOT)}: local link escapes repository: {target}")
                continue
            if not resolved.exists():
                fail(f"{markdown.relative_to(ROOT)}: broken local link: {target}")


def validate_unity_metadata() -> None:
    roots = (ROOT / "Editor", ROOT / "Runtime", ROOT / "Tests")
    for base in roots:
        if not base.exists():
            continue
        entries = [base, *base.rglob("*")]
        for entry in entries:
            if entry.name.endswith(".meta") or entry.name.startswith("."):
                continue
            meta = Path(f"{entry}.meta")
            if not meta.is_file():
                fail(f"missing Unity metadata: {meta.relative_to(ROOT)}")

    for relative in ("package.json", "README.md", "CHANGELOG.md", "LICENSE.md"):
        if not (ROOT / f"{relative}.meta").is_file():
            fail(f"missing Unity metadata: {relative}.meta")


def validate_repository_hygiene() -> None:
    forbidden = {"Library", "Temp", "Obj", "Logs", "UserSettings", "node_modules"}
    for path in ROOT.iterdir():
        if path.name in forbidden:
            fail(f"generated directory must not be committed: {path.name}/")

    for path in ROOT.rglob("*"):
        if path.is_file() and path.stat().st_size > 10 * 1024 * 1024:
            fail(f"unexpected file larger than 10 MiB: {path.relative_to(ROOT)}")


def main() -> int:
    validate_manifest()
    validate_required_files()
    validate_markdown_links()
    validate_unity_metadata()
    validate_repository_hygiene()

    if ERRORS:
        print("Package validation failed:", file=sys.stderr)
        for error in ERRORS:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print("Package validation passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
