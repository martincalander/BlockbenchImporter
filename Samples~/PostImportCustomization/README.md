# Post-import customization sample

This sample shows how to customize generated `.bbmodel` assets without changing the importer package.

Import it from **Window → Package Manager → Blockbench Importer → Samples**. The included `SampleBird.postprocess.bbmodel` opts in automatically because its filename ends in `.postprocess.bbmodel`.

The example postprocessor:

- subscribes safely from `AssetPostprocessor.OnPreprocessAsset`;
- enables GPU instancing on generated materials;
- adds `BlockbenchImportMarker` to the generated root;
- registers a `Post Import Summary` text sub-asset.

Select the imported model and expand its sub-assets to inspect the result. Rename another model to end in `.postprocess.bbmodel` to run the same example on it.

Use an explicit path, label, naming rule, or project setting in production so unrelated models are not modified. Keep callbacks deterministic: the same source and importer settings should always produce the same output.
