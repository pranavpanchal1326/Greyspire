using UnityEngine;

public struct Line
{
    public string who, text, shot;
    public Color color;

    // shot names a camera framing for this line: hero, lumi, heart, wide, boss, sky, low,
    // or them (whoever the hero is talking to). Null keeps the previous line's framing.
    public Line(string who, Color color, string text, string shot = null)
    {
        this.who = who; this.color = color; this.text = text; this.shot = shot;
    }
}

public class Echo
{
    public string title, text;
    public Echo(string title, string text) { this.title = title; this.text = text; }
}

// One of the Vermilion Brush: a Painter who went up the tower and was drained.
public class Lost
{
    public string id, name, farewell;
    public string[] sightings;
    public HumanLook look;
    public int floor;           // where they are found
    public Line[] rescue;       // said on the floor, as the colour comes back
    public Line[] town;         // said in Brushwick, once they are home
}

// All of the game's writing lives here so it can be edited in one place.
//
// The story: a grey tower tore up through Chroma Vale and drained its colour.
// "Painters", ranked E to S, climb it. Pip of Brushwick is the weakest ever
// registered: the bag-carrier of the Vermilion Brush, the party that went up
// three days ago and did not come down. He goes in after them and loses to a
// single slime, until Lumi, the last spark of colour, chooses him: he becomes
// the only Painter who can level up. Floor by floor he finds the party drained
// to grey stone and paints them back, breaks the Guardians, opens the Colour
// Hearts and learns what they remember. At the top waits the Gloom King, who is
// Aurel, the First Painter and Pip's lost master. He drained the world after he
// could not paint his daughter Mira back. Lumi is Mira's laugh, the one colour
// he could not swallow. Pip has to decide how it ends.
public static class Story
{
    static Story()
    {
        foreach (Lost rescue in Rescues)
        {
            int index = System.Array.IndexOf(Save.LegacyRescues, rescue.id);
            if (index < 0) continue; // New content supplies its own writing.
            rescue.farewell ??= Farewells[index]; rescue.sightings ??= Sightings[index];
        }
    }
    public const string Title = "#TITLE";   // a Line with this speaker is drawn as a chapter card
    public const string SystemName = "System";

    static readonly Color LumiC = new Color(1f, 0.92f, 0.45f);
    static readonly Color PipC = new Color(0.4f, 0.75f, 1f);
    static readonly Color KingC = new Color(0.8f, 0.4f, 0.95f);
    static readonly Color AurelC = new Color(1f, 0.7f, 0.4f);
    static readonly Color NarrC = new Color(0.92f, 0.9f, 0.82f);
    static readonly Color TownC = new Color(1f, 0.75f, 0.5f);
    static readonly Color NobleC = new Color(0.82f, 0.7f, 1f);
    static readonly Color PartyC = new Color(1f, 0.55f, 0.45f);
    public static readonly Color SystemC = new Color(0.5f, 0.78f, 0.94f);

    static Line N(string t, string shot = null) => new Line("", NarrC, t, shot);
    static Line Lumi(string t, string shot = "lumi") => new Line("Lumi", LumiC, t, shot);
    static Line Pip(string t, string shot = "hero") => new Line("Pip", PipC, t, shot);
    static Line King(string t, string shot = "sky") => new Line("The Gloom King", KingC, t, shot);
    static Line Sys(string t) => new Line(SystemName, SystemC, t, "hero");
    static Line Them(string who, string t) => new Line(who, PartyC, t, "them");
    static Line Chapter(string number, string name) => new Line(Title, NarrC, number + "|" + name, "wide");

    // ------------------------------------------------------------ the opening

