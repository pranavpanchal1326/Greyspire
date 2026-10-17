using System.Collections.Generic;
using UnityEngine;

// What a realistic character looks like: which outfit, whose head, which hair.
public class HumanLook
{
    public string outfit;           // model in Resources/Humans/Outfits (clothes only, no head)
    public string body;             // model in Resources/Humans/Body: the skeleton, head, eyes and brows
    public string hair, beard;      // models in Resources/Humans/Hair, or null
    public int hairColour = 1;      // 1 = brown, 2 = fair
    public int colour;              // 0 = the outfit's first colourway, 1 = its alternate
    public string[] hide;           // outfit parts to leave off (a hood, a pauldron)
    public Color tint = Color.white;    // multiplies the whole figure (bandits are grimy, the Gloom King is cold)
    public Color dye;                   // colours the clothes only; alpha is how deep the dye goes (nobles wear it)
    public bool fair;                   // the lighter of the two skins
    public float height = 1f;           // people are not all one size
}

public static class Looks
{
    public static readonly HumanLook Pip = new HumanLook
    {
        outfit = "Male_Ranger", body = "Superhero_Male_FullBody", hair = "Hair_SimpleParted",
        hide = new[] { "Male_Ranger_Head_Hood" },
    };
    public static readonly HumanLook Barbarian = new HumanLook
    {
        height = 1.07f, outfit = "Male_Peasant", body = "Superhero_Male_FullBody", hair = "Hair_Long", beard = "Hair_Beard", hairColour = 2, colour = 1,
    };
    public static readonly HumanLook Rogue = new HumanLook
    {
        fair = true, height = 0.96f, outfit = "Female_Ranger", body = "Superhero_Female_FullBody",
    };
    public static readonly HumanLook Mage = new HumanLook
    {
        fair = true, height = 0.95f, outfit = "Female_Peasant", body = "Superhero_Female_FullBody", hair = "Hair_Long",
    };
    public static readonly HumanLook Smith = new HumanLook
    {
        height = 1.05f, outfit = "Male_Peasant", body = "Superhero_Male_FullBody", hair = "Hair_Buzzed", beard = "Hair_Beard",
    };
    public static readonly HumanLook Archivist = new HumanLook
    {
        fair = true, height = 0.97f, outfit = "Female_Ranger", body = "Superhero_Female_FullBody", colour = 1, hairColour = 2,
    };
    public static readonly HumanLook Guildmaster = new HumanLook
    {
        height = 0.98f, outfit = "Female_Peasant", body = "Superhero_Female_FullBody", hair = "Hair_Long", hairColour = 2, colour = 1,
    };
    public static readonly HumanLook Guard = new HumanLook
    {
        height = 1.04f, outfit = "Male_Ranger", body = "Superhero_Male_FullBody", colour = 1,
    };
    public static readonly HumanLook Villager = new HumanLook
    {
        outfit = "Male_Peasant", body = "Superhero_Male_FullBody", hair = "Hair_SimpleParted", colour = 1,
    };
    public static readonly HumanLook VillagerWoman = new HumanLook
    {
        fair = true, height = 0.95f, outfit = "Female_Peasant", body = "Superhero_Female_FullBody", hair = "Hair_Buns",
    };
    public static readonly HumanLook Farmer = new HumanLook
    {
        fair = true, height = 1.02f, outfit = "Male_Peasant", body = "Superhero_Male_FullBody", hair = "Hair_Buzzed",
    };
    public static readonly HumanLook Elder = new HumanLook
    {
        fair = true, height = 0.95f, outfit = "Male_Peasant", body = "Superhero_Male_FullBody", hair = "Hair_SimpleParted", beard = "Hair_Beard",
        hairColour = 2, colour = 1,
    };
    public static readonly HumanLook Maid = new HumanLook
    {
        fair = true, height = 0.96f, outfit = "Female_Peasant", body = "Superhero_Female_FullBody", hair = "Hair_Long", colour = 1,
    };
    public static readonly HumanLook Traveller = new HumanLook
    {
        height = 0.97f, outfit = "Female_Ranger", body = "Superhero_Female_FullBody", hair = "Hair_Buns", hairColour = 2,
        hide = new[] { "Female_Ranger_Head_Hood" },
    };
    // The noble houses: the same cut of clothes as anyone, in cloth nobody else can afford.
    public static readonly HumanLook LadyVane = new HumanLook
    {
        fair = true, height = 0.98f, outfit = "Female_Peasant", body = "Superhero_Female_FullBody", hair = "Hair_Buns", hairColour = 2, colour = 1,
        dye = new Color(0.2f, 0.3f, 0.78f, 0.55f),
    };
    public static readonly HumanLook LordAshgrave = new HumanLook
    {
        fair = true, height = 1.03f, outfit = "Male_Peasant", body = "Superhero_Male_FullBody", hair = "Hair_SimpleParted", beard = "Hair_Beard", colour = 1,
        dye = new Color(0.62f, 0.1f, 0.16f, 0.55f),
    };
    public static readonly HumanLook Retainer = new HumanLook
    {
        outfit = "Male_Ranger", body = "Superhero_Male_FullBody", hair = "Hair_Buzzed", colour = 1,
        hide = new[] { "Male_Ranger_Head_Hood" }, dye = new Color(0.2f, 0.3f, 0.78f, 0.4f),
    };
    public static readonly HumanLook Warden = new HumanLook
    {
        outfit = "Male_Ranger", body = "Superhero_Male_FullBody", hair = "Hair_Buzzed", beard = "Hair_Beard", colour = 1,
        hide = new[] { "Male_Ranger_Head_Hood" },
    };

