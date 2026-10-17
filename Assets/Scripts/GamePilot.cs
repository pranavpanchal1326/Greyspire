using UnityEngine;

public partial class Game
{
    public bool TrainingActive { get; private set; }
    public Enemy PracticeTarget { get; private set; }
    GameObject pilotGate;
    public static readonly Vector3 PilotCourierAt = new Vector3(-22f, 3.2f, 2f);
    static readonly Color PilotStone = new Color(0.42f, 0.48f, 0.46f);
    static readonly string[] MasteryTitles = { "MOBILITY", "DUELIST", "BREAKER" };
    static readonly string[] MasteryBody = {
        "Evade Counter\nA perfect dodge arms one light hit for 1.2s: +30% damage.\n\nSecond node: that hit also staggers for 0.6s.\n\nTradeoff: heavy attacks take 20% longer.",
        "Measured Riposte\nOpen-target ripostes deal 2.7x instead of 2.2x damage.\n\nSecond node: ripostes grant 12 extra Colour charge.\n\nTradeoff: throws recharge 25% slower.",
        "Charged Impact\nHeavy attacks at 60% charge or more deal +20% damage.\n\nSecond node: those impacts have 25% more radius.\n\nTradeoff: heavy attacks take 20% longer."
    };

    GameObject PilotBox(string name, Vector3 centre, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(LevelRoot, false); go.transform.position = centre; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = ToonKit.Mat(color); return go;
    }
    void PilotRamp(string name, float z0, float z1, float y0, float y1)
    {
        var go = new GameObject(name); go.transform.SetParent(LevelRoot, false);
        var vertices = new[] { new Vector3(-25,y0,z0),new Vector3(-15,y0,z0),new Vector3(-25,y1,z1),new Vector3(-15,y1,z1),
            new Vector3(-25,-0.5f,z0),new Vector3(-15,-0.5f,z0),new Vector3(-25,-0.5f,z1),new Vector3(-15,-0.5f,z1) };
        var mesh = GeneratedResources.Own(new Mesh { name = name, vertices = vertices,
            triangles = new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7} }, go.transform);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = ToonKit.Mat(new Color(0.54f,0.64f,0.62f));
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }
    void RebuildPilotNavigation()
    {
        var old = LevelRoot.GetComponent<NavField>();
        if (old != null) { old.enabled = false; Destroy(old); }
        Physics.SyncTransforms(); NavField.BakeStaged(LevelRoot, FloorRadius, Player.GetComponent<CharacterController>());
    }
    void ApplyPilotShortcut(bool rebake)
    {
        if (pilotGate == null || !PilotProgress.Shortcut || !pilotGate.activeSelf) return;
        pilotGate.SetActive(false);
        if (rebake) RebuildPilotNavigation();
    }
    void BuildPilotFloor(LevelDef def, System.Random rng)
    {
        // The two solid ruin wings force a real route choice. The cross-pass is
        // collision- and navigation-backed, and its latch survives reconstruction.
        PilotBox("South ruin wing", new Vector3(0,4.5f,-10), new Vector3(26,9,12), PilotStone);
        PilotBox("North ruin wing", new Vector3(0,4.5f,10), new Vector3(26,9,12), PilotStone);
        for (int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
            Prop(new PropDef("Pillar", Kit, 10f, 0.6f),new Vector3(x*12,0,z*15),rng,1f,0);
        PilotRamp("South survey ramp",-24,-12,0,3.2f);
        PilotBox("Survey overlook",new Vector3(-20,2.9f,0),new Vector3(10,0.6f,24),new Color(0.55f,0.65f,0.62f));
        PilotRamp("North survey ramp",12,24,3.2f,0);
        PilotBox("Overlook outer rail",new Vector3(-25,3.65f,0),new Vector3(0.35f,0.9f,24),PilotStone);
        pilotGate = PilotBox("Cross-pass gate",new Vector3(0,2.3f,0),new Vector3(0.6f,4.6f,8),new Color(0.37f,0.29f,0.23f));
        ApplyPilotShortcut(false);
        for(float z=-28;z<=28;z+=2f) Disc(new Color(0.8f,0.72f,0.54f),2.7f,-0.006f,20,z,def.patch);
        for(float x=0;x<=20;x+=2f) { Disc(new Color(0.8f,0.72f,0.54f),2.7f,-0.006f,x,-28,def.patch); Disc(new Color(0.8f,0.72f,0.54f),2.7f,-0.006f,x,28,def.patch); }
        for(float x=-20;x<=0;x+=2f) { Disc(new Color(0.63f,0.73f,0.7f),2.7f,-0.005f,x,-28,def.patch); Disc(new Color(0.63f,0.73f,0.7f),2.7f,-0.005f,x,28,def.patch); }
        System.Func<Vector2,bool> clear = p => Mathf.Abs(p.x)<16 && Mathf.Abs(p.y)<33 || Mathf.Abs(Mathf.Abs(p.x)-20)<7 && Mathf.Abs(p.y)<32 || Mathf.Abs(p.y)<5;
        BuildGrass(def,FloorRadius+9,rng,6500,clear); BuildFlowers(def.accents,Vector2.zero,FloorRadius,800,rng,clear);
        for(int i=0;i<36;i++)
        {
            float angle=i*Mathf.PI*2/36; var at=new Vector3(Mathf.Sin(angle)*46,0,Mathf.Cos(angle)*46);
            if(at.z<-40) continue; Prop(def.ring[i%def.ring.Length],at,rng,1.1f);
        }
        packSpots.Add(new Vector2(20,-12)); packSpots.Add(new Vector2(20,11)); packSpots.Add(new Vector2(0,27));
        AddSpot(new Vector3(-3,0,-28),"Surveyor's Crossing","East road: combat / west ramp: courier and treasure",()=>SystemMessage("TWO APPROACHES","The road leads through the monsters. The west ramp reaches the courier and an overlook. Both join the north stairs. The cross-pass latch opens from the west."));
        AddSpot(new Vector3(-5,0,0),"Cross-pass latch","Open the shortcut between both routes",()=>
        {
            if (PilotProgress.OpenShortcut()) { ApplyPilotShortcut(true); Toast("SHORTCUT OPEN",UI.Blue); }
            else Toast("The cross-pass is already open.",UI.Blue);
        });
        BuildStairs(rng,PilotProgress.Floor); BuildWayDown(rng);
        // Keep the existing one-time chest identity for floor 3, slot 0.
        if(!Save.TowerChest(PilotProgress.Floor,0))
        {
            var at=new Vector3(-23,3.2f,9); var chest=Prop(new PropDef("Chest_Wood",Fur,1.5f,0.6f),at,rng,1f,0);
            Spot spot=null; spot=AddSpot(at,"Treasure","Open the overlook chest",()=>
            {
                if(!JourneyStore.Transaction(()=> { if(Save.TowerChest(PilotProgress.Floor,0))return false; Save.OpenTowerChest(PilotProgress.Floor,0); coins=checked(coins+12);return true; }))return;
                spots.Remove(spot);chest.SetActive(false);Toast("+12 SPARKS",UI.Gold);
            },chest.transform);
        }
        Physics.SyncTransforms();
        string selected=PilotProgress.SelectEvent(PilotProgress.Floor);
        if(selected.Length>0 && PilotProgress.Choice==0 && WorldSupport.TryStand(PilotCourierAt,0.4f,1.8f,out Vector3 feet))
        {
            var courier=new GameObject("Iven the courier");courier.transform.SetParent(LevelRoot,false);courier.transform.position=feet;
            var body=Humans.Build(Looks.Villager,courier.transform);if(body!=null)CharAnim.Attach(courier,body).Loop("Idle_Loop");
            AddSpot(PilotCourierAt,"Iven — stranded courier","Hear the courier's offer",()=>OpenCourierOffer(),courier.transform);
        }
        DressSurveyRuin(rng);
        RebuildPilotNavigation();
    }
    void OpenCourierOffer()
    {
        if(PilotProgress.Choice!=0) { Toast("The courier has already left. Report at the town field desk after clearing Floor 3.",UI.Blue); return; }
        PilotProgress.BeginOffer();
        bool town=InTown;
        var courier=GameObject.Find("Iven the courier");
        if(courier!=null)shotSubject=courier.transform;
        ApplyShot(town?"hero":"them");
        string token=PilotProgress.Token;
        string supplies=PilotProgress.Selected=="courier-medicine" ? "medicine" : "survey tools";
        Ask("Iven's pack of "+supplies,"SEND IT HOME","Iven opens the cross-pass now.\nCollect 20 banked Sparks in town after clearing this floor.",
            "KEEP THE FIELD KIT","Receive 24 banked Sparks now.\nOpen the cross-pass yourself; the Guild hears what happened.",pick=>
        {
            if(!PilotProgress.Resolve(pick+1,token))return;
            ApplyPilotShortcut(true);
            Say(new[]{new Line("Iven",UI.Blue,pick==0?"I'll release the cross-pass latch. Bring this receipt to the field desk once the road is safe.":"Twenty-four Sparks' worth. Take it. I'll tell the Guild why the delivery never arrived.",town?"hero":"them")},()=>SetState(town?GameState.Hub:GameState.Playing));
        });
    }
    void BuildPilotHub()
    {
        TrainingActive=false;
        PracticeTarget=null;
        AddSpot(new Vector3(7,0,-5),"Field desk","Mastery, practice and courier reports",()=>OpenHubPanel(5));
        if(PilotProgress.Choice==0)return;
        AddSpot(new Vector3(7,0,-1),PilotProgress.Choice==1?"Courier delivery":"Courier testimony","Read the courier's report",()=>
        {
            if(Save.BestFloor<PilotProgress.Floor) { SystemMessage("REPORT PENDING","Clear Floor 3 so the Guild can confirm the courier's journey.");return; }
            bool paid=PilotProgress.CompleteCallback(PilotProgress.Token);
            string text=PilotProgress.Choice==1 ? (paid?"The delivery arrived. Twenty Sparks, paid once, with the Guild's thanks.":"The delivery is recorded. The Guild still remembers your help.")
                : "Iven returned without the pack. The Guild records your choice. The 24 Sparks from the field kit were your only payment.";
            Say(new[]{new Line("Guild field clerk",UI.Gold,text)},()=>SetState(GameState.Hub));
        });
    }
    void DrawMastery(float w,float h,float cx)
    {
        Dim(w,h,0.72f);float width=Mathf.Min(1000,w-60),left=cx-width/2,top=76;
        SystemPanel(new Rect(left,top,width,550),1);
        UI.Text(new Rect(left,top+10,width,36),"FIELD MASTERY",30,UI.Gold);
        UI.Text(new Rect(left+24,top+52,width-48,44),"One path at a time. Each node costs 1 technique point. Points earned: "+PilotProgress.Budget+" / 2; spent: "+PilotProgress.Rank+".\nClear Floor 1 for the first point and Floor 3 for the second. Stat points are separate.",14,Color.white,TextAnchor.MiddleCenter,false);
        for(int i=0;i<3;i++)
        {
            string branch=PilotProgress.Branches[i];float cardWidth=(width-64)/3;
            var card=new Rect(left+24+i*(cardWidth+8),top+112,cardWidth,326);
            bool chosen=PilotProgress.Branch==branch;
            UI.Framed(card,new Color(0.08f,0.11f,0.13f),chosen?UI.Gold:UI.Blue,2);
            UI.Text(new Rect(card.x+12,card.y+10,card.width-24,30),MasteryTitles[i]+(chosen?"  "+PilotProgress.Rank+"/2":""),20,chosen?UI.Gold:UI.Blue);
            UI.Text(new Rect(card.x+16,card.y+48,card.width-32,210),MasteryBody[i],15,Color.white,TextAnchor.UpperLeft,false);
            bool allowed=(PilotProgress.Branch.Length==0||chosen)&&PilotProgress.Rank<PilotProgress.Budget;
            string label=chosen?"LEARN SECOND NODE":"LEARN "+MasteryTitles[i];
            if(UI.Button(new Rect(card.x+12,card.yMax-54,card.width-24,40),label,UI.Blue,14,allowed))
            { if(PilotProgress.Learn(branch)) { Player.RefreshStats();ToonKit.Sfx("buy"); } }
        }
        float button=(width-72)/4;
        if(UI.Button(new Rect(left+24,top+458,button,42),"FREE TOWN RESPEC",UI.Blue,14,PilotProgress.Rank>0))PilotProgress.Respec();
        if(UI.Button(new Rect(left+32+button,top+458,button,42),"PRACTICE (NO REWARDS)",UI.Green,14))StartPilotTraining();
        if(UI.Button(new Rect(left+40+button*2,top+458,button,42),"REVISIT CROSSING",UI.Blue,14,Save.BestFloor>=PilotProgress.Floor))ReturnToPilot();
        if(UI.Button(new Rect(left+48+button*3,top+458,button,42),"LEAVE",UI.Red,18))OpenHubPanel(0);
        UI.Text(new Rect(left+24,top+512,width-48,22),ControlGuide.Navigation+"   |   Town respec costs 0; all technique points return. Story and stat records stay.",12,Color.white);
    }
    void StartPilotTraining()
    {
        if(!InTown||DefeatPending)return;
        Transition(()=>
        {
            timers.Clear();numbers.Clear();Juice.Reset();ResetStyle();ResetSystem();hubPanel=0;InWilds=false;
            waveActive=clearPending=awakening=false;pending=0;hintStep=-1;wave=0;level=0;
            BuildLevel(0);TrainingActive=true;
            var practiceBounds=new Bounds(new Vector3(10,3,-4),new Vector3(10,10,18));
            bool cleared=false;
            foreach(var collider in LevelRoot.GetComponentsInChildren<Collider>())
                if(collider.name!="Floor"&&collider.transform!=LevelRoot&&WorldSupport.Geometry(collider)&&collider.bounds.Intersects(practiceBounds))
                {collider.gameObject.SetActive(false);Destroy(collider.gameObject);cleared=true;}
            Physics.SyncTransforms();
            if(cleared)
            {
                var old=LevelRoot.GetComponent<NavField>();if(old!=null){old.enabled=false;Destroy(old);}
                NavField.BakeStaged(LevelRoot,ArenaRadius,Player.GetComponent<CharacterController>());
            }
            Player.NewRun();Player.Revive(new Vector3(10,0.1f,-6),true);Player.weak=1;
            var dummy=Enemy.Create(Enemy.Kind.Minion,new Vector3(10,0.1f,0),0);dummy.Training=true;
            PracticeTarget=dummy;
            var follower=dummy.GetComponent<NavFollower>();if(follower!=null)follower.enabled=false;
            dummy.title="Practice partner — no rewards";
            LevelRoot.gameObject.AddComponent<PilotTraining>().target=dummy;
            AddSpot(new Vector3(10,0,-10),"Return to town","Finish practice",()=>EnterHub());
            Cam.Face(0);Cam.Snap();SetState(GameState.Playing);
            Toast("PRACTICE: approach the partner and read the gold cue.",UI.Blue);
        });
    }
    void ReturnToPilot()
    {
        if(!InTown||DefeatPending||Save.BestFloor<PilotProgress.Floor)return;
        Transition(()=>
        {
            Save.BeginExpedition(false);DefeatPending=false;InWilds=false;level=0;wave=2;Gloom=Save.GloomTier;
            timers.Clear();numbers.Clear();Juice.Reset();ResetStyle();hubPanel=0;waveActive=clearPending=awakening=false;pending=0;
            BuildFloor(0,2);Player.NewRun();Player.weak=1;Player.Revive(FloorStart,true);Cam.Face(0);Cam.Snap();sat=satTarget=1;
            UnsealStairs();stairSpot.name="The Way Home";stairSpot.prompt="Return to Brushwick";stairSpot.act=LeaveTower;
            SetState(GameState.Playing);Banner("SURVEYOR'S CROSSING",2.5f,"A liberated-floor visit. Both exits return home; tower progress stays earned.");
        });
    }
}
