# Roadmap

The roadmap communicates direction, not a delivery promise. Accepted work is tracked in [GitHub issues](https://github.com/martincalander/UnityBlockbenchImporter/issues) and prioritized according to fidelity impact, safety, reproducibility, and maintenance cost.

## Current priorities

- Expand real-world format fixtures without committing proprietary artwork.
- Strengthen cross-version Unity import and animation tests.
- Improve diagnostics for plugin-defined and partially supported model data.
- Validate additional render-pipeline material behavior.
- Add specialized geometry only where Blockbench semantics can be represented faithfully and safely.

## Non-goals

- Executing Molang or arbitrary plugin expressions from imported assets.
- Replacing Blockbench as an editor.
- Silently approximating unknown elements without diagnostics.
- Coupling generated assets to a proprietary runtime service.

Feature proposals should follow [CONTRIBUTING.md](CONTRIBUTING.md) and include a source-format analysis plus a testable Unity mapping.
