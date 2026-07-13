# Security

Please report security problems privately, not in a public issue.

Use [GitHub private vulnerability reporting](https://github.com/martincalander/com.martincalander.blockbenchimporter/security/advisories/new). If that is unavailable, email `martin.calander@gmail.com` with the subject `com.martincalander.blockbenchimporter security report`.

Include the affected package and Unity versions, what an attacker needs to do, the likely impact, and a small proof of concept if it is safe to share. Please say if you plan to publish the report later.

Martin will try to acknowledge a report within seven days, reproduce it, and coordinate a fix. Reporters are credited unless they ask not to be.

## Supported versions

Security fixes target the latest tagged release. `main` contains development work. If you are on an older release, please check the latest version before reporting the problem.

## The important boundaries

- `.bbmodel` files are data, not code.
- Molang and plugin expressions are not run.
- Blockbench plugins and arbitrary assemblies are not loaded.
- The source model is not rewritten.
- External textures are read-only inputs.
- Generated objects stay in Unity's import context.

Problems in Blockbench, Unity, or an unrelated plugin may need to be reported to that project instead, but importer-specific ways of reaching them are still useful to know about.