    // A new journey, told over the square of Brushwick before the hero takes a step.
    public static readonly Line[] Prologue =
    {
        Chapter("PROLOGUE", "The Weakest Painter"),
        N("Ten years ago a tower of grey stone tore up through the heart of Chroma Vale. Where its shadow fell, colour drained out of the world and did not come back.", "wide"),
        N("The few who could fight it were called Painters, ranked E to S by how much colour they could carry. They climbed. Most of them did not come down."),
        N("Pip of Brushwick was the weakest Painter the Guild ever put on its books. E-Rank. Bottom of the list. The one the Vermilion Brush took along to carry the bags.", "hero"),
        N("Three days ago the Vermilion Brush went up without him. \"Stay with the packs, bag boy,\" Kael said. \"The tower is no place for an E.\""),
        N("They have not come back. This morning the Guild struck their names off the board.", "wide"),
        Pip("...Somebody has to go and look."),
        Pip("Master Aurel used to say grey is only what's left when you stop looking. So I'll look."),
        N("One practice sword. One wooden shield. Thirty floors.", "sky"),
    };

    // Played mid-fight on the very first floor, when Pip is losing to a single slime.
    public static readonly Line[] Awakening =
    {
        Pip("It won't go down. I can't even beat the weakest monster in the tower...", "low"),
        Pip("...No. I'm not running. Not this time."),
        Sys("[ Condition met: refused to run. ]"),
        Sys("[ A spark has chosen you. You are now the only Painter who can grow. ]"),
        Lumi("That's me talking, by the way. I'm Lumi, the last spark the Gloom King couldn't swallow. And I pick you."),
        Sys("[ Defeat monsters to gain experience. Every level brings stat points: spend them in your Status. ]"),
        Lumi("Thirty floors. He's at the top. We get stronger on every one. Now hit it like you mean it!"),
    };

    public static readonly Line[] PetJoin =
    {
        N("One slime had not attacked. It was small, and blue, and it had been following Pip since Floor One.", "hero"),
        Lumi("It's not gloom-stained. Look, it's wagging. Do slimes wag?"),
        Pip("You can come. But you're carrying your own bags."),
        Sys("[ Companion acquired: Mop the slime. Mop fights beside you. ]"),
    };

    // ------------------------------------------------------------ the climb, floor by floor

