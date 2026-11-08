# Roadmap

This is the rough direction, not a promise or schedule.

- Preserve sub-asset references when source names change. Mesh, texture, material, and clip identifiers currently use names, so a rename can change their Unity file IDs. Moving to UUID-based identifiers needs a migration that preserves existing references.
- Track render-pipeline changes as import dependencies so generated materials refresh automatically. For now, reimport models after changing pipelines.
- Verify animated skinned-mesh bounds across complete clips. Current bounds cover the rest pose; large bone motions can move vertices outside them.
- Consider optional mesh combining for static models with many cubes, with animation, skinning, and per-element customization kept intact.
- Add more small, redistributable test models.
- Test imports across more Unity versions.
- Make warnings for plugin-defined data more useful.
- Check generated materials in more render pipelines.
- Add texture meshes and splines when their Blockbench behavior can be reproduced properly.
- Apply PBR texture groups to HDRP materials.

Some things are deliberately out of scope:

- running Molang or arbitrary plugin code from an asset;
- replacing Blockbench as an editor;
- guessing silently when an element is unknown;
- requiring a hosted service to use imported models.

If you want to work on one of these areas, open an issue and read [CONTRIBUTING.md](https://github.com/martincalander/BlockbenchImporter/blob/main/.github/CONTRIBUTING.md).
