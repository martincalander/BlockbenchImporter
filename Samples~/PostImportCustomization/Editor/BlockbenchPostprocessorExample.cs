using System;
using System.Collections.Generic;
using System.IO;
using Essentials.BlockbenchImporter.Samples.PostImportCustomization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Essentials.BlockbenchImporter.Samples.PostImportCustomization.Editor
{
    /// <summary>
    /// Demonstrates opt-in, deterministic customization of generated Blockbench assets.
    /// Rename any model to end in .postprocess.bbmodel to run this example on it.
    /// </summary>
    internal sealed class BlockbenchPostprocessorExample : AssetPostprocessor
    {
        private const string OptInSuffix = ".postprocess";

        private void OnPreprocessAsset()
        {
            if (!(assetImporter is BlockbenchImporter importer) || !IsOptedIn(assetPath))
                return;

            // Importer instances can be reused. Remove first so the callback is registered exactly once.
            importer.OnPostBlockbenchImport -= OnPostBlockbenchImport;
            importer.OnPostBlockbenchImport += OnPostBlockbenchImport;
        }

        private static bool IsOptedIn(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            return fileName.EndsWith(OptInSuffix, StringComparison.OrdinalIgnoreCase);
        }

        private static void OnPostBlockbenchImport(BlockbenchImporter.ImportEventArgs args)
        {
            var importedObjects = new List<Object>();
            args.context.GetObjects(importedObjects);

            int materialCount = 0;
            foreach (Object importedObject in importedObjects)
            {
                if (!(importedObject is Material material))
                    continue;

                material.enableInstancing = true;
                materialCount++;
            }

            BlockbenchImportMarker marker = args.rootObject.GetComponent<BlockbenchImportMarker>();
            if (marker == null)
                marker = args.rootObject.AddComponent<BlockbenchImportMarker>();
            marker.Configure(args.importer.assetPath, materialCount);

            var summary = new TextAsset(
                "Customized by BlockbenchPostprocessorExample\n" +
                "Source: " + args.importer.assetPath + "\n" +
                "Materials: " + materialCount)
            {
                name = "Post Import Summary"
            };
            args.context.AddObjectToAsset("post_import_summary", summary);
        }
    }
}