    // What is said the first time the hero sets foot on a floor. Null for a floor with nothing to say.
    public static Line[] FloorIntro(int floor)
    {
        switch (floor)
        {
            case 1:
                return new[]
                {
                    Pip("Floor One. One slime. Everybody says even a child can beat a slime."),
                    Pip("...Master Aurel, if you're still up there somewhere, watch me. Just this once."),
                };
            case 2:
                return new[]
                {
                    Lumi("Right. Lesson one. See the grey? That's the Gloom. It sits on a floor until somebody beats what's holding it."),
                    Lumi("Every monster you put down gives a little colour back. Clear the floor and it all comes home. Watch the grass."),
                    Sys("[ Stat points are waiting. Press TAB to open your Status. ]"),
                    Pip("The spark talks. And the air talks. This morning I carried bags."),
                };
            case 3:
                return new[]
                {
                    Sys("[ New monster detected: Puglin. It throws from a distance. ]"),
                    Lumi("They throw things! Dash through one at the very last instant and everything goes slow. It's GREAT."),
                };
            case 4:
                return new[]
                {
                    N("A little way off the path stood a figure with her guard raised, the colour of old stone.", "wide"),
                    Pip("That's Rhea. Rhea Vane. She isn't hurt. She isn't... anything."),
                    Lumi("Drained. He doesn't kill Painters, Pip. He keeps them. Clear the floor, and you can paint her back."),
                };
            case 5:
                return new[]
                {
                    Sys("[ Warning. Floor Guardian detected: Grol the Gatekeeper. ]"),
                    Sys("[ The stairs will not open while it lives. ]"),
                    Lumi("The tower grows one of those every five floors. Big, slow, hits like a falling house. When the ground goes red, be somewhere else."),
                    Pip("Bottom of the list against the keeper of the gate. Fine."),
                };
            case 7:
                return new[]
                {
                    King("...Who is making noise in my tower?"),
                    Pip("Did you hear..."),
                    Lumi("Keep walking. Don't answer him. Not yet.", "lumi"),
                };
            case 9:
                return new[]
                {
                    N("Another one in stone, a big man, braced behind a shield that was no longer there.", "wide"),
                    Pip("Dorn. I carried his shield for two years. He said I'd grow into it."),
                };
            case 10:
                return new[]
                {
                    Sys("[ Floor Guardian detected: The Heartwarden. Behind it: a Colour Heart. ]"),
                    Lumi("Everything this land used to be is locked inside that Heart. Ten floors of it. Break the warden, Pip."),
                };
            case 12:
                return new[]
                {
                    Sys("[ Re-evaluating. This Painter no longer fits the rank given at registration. ]"),
                    Pip("The Guild's crystal said I would be E-Rank for life."),
                    Lumi("Crystals measure what you ARE. I picked you for what you DO."),
                };
            case 14:
                return new[]
                {
                    N("Fourteen floors up, alone, a man in Ashgrave red stood frozen in the middle of a swing. He had got further than any of them.", "wide"),
                    Pip("Kael."),
                    Lumi("The one who called you bag boy?"),
                    Pip("The one who's coming home."),
                };
            case 15:
                return new[]
                {
                    Sys("[ Floor Guardian detected: Ash-Maw. ]"),
                    Lumi("It's hot. Why is it hot? I don't like the hot one."),
                };
            case 17:
                return new[]
                {
                    King("That footwork. Weight on the back foot before the cut. I taught that to one boy, once."),
                    Pip("...Master?"),
                    King("Go home, apprentice. There is nothing up here you want to find."),
                };
            case 19:
                return new[]
                {
                    N("A woman in grey, one hand out, as if she had been reaching for someone when it took her.", "wide"),
                    Pip("Sister Imel. She used to patch me up after the others had finished \"training\" me."),
                };
            case 20:
                return new[]
                {
                    Sys("[ Floor Guardian detected: The Second Heartwarden. ]"),
                    Lumi("The second Heart. Pip... when it opens you're going to see something. I think I already know what."),
                };
            case 24:
                return new[]
                {
                    N("The captain of the Brushwick watch stood facing the stairs with his sword up, the way he had stood at the gate for thirty years.", "wide"),
                    Pip("Captain Oswin. You nearly made it."),
                };
            case 25:
                return new[]
                {
                    Sys("[ Floor Guardian detected: The King's Shadow. ]"),
                    Lumi("That's him. What's left of him when he stops remembering her."),
                    Pip("Then it isn't him."),
                };
            case 27:
                return new[]
                {
                    Lumi("Pip. If you paint him back... I'm the paint. You know that, don't you?"),
                    Pip("We're not there yet."),
                    Lumi("That's not a no."),
                };
            case 29:
                return new[]
                {
                    Sys("[ One floor remains. ]"),
                    Pip("Bottom of the list. Top of the tower."),
                    Lumi("Whatever you choose up there: I picked right. I want you to know I picked right."),
                };
        }
        return null;
    }

    // What is said when a floor falls for the first time. Null for most.
    public static Line[] FloorClear(int floor)
    {
        switch (floor)
        {
            case 1:
                return new[]
                {
                    Sys("[ Floor 1 cleared. The stairs are open. ]"),
                    Lumi("See? COLOUR. That's one. The stairs are north. The way home is behind you, whenever you need it."),
                };
            case 5:
                return new[]
                {
                    Sys("[ Floor Guardian defeated: Grol the Gatekeeper. ]"),
                    N("The bars of his gate lay twisted in the dust. Behind them, the stairs breathed cold air down from floor six.", "wide"),
                    Lumi("It's GONE. The big ugly gate thing is gone! Brushwick is going to hear about this, Pip. I'll make sure of it."),
                    Pip("Bring one of those bars home. Nobody at the tavern is going to believe the size of it."),
                    Sys("[ A Guardian's Boon may be claimed. Grol's own is among them. It is yours for good. ]"),
                };
            case 15:
                return new[]
                {
                    Lumi("You beat the hot one! I take it back. I like the hot one. I like it best dead."),
                };
            case 25:
                return new[]
                {
                    King("You have cut down my shadow. Do you know what a man is without one, Pip? All light. It is unbearable."),
                };
        }
        return null;
    }