    // The Vermilion Brush: the party that went up the tower and did not come down. Found
    // drained on its floors, and seen again in Brushwick once they are brought home.
    public static readonly HumanLook Rhea = new HumanLook
    {
        fair = true, height = 0.97f, outfit = "Female_Ranger", body = "Superhero_Female_FullBody", hair = "Hair_Long", hairColour = 2,
        hide = new[] { "Female_Ranger_Head_Hood" }, dye = new Color(0.2f, 0.3f, 0.78f, 0.5f),
    };
    public static readonly HumanLook Dorn = new HumanLook
    {
        height = 1.08f, outfit = "Male_Peasant", body = "Superhero_Male_FullBody", hair = "Hair_Buzzed", beard = "Hair_Beard",
        dye = new Color(0.5f, 0.38f, 0.2f, 0.35f),
    };
    public static readonly HumanLook Kael = new HumanLook
    {
        fair = true, height = 1.03f, outfit = "Male_Ranger", body = "Superhero_Male_FullBody", hair = "Hair_Long", hairColour = 2,
        hide = new[] { "Male_Ranger_Head_Hood" }, dye = new Color(0.62f, 0.1f, 0.16f, 0.5f),
    };
    public static readonly HumanLook Imel = new HumanLook
    {
        fair = true, height = 0.94f, outfit = "Female_Peasant", body = "Superhero_Female_FullBody", hair = "Hair_Buns", hairColour = 2,
        dye = new Color(0.9f, 0.9f, 0.84f, 0.4f),
    };
    public static readonly HumanLook Oswin = new HumanLook
    {
        height = 1.04f, outfit = "Male_Ranger", body = "Superhero_Male_FullBody", hair = "Hair_Buzzed", beard = "Hair_Beard", hairColour = 2, colour = 1,
        hide = new[] { "Male_Ranger_Head_Hood" }, dye = new Color(0.25f, 0.3f, 0.36f, 0.35f),
    };

    // Bandits: the people who rob the roads of the Wilds. Hooded, armed and none too clean.
    static readonly Color Grime = new Color(0.84f, 0.8f, 0.78f), Crowned = new Color(0.8f, 0.7f, 1f);

    public static readonly HumanLook Bandit = new HumanLook
    {
        outfit = "Male_Ranger", body = "Superhero_Male_FullBody", colour = 1, tint = Grime,
    };
    public static readonly HumanLook BanditWoman = new HumanLook
    {
        fair = true, height = 0.97f, outfit = "Female_Ranger", body = "Superhero_Female_FullBody", colour = 1, tint = Grime,
    };
    public static readonly HumanLook Cutthroat = new HumanLook
    {
        fair = true, height = 1.02f, outfit = "Male_Peasant", body = "Superhero_Male_FullBody", hair = "Hair_Buzzed", colour = 1, tint = Grime,
    };
    public static readonly HumanLook Thrower = new HumanLook
    {
        fair = true, height = 0.96f, outfit = "Female_Ranger", body = "Superhero_Female_FullBody", tint = Grime,
    };
    public static readonly HumanLook BanditChief = new HumanLook
    {
        outfit = "Male_Peasant", body = "Superhero_Male_FullBody", hair = "Hair_Buzzed", beard = "Hair_Beard", colour = 1, tint = Grime,
    };

