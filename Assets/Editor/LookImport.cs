using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Import rule for surface detail: any texture in Resources whose name ends in
// _Normal is a normal map. And a dump of every scenery model the kits provide.
public class LookImport : AssetPostprocessor
{
    public override uint GetVersion() => 2;

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/")) return;
        string name = Path.GetFileNameWithoutExtension(assetPath);
        var importer = (TextureImporter)assetImporter;
        if (name.EndsWith("_ORM") || name.EndsWith("_Roughness"))
        {
            importer.sRGBTexture = false;       // numbers, not colours
            return;
        }
        if (!name.EndsWith("_Normal")) return;
        importer.textureType = TextureImporterType.NormalMap;
        importer.anisoLevel = 4;
    }

    // Writes the size and materials of every model in a Resources folder to Logs/<name>.txt
    // (run with -executeMethod LookImport.Dump).
    public static void Dump()
    {
        Write("Nature", "nature");
        Write("Humans/Props", "props");
        Debug.Log("LOOK DUMP DONE");
    }

    static void Write(string folder, string file)
    {
        var sb = new StringBuilder();
        foreach (GameObject prefab in Resources.LoadAll<GameObject>(folder))
        {
            GameObject go = Object.Instantiate(prefab);
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.DestroyImmediate(go); continue; }
            Bounds b = rs[0].bounds;
            string mats = "";
            foreach (Renderer r in rs)
            {
                b.Encapsulate(r.bounds);
                foreach (Material m in r.sharedMaterials)
                    if (m != null && !mats.Contains(m.name)) mats += m.name + (m.mainTexture != null ? "(" + m.mainTexture.name + ") " : " ");
            }
            sb.AppendLine(string.Format("{0,-28} x {1,5:0.00}..{2,5:0.00}  y {3,5:0.00}..{4,5:0.00}  z {5,5:0.00}..{6,5:0.00}  | {7}",
                prefab.name, b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z, mats));
            Object.DestroyImmediate(go);
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/" + file + ".txt", sb.ToString());
    }
}