    // ------------------------------------------------------------ the Vermilion Brush

    static Line[] Taught(params Line[] lines)
    {
        var all = new Line[lines.Length + 1];
        lines.CopyTo(all, 0);
        all[lines.Length] = Sys("[ A Painter has been brought back. +2 stat points. ]");
        return all;
    }

    // IDs own saved identity; order and names are presentation.
    public static readonly Lost[] Rescues =
    {
        new Lost
        {
            id = "rhea", name = "Rhea Vane", look = Looks.Rhea, floor = 4,
            rescue = Taught(
                Them("Rhea Vane", "...Pip? The bag boy? What are you... why are you GLOWING?"),
                Pip("It's a long story. Can you walk?"),
                Them("Rhea Vane", "Kael pushed on without us. He said the weak ones were slowing him down. Dorn is further up. Imel too."),
                Them("Rhea Vane", "I'm a Vane. We pay what we owe. Turn your wrist over when you cut: like THIS. You are wasting half the swing.")),
            town = new[]
            {
                Them("Rhea Vane", "My aunt pretends she is not relieved. She has had my room dusted three times since I walked in."),
                Them("Rhea Vane", "When you go back up: wrist over on the cut. I will know if you forget."),
            },
        },
        new Lost
        {
            id = "dorn", name = "Dorn Half-Wall", look = Looks.Dorn, floor = 9,
            rescue = Taught(
                Them("Dorn Half-Wall", "Hrrgh. Feels like I slept in a quarry."),
                Them("Dorn Half-Wall", "Pip? You came up NINE floors? With that toothpick?"),
                Them("Dorn Half-Wall", "Two years you carried my shield, lad. Looks like it's my turn to carry something for you. I'll hold the gate at home.")),
            town = new[]
            {
                Them("Dorn Half-Wall", "Brought back from the dead by the bag boy. The lads at the tavern don't believe it. I make them buy the round until they do."),
            },
        },
        new Lost
        {
            id = "kael", name = "Kael Ashgrave", look = Looks.Kael, floor = 14,
            rescue = Taught(
                Them("Kael Ashgrave", "...You."),
                Them("Kael Ashgrave", "Don't look at me like that, bag boy."),
                Pip("Like what?"),
                Them("Kael Ashgrave", "Like you're not surprised. I left them behind because they were slowing me down. Then I was alone, and it turned out so was I."),
                Them("Kael Ashgrave", "Fourteen floors. Tell my cousin I lost. ...No. I'll tell him myself. Go on up. You're the Painter here.")),
            town = new[]
            {
                Them("Kael Ashgrave", "I told my cousin. He laughed for a full minute. Then he could not speak for another."),
                Them("Kael Ashgrave", "...Thank you. I am not going to say it twice."),
            },
        },
        new Lost
        {
            id = "imel", name = "Sister Imel", look = Looks.Imel, floor = 19,
            rescue = Taught(
                Them("Sister Imel", "Oh. Oh, colours. I had forgotten they hurt a little, coming back."),
                Them("Sister Imel", "He doesn't hate us, Pip. I could hear him, all the time I was standing here. He keeps saying a name. Mira."),
                Them("Sister Imel", "Whatever you find at the top, be kind to it.")),
            town = new[]
            {
                Them("Sister Imel", "Sit. Let me look at that arm. No? Then at least eat something before you climb."),
            },
        },
        new Lost
        {
            id = "oswin", name = "Captain Oswin", look = Looks.Oswin, floor = 24,
            rescue = Taught(
                Them("Captain Oswin", "Report. ...No. You report. I can't feel my legs."),
                Them("Captain Oswin", "I was on the wall the night the tower rose. I watched the First Painter walk into it. He was carrying a child's portrait, and he was not coming out."),
                Them("Captain Oswin", "Six floors left. I'll have the gate open when you come down. Both of you.")),
            town = new[]
            {
                Them("Captain Oswin", "Gate's in good order. The watch is doubled after dark. Go and finish it, Painter."),
            },
        },
    };