    // The Gloom King is the one human in the tower: a king the Gloom took, not a robber.
    public static readonly HumanLook GloomKing = new HumanLook
    {
        outfit = "Male_Ranger", body = "Superhero_Male_FullBody", colour = 1, tint = Crowned,
    };
}

// Builds the realistic characters from their parts. A base body provides the
// skeleton and the head; the outfit's clothes are re-bound onto that skeleton;
// hair is pinned to the head; everything gets the toon shader with the right
// texture.
public static class Humans
{
    static readonly Dictionary<Mesh, Mesh> headOnly = new Dictionary<Mesh, Mesh>();

    // How a sword or axe, and a shield, sit in the hand bone's space.
    public static readonly Vector3 Grip = new Vector3(0f, 0.09f, 0.03f), GripAngles = Vector3.zero;
    public static readonly Vector3 ShieldGrip = new Vector3(0f, 0.06f, -0.06f), ShieldAngles = new Vector3(0f, 0f, 90f);
    // A second blade in the left hand (the light-blade family's parrying dagger).
    public static readonly Vector3 OffhandGrip = new Vector3(0f, 0.09f, 0.03f), OffhandAngles = Vector3.zero;

    public static Transform Build(HumanLook look, Transform parent, float scale = 1f)
    {
        var outfitPrefab = Resources.Load<GameObject>("Humans/Outfits/" + look.outfit);
        var donorPrefab = Resources.Load<GameObject>("Humans/Body/" + look.body);
        if (outfitPrefab == null || donorPrefab == null) return null;

        // The base body owns the skeleton (it is the one model guaranteed to have every
        // bone); its mesh is cut down to the head, and the clothes are re-bound onto it.
        GameObject go = Object.Instantiate(donorPrefab);
        go.name = look.outfit;
        go.transform.SetParent(parent, false);
        go.transform.localScale *= scale * look.height;
        // These models are authored facing the other way from the rest of the game.
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * go.transform.localRotation;

        var bones = new Dictionary<string, Transform>();
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            if (!bones.ContainsKey(t.name)) bones[t.name] = t;
        foreach (var body in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (body.name.ToLowerInvariant().Contains("superhero")) body.sharedMesh = HeadOnly(body);

        GameObject outfit = Object.Instantiate(outfitPrefab);
        outfit.transform.SetParent(parent, false);
        outfit.transform.localScale = go.transform.localScale;
        foreach (var src in outfit.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (look.hide != null && System.Array.IndexOf(look.hide, src.name) >= 0) continue;
            var part = new GameObject(src.name);
            part.transform.SetParent(go.transform, false);
            var smr = part.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = src.sharedMesh;
            var map = new Transform[src.bones.Length];
            for (int i = 0; i < map.Length; i++)
                if (src.bones[i] != null) bones.TryGetValue(src.bones[i].name, out map[i]);
            smr.bones = map;
            if (src.rootBone != null && bones.TryGetValue(src.rootBone.name, out Transform root)) smr.rootBone = root;
            smr.localBounds = src.localBounds;
            smr.sharedMaterials = src.sharedMaterials;
        }
        Object.DestroyImmediate(outfit);

        if (bones.TryGetValue("Head", out Transform head))
        {
            Pin(look.hair, go.transform, head);
            Pin(look.beard, go.transform, head);
        }

        Dress(go, look);
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
        return go.transform;
    }

    // Hair is authored at the character's origin, so it lines up with the head in the
    // rest pose; from then on it rides the head bone.
    static void Pin(string model, Transform character, Transform head)
    {
        if (model == null) return;
        var prefab = Resources.Load<GameObject>("Humans/Hair/" + model);
        if (prefab == null) return;
        GameObject go = Object.Instantiate(prefab, character, false);
        go.name = model;
        // The hair files face the opposite way to the bodies, so turn them round first.
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * go.transform.localRotation;
        go.transform.SetParent(head, true);
    }

    // A copy of the body mesh with only the head and neck triangles left in.
    static Mesh HeadOnly(SkinnedMeshRenderer src)
    {
        Mesh full = src.sharedMesh;
        if (headOnly.TryGetValue(full, out Mesh cached) && cached != null) return cached;

        var keep = new HashSet<int>();
        for (int i = 0; i < src.bones.Length; i++)
            if (src.bones[i] != null && (src.bones[i].name == "Head" || src.bones[i].name == "neck_01")) keep.Add(i);

        Mesh mesh = Object.Instantiate(full);
        mesh.name = full.name + "_Head";
        BoneWeight[] weights = mesh.boneWeights;
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            int[] tris = mesh.GetTriangles(sub);
            var kept = new List<int>(tris.Length / 3);
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (keep.Contains(weights[tris[i]].boneIndex0) && keep.Contains(weights[tris[i + 1]].boneIndex0)
                    && keep.Contains(weights[tris[i + 2]].boneIndex0))
                {
                    kept.Add(tris[i]); kept.Add(tris[i + 1]); kept.Add(tris[i + 2]);
                }
            }
            mesh.SetTriangles(kept, sub);
        }
        headOnly[full] = mesh;
        return mesh;
    }

    // Gives every part the toon shader with the texture that belongs to its material.
    static void Dress(GameObject go, HumanLook look)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name : "";
                Texture tex = TextureFor(n, look);
                if (tex == null && mats[i] != null && mats[i].HasProperty("_MainTex")) tex = mats[i].mainTexture;
                // The hair texture is a pale base meant to be tinted to the hair colour.
                Color tint = n.Contains("Hair") ? (look.hairColour == 2 ? new Color(0.92f, 0.76f, 0.45f) : new Color(0.42f, 0.27f, 0.16f)) * look.tint : look.tint;
                mats[i] = tex != null ? ToonKit.Mat(tint, false, 0f, tex) : ToonKit.Mat(new Color(0.6f, 0.5f, 0.4f));
                // Skin and cloth are soft; eyes are wet.
                mats[i].SetFloat("_Spec", n.Contains("Eyes") ? 0.7f : n.Contains("Hair") ? 0.3f : 0.1f);
                if (n.Contains("Superhero")) mats[i].SetFloat("_Skin", 1f);
                if (n.Contains("Hair")) mats[i].SetFloat("_Rim", 0.35f);
                if (n.Contains("Ranger") || n.Contains("Peasant")) mats[i] = ToonKit.Washed(mats[i], look.dye);
            }
            r.sharedMaterials = mats;
        }
    }

    static Texture TextureFor(string material, HumanLook look)
    {
        string path = null;
        if (material.Contains("Ranger")) path = look.colour == 0 ? "Outfits/T_Ranger_BaseColor" : "Outfits/T_Ranger_3_BaseColor";
        else if (material.Contains("Peasant")) path = look.colour == 0 ? "Outfits/T_Peasant_BaseColor" : "Outfits/T_Peasant_2_BaseColor";
        else if (material.Contains("Regular_Male")) path = "Outfits/T_Regular_Male_Dark_BaseColor";
        else if (material.Contains("Regular_Female")) path = "Outfits/T_Regular_Female_Dark_BaseColor";
        else if (material.Contains("Superhero_Male")) path = look.fair ? "Body/T_Superhero_Male_Ligh" : "Body/T_Superhero_Male_Dark";
        else if (material.Contains("Superhero_Female")) path = look.fair ? "Body/T_Superhero_Female_Light_BaseColor" : "Body/T_Superhero_Female_Dark_BaseColor";
        else if (material.Contains("Eyes")) path = "Body/T_Eye_Brown";
        else if (material.Contains("Hair")) path = "Body/T_Hair_" + look.hairColour + "_BaseColor";
        return path != null ? Resources.Load<Texture2D>("Humans/" + path) : null;
    }

    // True if the model came in with a working humanoid rig (so the shared clips can drive it).
    public static bool Animated(Transform model)
    {
        var a = model.GetComponentInChildren<Animator>();
        return a != null && a.avatar != null && a.avatar.isValid && a.avatar.isHuman;
    }

    // A monster from Resources/Humans/Monsters, sized to a target height.
    public static Transform Monster(string model, int colour, Transform parent, float height)
    {
        var prefab = Resources.Load<GameObject>("Humans/Monsters/" + model);
        if (prefab == null) return null;
        GameObject go = Object.Instantiate(prefab);
        go.name = model;
        go.transform.SetParent(parent, false);
        // Same authoring direction as the humans: turn it to face forward.
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * go.transform.localRotation;
        Texture tex = Resources.Load<Texture2D>("Humans/Monsters/T_" + model + "_BaseColor_" + Mathf.Clamp(colour, 1, 3));
        ToonKit.Toonify(go, false, 0f, tex);
        // Their eyes and markings burn with a light of their own.
        Texture glow = Resources.Load<Texture2D>("Humans/Monsters/T_" + model + "_Emissive");
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || glow == null) continue;
                m.SetTexture("_EmitMap", glow);
                m.SetFloat("_EmitMapOn", 1.6f);
            }

        var rends = go.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            foreach (var r in rends) { b.Encapsulate(r.bounds); r.updateWhenOffscreen = true; }
            if (b.size.y > 0.01f) go.transform.localScale *= height / b.size.y;
        }
        return go.transform;
    }

    // Straps a prop to a bone (a keg on a back), a given size across, a little way out behind.
    // Call while the character still stands in its rest pose.
    public static Transform Strap(Transform character, string prop, string bone, float size)
    {
        Transform on = LimbAnimator.FindDeep(character, bone);
        GameObject go = on != null ? Furniture(prop) : null;
        if (go == null) return null;
        Transform body = character.parent != null ? character.parent : character;
        Renderer r = go.GetComponentInChildren<Renderer>();
        float widest = r != null ? Mathf.Max(r.bounds.size.x, r.bounds.size.z) : 1f;
        go.transform.localScale *= size / Mathf.Max(0.01f, widest);
        go.transform.rotation = Quaternion.LookRotation(body.up, -body.forward);     // lying along the spine
        go.transform.position = on.position - body.forward * (size * 0.62f) - body.up * (size * 0.45f);
        go.transform.SetParent(on, true);
        return go.transform;
    }

    // Puts a prop from Resources/Humans/Props into a hand, with a grip offset.
    public static Transform Hold(Transform character, string prop, bool leftHand, Vector3 position, Vector3 euler)
    {
        Transform hand = LimbAnimator.FindDeep(character, leftHand ? "hand_l" : "hand_r");
        GameObject go = hand != null ? Furniture(prop) : null;
        if (go == null) return null;
        // Keep the prop at its authored size whatever scale the bones carry.
        Vector3 size = go.transform.lossyScale * character.lossyScale.x;
        // Some props are modelled lying down and stood up by their own rotation: keep it.
        Quaternion authored = go.transform.localRotation;
        go.transform.SetParent(hand, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler) * authored;
        Vector3 lossy = hand.lossyScale;
        go.transform.localScale = new Vector3(size.x / lossy.x, size.y / lossy.y, size.z / lossy.z);
        return go.transform;
    }

    static readonly Color Cloth = new Color(0.62f, 0.14f, 0.16f);

    // A loose prop from Resources/Humans/Props (a weapon, a bench, a market stall), in toon materials.
    public static GameObject Furniture(string prop, bool world = false)
    {
        var prefab = prop != null ? Resources.Load<GameObject>("Humans/Props/" + prop) : null;
        if (prefab == null) return null;
        GameObject go = Object.Instantiate(prefab);
        go.name = prop;
        // Banners and awnings are dyed: the cloth comes white from the loom.
        var dyed = new HashSet<(Renderer, int)>();
        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in rends)
            for (int i = 0; i < r.sharedMaterials.Length; i++)
                if (r.sharedMaterials[i] != null && r.sharedMaterials[i].name.StartsWith("MI_Banner")) dyed.Add((r, i));
        ToonKit.Toonify(go, world);
        foreach (Renderer r in rends)
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (dyed.Contains((r, i))) mats[i] = ToonKit.Mat(Cloth, world, 0.25f, mats[i].mainTexture);
                // Metal takes a shine; wood and cloth do not.
                mats[i].SetFloat("_Spec", mats[i].mainTexture != null && mats[i].mainTexture.name.Contains("Metal") ? 0.45f : 0.1f);
            }
            r.sharedMaterials = mats;
        }
        return go;
    }

    // Works out where a held prop rests when it is put away: a blade hangs at the left hip,
    // a shield sits flat between the shoulders. Call while the character is standing upright
    // in an animated pose (the skeleton's bind pose is not a pose anyone stands in). The prop
    // is left where it was; the returned pose is local to the bone it will ride.
    // A blade hangs at the hip given by side (-1 left, +1 right); a weapon too long to hang
    // there (a great axe) is slung diagonally across the back instead.
    public static bool RestPose(Transform character, Transform prop, bool blade, out Transform bone, out Vector3 position, out Quaternion rotation, float side = -1f)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        var filter = prop != null ? prop.GetComponentInChildren<MeshFilter>() : null;
        if (filter == null || filter.sharedMesh == null) { bone = null; return false; }
        Vector3 extent = filter.sharedMesh.bounds.size;
        float length = Mathf.Max(filter.transform.TransformVector(new Vector3(extent.x, 0f, 0f)).magnitude,
            filter.transform.TransformVector(new Vector3(0f, extent.y, 0f)).magnitude, filter.transform.TransformVector(new Vector3(0f, 0f, extent.z)).magnitude);
        bool slung = blade && length > 1f;
        bone = LimbAnimator.FindDeep(character, blade && !slung ? "pelvis" : "spine_03");
        if (bone == null) bone = LimbAnimator.FindDeep(character, "spine_02");
        if (bone == null) return false;

        Transform body = character.parent != null ? character.parent : character;
        Vector3 up = body.up, back = -body.forward, right = body.right;
        Vector3 pos0 = prop.localPosition;
        Quaternion rot0 = prop.localRotation;

        // The thinnest side of the mesh lies against the body. The mesh sits to one side of its
        // pivot (the grip): that side is the tip of a blade, and the outer face of a shield.
        Bounds b = filter.sharedMesh.bounds;
        Vector3 size = b.size;
        int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
        int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        Vector3 thinAxis = Vector3.zero, longAxis = Vector3.zero;
        thinAxis[thin] = b.center[thin] >= 0f ? 1f : -1f;
        longAxis[longest] = b.center[longest] >= 0f ? 1f : -1f;

        Transform mesh = filter.transform;
        if (slung)
        {
            // Haft running from the left hip up over the right shoulder, head high behind it.
            Vector3 lie = (up * 0.74f + right * 0.67f).normalized;
            prop.rotation = Quaternion.FromToRotation(mesh.TransformDirection(longAxis), lie) * prop.rotation;
            Vector3 flat = Vector3.ProjectOnPlane(mesh.TransformDirection(thinAxis), lie);
            Vector3 want = Vector3.ProjectOnPlane(back, lie);
            if (flat.sqrMagnitude > 0.0001f) prop.rotation = Quaternion.FromToRotation(flat, want) * prop.rotation;
            // Centred low on the back, so the head rides behind the right shoulder, not the skull.
            prop.position += bone.position + back * 0.17f - up * 0.3f - mesh.TransformPoint(b.center);
        }
        else if (blade)
        {
            // Grip at the belt, blade trailing down behind the leg.
            Vector3 lie = (-up * 0.9f + back * 0.42f).normalized;
            prop.rotation = Quaternion.FromToRotation(mesh.TransformDirection(longAxis), lie) * prop.rotation;
            Vector3 flat = Vector3.ProjectOnPlane(mesh.TransformDirection(thinAxis), lie);
            Vector3 want = Vector3.ProjectOnPlane(-right, lie);
            if (flat.sqrMagnitude > 0.0001f) prop.rotation = Quaternion.FromToRotation(flat, want) * prop.rotation;
            prop.position = bone.position + right * (0.21f * side) + up * 0.04f + back * 0.03f;
        }
        else
        {
            prop.rotation = Quaternion.FromToRotation(mesh.TransformDirection(thinAxis), back) * prop.rotation;
            prop.position += bone.position + back * 0.13f - up * 0.12f - mesh.TransformPoint(b.center);
        }

        position = bone.InverseTransformPoint(prop.position);
        rotation = Quaternion.Inverse(bone.rotation) * prop.rotation;
        prop.localPosition = pos0;
        prop.localRotation = rot0;
        return true;
    }
}
