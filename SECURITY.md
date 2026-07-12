# Security policy

Unity Blockbench Importer processes asset files inside the Unity Editor. Treating imported data safely is a core project requirement.

## Supported versions

| Version | Security updates |
|---|---|
| Latest tagged release | Supported |
| `main` | Supported for development; not recommended for production |
| Older releases | Upgrade before requesting a fix |

## Report a vulnerability privately

Do not open a public issue for a suspected vulnerability.

Use [GitHub's private vulnerability reporting](https://github.com/martincalander/UnityBlockbenchImporter/security/advisories/new) when available. If that form is unavailable, email `martin.calander@gmail.com` with the subject `UnityBlockbenchImporter security report`.

Include:

- affected package and Unity versions;
- the attack preconditions and impact;
- a minimal proof of concept or malformed `.bbmodel`, if safe to share;
- suggested mitigations, if known;
- whether and when you plan to disclose the issue.

You should receive an acknowledgement within seven days. The maintainer will validate the report, coordinate a fix and release, and credit the reporter unless anonymity is requested. Timelines depend on severity and reproducibility; please allow a reasonable remediation window before public disclosure.

## Security boundaries

- `.bbmodel` content is parsed as data and must never execute embedded expressions.
- The importer does not load Blockbench plugins or arbitrary managed assemblies.
- Source models are never rewritten.
- External texture paths are read-only inputs; unexpected or missing files produce diagnostics.
- Generated assets remain within Unity's import context.

Reports about a malicious Blockbench or Unity installation, social engineering, unsupported custom plugins, or vulnerabilities in unrelated dependencies are outside this project's direct scope, though actionable integration risks are still welcome.
