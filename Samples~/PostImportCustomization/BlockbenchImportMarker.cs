using UnityEngine;

namespace MartinCalander.BlockbenchImporter.Samples.PostImportCustomization
{
    /// <summary>Example runtime data added to an imported Blockbench root by the sample postprocessor.</summary>
    [DisallowMultipleComponent]
    public sealed class BlockbenchImportMarker : MonoBehaviour
    {
        [SerializeField] private string sourceAsset;
        [SerializeField] private int importedMaterialCount;

        public string SourceAsset => sourceAsset;
        public int ImportedMaterialCount => importedMaterialCount;

        public void Configure(string importedSourceAsset, int materialCount)
        {
            sourceAsset = importedSourceAsset;
            importedMaterialCount = materialCount;
        }
    }
}
