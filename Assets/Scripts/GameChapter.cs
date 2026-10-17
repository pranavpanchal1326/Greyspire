using UnityEngine;

public partial class Game
{
    GameObject millraceGate;
    const float MillraceDeck = 5.4f;

    void MillraceRamp(string name, float z0, float z1, float y0, float y1)
    {
        var go = new GameObject(name);
        go.transform.SetParent(LevelRoot, false);
        var mesh = GeneratedResources.Own(new Mesh {
            name = name,
            vertices = new[] {
                new Vector3(-5,y0,z0), new Vector3(5,y0,z0), new Vector3(-5,y1,z1), new Vector3(5,y1,z1),
                new Vector3(-5,-0.5f,z0), new Vector3(5,-0.5f,z0), new Vector3(-5,-0.5f,z1), new Vector3(5,-0.5f,z1) },
            triangles = new[] {0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7}
        }, go.transform);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = ToonKit.Mat(new Color(0.55f,0.62f,0.57f));
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    void ApplyMillraceShortcut(bool rebake)
    {
        if (millraceGate == null || !ChapterProgress.MillraceOpen || !millraceGate.activeSelf) return;
        millraceGate.SetActive(false);
        if (rebake) RebuildPilotNavigation();
    }

    void MillraceTrail(LevelDef def, Vector2 from, Vector2 to)
    {
        int count = Mathf.CeilToInt(Vector2.Distance(from, to) / 2);
        for (int i = 0; i <= count; i++)
        {
            Vector2 at = Vector2.Lerp(from, to, (float)i / count);
            Disc(new Color(0.75f,0.68f,0.49f),2.4f,-0.006f,at.x,at.y,def.patch);
        }
    }

    void BuildMillraceFloor(LevelDef def, System.Random rng)
    {
        var heart = LevelRoot.Find("ColourHeart");
        if (heart != null) heart.position = new Vector3(27,0,-14);
        // A bridge over a crossing, with actual clearance under its deck.
        // Ground actors stay on their own support instead of snapping upstairs.
        MillraceRamp("Millrace south ramp",-27,-7,0,MillraceDeck);
        PilotBox("Millrace bridge",new Vector3(0,MillraceDeck-0.25f,0),new Vector3(10,0.5f,14),new Color(0.57f,0.64f,0.59f));
        MillraceRamp("Millrace north ramp",7,27,MillraceDeck,0);
        foreach (int side in new[] {-1,1})
        {
            PilotBox("Millrace bridge parapet",new Vector3(side*5,MillraceDeck+0.45f,0),new Vector3(0.35f,0.9f,14),PilotStone);
            PilotBox("Millrace sluice pier",new Vector3(0,2.4f,side*5.5f),new Vector3(0.7f,4.8f,5),PilotStone);
        }
        millraceGate = PilotBox("Millrace service gate",new Vector3(0,2.4f,0),new Vector3(0.6f,4.8f,6),new Color(0.40f,0.31f,0.22f));
        ApplyMillraceShortcut(false);

        MillraceTrail(def,new Vector2(0,-35),new Vector2(0,-29));
        MillraceTrail(def,new Vector2(0,-29),new Vector2(-18,-29));
        MillraceTrail(def,new Vector2(-18,-29),new Vector2(-18,0));
        MillraceTrail(def,new Vector2(-18,0),new Vector2(18,0));
        MillraceTrail(def,new Vector2(18,0),new Vector2(18,29));
        MillraceTrail(def,new Vector2(18,29),new Vector2(0,29));
        MillraceTrail(def,new Vector2(0,29),new Vector2(0,35));
        System.Func<Vector2,bool> clear = p => Mathf.Abs(p.x)<8 && Mathf.Abs(p.y)<36
            || Mathf.Abs(p.y)<5 && Mathf.Abs(p.x)<25
            || Mathf.Abs(Mathf.Abs(p.x)-18)<7 && Mathf.Abs(p.y)<33
            || Mathf.Abs(Mathf.Abs(p.y)-29)<5 && Mathf.Abs(p.x)<24;
        BuildGrass(def,FloorRadius+9,rng,6500,clear);
        BuildFlowers(def.accents,Vector2.zero,FloorRadius,800,rng,clear);
        for (int i=0;i<36;i++)
        {
            float angle=i*Mathf.PI*2/36;
            var at=new Vector3(Mathf.Sin(angle)*46,0,Mathf.Cos(angle)*46);
            if (Mathf.Abs(at.z)>40) continue;
            Prop(def.ring[i%def.ring.Length],at,rng,1.1f);
        }
        // Retain the existing encounter budget; positions make the lower lane risky.
        packSpots.Add(new Vector2(-18,-12));
        packSpots.Add(new Vector2(18,13));
        packSpots.Add(new Vector2(0,30));
        var wood = new Color(0.43f,0.31f,0.20f);
        PilotBox("Millrace route-board post",new Vector3(4,0.8f,-30),new Vector3(0.25f,1.6f,0.25f),wood);
        PilotBox("Millrace route board",new Vector3(4,1.7f,-30),new Vector3(2.2f,1,0.18f),wood);
        PilotBox("Millrace winch post",new Vector3(-7,0.7f,1.5f),new Vector3(0.3f,1.4f,0.3f),wood);
        PilotBox("Millrace winch handle",new Vector3(-7,1.3f,1.5f),new Vector3(1.3f,0.18f,0.18f),wood);
        AddSpot(new Vector3(3,0,-30),"The Millrace","Upper bridge or lower crossing: read the route board",()=>
            SystemMessage("OVER OR UNDER","The upper bridge avoids the lower packs and has an overlook chest. The lower crossing has a healing spring and a second chest. Lift the service gate at the west winch to connect both banks. Both routes meet the north stairs; all monsters must still be defeated."));
        string token=ChapterProgress.MillraceToken;
        AddSpot(new Vector3(-7,0,0),"Millrace winch","Lift the service gate (permanent shortcut)",()=>
        {
            if (ChapterProgress.OpenMillrace(token)) { ApplyMillraceShortcut(true);Toast("MILLRACE SHORTCUT OPEN",UI.Blue); }
            else Toast("The service gate is already open.",UI.Blue);
        });
        BuildStairs(rng,ChapterProgress.MillraceFloor);
        BuildWayDown(rng);
        BuildMillraceChest(0,new Vector3(3.5f,MillraceDeck,2),rng);
        BuildMillraceChest(1,new Vector3(22,0,6),rng);

        // Floor 2 already offered a spring. Preserve its two-heart, once-per-visit use.
        var springAt=new Vector3(-22,0,-16);
        var spring=Prop(new PropDef("Crystal",null,2.2f,0.5f),springAt,rng,0.7f,0);
        AddGlow(spring.transform,1.6f,new Color(0.5f,0.9f,1),3.2f);
        Spot springSpot=null;
        springSpot=AddSpot(springAt,"Colour Spring","Drink: restore 2 hearts",()=>
        {
            Player.Heal(2);heartPunch=1;ToonKit.Sfx("heart");Toast("+2 HEARTS",UI.Blue);spots.Remove(springSpot);
        },spring.transform);
        DressMillrace(rng);
        RebuildPilotNavigation();
    }

    void BuildMillraceChest(int slot, Vector3 at, System.Random rng)
    {
        if (Save.TowerChest(ChapterProgress.MillraceFloor,slot)) return;
        var chest=Prop(new PropDef("Chest_Wood",Fur,1.5f,0.6f),at,rng,1,0);
        Spot spot=null;
        spot=AddSpot(at,"Treasure",slot==0?"Open the bridge chest":"Open the lower-bank chest",()=>
        {
            if (!JourneyStore.Transaction(()=>
            {
                if (Save.TowerChest(ChapterProgress.MillraceFloor,slot)) return false;
                Save.OpenTowerChest(ChapterProgress.MillraceFloor,slot);
                coins=checked(coins+10);
                return true;
            })) return;
            coinPunch=1;ToonKit.Sfx("buy");Toast("+10 SPARKS",UI.Gold);
            Fx.Burst(chest.transform.position+Vector3.up,UI.Gold,16,7);
            chest.SetActive(false);spots.Remove(spot);
        },chest.transform);
    }
}
