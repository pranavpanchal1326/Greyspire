using System.Collections.Generic;
using UnityEngine;

// Shared helpers: toon materials, model spawning and catalogued recorded audio.
public static class ToonKit
{
    static Shader toon;
    static readonly Dictionary<(Color, bool, float, Texture, bool), Material> mats = new Dictionary<(Color, bool, float, Texture, bool), Material>();

    // world = scenery that loses its colour while the arena is under gloom.
    // sway = how much the wind moves it (trees, cacti).
    // cutout = leaves and petals: the shape is cut from the texture, both sides show, no ink line.
    public static Material Mat(Color c, bool world = false, float sway = 0f, Texture tex = null, bool cutout = false)
    {
        var key = (c, world, sway, tex, cutout);
        if (mats.TryGetValue(key, out var m) && m != null) return m;
        if (toon == null) toon = Resources.Load<Shader>("Toon");
        m = new Material(toon != null ? toon : Shader.Find("Standard"));
        m.color = c;
        if (world) m.SetFloat("_World", 1f);
        m.SetFloat("_Sway", sway);
        if (tex != null)
        {
            m.mainTexture = tex;
            // Surface detail, wherever the kit that made the texture also made a normal map for it.
            Texture bump = BumpFor(tex);
            if (bump != null)
            {
                m.SetTexture("_BumpMap", bump);
                m.SetFloat("_Bump", 1f);
            }
            Texture surface = Companion(tex, "_ORM");
            if (surface != null)
            {
                m.SetTexture("_OrmMap", surface);
                m.SetFloat("_Orm", 1f);
            }
            else if ((surface = Companion(tex, "_Roughness")) != null)
            {
                m.SetTexture("_OrmMap", surface);
                m.SetFloat("_Orm", 2f);
            }
        }
        if (cutout)
        {
            m.SetFloat("_Cutoff", 0.45f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_OutlineWidth", 0f);
            m.SetFloat("_Rim", 0f);
            m.SetFloat("_Spec", 0f);
        }
        mats[key] = m;
        return m;
    }

    static readonly Dictionary<(Texture, string), Texture> companions = new Dictionary<(Texture, string), Texture>();
    static readonly string[] MapFolders = { "Village/Tex/", "Humans/Props/", "Nature/", "Humans/Body/", "Humans/Outfits/", "Humans/Monsters/" };

    // The normal map that goes with a colour texture.
    public static Texture BumpFor(Texture tex) => Companion(tex, "_Normal");

    // A map made to go with a colour texture: the same name with the suffix on the end,
    // trying shorter and shorter stems (T_Peasant_2_BaseColor -> T_Peasant_2 -> T_Peasant).
    public static Texture Companion(Texture tex, string suffix)
    {
        if (companions.TryGetValue((tex, suffix), out Texture found)) return found;
        string stem = tex.name;
        int cut = stem.IndexOf("_BaseColor");
        if (cut > 0) stem = stem.Substring(0, cut);
        if (stem == "T_RedBrick") stem = "T_Brick";
        // The body textures are named by skin tone; their maps are not.
        stem = stem.Replace("_Dark", "").Replace("_Light", "").Replace("_Ligh", "");
        while (found == null && stem.Length > 1)
        {
            foreach (string folder in MapFolders)
            {
                found = Resources.Load<Texture2D>(folder + stem + suffix);
                if (found != null) break;
            }
            int bar = stem.LastIndexOf('_');
            if (bar <= 1) break;
            stem = stem.Substring(0, bar);
        }
        companions[(tex, suffix)] = found;
        return found;
    }

    // Swaps whatever materials the FBX imported with for the game's own, in the same colour.
    public static void Toonify(GameObject go, bool world = false, float sway = 0f, Texture tex = null, Color tint = default)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var src = r.sharedMaterials;
            for (int i = 0; i < src.Length; i++)
            {
                Color c = Color.white;
                if (src[i] != null)
                {
                    if (src[i].HasProperty("_Color")) c = src[i].GetColor("_Color");
                    else if (src[i].HasProperty("_BaseColor")) c = src[i].GetColor("_BaseColor");
                }
                c.a = 1f;
                // Textured models carry their colours in the texture: either the one passed
                // in, or the one the model was imported with.
                Texture use = tex;
                if (use == null && src[i] != null && src[i].HasProperty("_MainTex")) use = src[i].mainTexture;
                src[i] = use != null ? Mat(tint == default ? Color.white : tint, world, sway, use) : Mat(c, world, sway);
            }
            r.sharedMaterials = src;
        }
    }

    // Rigged, textured character from Resources/Characters.
    public static Transform SpawnCharacter(string model, string texture, Transform parent, float scale, Color tint = default)
    {
        var prefab = Resources.Load<GameObject>("Characters/" + model);
        if (prefab == null) return null;
        GameObject go = Object.Instantiate(prefab);
        go.name = model;
        go.transform.SetParent(parent, false);
        go.transform.localScale *= scale;
        Toonify(go, false, 0f, Resources.Load<Texture2D>("Characters/" + texture), tint);
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
        return go.transform;
    }

    // Puts one of the loose weapon models into a character's hand slot.
    public static void Arm(Transform character, string weapon, string texture, bool leftHand = false)
    {
        string slot = leftHand ? "handslot.l" : "handslot.r";
        Transform hand = LimbAnimator.FindDeep(character, slot);
        if (hand == null) hand = LimbAnimator.FindDeep(character, slot.Replace('.', '_'));
        var prefab = Resources.Load<GameObject>("Characters/" + weapon);
        if (hand == null || prefab == null) return;
        GameObject go = Object.Instantiate(prefab, hand, false);
        go.name = weapon;
        Toonify(go, false, 0f, Resources.Load<Texture2D>("Characters/" + texture));
    }

    // Textured scenery model from Resources/Env (drained by gloom like the rest of the world).
    public static Transform SpawnEnv(string model, string texture, Transform parent, float sway = 0f)
    {
        var prefab = Resources.Load<GameObject>("Env/" + model);
        if (prefab == null) return null;
        GameObject go = Object.Instantiate(prefab);
        go.name = model;
        go.transform.SetParent(parent, false);
        Toonify(go, true, sway, Resources.Load<Texture2D>("Env/" + texture));
        return go.transform;
    }

    static readonly Dictionary<(Material, Color), Material> washed = new Dictionary<(Material, Color), Material>();

    // The same material under a coat of something: snow on a pine, ash on a burnt trunk.
    // wash.a is how thick the coat lies.
    public static Material Washed(Material m, Color wash)
    {
        if (wash.a <= 0f) return m;
        if (washed.TryGetValue((m, wash), out Material w) && w != null) return w;
        w = new Material(m);
        w.SetColor("_Wash", wash);
        washed[(m, wash)] = w;
        return w;
    }

    // Which texture each material of the nature kit wears, and whether it is foliage.
    static readonly (string material, string texture, bool foliage)[] NatureSkins =
    {
        ("Leaves_NormalTree", "Leaves_NormalTree_C", true), ("Leaves_Pine", "Leaf_Pine_C", true),
        ("Leaves_TwistedTree", "Leaves_TwistedTree_C", true), ("Leaves", "Leaves", true), ("Flowers", "Flowers", true),
        ("Grass", "Grass", true),
        ("Bark_NormalTree", "Bark_NormalTree", false), ("Bark_TwistedTree", "Bark_TwistedTree", false),
        ("Bark_DeadTree", "Bark_DeadTree", false), ("Mushrooms", "Mushrooms", false),
        ("PathRocks", "PathRocks_Diffuse", false), ("Rocks", "Rocks_Diffuse", false),
    };

    // A tree, bush, rock or plant from Resources/Nature. Foliage is cut out of its texture and
    // moves in the wind with the trunk; leafTint recolours just the leaves (autumn, frost, ash).
    public static Transform SpawnNature(string model, Transform parent, float sway = 0f, Color leafTint = default, bool desert = false, Color wash = default)
    {
        var prefab = Resources.Load<GameObject>("Nature/" + model);
        if (prefab == null) return null;
        GameObject go = Object.Instantiate(prefab);
        go.name = model;
        go.transform.SetParent(parent, false);
        Color leaves = leafTint == default ? Color.white : leafTint;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var src = r.sharedMaterials;
            for (int i = 0; i < src.Length; i++)
            {
                string name = src[i] != null ? src[i].name : "";
                string texture = null;
                bool foliage = false;
                foreach (var skin in NatureSkins)
                {
                    if (!name.StartsWith(skin.material)) continue;
                    texture = skin.texture;
                    foliage = skin.foliage;
                    break;
                }
                if (desert && texture == "Rocks_Diffuse") texture = "Rocks_Desert_Diffuse";
                Texture tex = texture != null ? Resources.Load<Texture2D>("Nature/" + texture) : null;
                if (tex == null && src[i] != null && src[i].HasProperty("_MainTex")) tex = src[i].mainTexture;
                src[i] = Mat(foliage ? leaves : Color.white, true, sway, tex, foliage);
                // Bark and stone are matt.
                if (!foliage) src[i].SetFloat("_Spec", 0.05f);
                src[i].SetFloat("_NearFade", 1f);
                src[i] = Washed(src[i], wash);
            }
            r.sharedMaterials = src;
        }
        return go.transform;
    }

    public static Transform Spawn(string model, Transform parent, float scale = 1f, bool world = false, float sway = 0f)
    {
        var prefab = Resources.Load<GameObject>("Models/" + model);
        GameObject go;
        if (prefab != null)
        {
            go = Object.Instantiate(prefab);
        }
        else
        {
            Debug.LogWarning("Model not found: Resources/Models/" + model);
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        go.name = model;
        go.transform.SetParent(parent, false);
        go.transform.localScale *= scale;
        Toonify(go, world, sway);
        if (model == "Crystal")
        {
            // Cut glass: a hard highlight, the sky in its edges, and a light of its own.
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = GeneratedResources.Own(new Material(mats[i]), go.transform);
                    mats[i].SetFloat("_Spec", 0.85f);
                    mats[i].SetFloat("_Rim", 0.9f);
                    Color c = mats[i].color;
                    mats[i].SetColor("_Emit", new Color(c.r, c.g, c.b, world ? 0.12f : 0.4f));
                }
                r.sharedMaterials = mats;
            }
        }
        return go.transform;
    }

    public static GameObject Primitive(PrimitiveType type, Color c, Transform parent, bool world = false)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = Mat(c, world);
        go.transform.SetParent(parent, false);
        return go;
    }

    // ------------------------------------------------------------ audio

    public static void Sfx(string id, float volume = 1f, float pitch = 1f, Vector3? position = null)
    {
        if (!position.HasValue && !GameAudio.Definition(id).ui && Game.I != null && Game.I.Player != null)
            position = Game.I.Player.transform.position + Vector3.up;
        GameAudio.Play(id, volume, pitch, position);
    }
}
