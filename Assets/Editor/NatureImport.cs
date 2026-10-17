using UnityEditor;
using UnityEngine;

// Import rules for the scenery in Resources/Nature: plain static meshes, and
// leaf textures that keep their cut-out shape when seen from far away.
public class NatureImport : AssetPostprocessor
{
    public override uint GetVersion() => 2;

    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Resources/Nature/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = true;     // grass clumps are stamped into one big mesh per meadow
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/Nature/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.alphaIsTransparency = true;
        // Without this, leaves thin out and vanish as the smaller mip levels average their edges away.
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = 0.45f;
        importer.anisoLevel = 4;
    }
}
