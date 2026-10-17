using System.Text;
using UnityEditor;
using UnityEngine;

// Import rules for the rigged characters in Resources/Characters: they come
// in as Legacy animation so the game can cross-fade clips by name from code,
// with no Animator Controller asset to maintain.
public class CharacterImport : AssetPostprocessor
{
    public override uint GetVersion() => 2;

    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Resources/Characters/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Legacy;
        importer.generateAnimations = ModelImporterGenerateAnimations.GenerateAnimations;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
    }

    // Prints every character's parts and animation clip names (run from the menu
    // or with -executeMethod CharacterImport.Dump).
    [MenuItem("Greyspire/Log Character Parts And Clips")]
    public static void Dump()
    {
        foreach (GameObject go in Resources.LoadAll<GameObject>("Characters"))
        {
            var sb = new StringBuilder("CHARDUMP " + go.name + " scale=" + go.transform.localScale + "\n  parts:");
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                sb.Append(" [" + r.name + (r.gameObject.activeSelf ? "" : " off") + " <" + r.transform.parent.name + " " + r.GetType().Name[0]
                    + " tex=" + (r.sharedMaterial != null && r.sharedMaterial.mainTexture != null) + " h=" + r.bounds.max.y.ToString("0.00") + "]");
            sb.Append("\n  clips:");
            var anim = go.GetComponent<Animation>();
            if (anim != null)
                foreach (AnimationState s in anim) sb.Append(" " + s.name + "(" + s.length.ToString("0.00") + ")");
            Debug.Log(sb.ToString());
        }
    }
}
