using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Import rules for the building kit in Resources/Village: plain static meshes,
// and a vine-leaf texture that keeps its cut-out shape at a distance.
public class VillageImport : AssetPostprocessor
{
    public override uint GetVersion() => 2;

    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Resources/Village/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = true;     // buildings are merged into a few meshes once they are standing
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/Village/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.anisoLevel = 4;
        if (!assetPath.Contains("VineLeaf")) return;
        importer.alphaIsTransparency = true;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = 0.45f;
    }

    // Builds the landmarks and a few houses in an empty scene and photographs them into Logs/
    // (run with -executeMethod VillageImport.Preview): the quick way to check the builders and the lighting.
    public static void Preview()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        Shader.SetGlobalFloat("_WorldSat", 1f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.6f, 0.66f, 0.76f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.color = new Color(1f, 0.96f, 0.86f);
        sun.transform.rotation = Quaternion.Euler(42f, -150f, 0f);
        QualitySettings.shadowDistance = 120f;

        var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sky.transform.localScale = Vector3.one * 900f;
        var skyMat = new Material(Resources.Load<Shader>("Sky"));
        skyMat.SetColor("_Top", new Color(0.27f, 0.52f, 0.92f));
        skyMat.SetColor("_Bottom", new Color(0.74f, 0.88f, 0.97f));
        sky.GetComponent<Renderer>().sharedMaterial = skyMat;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.transform.localScale = new Vector3(400f, 0.05f, 400f);
        ground.transform.position = new Vector3(0f, -0.05f, 0f);
        var gm = new Material(ToonKit.Mat(new Color(0.43f, 0.68f, 0.33f)));
        gm.SetFloat("_Ground", 1f); gm.SetColor("_Color2", new Color(0.34f, 0.57f, 0.28f)); gm.SetFloat("_Radius", 200f); gm.SetFloat("_Spec", 0f); gm.SetFloat("_Rim", 0f);
        ground.GetComponent<Renderer>().sharedMaterial = gm;
        var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plaza.transform.localScale = new Vector3(18f, 0.05f, 18f);
        plaza.transform.position = new Vector3(0f, -0.03f, 14f);
        var pm = new Material(ToonKit.Mat(Color.white, false, 0f, Resources.Load<Texture2D>("Village/Tex/T_UnevenBrick_BaseColor")));
        pm.SetFloat("_Ground", 1f); pm.SetColor("_Color2", new Color(0.9f, 0.88f, 0.84f)); pm.SetFloat("_Radius", 9f); pm.SetFloat("_Fray", 1.2f); pm.SetFloat("_Tile", 0.25f); pm.SetFloat("_Spec", 0.05f); pm.SetFloat("_Rim", 0f);
        plaza.GetComponent<Renderer>().sharedMaterial = pm;

        GameObject well = Village.Well(null);
        well.transform.position = new Vector3(0f, 0f, 14f);
        Village.Site mill = Village.Windmill(null, 18);
        mill.root.transform.position = new Vector3(-22f, 0f, 0f);
        Village.Site church = Village.Church(null, 21);
        church.root.transform.position = new Vector3(22f, 0f, -4f);
        Village.Site castle = Village.Castle(null, 23);
        castle.root.transform.position = new Vector3(0f, 0f, -22f);
        GameObject a = Village.House(new Village.Plan { w = 6, d = 8, floors = 2, seed = 12, sideDoor = true, balcony = true, dormers = true }, null);
        a.transform.position = new Vector3(-12f, 0f, 14f); a.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        GameObject b = Village.House(new Village.Plan { w = 4, d = 6, floors = 2, seed = 11, balcony = true }, null);
        b.transform.position = new Vector3(12f, 0f, 16f); b.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

        string[] small = { "LampPost", "Torch", "Pillar", "Rubble", "Headstone", "LeanTo", "Banner", "Fence", "Campfire", "Prop_Wagon" };
        for (int i = 0; i < small.Length; i++)
        {
            Transform t = Village.Make(small[i], null);
            if (t != null) t.position = new Vector3(-13.5f + i * 3f, 0f, 26f);
        }
        string[] nature = { "CommonTree_3", "Pine_4", "Bush_Common_Flowers", "Rock_Medium_1", "Grass_Common_Tall", "Flower_3_Single" };
        for (int i = 0; i < nature.Length; i++)
        {
            Transform t = ToonKit.SpawnNature(nature[i], null, 0.3f);
            if (t != null) t.position = new Vector3(-8f + i * 3.2f, 0f, 21f);
        }
        var person = new GameObject("Person");
        person.transform.position = new Vector3(2.5f, 0f, 17f);
        Humans.Build(Looks.Pip, person.transform);
        var imp = new GameObject("Imp");
        imp.transform.position = new Vector3(4.5f, 0f, 17f);
        Humans.Monster("Imp", 1, imp.transform, 1.55f);
        var woman = new GameObject("Woman");
        woman.transform.position = new Vector3(0.7f, 0f, 17f);
        Humans.Build(Looks.Rhea, woman.transform);
        var sapper = new GameObject("Sapper");
        sapper.transform.position = new Vector3(6.3f, 0f, 17f);
        sapper.transform.rotation = Quaternion.Euler(0f, 140f, 0f);
        Transform pug = Humans.Monster("Puglin", 2, sapper.transform, 1.15f);
        if (pug != null) Humans.Strap(pug, "Barrel", "spine_03", 0.5f);
        Gel.Build(null, 1.05f, Gel.Green, Gel.GreenDeep).position = new Vector3(-1.2f, 0f, 18.2f);
        Gel.Build(null, 2f, new Color(0.3f, 0.42f, 0.12f), new Color(0.04f, 0.08f, 0.02f)).position = new Vector3(-3.4f, 0f, 17.4f);
        Gel.Build(null, 0.55f, Gel.Blue, Gel.BlueDeep, 0.9f).position = new Vector3(-0.2f, 0f, 18.6f);

        Snap("look_overview", new Vector3(6f, 20f, 58f), new Vector3(0f, 5f, 0f), 50f);
        Snap("look_well", new Vector3(4.5f, 2.6f, 20f), new Vector3(0f, 1.4f, 14f), 40f);
        Snap("look_windmill", new Vector3(-30f, 6f, 17f), new Vector3(-22f, 8f, 0f), 46f);
        Snap("look_church", new Vector3(36f, 9f, 24f), new Vector3(22f, 6f, 0f), 46f);
        Snap("look_castle", new Vector3(14f, 10f, 10f), new Vector3(0f, 6f, -22f), 50f);
        Snap("look_houses", new Vector3(-1f, 4f, 24f), new Vector3(-12f, 4f, 14f), 46f);
        Snap("look_balcony", new Vector3(4f, 5f, 21f), new Vector3(12f, 3.5f, 16f), 40f);
        Snap("look_small", new Vector3(0f, 3.5f, 39f), new Vector3(0f, 1.5f, 24f), 46f);
        Snap("look_people", new Vector3(3.6f, 1.5f, 21.4f), new Vector3(3.6f, 1.0f, 17f), 40f);
        Snap("look_faces", new Vector3(1.6f, 1.7f, 18.6f), new Vector3(1.6f, 1.6f, 17f), 36f);
        Snap("look_slimes", new Vector3(-1.8f, 1.1f, 21.2f), new Vector3(-1.8f, 0.5f, 18f), 40f);
        Snap("look_sapper", new Vector3(7.6f, 1.2f, 15.2f), new Vector3(6.3f, 0.7f, 17f), 36f);
        Village.Lamps(1f, 21f);
        sun.intensity = 0.3f;
        sun.color = new Color(0.5f, 0.6f, 1f);
        RenderSettings.ambientLight = new Color(0.2f, 0.23f, 0.36f);
        skyMat.SetColor("_Top", new Color(0.03f, 0.04f, 0.14f));
        skyMat.SetColor("_Bottom", new Color(0.1f, 0.13f, 0.32f));
        skyMat.SetColor("_CloudLit", new Color(0.2f, 0.23f, 0.36f));
        skyMat.SetColor("_CloudDark", new Color(0.08f, 0.09f, 0.18f));
        Snap("look_night", new Vector3(6f, 9f, 40f), new Vector3(0f, 4f, 0f), 50f);
        Debug.Log("VILLAGE PREVIEW DONE");
    }

    static void Snap(string name, Vector3 from, Vector3 lookAt, float fov)
    {
        var go = new GameObject("Cam");
        var c = go.AddComponent<Camera>();
        c.fieldOfView = fov;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.62f, 0.78f, 0.95f);
        c.farClipPlane = 900f;
        go.transform.position = from;
        go.transform.LookAt(lookAt);
        var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
        c.targetTexture = rt;
        c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        c.targetTexture = null;
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/" + name + ".png", tex.EncodeToPNG());
        Object.DestroyImmediate(go);
        rt.Release();
    }

    // Writes what every piece of the kit measures, and every animation clip there is, to
    // Logs/ (run with -executeMethod VillageImport.Dump). Sizes are of the model as imported,
    // standing at the origin, unturned: x across, y up, z deep.
    public static void Dump()
    {
        var sb = new StringBuilder();
        foreach (GameObject prefab in Resources.LoadAll<GameObject>("Village"))
        {
            GameObject go = Object.Instantiate(prefab);
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.DestroyImmediate(go); continue; }
            Bounds b = rs[0].bounds;
            string mats = "";
            foreach (Renderer r in rs)
            {
                b.Encapsulate(r.bounds);
                foreach (Material m in r.sharedMaterials) if (m != null && !mats.Contains(m.name)) mats += m.name + " ";
            }
            sb.AppendLine(string.Format("{0,-40} x {1,6:0.00}..{2,6:0.00}  y {3,6:0.00}..{4,6:0.00}  z {5,6:0.00}..{6,6:0.00}  scale {7:0.###}  rot {8}  | {9}",
                prefab.name, b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z, go.transform.localScale.x, go.transform.eulerAngles, mats));
            Object.DestroyImmediate(go);
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/village.txt", sb.ToString());

        var clips = new StringBuilder();
        foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>("Humans/Anim"))
            if (!clip.name.StartsWith("__preview__")) clips.AppendLine(clip.name + "  " + clip.length.ToString("0.00"));
        File.WriteAllText("Logs/clips.txt", clips.ToString());
        Debug.Log("VILLAGE DUMP DONE");
    }
}
