using System;
using UnityEngine;

// A playable character: what they look like, what they carry, their three combo
// clips, and how they bend the stats.
public class HeroDef
{
    public string id, name, model, texture, weapon, blurb;
    public HumanLook look;      // set for the realistic characters built by Humans.Build
    public string shield;
    public string offhand;      // a second blade carried in the left hand
    public WeaponFamily family = WeaponFamily.SwordShield;   // spacing, tempo, guard and feel of the combo
    public float[] comboSpeed;  // humans: playback speed per combo hit (clips carry long recoveries)
    // Seconds into each combo clip at which the weapon is moving fastest: the frame the cut
    // visibly lands. Measured by WeaponShowcase (Reports/weapon-clip-probe.csv); the hit is
    // timed to it, so damage never registers before the blade arrives.
    public float[] comboContact;
    // Degrees the wrist turns the weapon about its own length for each blow, so the edge meets
    // the target first rather than the flat (0 for a thrust). Measured by WeaponShowcase around
    // each contact: Reports/weapon-edge-alignment.csv.
    public float[] comboTwist;
    public string[] hide;       // cartoon models only: parts switched off (unused weapons)
    public string[] combo;      // animation clip for each of the three combo hits
    public int cost;            // Sparks to unlock (0 = owned from the start)
    public Action<PlayerStats> tune;
}

public static class Heroes
{
    static readonly string[] SwordCombo = { "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C" };
    static readonly float[] SwordContact = { 0.338f, 0.267f, 0.658f };
    // The kit's clips carry a sword flat-first; a quarter turn of the wrist puts its edge into every cut.
    static readonly float[] SwordTwist = { 52f, 90f, 90f };

    public static readonly HeroDef[] All =
    {
        new HeroDef
        {
            id = "pip", name = "Pip of Brushwick", look = Looks.Pip, model = "Male_Ranger",
            weapon = "Sword_Bronze", shield = "Shield_Wooden",
            blurb = "Sword and shield.\nHeld guard, late parry, bash.",
            combo = SwordCombo, comboSpeed = new[] { 1.25f, 1.4f, 1.7f }, comboContact = SwordContact, comboTwist = SwordTwist,
        },
        new HeroDef
        {
            id = "bram", name = "Bram the Barbarian", look = Looks.Barbarian, model = "Male_Peasant", weapon = "Greataxe_Iron",
            blurb = "Great axe: wide, slow arcs.\nThird blow breaks guards.\nTight parry, no bash. 6 hearts.",
            family = WeaponFamily.GreatAxe,
            // A wide diagonal sweep, a downward chop, then the full spin the finisher's hit describes.
            combo = new[] { "Sword_Attack", "TreeChopping_Loop", "Sword_Regular_C" }, comboSpeed = new[] { 1.1f, 1.2f, 1.5f },
            comboContact = new[] { 0.4f, 0.158f, 0.658f }, comboTwist = new[] { 85f, 91f, 90f },
            cost = 40,
            // The axe's tempo and reach live in its family; the hero brings weight and a stouter body.
            tune = s => { s.damage *= 1.25f; s.maxHearts += 1; },
        },
        new HeroDef
        {
            id = "nix", name = "Nix the Rogue", look = Looks.Rogue, model = "Female_Ranger", weapon = "Seax_Steel", offhand = "Dagger_Steel",
            blurb = "Light blade: quick cuts.\nFlank strikes find weak points.\nNo held guard. 4 hearts.",
            family = WeaponFamily.LightBlade,
            // The finisher is the same lunge the dash stab uses: a thrust, not a spin.
            // A quick backhand hook, a forehand cut, then the lunge.
            combo = new[] { "Melee_Hook", "Sword_Regular_B", "1H_Melee_Attack_Stab" }, comboSpeed = new[] { 1.5f, 1.65f, 2f },
            comboContact = new[] { 0.258f, 0.267f, 0.333f }, comboTwist = new[] { -43f, 87f, 0f },
            cost = 80,
            tune = s =>
            {
                s.damage *= 0.85f; s.crit += 0.2f; s.moveSpeed += 0.15f;
                s.dashCooldown *= 0.65f; s.maxHearts -= 1;
            },
        },
        new HeroDef
        {
            id = "sol", name = "Sol the Mage", look = Looks.Mage, model = "Female_Peasant",
            blurb = "Bare hands and old magic.\nFinishers send a shockwave.\nColour Burst charges twice as fast. 4 hearts.",
            family = WeaponFamily.BareHands,
            combo = new[] { "Punch_Jab", "Punch_Cross", "Spell_Simple_Shoot" }, comboSpeed = new[] { 1.6f, 1.6f, 1.2f },
            cost = 120,
            tune = s => { s.damage *= 0.9f; s.burstGain *= 2f; s.shockFinisher = true; s.maxHearts -= 1; },
        },
        new HeroDef
        {
            id = "cedric", name = "Cedric the Warden", look = Looks.Warden, model = "Male_Ranger",
            weapon = "Sword_Bronze", shield = "Shield_Wooden",
            blurb = "Sword and shield.\nCaptain of the gate guard.",
            combo = SwordCombo, comboSpeed = new[] { 1.25f, 1.4f, 1.7f }, comboContact = SwordContact, comboTwist = SwordTwist,
        },
    };

    public static bool Owned(int index)
    {
        return index >= 0 && index < All.Length && (All[index].cost == 0 || JourneyStore.GetInt(Save.OwnedKey("hero", All[index].id)) == 1);
    }

    public static bool Buy(int index)
    {
        if (index < 0 || index >= All.Length) return false;
        return JourneyStore.Transaction(() =>
        {
            if (Owned(index) || Save.Sparks < All[index].cost) return false;
            Save.Sparks -= All[index].cost;
            JourneyStore.SetInt(Save.OwnedKey("hero", All[index].id), 1);
            return true;
        });
    }

    // The hero the next run will use (always one the player owns).
    public static int Selected
    {
        get
        {
            int i = Array.FindIndex(All, h => h.id == JourneyStore.GetString("heroSelected", "pip"));
            return Owned(i) ? i : Mathf.Max(0, Array.FindIndex(All, h => h.id == "pip"));
        }
        set { if (value >= 0 && value < All.Length && Owned(value)) JourneyStore.SetString("heroSelected", All[value].id); }
    }
}
