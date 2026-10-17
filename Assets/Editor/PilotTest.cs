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
public static class PilotTest
{
    const string Session="GreyspirePilotTest";
    const BindingFlags Any=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> errors=new List<string>();
    static readonly StringBuilder evidence=new StringBuilder("kind,identity,value,result\n");
    static int passed,stage,waypoint,walkFrames,lifeCycle;
    static double next,deadline;
    static Vector3[] route;
    static float closedLength,walkStart;
    static int[] warmNative;
    static Keyboard keyboard;
    static Gamepad pad;
    static Game G=>Game.I;
    static PlayerController P=>G.Player;
    static object Get(object host,string name)=>host.GetType().GetField(name,Any).GetValue(host);
    static void Set(object host,string name,object value)=>(host.GetType().GetField(name,Any)??host.GetType().GetField("<"+name+">k__BackingField",Any)).SetValue(host,value);
    static object Call(object host,string name,params object[] args)=>host.GetType().GetMethod(name,Any).Invoke(host,args);
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message);passed++;Debug.Log("PILOT PASS "+message); }
    static void Row(string kind,string id,double value)=>evidence.AppendLine(kind+","+id+","+value.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture)+",PASS");
    static void OnLog(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message); }
    static void State(Game.GameState state) { Call(G,"SetState",state);Set(G,"fade",0f);Set(G,"fadeDir",0f);Set(G,"introT",0f);Set(G,"showMap",false);Set(G,"resumeFrame",Time.frameCount-2); }
    static void Fresh(int floor=3)
    {
        Save.Wipe();Save.SeenIntro=true;Save.Level=12;Save.BestFloor=floor;Save.TowerFloor=floor;Save.Sparks=100;Save.StatPoints=7;
        JourneyStore.SetInt("productionSeed",734912);JourneyStore.SetInt("seedPolicy",1);Save.Flush();Upgrades.Reset();ProductionRandom.ClearRuntimeStreams();
    }
    static void Reload() { JourneyStore.BeforeReplaceForTest=JourneyStore.AfterMutationForTest=null;JourneyStore.ReloadForTest();JourneyStore.Ensure(); }
    static void Fault(Action action)
    {
        bool threw=false;JourneyStore.BeforeReplaceForTest=path=>{if(path==JourneyStore.FilePath)throw new IOException("injected pilot interruption");};
        try {action();}catch(Exception e){threw=e.GetBaseException() is IOException;}
        finally {JourneyStore.BeforeReplaceForTest=null;}
        Check(threw,"injected pilot write interruption is reported");Reload();
    }
    static void Town() {Set(G,"InTown",true);Set(G,"DefeatPending",false);State(Game.GameState.Hub);}
    static void Records()
    {
        G.enabled=P.enabled=false;Town();Fresh(0);
        string before=File.ReadAllText(JourneyStore.FilePath);PilotProgress.Validate();
        Check(PilotProgress.Rank==0&&PilotProgress.Selected==""&&File.ReadAllText(JourneyStore.FilePath)==before,"prior schema-2 journey needs no destructive pilot migration or free mastery grants");
        Check(!PilotProgress.Learn("mobility")&&PilotProgress.Budget==0,"cleared-floor prerequisite prevents unearned techniques");
        Fresh();int stat=Save.StatPoints;
        foreach(string branch in PilotProgress.Branches)
        {
            Check(PilotProgress.Learn(branch)&&PilotProgress.Rank==1,"learn root "+branch+" with one technique point");
            Check(!PilotProgress.Learn(PilotProgress.Branches.First(b=>b!=branch)),"exclusive mastery cannot spend across branches");
            Check(PilotProgress.Learn(branch)&&!PilotProgress.Learn(branch)&&PilotProgress.Rank==2,"second node is bounded for "+branch);
            Reload();Check(PilotProgress.Branch==branch&&PilotProgress.Rank==2,"stable mastery identity survives reload for "+branch);
            Check(PilotProgress.Respec()&&PilotProgress.Rank==0&&PilotProgress.Budget==2&&Save.StatPoints==stat&&Save.Sparks==100,"free respec refunds technique budget without changing stat points or currency");
        }
        Check(!PilotProgress.Learn("invented"),"unknown mastery identity is rejected");
        Save.BestFloor=1;Save.Flush();Check(PilotProgress.Learn("breaker")&&!PilotProgress.Learn("breaker"),"second node waits for floor 3 clearance");PilotProgress.Respec();Save.BestFloor=3;Save.Flush();
        Fault(()=>PilotProgress.Learn("mobility"));Check(PilotProgress.Branch==""&&PilotProgress.Rank==0,"interrupted node purchase retains the unspent technique budget");
        PilotProgress.Learn("duelist");Fault(()=>PilotProgress.Respec());Check(PilotProgress.Branch=="duelist"&&PilotProgress.Rank==1,"interrupted respec cannot erase the learned node");PilotProgress.Respec();
        State(Game.GameState.Playing);Set(G,"InTown",false);Check(!PilotProgress.Learn("duelist")&&!PilotProgress.Respec(),"mastery mutations are rejected away from town");Town();
        Fresh();Check(PilotProgress.SelectEvent(2)==""&&PilotProgress.Selected=="","event eligibility excludes adjacent floors");
        Fault(()=>PilotProgress.SelectEvent(3));Check(PilotProgress.Selected=="","failed event selection leaves no partial assignment");
        string selected=PilotProgress.SelectEvent(3);Reload();
        for(int i=0;i<100;i++) {ProductionRandom.Cosmetic.Next();ProductionRandom.Audio.Next();}
        Check(PilotProgress.SelectEvent(3)==selected,"event selection persists across reload and unrelated random consumption");Row("event",selected,1);
        Check(!PilotProgress.OfferPending,"unseen optional event selection does not force a town dialogue");
        PilotProgress.BeginOffer();Reload();Check(PilotProgress.OfferPending,"an interrupted opened offer retains its required choice");
        Check((bool)Call(G,"ResumeRewards")&&G.Asking&&Save.Sparks==100,"town recovery resumes the opened courier offer before any payment");
        G.Answer(0);Check(PilotProgress.Choice==1&&PilotProgress.Shortcut,"recovered courier offer commits the selected immediate consequence");
        Call(G,"AbandonStory");Set(G,"dialog",null);Town();
        foreach(int choice in new[]{1,2})
        {
            Fresh();PilotProgress.SelectEvent(3);string token=PilotProgress.Token;
            Fault(()=>PilotProgress.Resolve(choice,token));Check(PilotProgress.Choice==0&&Save.Sparks==100&&!PilotProgress.Shortcut,"failed event resolution cannot partially pay or open the shortcut");
            Check(PilotProgress.Resolve(choice,token),"event branch "+choice+" commits");Reload();
            Check(PilotProgress.Choice==choice&&Save.Sparks==(choice==1?100:124)&&PilotProgress.Shortcut==(choice==1),"event branch "+choice+" reloads its exact immediate consequence");
            Check(!PilotProgress.Resolve(choice,token)&&!PilotProgress.Resolve(3-choice,token),"event re-entry cannot reroll the decision or repeat payment");
            Save.BestFloor=2;Save.Flush();Check(!PilotProgress.CompleteCallback(token),"later callback waits for the floor to be cleared");Save.BestFloor=3;Save.Flush();
            Fault(()=>PilotProgress.CompleteCallback(token));Check(!PilotProgress.Callback&&Save.Sparks==(choice==1?100:124),"interrupted later callback retains its exact unpaid entitlement");
            Check(PilotProgress.CompleteCallback(token),"later callback commits for branch "+choice);Reload();
            Check(PilotProgress.Callback&&Save.Sparks==(choice==1?120:124)&&!PilotProgress.CompleteCallback(token),"later callback pays once and retains the branch consequence");Row("choice","courier-"+choice,Save.Sparks-100);
            Fresh();PilotProgress.SelectEvent(3);Check(!PilotProgress.Resolve(choice,token)&&!PilotProgress.CompleteCallback(token),"old-campaign callbacks cannot mutate the new journey");
        }
        Fresh();Fault(()=>PilotProgress.OpenShortcut());Check(!PilotProgress.Shortcut,"interrupted latch opening retains the closed state");Check(PilotProgress.OpenShortcut(),"manual latch opens without an event or build prerequisite");Reload();Check(PilotProgress.Shortcut&&!PilotProgress.OpenShortcut(),"shortcut receipt survives reconstruction and replay");
        Fresh();byte[] supported=File.ReadAllBytes(JourneyStore.FilePath);JourneyStore.Transaction(()=>JourneyStore.SetInt("pilotVersion",2));byte[] future=File.ReadAllBytes(JourneyStore.FilePath);JourneyStore.ReloadForTest();
        bool rejected=false;try {JourneyStore.Ensure();}catch(NotSupportedException){rejected=true;}
        Check(rejected&&File.ReadAllBytes(JourneyStore.FilePath).SequenceEqual(future),"future pilot policy rejects loading before any runtime write and retains committed bytes");
        File.WriteAllBytes(JourneyStore.FilePath,supported);File.WriteAllBytes(JourneyStore.BackupPath,supported);Reload();
    }
    static HitResult Hit(Enemy target,float amount=10f)=>(HitResult)Call(P,"Hit",target,amount,false,0f,false,DamageKind.Physical,false,0f,0f);
    static void Combat()
    {
        Fresh();Town();var origin=new Vector3(500,0.05f,500);
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(G.LevelRoot);ground.transform.position=origin+Vector3.down*0.55f;ground.transform.localScale=new Vector3(50,1,50);Physics.SyncTransforms();
        P.NewRun();P.stats=new PlayerStats();P.Revive(origin,true);P.weak=1;P.transform.rotation=Quaternion.identity;
        var target=Enemy.Create(Enemy.Kind.Minion,origin+Vector3.forward*2,0);target.Training=true;
        State(Game.GameState.Playing);Set(G,"InTown",false);Set(P,"invuln",0f);
        target.Stagger(1);float basic=Hit(target).damage;target.CloseOpening();Check(Mathf.Abs(basic-22)<0.01,"baseline riposte remains 2.2x");
        Call(P,"StartHeavy",0.8f);float heavy=(float)Get(P,"moveDur");Call(P,"End");
        Town();PilotProgress.Learn("duelist");PilotProgress.Learn("duelist");State(Game.GameState.Playing);Set(G,"InTown",false);target.Stagger(1);P.burst=0;
        Check(Mathf.Abs(Hit(target).damage-27)<0.01&&P.burst>=16,"duelist signature changes actual riposte damage and Colour charge");
        Call(P,"StartThrow");Check(Mathf.Abs((float)Get(P,"throwCd")-1.75f)<0.001,"duelist pays its slower throw recharge tradeoff");Call(P,"End");
        Town();PilotProgress.Respec();PilotProgress.Learn("mobility");State(Game.GameState.Playing);Set(G,"InTown",false);
        P.Revive(origin,true);P.weak=1;Set(P,"invuln",0f);Set(P,"dashT",0.1f);Set(P,"perfectUsed",false);target.CloseOpening();
        Check(P.ReceiveHit(new DamageRequest(1,target.transform.position,0,source:target)).outcome==HitOutcome.Immune&&P.MasteryCounter>1,"mobility arms its counter from an actual perfect dodge");
        Set(P,"dashT",0f);Set(P,"dashGrace",0f);Set(P,"attackT",0f);
        Check(Mathf.Abs(Hit(target).damage-13)<0.01&&P.MasteryCounter==0,"mobility counter improves exactly one accepted light contact");Check(Mathf.Abs(Hit(target).damage-10)<0.01,"following contact cannot reuse the consumed counter");
        Call(P,"StartHeavy",0.8f);Check(Mathf.Abs((float)Get(P,"moveDur")/heavy-1.2f)<0.01,"mobility preserves animation contact while paying 20 percent longer heavy time");Call(P,"End");
        Town();PilotProgress.Learn("mobility");State(Game.GameState.Playing);Set(G,"InTown",false);Set(P,"invuln",0f);Set(P,"dashT",0.1f);Set(P,"perfectUsed",false);P.ReceiveHit(new DamageRequest(1,target.transform.position,0,source:target));Set(P,"dashT",0f);Set(P,"attackT",0f);target.CloseOpening();Hit(target);Check(target.Open,"second mobility node staggers its actual counter target");
        Town();PilotProgress.Respec();PilotProgress.Learn("breaker");PilotProgress.Learn("breaker");State(Game.GameState.Playing);Set(G,"InTown",false);
        P.Revive(origin,true);P.weak=1;target.CloseOpening();Set(target,"airT",0f);
        var edge=Enemy.Create(Enemy.Kind.Minion,origin+Vector3.forward*6.8f,0);edge.Training=true;
        Call(P,"StartHeavy",0.8f);float duration=(float)Get(P,"moveDur");Call(P,"TickMove",duration*0.51f);
        Check(target.LastTrainingDamage>10*4.5f&&edge.LastTrainingDamage>0,"breaker charged impact damages the centre and expanded-radius target");
        Check(Mathf.Abs(duration/heavy-1.2f)<0.01,"breaker pays its real heavy duration tradeoff");
        int xp=Save.Xp,sparks=Save.Sparks,daily=Save.DailyCount;target.ReceiveHit(new DamageRequest(float.MaxValue,origin,0,source:P));G.OnEnemyKilled(target);
        Check(!target.Dead&&Save.Xp==xp&&Save.Sparks==sparks&&Save.DailyCount==daily,"practice target survives maximum damage and cannot farm XP currency or daily progress");
        foreach(var enemy in new[]{target,edge})Object.DestroyImmediate(enemy.gameObject);Object.DestroyImmediate(ground);Call(P,"End");Fresh();
    }
    static NavMeshPath Path(Vector3 from,Vector3 to,float radius=0.6f,float height=2.2f)
    {
        var filter=new NavMeshQueryFilter{agentTypeID=NavField.Profile(radius,height),areaMask=NavMesh.AllAreas};
        if(!NavMesh.SamplePosition(from,out var a,1.5f,filter)||!NavMesh.SamplePosition(to,out var b,1.5f,filter))throw new Exception("Missing authored navigation socket "+from+" -> "+to);
        var path=new NavMeshPath();if(!NavMesh.CalculatePath(a.position,b.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Incomplete authored path "+from+" -> "+to);return path;
    }
    static float Length(NavMeshPath path) {float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);return length;}
    static readonly Vector3[] Road={new Vector3(0,0.05f,-28),new Vector3(20,0.05f,-28),new Vector3(20,0.05f,28),new Vector3(0,0.05f,28),new Vector3(0,0.05f,35)};
    static readonly Vector3[] Ridge={new Vector3(0,0.05f,-28),new Vector3(-20,0.05f,-28),new Vector3(-20,0.05f,-24),new Vector3(-20,3.25f,-12),new Vector3(-20,3.25f,12),new Vector3(-20,0.05f,24),new Vector3(-20,0.05f,28),new Vector3(0,0.05f,28),new Vector3(0,0.05f,35)};
    static void RouteChecks()
    {
        foreach(var shape in new[]{new[]{0.35f,1f},new[]{0.6f,2.2f},new[]{1.25f,4.1f}})
            foreach(var pair in new[]{new{Id="road",Points=Road},new{Id="ridge",Points=Ridge}})
            {
                Vector3 from=new Vector3(0,0.05f,-37);float total=0;
                foreach(var point in pair.Points){total+=Length(Path(from,point,shape[0],shape[1]));from=point;}
                Check(total>70,"complete "+pair.Id+" route for body "+shape[0]+" / "+shape[1]);Row("route",pair.Id+"-"+shape[0].ToString(System.Globalization.CultureInfo.InvariantCulture),total);
            }
        Check(WorldSupport.CanInteract(Game.PilotCourierAt+Vector3.back*2+Vector3.up*0.05f,Game.PilotCourierAt),"elevated courier is reachable and interactable on its own support");
        closedLength=Length(Path(new Vector3(-10,0.05f,0),new Vector3(10,0.05f,0)));
        Check(closedLength>40,"closed cross-pass requires a real detour");
        Check(GameObject.Find("Iven the courier")!=null,"eligible seeded courier is built on the elevated route");
    }
    static void StartWalk(Vector3[] points)
    {
        route=points;waypoint=0;walkFrames=0;walkStart=Time.realtimeSinceStartup;
        P.NewRun();P.stats=new PlayerStats();P.Revive(new Vector3(0,0.1f,-37),true);P.weak=1;G.enabled=P.enabled=true;State(Game.GameState.Playing);
        Set(G,"waveActive",false);Set(G,"awakening",false);Set(G,"hintStep",-1);Set(G,"hubPanel",0);
        Set(G,"InWilds",false);Set(G,"InTown",false);
    }
    static bool Walk()
    {
        if(Time.realtimeSinceStartup-walkStart>75)throw new Exception("baseline keyboard traversal stuck at "+P.transform.position+" toward "+route[waypoint]);
        var target=route[waypoint];var delta=target-P.transform.position;delta.y=0;
        if(delta.magnitude<1f&&Mathf.Abs(target.y-P.transform.position.y)<0.6f)
        {
            if(++waypoint==route.Length){InputSystem.QueueStateEvent(keyboard,new KeyboardState());Row("walk",stage==2?"road":"ridge",Time.realtimeSinceStartup-walkStart);return true;}
            target=route[waypoint];delta=target-P.transform.position;delta.y=0;
        }
        float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;G.Cam.Face(yaw);G.Cam.transform.rotation=Quaternion.Euler(12,yaw,0);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));walkFrames++;return false;
    }
    static void Pad(params GamepadButton[] buttons) {var state=new GamepadState();foreach(var button in buttons)state=state.WithButton(button);InputSystem.QueueStateEvent(pad,state);}
    static void Capture(string file)=>ScreenCapture.CaptureScreenshot("Logs/pilot-"+file+".png");
    static void Next(int value,double delay=0.3) {stage=value;next=EditorApplication.timeSinceStartup+delay;}
    static void Frame()
    {
        switch(stage)
        {
            case 0:
                Records();Combat();keyboard=InputSystem.AddDevice<Keyboard>();pad=InputSystem.AddDevice<Gamepad>();
                Save.ReducedEffects();Check(Save.Flashes==0&&Save.CameraMotion==0&&Save.MotionLines==0,"pilot traversal controller and practice scenarios run with reduced effects enabled");Row("comfort","reduced-effects",1);
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
                Fresh();Call(G,"BuildFloor",0,2);P.Revive(new Vector3(0,0.1f,-37),true);State(Game.GameState.Playing);Next(1);break;
            case 1:
                if(NavField.Current==null||!NavField.Current.Ready)return;
                RouteChecks();StartWalk(Road);Next(2,0);break;
            case 2:
                if(!Walk())return;Check(walkFrames>200,"baseline player physically traverses the combat road using actual keyboard movement");StartWalk(Ridge);Next(3,0);break;
            case 3:
                if(!Walk())return;Check(walkFrames>200,"baseline player physically traverses both ramps and the elevated route using actual keyboard movement");
                G.enabled=P.enabled=false;PilotProgress.OpenShortcut();Call(G,"ApplyPilotShortcut",true);Next(4);break;
            case 4:
                if(NavField.Current==null||!NavField.Current.Ready)return;
                float open=Length(Path(new Vector3(-10,0.05f,0),new Vector3(10,0.05f,0)));Check(open<closedLength*0.6f&&open<24,"opened cross-pass changes real navigation to the shorter route");Row("shortcut","closed",closedLength);Row("shortcut","open",open);
                Reload();Call(G,"BuildFloor",0,2);Next(5);break;
            case 5:
                if(NavField.Current==null||!NavField.Current.Ready)return;
                Check(!GameObject.Find("Cross-pass gate")&&Length(Path(new Vector3(-10,0.05f,0),new Vector3(10,0.05f,0)))<24,"reconstruction applies the persisted shortcut before baking navigation");
                CaptureWorld();Fresh();Call(G,"BuildHub");Town();Set(G,"prologueDue",false);Set(G,"hubPanel",5);P.NewRun();P.Revive(new Vector3(7,0.1f,-7),true);G.enabled=P.enabled=true;UiNavigation.Reset();Pad();Next(6,0.7);break;
            case 6:
                Check(UiNavigation.Controls.Count>=6&&UiNavigation.Controls.All(c=>c.rect.x>=0&&c.rect.y>=0&&c.rect.xMax<=UI.W+1&&c.rect.yMax<=UI.H+1),"mastery previews purchases respec and practice register within the usable screen");Pad(GamepadButton.South);Next(7);break;
            case 7:
                Pad();Check(PilotProgress.Branch=="mobility"&&PilotProgress.Rank==1,"controller confirmation purchases the actual focused mastery node");Capture("mastery");Next(8);break;
            case 8:
                Check(PilotProgress.Rank==1,"held confirmation cannot purchase an extra node");Fresh();Call(G,"BuildFloor",0,2);P.Revive(Game.PilotCourierAt+Vector3.back*2+Vector3.up*0.05f,true);State(Game.GameState.Playing);P.enabled=false;Call(G,"OpenCourierOffer");UiNavigation.Reset();Next(9,0.6);break;
            case 9:
                Check(G.Asking&&UiNavigation.Controls.Count(c=>c.enabled)==2,"both courier consequences are actual controller-accessible choices");Pad(GamepadButton.DpadRight);Next(10,0.15);break;
            case 10:Pad();Next(11,0.15);break;
            case 11:Pad(GamepadButton.South);Next(12,0.2);break;
            case 12:
                Pad();Check(PilotProgress.Choice==2&&Save.Sparks==124,"controller branch selection commits the field-kit payment before its dialogue");Set(G,"dialogChars",999f);Capture("event");Next(13);break;
            case 13:
                Call(G,"AbandonStory");Call(G,"BuildHub");Town();Set(G,"dialog",null);Set(G,"prologueDue",false);Set(G,"hubPanel",0);P.Revive(new Vector3(7,0.1f,-3),true);
                var spot=((IList)Get(G,"spots")).Cast<object>().First(s=>(string)Get(s,"name")=="Courier testimony");((Action)Get(spot,"act"))();
                Check(PilotProgress.Callback&&Save.Consequence("pilot:courier:reported"),"actual town conversation commits the later consequence");Set(G,"dialogChars",999f);Capture("callback");Next(14);break;
            case 14:
                Call(G,"AbandonStory");Set(G,"dialog",null);Town();Call(G,"StartPilotTraining");P.enabled=true;Next(15,2);break;
            case 15:
                Check(G.TrainingActive&&G.PracticeTarget!=null&&G.PracticeTarget.Training&&G.State==Game.GameState.Playing,"practice button enters the real isolated training arena");
                int xp=Save.Xp,coins=Save.Sparks,best=Save.BestFloor;G.PracticeTarget.ReceiveHit(new DamageRequest(float.MaxValue,P.transform.position,0,source:P));G.OnEnemyKilled(G.PracticeTarget);
                Check(Save.Xp==xp&&Save.Sparks==coins&&Save.BestFloor==best&&!G.PracticeTarget.Dead,"actual training actor cannot pay kill or floor rewards");
                int hearts=P.stats.maxHearts;P.stats.maxHearts=1;P.Revive(G.PracticeTarget.transform.position+Vector3.back*2.5f,true);Set(P,"invuln",0f);
                var practiceHit=P.ReceiveHit(new DamageRequest(1,G.PracticeTarget.transform.position,0,source:G.PracticeTarget));
                Check(!P.Dead&&P.hp==1&&practiceHit.damage==0,"practice remains nonlethal even for a legal one-heart build");
                P.stats.maxHearts=hearts;P.Revive(G.PracticeTarget.transform.position+Vector3.back*2.5f,true);
                G.PracticeTarget.ReceiveHit(new DamageRequest(10,P.transform.position,0,source:P));Next(16,6.5);break;
            case 16:
                Check(!P.Dead&&!G.DefeatPending&&G.TrainingActive&&G.LevelRoot.GetComponent<PilotTraining>().AcceptedStrikes>=2,"repeated actual signalled practice strikes reach the player without defeating the hero");Capture("training");P.Revive(G.PracticeTarget.transform.position+Vector3.back*8,true);Pad(GamepadButton.North);Next(17,0.2);break;
            case 17:Pad();Next(18,2);break;
            case 18:
                Check(G.InTown&&!G.TrainingActive&&!Enemy.All.Any(e=>e!=null&&e.Training),"controller return marker restores town and releases every training actor");
                G.enabled=P.enabled=false;Call(G,"BuildFloor",0,2);State(Game.GameState.Playing);lifeCycle=0;Next(19,1);break;
            case 19:
                if(NavField.Current==null||!NavField.Current.Ready)return;
                int[] counts={GeneratedResources.LiveAssets,Resources.FindObjectsOfTypeAll<Mesh>().Length,Resources.FindObjectsOfTypeAll<Material>().Length};
                if(lifeCycle==1)warmNative=counts;
                if(lifeCycle>=2)Check(counts.SequenceEqual(warmNative),"pilot reconstruction "+lifeCycle+" retains exact warmed native ownership counts");
                Row("lifecycle","pilot-"+lifeCycle,counts[0]);
                if(++lifeCycle<4){Call(G,"BuildHub");Call(G,"BuildFloor",0,2);Next(19,1);return;}
                Call(G,"BuildHub");Town();Save.BestFloor=20;Save.TowerFloor=20;Save.Flush();G.enabled=P.enabled=true;Set(G,"prologueDue",false);Call(G,"ReturnToPilot");Next(20,2);break;
            case 20:
                if(NavField.Current==null||!NavField.Current.Ready)return;
                Check(G.Floor==3&&Save.TowerFloor==20&&Save.BestFloor==20&&(string)Get(Get(G,"stairSpot"),"name")=="The Way Home","liberated crossing revisit retains earned tower progress and redirects stairs home");
                var chest=((IList)Get(G,"spots")).Cast<object>().First(s=>(string)Get(s,"name")=="Treasure");int carried=Save.CarriedSparks;var openChest=(Action)Get(chest,"act");openChest();openChest();
                Check(Save.TowerChest(3,0)&&Save.CarriedSparks==carried+12,"revisited legacy chest identity pays exactly once");
                Call(G,"BuildFloor",0,2);Check(!((IList)Get(G,"spots")).Cast<object>().Any(s=>(string)Get(s,"name")=="Treasure"),"reconstruction cannot refill the earned overlook chest");
                Check(errors.Count==0,"pilot runtime contains no unexpected errors"+(errors.Count>0?": "+errors[0]:""));Check(JourneyStore.Error==null,"pilot leaves the isolated journey healthy");
                File.WriteAllText("Reports/pilot-evidence.csv",evidence.ToString());Debug.Log("PILOT DONE "+passed+" checks passed");SessionState.SetBool(Session,false);EditorApplication.Exit(0);break;
        }
    }
    static void CaptureWorld()
    {
        var go=new GameObject("Pilot evidence camera");var cam=go.AddComponent<Camera>();cam.transform.position=new Vector3(65,70,-80);cam.transform.LookAt(new Vector3(0,0,0));cam.fieldOfView=50;cam.farClipPlane=250;
        var rt=new RenderTexture(1400,1000,24);var previous=RenderTexture.active;var pixels=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        bool fog=RenderSettings.fog;
        try{RenderSettings.fog=false;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1400,1000),0,0);pixels.Apply();File.WriteAllBytes("Logs/pilot-floor.png",pixels.EncodeToPNG());}
        finally{RenderSettings.fog=fog;cam.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);Object.DestroyImmediate(go);}
    }
    static PilotTest(){if(SessionState.GetBool(Session,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
    public static void Run()
    {
        Save.Wipe();Save.SeenIntro=true;Save.Flush();SessionState.SetBool(Session,true);EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||G==null||P==null)return;
        if(deadline==0){deadline=EditorApplication.timeSinceStartup+330;next=EditorApplication.timeSinceStartup+2;Application.logMessageReceived-=OnLog;Application.logMessageReceived+=OnLog;}
        try{if(EditorApplication.timeSinceStartup>deadline)throw new Exception("pilot deadline");if(EditorApplication.timeSinceStartup<next)return;Frame();}
        catch(Exception e){JourneyStore.BeforeReplaceForTest=JourneyStore.AfterMutationForTest=null;Debug.LogError("PILOT FAILED stage="+stage+" "+e);SessionState.SetBool(Session,false);EditorApplication.Exit(1);}
    }
}
