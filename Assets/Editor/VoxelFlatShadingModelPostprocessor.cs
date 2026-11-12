using System;
using UnityEditor;

/// <summary>
/// Keeps rigid voxel FBX files from being welded or smoothed during import.
/// The source FBX already contains one isolated vertex set per polygon.
/// </summary>
internal sealed class VoxelFlatShadingModelPostprocessor : AssetPostprocessor
{
    private const string KingKongVoxelFamilyFolder =
        "Assets/Art/Models/KingKong";

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(KingKongVoxelFamilyFolder, StringComparison.Ordinal) ||
            !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var importer = (ModelImporter)assetImporter;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.weldVertices = false;
        importer.optimizeMeshVertices = false;
        importer.optimizeMeshPolygons = false;
        importer.keepQuads = false;
        importer.generateSecondaryUV = false;
    }
}