    // Which of the lost is found on this floor, or -1.
    // The two branches of a rescue. Asked to go home, each says goodbye in their own way.
    static readonly string[] Farewells =
    {
        "A Vane does not run. But she does know when to walk. Don't die up there, bag boy.",
        "Aye. My knees agree with you. Mind the next one, lad: it hits like a falling wall.",
        "Home. I had forgotten the word. ...Go on. Don't make me watch you leave.",
        "I will light a candle for you in the chapel. Two, since you never eat.",
        "Understood. I'll walk it. A captain does not get carried through his own gate.",
    };

    // Asked what they saw, each tells what stands on the Guardian's floor above them.
    static readonly string[][] Sightings =
    {
        new[] { "Grol. The Gatekeeper. It was a Brute once, before the tower fed it.",
                "It shows you where the blow will land: the ground goes red first. And once it charges, it cannot turn. Put stone at your back." },
        new[] { "The Heartwarden. It does not come alone, and it does not tire.",
                "Thin the small ones first, lad. Then it is only big, and big I taught you how to block." },
        new[] { "Ash-Maw. I got one cut on it. One.",
                "It punishes anyone who stands still to admire their own swing. I should know. Keep moving." },
        new[] { "A second Heartwarden, child, and it is grieving. They all are, this high up.",
                "It strikes slower than the first. It strikes harder. Wait for the red to fade before you step in." },
        new[] { "The King's Shadow. It wears his shape and none of his mercy.",
                "It fights the way he trained us to fight. So fight it the way you taught yourself. It has never seen that." },
    };

    public static Line[] GoHome(int who) => new[]
    {
        Pip("Go home. The Way Down is clear. I made sure of it."),
        Them(Rescues[who].name, Rescues[who].farewell),
    };

    public static Line[] Saw(int who) => new[]
    {
        Pip("Before you go. What is on the floor above?"),
        Them(Rescues[who].name, Rescues[who].sightings[0]),
        Them(Rescues[who].name, Rescues[who].sightings[1]),
        Pip("Then I know what I'm walking into. Go home."),
    };

    public static int RescueOnFloor(int floor)
    {
        for (int i = 0; i < Rescues.Length; i++) if (Rescues[i].floor == floor) return i;
        return -1;
    }
    public static int RescueIndex(string id) => System.Array.FindIndex(Rescues, r => r.id == id);
    public static Line[] RescueTown(int who)
    {
        Lost lost = Rescues[who];
        var lines = new System.Collections.Generic.List<Line>(lost.town);
        bool intel = Save.Consequence("rescue:" + lost.id + ":guardian-intel");
        // Rhea's intel was about the Gatekeeper: once he has fallen, she knows it helped.
        if (intel && lost.id == "rhea" && Save.TowerFloor >= 5)
            lines.Add(Them(lost.name, "It ran itself into the stone, didn't it? I told you it could not turn. A Vane pays what she owes, Pip, and so do Gatekeepers."));
        else if (intel)
            lines.Add(Them(lost.name, "You asked what I saw. Remember: " + lost.sightings[1]));
        else if (Save.Consequence("rescue:" + lost.id + ":safe-return"))
            lines.Add(Them(lost.name, "You sent me straight home. I reached Brushwick safely, and I have not forgotten."));
        return lines.ToArray();
    }

    // Lady Vane, once her niece is home safe because the hero sent her straight down.
    public static Line[] VaneThanks() => new[]
    {
        Them("Lady Aurelia Vane", "You found Rhea on the fourth floor, and you did not keep her there one breath longer than you had to."),
        Them("Lady Aurelia Vane", "She walked in through my door on her own two feet. House Vane pays what it owes. Take this."),
    };

    // Lady Vane, once she hears the hero kept her niece talking on a Guardian's floor.
    public static Line[] VaneCool() => new[]
    {
        Them("Lady Aurelia Vane", "Rhea tells me you stood her up on the fourth floor and asked her questions while the Gloom listened."),
        Them("Lady Aurelia Vane", "I am told it was useful. I am sure it was. My niece is not a map, Painter. Our business continues; my gratitude waits."),
    };

