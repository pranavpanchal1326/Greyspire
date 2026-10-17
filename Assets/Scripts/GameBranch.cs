using UnityEngine;

// What a rescue choice does at once, and what it changes later (package N01). Every rescue
// asks the freed Painter to GO HOME or to say WHAT THEY SAW:
//   Go home   - now: they press their last draught on the hero (two hearts back) and leave at
//               once. Later (Rhea): Lady Vane, her niece home safe, sends for the hero and
//               pays a thanks once, through a durable receipt.
//   What did you see - now: the talking carries, and a small pack of the Gloom comes to see
//               who is talking. Later (Rhea): the Gatekeeper fight opens with her warning and
//               the pillars marked (package C03), and Lady Vane is cool with the hero.
// The interrupted-rescue path that finishes a rescue in town plays the later effects only.
public partial class Game
{
    public const int DraughtHearts = 2, VaneThanks = 30;
    public static readonly Enemy.Kind[] AmbushPack = { Enemy.Kind.Minion, Enemy.Kind.Minion, Enemy.Kind.Spitter };
    const string VaneThanksKey = "consequence:rescue:rhea:vane-thanks-paid";

    public static bool VaneThanksPaid => JourneyStore.GetInt(VaneThanksKey) == 1;
    public static bool VaneThanksDue => Save.Consequence("rescue:rhea:safe-return") && Save.Consequence("rescue:rhea:home") && !VaneThanksPaid;

    // Played in the tower as the freed Painter leaves.
    void RescueAftermath(int who, int pick, Vector3 at)
    {
        if (pick == 0)
        {
            Player.Heal(DraughtHearts);
            heartPunch = 1f;
            Fx.Ring(Player.transform.position, UI.Blue, 0.4f, 4f, 0.5f);
            ToonKit.Sfx("heart");
            Toast(Story.Rescues[who].name.Split(' ')[0] + " pressed her last draught on you: +" + DraughtHearts + " hearts", UI.Blue);
            return;
        }
        // The Gloom heard them talking.
        // They come out where the hero can see them coming: ahead, spread across the view.
        Vector3 hero = Player.transform.position;
        Vector3 ahead = Cam != null ? Vector3.ProjectOnPlane(Cam.transform.forward, Vector3.up).normalized : Player.transform.forward;
        if (ahead.sqrMagnitude < 0.01f) ahead = Player.transform.forward;
        for (int i = 0; i < AmbushPack.Length; i++)
        {
            Vector3 p = hero + Quaternion.Euler(0f, -40f + i * 40f, 0f) * ahead * (AmbushPack[i] == Enemy.Kind.Spitter ? 10f : 7f);
            if (!WorldSupport.TryStand(p, 0.5f, 1.8f, out Vector3 spot)) spot = p;
            Enemy e = Enemy.Create(AmbushPack[i], spot, level);
            e.aggro = 30f;
            e.name = "Ambush " + AmbushPack[i];
        }
        Banner("THE GLOOM HEARD YOU", 2f, "Something came to see who was talking");
        ToonKit.Sfx("roar", 0.8f, 1.2f);
        Cam.Shake(0.3f);
    }

    // Lady Vane's thanks for a niece sent straight home. Paid once, ever, into the bank.
    public bool PayVaneThanks() => JourneyStore.Transaction(() =>
    {
        if (!VaneThanksDue) return false;
        Save.Sparks = checked(Save.Sparks + VaneThanks);
        JourneyStore.SetInt(VaneThanksKey, 1);
        return true;
    });

    // Talking to Lady Vane: what she says depends on what the hero asked of her niece.
    void VaneGreeting(System.Action then)
    {
        if (VaneThanksDue)
        {
            Say(Story.VaneThanks(), () =>
            {
                if (PayVaneThanks()) { Toast("+" + VaneThanks + " SPARKS from House Vane", UI.Gold); ToonKit.Sfx("buy"); }
                then();
            });
            return;
        }
        if (Save.Consequence("rescue:rhea:guardian-intel") && JourneyStore.GetInt("consequence:rescue:rhea:vane-cool-heard") == 0)
        {
            Say(Story.VaneCool(), () =>
            {
                JourneyStore.Transaction(() => JourneyStore.SetInt("consequence:rescue:rhea:vane-cool-heard", 1));
                then();
            });
            return;
        }
        then();
    }
}
