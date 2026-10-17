using System.Text;
using UnityEditor;
using UnityEngine;

// Import rules for the realistic characters in Resources/Humans.
//  - Anim/      animation libraries: Humanoid, clips renamed to their short names.
//               Height and turning stay in the pose; travel across the ground is left
//               as root motion, which the game never applies, so every clip plays on
//               the spot and the body never drifts away from where the character is.
//  - Hair/, Props/  plain static meshes.
//  - everything else: rigged characters, Humanoid, no clips of their own.
// Humanoid is what lets clips made on one skeleton drive every other character.
public class HumanImport : AssetPostprocessor
{
    public override uint GetVersion() => 3;

    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Resources/Humans/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.isReadable = true;     // the head is cut out of the base body at run time

        if (assetPath.Contains("/Hair/") || assetPath.Contains("/Props/"))
        {
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            return;
        }
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = assetPath.Contains("/Anim/");
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.Contains("/Resources/Humans/Anim/")) return;
        var importer = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation c in clips)
        {
            int bar = c.name.LastIndexOf('|');
            if (bar >= 0) c.name = c.name.Substring(bar + 1);
            c.loopTime = c.name.Contains("_Loop");
            c.lockRootRotation = true;
            c.lockRootHeightY = true;
            c.lockRootPositionXZ = false;
            c.keepOriginalOrientation = true;
            c.keepOriginalPositionY = true;
            c.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
    }

    // Prints what every model under Resources/Humans turned into (run with
    // -executeMethod HumanImport.Dump, or from the menu).
    [MenuItem("Greyspire/Log Human Models And Clips")]
    public static void Dump()
    {
        foreach (GameObject go in Resources.LoadAll<GameObject>("Humans"))
        {
            var sb = new StringBuilder("HUMANDUMP " + go.name);
            var animator = go.GetComponent<Animator>();
            sb.Append(animator == null ? " no-animator" : " avatar=" + (animator.avatar == null ? "none"
                : (animator.avatar.isValid ? "valid" : "INVALID") + (animator.avatar.isHuman ? "/human" : "/generic")));
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                sb.Append("\n   " + r.GetType().Name[0] + " " + r.name + " h=" + r.bounds.min.y.ToString("0.00") + ".." + r.bounds.max.y.ToString("0.00") + " mats=");
                foreach (Material m in r.sharedMaterials)
                    sb.Append(m == null ? "null " : m.name + (m.mainTexture != null ? "(" + m.mainTexture.name + ") " : "(no tex) "));
            }
            Debug.Log(sb.ToString());
        }
        var names = new StringBuilder("HUMANDUMP clips:");
        foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>("Humans/Anim"))
            if (!clip.name.StartsWith("__preview__"))
                names.Append(" " + clip.name + (clip.isHumanMotion ? "" : "[NOT-HUMAN]") + "(" + clip.length.ToString("0.0") + ")");
        Debug.Log(names.ToString());
    }
}