    // What the town says once Grol the Gatekeeper has fallen (the gate sergeant by the tower door).
    public static Line[] GatekeeperAftermath()
    {
        var lines = new System.Collections.Generic.List<Line>
        {
            Them("Gate Sergeant Hale", "Those are its bars, hung by the tower door. The smith says the iron is older than Brushwick."),
            Them("Gate Sergeant Hale", "Forty years I have stood at this gate and watched people walk into that tower. You are the first I have seen bring the tower's gate back out."),
        };
        if (Save.Boon(Upgrades.GatebreakerId) > 0)
            lines.Add(Them("Gate Sergeant Hale", "And you carry something of it with you now. I can see it in how you stand. Heavier. Harder to move."));
        if (Save.Consequence("rescue:rhea:guardian-intel"))
            lines.Add(Them("Gate Sergeant Hale", "Young Vane tells everyone she told you how. Let her. She earned it."));
        return lines.ToArray();
    }

    // ------------------------------------------------------------ Brushwick

    // First words from each of Brushwick's people. Null for someone with no introduction to make.
    public static Line[] Meet(string who)
    {
        if (who.StartsWith("Bram"))
            return new[] { new Line(who, TownC, "So you're the E-Rank who walked out of the tower alive. Hah! Bring me Sparks and I'll hammer some strength into you.", "them") };
        if (who.StartsWith("Sol"))
            return new[] { new Line(who, TownC, "Every Heart you open gives back a memory. Bring them to me. Someone should remember him as he was.", "them") };
        if (who.StartsWith("Lady"))
            return new[]
            {
                new Line(who, NobleC, "So this is the E-Rank the whole square is whispering about. You are shorter than the rumour.", "them"),
                new Line(who, NobleC, "House Vane has sponsored Painters for four hundred years. My own niece went up with the Vermilion Brush. I find I am short of people I can trust."),
                new Line(who, NobleC, "Do a small thing for me, and do it well. Houses remember who was useful when it mattered."),
            };
        if (who.StartsWith("Lord"))
            return new[]
            {
                new Line(who, NobleC, "Ah. The bag-carrier. I am told you beat a slime and half of Brushwick wept for joy.", "them"),
                new Line(who, NobleC, "My cousin Kael was A-Rank. A-Rank! And the tower ate him. You will forgive me if I do not expect much of the bottom of the list."),
                new Line(who, NobleC, "Still. I am a sporting man. Let us put money on how far you get."),
            };
        if (who.StartsWith("Nix"))
            return new[]
            {
                new Line(who, TownC, "The Guild hall's empty, kid. Everyone who could fight went up. But the board has never been fuller: bandits on every road, monsters in every field, and nobody left to take the work.", "them"),
                new Line(who, TownC, "So here is how it goes. You take a commission off the board. You do it. You come back and I pay you. Do enough of them and the harder work opens."),
                new Line(who, TownC, "The noble houses post their own, by the way. They pay better. They are also nobles. Mind yourself."),
            };
        return null;
    }

    // What a client says when the work is done and the purse comes out.
    public static Line[] Thanks(string who, string text)
    {
        return new[] { new Line(who, who.StartsWith("Nix") ? TownC : NobleC, text, "them") };
    }

    static readonly string[][] PhaseBarks =
    {
        new[] { "All quiet.", "Gate's shut. Mind yourself out there.", "The Gloom is restless tonight.", "Can't sleep either?" },
        new[] { "Up before the sun, Painter?", "Bread's not out yet. Soon.", "Cold morning." },
        new[] { "Morning, Painter.", "Fine morning for it.", "Off to the tower again?", "Mind the cart." },
        new[] { "Warm one today.", "Market's busy.", "Good day to you.", "Sit a while, the tower will keep." },
        new[] { "Lamps are going up.", "Tavern's open, if you've a thirst.", "Get home before dark, lad.", "Long day." },
    };

