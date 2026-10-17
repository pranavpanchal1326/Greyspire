using System;
using System.Collections.Generic;

public readonly struct FloorDefinition
{
    public readonly string id, stratum;
    public readonly int number, theme, layout, guardian;
    public readonly bool intro, heart, boss;
    public int LayoutSeed => boss ? 1000 + theme : 5000 + theme * 100 + layout;
    public FloorDefinition(int number, int theme, int layout, string stratum, int guardian = -1, bool intro = false, bool heart = false, bool boss = false)
    { this.number = number; this.theme = theme; this.layout = layout; this.stratum = stratum; this.guardian = guardian; this.intro = intro; this.heart = heart; this.boss = boss; id = "tower:" + number.ToString("D2"); }
}
public static class FloorCatalog
{
    // Explicit legacy content mapping. Six five-floor grouping IDs are available
    // to future authoring; the three existing themes and all story beats stay put.
    public static readonly FloorDefinition[] All =
    {
        new FloorDefinition(1,0,0,"meadow-i",intro:true), new FloorDefinition(2,0,1,"meadow-i"), new FloorDefinition(3,0,2,"meadow-i"), new FloorDefinition(4,0,3,"meadow-i"), new FloorDefinition(5,0,4,"meadow-i",0),
        new FloorDefinition(6,0,5,"meadow-ii"), new FloorDefinition(7,0,6,"meadow-ii"), new FloorDefinition(8,0,7,"meadow-ii"), new FloorDefinition(9,0,8,"meadow-ii"), new FloorDefinition(10,0,9,"meadow-ii",1,heart:true),
        new FloorDefinition(11,1,0,"dunes-i",intro:true), new FloorDefinition(12,1,1,"dunes-i"), new FloorDefinition(13,1,2,"dunes-i"), new FloorDefinition(14,1,3,"dunes-i"), new FloorDefinition(15,1,4,"dunes-i",2),
        new FloorDefinition(16,1,5,"dunes-ii"), new FloorDefinition(17,1,6,"dunes-ii"), new FloorDefinition(18,1,7,"dunes-ii"), new FloorDefinition(19,1,8,"dunes-ii"), new FloorDefinition(20,1,9,"dunes-ii",3,heart:true),
        new FloorDefinition(21,2,0,"hollow-i",intro:true), new FloorDefinition(22,2,1,"hollow-i"), new FloorDefinition(23,2,2,"hollow-i"), new FloorDefinition(24,2,3,"hollow-i"), new FloorDefinition(25,2,4,"hollow-i",4),
        new FloorDefinition(26,2,5,"hollow-ii"), new FloorDefinition(27,2,6,"hollow-ii"), new FloorDefinition(28,2,7,"hollow-ii"), new FloorDefinition(29,2,8,"hollow-ii"), new FloorDefinition(30,2,9,"hollow-ii",boss:true,heart:true)
    };
    public static FloorDefinition At(int number) { foreach (var floor in All) if (floor.number == number) return floor; throw new ArgumentOutOfRangeException(nameof(number)); }
    public static int ArenaSeed(int theme) { foreach (var floor in All) if (floor.theme == theme && floor.boss) return floor.LayoutSeed; return 1000 + theme; }
    public static FloorDefinition Legacy(int theme, int layout) { foreach (var floor in All) if (floor.theme == theme && floor.layout == Math.Max(0,layout)) return floor; throw new InvalidOperationException("Undefined floor mapping: " + theme + "/" + layout); }
    public static void Validate()
    {
        var ids = new HashSet<string>(); var numbers = new HashSet<int>(); var layouts = new HashSet<string>();
        foreach (var f in All) if (!ids.Add(f.id) || !numbers.Add(f.number) || !layouts.Add(f.theme+":"+f.layout) || f.theme < 0 || f.theme > 2 || f.layout < 0 || f.layout > 9) throw new InvalidOperationException("Duplicate or unsupported floor definition");
        for (int i=1;i<=30;i++) At(i);
        if (All.Length != 30 || !At(30).boss) throw new InvalidOperationException("The thirty-floor journey must retain its final confrontation");
    }
}
