using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ChapterRouteTest
{
    const string Session = "GreyspireChapterRouteTest";
    const BindingFlags Any = BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> errors = new List<string>();
    static readonly StringBuilder evidence = new StringBuilder("kind,identity,value,result\n");
    static int passed, stage, waypoint, walkFrames, cycle;
    static double next, deadline;
    static float walkStart, closedLength;
    static int[] warm;
    static Vector3[] route;
    static Keyboard keyboard;
    static Gamepad pad;
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host,string name) => host.GetType().GetField(name,Any).GetValue(host);
    static void Set(object host,string name,object value) => (host.GetType().GetField(name,Any)??host.GetType().GetField("<"+name+">k__BackingField",Any)).SetValue(host,value);
    static object Call(object host,string name,params object[] args) => host.GetType().GetMethod(name,Any).Invoke(host,args);
    static IEnumerable<object> Spots => ((IList)Get(G,"spots")).Cast<object>();
    static void Check(bool ok,string message) { if (!ok) throw new Exception(message);passed++;Debug.Log("CHAPTER PASS "+message); }
    static void Row(string kind,string id,double value) => evidence.AppendLine(kind+","+id+","+value.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture)+",PASS");
    static void OnLog(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message); }
    static void State()
    {
        Call(G,"SetState",Game.GameState.Playing);Set(G,"fade",0f);Set(G,"fadeDir",0f);Set(G,"introT",0f);
        Set(G,"showMap",false);Set(G,"resumeFrame",Time.frameCount-2);Set(G,"awakening",false);Set(G,"hintStep",-1);
        Set(G,"hubPanel",0);Set(G,"waveActive",false);Set(G,"InTown",false);Set(G,"InWilds",false);Set(G,"DefeatPending",false);
    }
    static void Fresh()
    {
        Save.Wipe();Save.SeenIntro=true;Save.Level=1;Save.BestFloor=1;Save.TowerFloor=1;Save.Sparks=100;
        JourneyStore.SetInt("productionSeed",734912);JourneyStore.SetInt("seedPolicy",1);Save.Flush();Upgrades.Reset();ProductionRandom.ClearRuntimeStreams();
    }
    static void Reload() {JourneyStore.BeforeReplaceForTest=null;JourneyStore.ReloadForTest();JourneyStore.Ensure();}
    static void Build() {Set(G,"level",0);Set(G,"wave",1);Call(G,"BuildFloor",0,1);State();}
    static void Records()
    {
        G.enabled=P.enabled=false;Fresh();
        byte[] original=File.ReadAllBytes(JourneyStore.FilePath);ChapterProgress.Validate();
        Check(!ChapterProgress.MillraceOpen&&File.ReadAllBytes(JourneyStore.FilePath).SequenceEqual(original),"old schema-2 journey needs no destructive chapter migration or free gate opening");
        bool interrupted=false;JourneyStore.BeforeReplaceForTest=path=>{if(path==JourneyStore.FilePath)throw new IOException("injected chapter gate interruption");};
        try {ChapterProgress.OpenMillrace(ChapterProgress.MillraceToken);}catch(IOException){interrupted=true;}finally{JourneyStore.BeforeReplaceForTest=null;}
        Reload();Check(interrupted&&!ChapterProgress.MillraceOpen,"interrupted gate commit keeps the closed gate and original entitlement");
        string token=ChapterProgress.MillraceToken;
        Check(ChapterProgress.OpenMillrace(token),"gate receipt commits without a mastery or payment requirement");Reload();
        Check(ChapterProgress.MillraceOpen&&!ChapterProgress.OpenMillrace(token)&&Save.BestFloor==1&&Save.TowerFloor==1&&Save.Sparks==100,"gate reload and replay cannot award progress or currency");
        Fresh();Check(!ChapterProgress.OpenMillrace(token)&&!ChapterProgress.MillraceOpen,"old campaign winch callback cannot mutate a new journey");
        foreach (var record in new[]{new{Key="chapterVersion",Value=2},new{Key="chapter:tower:02:millrace-gate-v1",Value=-1}})
        {
            byte[] supported=File.ReadAllBytes(JourneyStore.FilePath);
            JourneyStore.Transaction(()=>JourneyStore.SetInt(record.Key,record.Value));byte[] future=File.ReadAllBytes(JourneyStore.FilePath);JourneyStore.ReloadForTest();
            bool rejected=false;try{JourneyStore.Ensure();}catch(NotSupportedException){rejected=true;}
            Check(rejected&&File.ReadAllBytes(JourneyStore.FilePath).SequenceEqual(future),"unsupported chapter record retains exact committed bytes: "+record.Key);
            File.WriteAllBytes(JourneyStore.FilePath,supported);File.WriteAllBytes(JourneyStore.BackupPath,supported);Reload();
        }
        Fresh();Save.ReducedEffects();Check(Save.Flashes==0&&Save.CameraMotion==0,"chapter routes run with reduced effects enabled");
    }
    static NavMeshPath Path(Vector3 from,Vector3 to,float radius=0.6f,float height=2.2f)
    {
        var filter=new NavMeshQueryFilter{agentTypeID=NavField.Profile(radius,height),areaMask=NavMesh.AllAreas};
        if(!NavMesh.SamplePosition(from,out var a,1.5f,filter)||!NavMesh.SamplePosition(to,out var b,1.5f,filter))throw new Exception("Missing chapter socket "+from+" -> "+to);
        var path=new NavMeshPath();
        if(!NavMesh.CalculatePath(a.position,b.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Incomplete chapter path "+from+" -> "+to);
        return path;
    }
    static float Length(NavMeshPath path) {float value=0;for(int i=1;i<path.corners.Length;i++)value+=Vector3.Distance(path.corners[i-1],path.corners[i]);return value;}
    static readonly Vector3[] Upper={new Vector3(0,0.05f,-29),new Vector3(0,5.45f,-7),new Vector3(0,5.45f,7),new Vector3(0,0.05f,29),new Vector3(0,0.05f,35)};
    static readonly Vector3[] Lower={new Vector3(0,0.05f,-29),new Vector3(-18,0.05f,-29),new Vector3(-18,0.05f,0),new Vector3(18,0.05f,0),new Vector3(18,0.05f,29),new Vector3(0,0.05f,29),new Vector3(0,0.05f,35)};
    static void RouteChecks(bool lower)
    {
        foreach(var body in new[]{new[]{0.35f,1f},new[]{0.6f,2.2f},new[]{1.25f,4.1f}})
        {
            Vector3 from=new Vector3(0,0.05f,-37);float length=0;
            foreach(var point in lower?Lower:Upper){length+=Length(Path(from,point,body[0],body[1]));from=point;}
            string id=(lower?"lower-":"upper-")+body[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
            Check(length>70,"complete chapter "+id+" path");Row("route",id,length);
        }
    }
    static void StartWalk(Vector3[] points)
    {
        route=points;waypoint=walkFrames=0;walkStart=Time.realtimeSinceStartup;
        P.NewRun();P.stats=new PlayerStats();P.Revive(new Vector3(0,0.1f,-37),true);P.weak=1;G.enabled=P.enabled=true;State();
    }
    static bool Walk(string id)
    {
        if(Time.realtimeSinceStartup-walkStart>80)throw new Exception("chapter keyboard traversal stuck at "+P.transform.position+" toward "+route[waypoint]);
        Vector3 target=route[waypoint],delta=target-P.transform.position;delta.y=0;
        if(delta.magnitude<1&&Mathf.Abs(target.y-P.transform.position.y)<0.6f)
        {
            if(++waypoint==route.Length){InputSystem.QueueStateEvent(keyboard,new KeyboardState());Row("walk",id,Time.realtimeSinceStartup-walkStart);return true;}
            target=route[waypoint];delta=target-P.transform.position;delta.y=0;
        }
        // Prove the controller stays below the deck while crossing underneath it.
        if(id=="lower"&&Mathf.Abs(P.transform.position.x)<4&&Mathf.Abs(P.transform.position.z)<2&&P.transform.position.y>0.6f)
            throw new Exception("lower-route player snapped onto the overhead bridge");
        float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;G.Cam.Face(yaw);G.Cam.transform.rotation=Quaternion.Euler(12,yaw,0);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));walkFrames++;return false;
    }
    static void Pad(params GamepadButton[] buttons) {var state=new GamepadState();foreach(var button in buttons)state=state.WithButton(button);InputSystem.QueueStateEvent(pad,state);}
    static void Next(int value,double delay=0.3) {stage=value;next=EditorApplication.timeSinceStartup+delay;}
    static void Rewards()
    {
        G.enabled=P.enabled=false;int carried=Save.CarriedSparks;
        var chests=Spots.Where(s=>(string)Get(s,"name")=="Treasure").ToArray();
        Check(chests.Length==2,"both original floor-2 chest identities remain available");
        var first=(Action)Get(chests[0],"act");bool interrupted=false;
        JourneyStore.BeforeReplaceForTest=path=>{if(path==JourneyStore.FilePath)throw new IOException("injected chapter chest interruption");};
        try{first();}catch(IOException){interrupted=true;}finally{JourneyStore.BeforeReplaceForTest=null;}
        Reload();Check(interrupted&&!Save.TowerChest(2,0)&&Save.CarriedSparks==carried,"interrupted bridge chest cannot lose its receipt or partially pay");
        first();first();
        Check(Save.TowerChest(2,0)&&Save.CarriedSparks==carried+10,"bridge chest retains slot 0 and exactly ten carried Sparks");
        Build();Check(Spots.Count(s=>(string)Get(s,"name")=="Treasure")==1,"old opened bridge chest stays absent after floor rebuild");
        var second=(Action)Get(Spots.Single(s=>(string)Get(s,"name")=="Treasure"),"act");second();second();Reload();
        Check(Save.TowerChest(2,1)&&Save.CarriedSparks==carried+20&&Save.BestFloor==1,"lower chest retains slot 1 and cannot clear the floor or pay twice");
        P.stats.maxHearts=5;P.hp=1;
        var spring=Spots.Single(s=>(string)Get(s,"name")=="Colour Spring");((Action)Get(spring,"act"))();
        Check(P.hp==3&&!Spots.Any(s=>(string)Get(s,"name")=="Colour Spring"),"original two-heart spring remains once per visit");
        Call(G,"SpawnPacks",Call(G,"FloorWave",0,1));
        Check(Enemy.All.Count>0&&Enemy.All.All(e=>WorldSupport.TryStand(e.transform.position,0.35f,1f,out _)),"original floor-2 encounter budget produces supported real enemies beside the new routes");
        Call(G,"ClimbStairs");Check(G.Floor==2&&Save.TowerFloor==1&&!(bool)Get(G,"stairsOpen"),"shortcut and chest interactions cannot bypass the monster-clear stair gate");
    }
    static void CaptureWorld()
    {
        var go=new GameObject("Chapter route evidence camera");var cam=go.AddComponent<Camera>();
        cam.transform.position=new Vector3(60,62,-75);cam.transform.LookAt(Vector3.zero);cam.fieldOfView=50;cam.farClipPlane=250;
        var rt=new RenderTexture(1400,1000,24);var previous=RenderTexture.active;var pixels=new Texture2D(1400,1000,TextureFormat.RGB24,false);bool fog=RenderSettings.fog;
        try{RenderSettings.fog=false;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1400,1000),0,0);pixels.Apply();File.WriteAllBytes("Logs/chapter-route-overview.png",pixels.EncodeToPNG());}
        finally{RenderSettings.fog=fog;cam.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);Object.DestroyImmediate(go);}
    }
    static void Frame()
    {
        switch(stage)
        {
            case 0:
                Records();keyboard=InputSystem.AddDevice<Keyboard>();pad=InputSystem.AddDevice<Gamepad>();
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;Build();Next(1);break;
            case 1:
                if(NavField.Current==null||!NavField.Current.Ready)return;
                RouteChecks(false);closedLength=Length(Path(new Vector3(-8,0.05f,0),new Vector3(8,0.05f,0)));
                Check(closedLength>30,"closed millrace gate requires a real detour");StartWalk(Upper);Next(2,0);break;
            case 2:
                if(!Walk("upper"))return;Check(walkFrames>200,"baseline keyboard controller traverses both ramps and the upper bridge");
                P.Revive(new Vector3(-8,0.1f,0),true);Pad(GamepadButton.North);Next(3,0.2);break;
            case 3:
                Pad();Check(ChapterProgress.MillraceOpen,"actual controller interaction at west winch commits the shortcut");Next(4);break;
            case 4:
                if(NavField.Current==null||!NavField.Current.Ready)return;
                RouteChecks(true);float open=Length(Path(new Vector3(-8,0.05f,0),new Vector3(8,0.05f,0)));
                Check(open<18&&open<closedLength*0.6f,"open gate provides the shorter ground-level connection");Row("shortcut","closed",closedLength);Row("shortcut","open",open);
                Check(WorldSupport.TryStand(new Vector3(0,0.1f,0),0.6f,2.2f,out Vector3 lower)&&lower.y<0.1f
                    &&WorldSupport.TryStand(new Vector3(0,5.5f,0),0.6f,2.2f,out Vector3 upper)&&Mathf.Abs(upper.y-5.4f)<0.1f,"stacked bridge and ground retain distinct interaction support");
                StartWalk(Lower);Next(5,0);break;
            case 5:
                if(!Walk("lower"))return;Check(walkFrames>300,"baseline keyboard controller crosses below the bridge and completes the lower route");
                G.enabled=P.enabled=false;Reload();Build();Next(6);break;
            case 6:
                if(NavField.Current==null||!NavField.Current.Ready)return;
                Check(!GameObject.Find("Millrace service gate")&&Length(Path(new Vector3(-8,0.05f,0),new Vector3(8,0.05f,0)))<18,"persisted shortcut is applied before reconstruction navigation");
                CaptureWorld();Rewards();G.enabled=P.enabled=true;P.Revive(new Vector3(0,5.5f,-3),true);G.Cam.Face(0);G.Cam.Snap();State();Set(G,"waveActive",true);Set(G,"bannerT",0f);Set(G,"toastT",0f);Next(7,0.8);break;
            case 7:
                ScreenCapture.CaptureScreenshot("Logs/chapter-route-upper.png");Next(8,0.5);break;
            case 8:
                P.Revive(new Vector3(-8,0.1f,0),true);G.Cam.Face(90);G.Cam.Snap();Next(9,0.8);break;
            case 9:
                ScreenCapture.CaptureScreenshot("Logs/chapter-route-lower.png");Next(11,0.5);break;
            case 11:
                G.enabled=P.enabled=false;
                foreach(var enemy in Enemy.All.ToArray()){enemy.gameObject.SetActive(false);Object.Destroy(enemy.gameObject);}
                Build();cycle=0;Next(10,1);break;
            case 10:
                if(NavField.Current==null||!NavField.Current.Ready)return;
                var counts=new[]{GeneratedResources.LiveAssets,Resources.FindObjectsOfTypeAll<Mesh>().Length,Resources.FindObjectsOfTypeAll<Material>().Length};
                Debug.Log("CHAPTER OWNERS cycle "+cycle+": "+string.Join(", ",Object.FindObjectsByType<GeneratedResources>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(g=>g.name+"="+((System.Collections.Generic.HashSet<Object>)typeof(GeneratedResources).GetField("owned",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(g)).Count).OrderBy(x=>x)));
                if(cycle==1)warm=counts;
                if(cycle>=2)Check(counts.SequenceEqual(warm),"chapter reconstruction "+cycle+" retains warmed native ownership counts ("+string.Join("/",counts)+" vs warmed "+string.Join("/",warm)+"; owners: "+string.Join(", ",Object.FindObjectsByType<GeneratedResources>(FindObjectsInactive.Include,FindObjectsSortMode.None).GroupBy(g=>g.name).Select(g=>g.Key+" x"+g.Count()))+")");
                Row("lifecycle","chapter-"+cycle,counts[0]);
                if(++cycle<4){Call(G,"BuildFloor",0,2);Call(G,"BuildHub");Build();Next(10,1);return;}
                Check(errors.Count==0,"chapter runtime has no unexpected errors"+(errors.Count>0?": "+errors[0]:""));
                Check(JourneyStore.Error==null,"chapter leaves the isolated journey healthy");
                File.WriteAllText("Reports/chapter-route-evidence.csv",evidence.ToString());Debug.Log("CHAPTER DONE "+passed+" checks passed");SessionState.SetBool(Session,false);EditorApplication.Exit(0);break;
        }
    }
    static ChapterRouteTest(){if(SessionState.GetBool(Session,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
    public static void Run()
    {
        Save.Wipe();Save.SeenIntro=true;Save.Flush();SessionState.SetBool(Session,true);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||G==null||P==null)return;
        if(deadline==0){deadline=EditorApplication.timeSinceStartup+330;next=EditorApplication.timeSinceStartup+2;Application.logMessageReceived-=OnLog;Application.logMessageReceived+=OnLog;}
        try{if(EditorApplication.timeSinceStartup>deadline)throw new Exception("chapter route deadline");if(EditorApplication.timeSinceStartup<next)return;Frame();}
        catch(Exception e){JourneyStore.BeforeReplaceForTest=null;Debug.LogError("CHAPTER FAILED stage="+stage+" "+e);SessionState.SetBool(Session,false);EditorApplication.Exit(1);}
    }
}