    // What the town has to say about the hero, by how far up the tower he has been.
    static readonly string[][] FameBarks =
    {
        new[] { "That's the bag boy, isn't it?", "E-Rank. Bless him.", "Don't go in there, Pip. It ate A-Ranks." },
        new[] { "They say you beat a slime! ...Well. It's a start.", "Walked out on his own legs, he did.", "Is it true the air talks to you?" },
        new[] { "Gatebreaker! That's what they're calling you.", "You killed a Guardian? YOU?", "My boy saw you come down. Wouldn't stop talking." },
        new[] { "The grass. Did you see the grass? It's GREEN.", "My mother cried when the colour came back.", "A Heart-Opener, in Brushwick. Imagine." },
        new[] { "Two Hearts. Nobody has opened two Hearts.", "My girl wants to be a Painter now. E-Rank, she says. Like Pip.", "We named the new well after you. Don't look at it." },
        new[] { "You painted it back. All of it.", "Drinks are on the house. Forever.", "The weakest Painter. Hah!" },
    };

    static readonly string[] RescueBarks =
    {
        "They came walking in through the gate. ALIVE. Did you hear?", "The Vermilion Brush is coming home, one at a time.", "You brought them back. The Guild put their names up again.",
    };

    // A word from someone the hero walks past.
    public static string Bark(int phase, int bestFloor, int rescued, bool atPost)
    {
        float roll = ProductionRandom.Narrative.value;
        if (!atPost) return roll < 0.5f ? "Evening. I'm for my bed." : "Can't stop. Home time.";
        if (rescued > 0 && roll < 0.18f) return RescueBarks[ProductionRandom.Narrative.Range(0, RescueBarks.Length)];
        if (roll < 0.5f)
        {
            string[] pool = PhaseBarks[Mathf.Clamp(phase, 0, PhaseBarks.Length - 1)];
            return pool[ProductionRandom.Narrative.Range(0, pool.Length)];
        }
        int fame = bestFloor >= 30 ? 5 : bestFloor >= 20 ? 4 : bestFloor >= 10 ? 3 : bestFloor >= 5 ? 2 : bestFloor >= 1 ? 1 : 0;
        string[] said = FameBarks[fame];
        return said[ProductionRandom.Narrative.Range(0, said.Length)];
    }

    // ------------------------------------------------------------ chapters

    public static readonly Line[][] LevelIntro =
    {
        new[]
        {
            Chapter("CHAPTER ONE  -  FLOORS 1 TO 10", "The Weakest Painter"),
        },
        new[]
        {
            Chapter("CHAPTER TWO  -  FLOORS 11 TO 20", "The Buried Gold"),
            Lumi("These floors were dunes once. He painted them gold for somebody's birthday."),
            Pip("How do you know that?"),
            Lumi("I just... do. Careful. The ones with fuses don't knock."),
        },
        new[]
        {
            Chapter("CHAPTER THREE  -  FLOORS 21 TO 30", "The Hollow Crown"),
            Lumi("He's close. Under all that grey, he's still here.", "heart"),
            Pip("Then I didn't climb thirty floors to break a king. I came to wake my teacher."),
        },
    };

    // Told when a Colour Heart opens: floor 10 and floor 20.
    public static readonly Line[][] LevelOutro =
    {
        new[]
        {
            Lumi("Green! Oh, I'd forgotten green!"),
            N("An echo spills out of the Heart: a man laughing, paint on his sleeves, lifting a little girl so she can reach the top of a canvas.", "heart"),
            Pip("That was Master Aurel. Who's the girl?"),
            Lumi("...We should keep climbing."),
            King("Little brush. The weakest of them, and still you scratch at my quiet. Stop."),
        },
        new[]
        {
            N("The echo here is quieter. A small bed. A grey winter. A painter mixing every colour he owns, again and again, trying to paint one laugh back into the world.", "heart"),
            Pip("Mira. His daughter. She..."),
            Lumi("He couldn't finish the painting. So he decided nothing should ever be bright enough to lose again."),
            Pip("Lumi. The Gloom King is Master Aurel."),
            Lumi("And I'm the one colour he couldn't bring himself to drink."),
            King("Bring her to me, apprentice. She is all I have left of the noise."),
        },
    };

    public static readonly Line[] BossIntro =
    {
        King("Floor Thirty. They told me you were E-Rank, Pip. You mixed my colours. You swept my floors.", "boss"),
        Pip("I was. Then I climbed. You taught me that grey is only what's left when you stop looking."),
        King("I looked. I looked until there was nothing left to see. Colour is a promise the world does not keep.", "boss"),
        Lumi("Papa. It's me. I'm still loud. I'm still here."),
        King("...Then be quiet with the rest.", "boss"),
    };

    public static readonly Line[] BossDefeat =
    {
        King("It's so... loud. I can hear her in every colour.", "boss"),
        Lumi("Pip. His crown is the last Heart. Shatter it, and everything comes back at once."),
        Lumi("Or... use me. I'm her laugh. Paint him back with it. But a spark can only be spent once."),
    };

    public const string ChoiceA = "SHATTER THE CROWN";
    public const string ChoiceB = "PAINT HIM BACK";

    public static readonly Line[] EndingShatter =
    {
        N("The crown broke like old varnish. Every colour Aurel had ever swallowed came roaring down the tower, floor by floor.", "wide"),
        N("The Gloom King was gone. On the thirtieth floor there was only a tired painter's coat, and it was empty.", "heart"),
        Lumi("He's with her now, I think. And I'm still here. Somebody has to keep you loud, Pip."),
        N("Chroma Vale was bright again. Its First Painter was a story. Its weakest one had become its strongest, and he was only beginning.", "sky"),
    };

    public static readonly Line[] EndingPaint =
    {
        Lumi("Hold the brush steady. And don't cry, you'll smudge me."),
        N("Pip painted a single stroke across the grey king's chest: the exact yellow of a small girl's laugh.", "boss"),
        new Line("Aurel", AurelC, "Pip? ...I had the most terrible dream. Everything was quiet.", "boss"),
        N("The colour came back slowly, the way morning does. Lumi did not. But on windy days Brushwick laughs, and Aurel always puts down his brush to listen.", "wide"),
        N("Chroma Vale had two painters now. Neither of them ever used grey again.", "sky"),
    };

    public static readonly string[] EndingNames = { "Ending: The Empty Coat", "Ending: The Yellow Stroke" };

    public static readonly string[] BossTaunts =
    {
        "\"Nothing bright survives!\"",
        "\"Mira... forgive me...\"",
    };

    public static readonly string[] DefeatLines =
    {
        "\"You fell, but your level didn't. The floors you cleared stay cleared.\"",
        "\"We almost had that floor. Next time, dash at the very last moment.\"",
        "\"Rest. Spend your stat points. Then we go back for that one.\"",
        "\"Mix your moves up: the better you fight, the faster you grow.\"",
        "\"He was a good man once. Don't give up on him. Don't give up on me.\"",
    };

    // Memories recovered along the way. Re-readable in town.
    public static readonly Echo[] Echoes =
    {
        new Echo("The First Painter", "Aurel did not find Chroma Vale. He painted it: one hill at a time, until there was a whole world to stand in."),
        new Echo("The Girl at the Canvas", "A man laughing with paint on his sleeves, lifting a little girl so she can reach the top of a canvas. Her name was Mira. She signed every sky he made."),
        new Echo("The Gold Birthday", "The Dunes were grey rock until Mira turned seven. Aurel painted them gold overnight and told her the sun had dropped its change."),
        new Echo("The Unfinished Portrait", "A small bed. A grey winter. A painter mixing every colour he owns, trying to paint one laugh back into the world. The portrait was never finished."),
        new Echo("The Hollow Crown", "He drank the colours one by one so that nothing bright could be taken from him again. The last one, a yellow spark that laughed, he could not swallow. He called her Lumi and let her go."),
        new Echo("The Empty Coat", "Pip shattered the crown. The colours came home. The painter did not. Lumi stayed, to keep the apprentice loud."),
        new Echo("The Yellow Stroke", "Pip spent the last spark on one brushstroke. Aurel woke. Lumi is gone, but on windy days the meadow still laughs."),
    };
}
