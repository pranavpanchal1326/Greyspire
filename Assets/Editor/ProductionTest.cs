using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Profiling;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Debug = UnityEngine.Debug;

// Isolated seeded replay, authoritative-event, pause and lifecycle acceptance.
// Measurements are editor results on the recorded machine, not hardware certification.
[InitializeOnLoad]
public static class ProductionTest
{
    const string Session = "GreyspireProductionTest";
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    struct Step { public float wait; public Action run; }
    static readonly List<Step> steps = new List<Step>();
    static readonly List<string> errors = new List<string>();
    static readonly List<double> dashCosts = new List<double>(), frames = new List<double>();
    static readonly StringBuilder cycles = new StringBuilder("cycle,scope,owned_assets,meshes,materials,nav_agents,world_voices\n");
    static readonly Dictionary<string, int[]> warmed = new Dictionary<string, int[]>();
    static int passed, at, dashPeak, meshPeak, frame = -1;
    static double next, deadline;
    static bool stress;
    static int stressSeconds = 60;
    static int[] soakNative;
    static int soakDay;
    static long gcPeak;
    static double stressStarted;
    static Vector3 hubHeroAt;
    static ProfilerRecorder allocation;
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host, string name) => host.GetType().GetField(name, Any).GetValue(host);
    static void Set(object host, string name, object value) => (host.GetType().GetField(name, Any) ?? host.GetType().GetField("<"+name+">k__BackingField", Any)).SetValue(host, value);
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Debug.Log("PRODUCTION PASS " + message); }
    static void Do(float wait, Action action) => steps.Add(new Step { wait = wait, run = action });
    static void State(Game.GameState state) { Call(G, "SetState", state); Set(G,"fade",0f); Set(G,"fadeDir",0f); Set(G,"introT",0f); Set(G,"showMap",false); }
    static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text); }
    static ProductionTest() { if (SessionState.GetBool(Session,false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; } }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Reports"); Save.Wipe();
            JourneyStore.Transaction(() => { JourneyStore.SetInt("productionSeed", 734912); JourneyStore.SetInt("seedPolicy",1); });
            int root = ProductionRandom.Root;
            JourneyStore.ReloadForTest(); ProductionRandom.ClearRuntimeStreams();
            Check(root == ProductionRandom.Root && root == 734912, "root seed survives journal reload");
            var known = new RandomStream(1); Check(known.Next() == 270369 && known.Next() == 67634689, "versioned portable stream matches known vectors");
            for (int seed = 1; seed <= 8; seed++)
            {
                ProductionRandom.OverrideSeedForTest = seed;
                ProductionRandom.BeginScene("tower:08:gloom:0");
                uint combat = ProductionRandom.Combat.Next(), spawn = ProductionRandom.Spawn.Next(), ai = ProductionRandom.Stream("ai","actor:0001").Next();
                ProductionRandom.BeginScene("tower:08:gloom:0");
                for (int i=0;i<2048;i++) { _ = ProductionRandom.Cosmetic.Next(); _ = ProductionRandom.Audio.Next(); _ = ProductionRandom.Narrative.Next(); }
                Check(combat == ProductionRandom.Combat.Next() && spawn == ProductionRandom.Spawn.Next() && ai == ProductionRandom.Stream("ai","actor:0001").Next(), "seed " + seed + " cosmetic/audio/narrative consumption preserves gameplay replay");
                Check(ProductionRandom.Stream("ai","a").Next() != ProductionRandom.Stream("ai","b").Next() && ProductionRandom.Stream("drops","a").Next() != ProductionRandom.Stream("ai","a").Next(), "seed " + seed + " actor identity and domains are independent");
            }
            ProductionRandom.OverrideSeedForTest = -734912;
            uint invariant = ProductionRandom.Stream("combat","culture-fixture").Next();
            var originalCulture = CultureInfo.CurrentCulture;
            var alternateCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone(); alternateCulture.NumberFormat.NegativeSign = "~";
            try { CultureInfo.CurrentCulture=alternateCulture; Check(ProductionRandom.Stream("combat","culture-fixture").Next()==invariant,"negative seeds replay independently of number formatting culture"); Check(ProductionRandom.ChildId("parent",-1)=="parent:split:-1","split spawn identities are invariant under number formatting culture"); }
            finally { CultureInfo.CurrentCulture=originalCulture; }
            ProductionRandom.OverrideSeedForTest = null; ProductionRandom.ClearRuntimeStreams();
            Upgrades.Reset(); JourneyStore.Transaction(() => JourneyStore.SetInt("rng:loot",1234)); int initial = JourneyStore.GetInt("rng:loot");
            var offer = JourneyStore.Transaction(() => { var chosen = Upgrades.Roll(3).Select(u=>u.id).ToArray(); JourneyStore.SetInt("rng:loot",initial); return chosen; });
            JourneyStore.BeforeReplaceForTest = path => { if (path == JourneyStore.FilePath) throw new IOException("injected loot commit"); };
            try { Upgrades.Roll(3); throw new Exception("expected interrupted loot write"); } catch (IOException) { }
            JourneyStore.BeforeReplaceForTest = null; JourneyStore.ReloadForTest();
            Check(JourneyStore.GetInt("rng:loot") == initial, "failed offer rolls back its persistent random state");
            Check(Upgrades.Roll(3).Select(u=>u.id).SequenceEqual(offer), "retry after interruption produces the same offered cards");
            int committed = JourneyStore.GetInt("rng:loot"); JourneyStore.ReloadForTest();
            Check(JourneyStore.GetInt("rng:loot") == committed && committed != initial, "successful offer advances persistent stream once");
            byte[] supported = File.ReadAllBytes(JourneyStore.FilePath);
            JourneyStore.Transaction(() => JourneyStore.SetInt("seedPolicy",ProductionRandom.PolicyVersion+1));
            byte[] future = File.ReadAllBytes(JourneyStore.FilePath); JourneyStore.ReloadForTest();
            bool rejected = false; try { JourneyStore.Ensure(); } catch(NotSupportedException) { rejected = true; }
            Check(rejected && JourneyStore.Error!=null,"unsupported future stream policy rejects the journey before runtime writes");
            Check(File.ReadAllBytes(JourneyStore.FilePath).SequenceEqual(future),"future-policy rejection preserves the exact committed journey bytes");
            File.WriteAllBytes(JourneyStore.FilePath,supported); File.WriteAllBytes(JourneyStore.BackupPath,supported); JourneyStore.ReloadForTest(); ProductionRandom.ClearRuntimeStreams();
            FloorCatalog.Validate();
            var table = new StringBuilder("id,number,theme,layout,stratum,layout_seed,guardian,intro,heart,boss,rescue\n");
            for (int number=1;number<=30;number++)
            {
                var f=FloorCatalog.At(number);
                Check(f.theme==(number-1)/10 && f.layout==(number-1)%10 && FloorCatalog.Legacy(f.theme,f.layout).number==number, "floor " + number + " preserves legacy theme/layout and number");
                Check(f.LayoutSeed==(number==30 ? 1002 : 5000+((number-1)/10)*100+(number-1)%10), "floor " + number + " retains layout seed");
                Check(f.intro==(number==1||number==11||number==21) && f.heart== (number%10==0) && f.boss==(number==30) && Game.GuardianFloor(number)==(number%5==0&&number<30), "floor " + number + " preserves intro/heart/boss/Guardian beats");
                table.AppendLine(string.Join(",",f.id,number,f.theme,f.layout,f.stratum,f.LayoutSeed,f.guardian,f.intro,f.heart,f.boss,Story.RescueOnFloor(number)));
            }
            File.WriteAllText("Reports/floor-definitions.csv",table.ToString());
            Check(FloorCatalog.All.Select(f=>f.stratum).Distinct().Count()==6, "six grouping IDs cover the preserved thirty floors");
            Save.SeenIntro=true; Save.Level=20; Save.StatPoints=5; Save.StoryFloor=30; Save.Sparks=500; Save.Flush();
            File.WriteAllText("Reports/production-manifest.txt", "build="+ProductionRandom.BuildVersion+"\ncontent="+ProductionRandom.ContentVersion+"\nsave_schema="+JourneyStore.SchemaVersion+"\nseed_policy="+ProductionRandom.PolicyVersion+"\nroot_seed="+ProductionRandom.Root+"\ncampaign="+JourneyStore.Campaign+"\nunity="+Application.unityVersion+"\nos="+SystemInfo.operatingSystem+"\ncpu="+SystemInfo.processorType+"\ngpu="+SystemInfo.graphicsDeviceName+"\nram_mb="+SystemInfo.systemMemorySize+"\n");
            SessionState.SetInt(Session+"Passed",passed); SessionState.SetBool(Session,true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Authored light fixture").AddComponent<Light>().intensity=0.37f;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
        }
        catch(Exception e) { Debug.LogError("PRODUCTION FAILED pure "+e); SessionState.SetBool(Session,false); EditorApplication.Exit(1); }
        finally { JourneyStore.BeforeReplaceForTest=JourneyStore.AfterMutationForTest=null; }
    }
    static void Runtime()
    {
        Application.runInBackground = true;
        passed=SessionState.GetInt(Session+"Passed",0); State(Game.GameState.Hub);
        string[] args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++) if(args[i]=="-greyspire-stress-seconds" && int.TryParse(args[i+1],out int requested)) stressSeconds=Mathf.Clamp(requested,60,900);
        File.AppendAllText("Reports/production-manifest.txt","render_width="+Screen.width+"\nrender_height="+Screen.height+"\nquality="+Save.Quality+"\nantialiasing="+QualitySettings.antiAliasing+"\n");
        Check(GameObject.Find("Authored light fixture") != null && Math.Abs(GameObject.Find("Authored light fixture").GetComponent<Light>().intensity-0.37f)<0.0001f, "authored scene light survives normal game startup");
        Check(GameObject.Find("Sun").transform.parent==G.transform,"generated sunlight belongs to the game owner");
        Check(PlaytestLog.Active==null,"normal startup creates no opt-in telemetry session");
        Do(0.1f,Replay);
        Do(0.1f,() => { Call(G,"BuildHub"); State(Game.GameState.Hub); P.Revive(new Vector3(0,0.1f,-24),false); });
        Do(0.2f,() => { hubHeroAt=P.transform.position; InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W)); });
        Do(0.6f,() => { InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); Check(Vector3.Distance(hubHeroAt,P.transform.position)>0.5f,"actual keyboard movement remains live on the town world clock"); });
        Do(0.1f,Telemetry);
        Do(0.2f,() => { State(Game.GameState.Hub); for(int i=0;i<24;i++) Fx.Ghost(P.GetComponentInChildren<SkinnedMeshRenderer>().transform.parent,UI.Blue); Check(G.LevelRoot.GetComponentInChildren<DashGhostPool>().Created<=DashGhostPool.Capacity && G.LevelRoot.GetComponentInChildren<DashGhostPool>().Dropped>0,"oversubscribed dash visuals respect the hard object bound"); });
        Do(0.6f,() => Check(UnityEngine.Object.FindObjectsByType<GhostFade>().Length==0,"every reused ghost becomes inactive after its lifetime"));
        for(int i=0;i<50;i++) Do(0.025f,() => { var watch=Stopwatch.StartNew(); Fx.Ghost(P.GetComponentInChildren<SkinnedMeshRenderer>().transform.parent,UI.Blue); watch.Stop(); dashCosts.Add(watch.Elapsed.TotalMilliseconds); dashPeak=Math.Max(dashPeak,UnityEngine.Object.FindObjectsByType<GhostFade>().Length); });
        Do(0.6f,() => { var pool=G.LevelRoot.GetComponentInChildren<DashGhostPool>(); Check(pool.Created<=DashGhostPool.Capacity && UnityEngine.Object.FindObjectsByType<GhostFade>().Length==0,"warm dash pool stays bounded and releases active poses"); dashCosts.Sort(); File.WriteAllText("Reports/dash-reuse.csv","calls,median_ms,p95_ms,peak_live_pieces,created_slots,baked_meshes,remaining_active\n50,"+N(dashCosts[25])+","+N(dashCosts[47])+","+dashPeak+","+pool.Created+","+pool.GetComponentsInChildren<GhostFade>(true).Count(f=>f.baked!=null)+",0\n"); });
        Do(0.1f,DenseCamera);
        AddPauseChecks(); AddStoryChecks();
        for(int cycle=0;cycle<6;cycle++)
        {
            int c=cycle;
            foreach(string scene in new[]{"town","floor","wilds"})
            {
                string scope=scene;
                Do(0.1f,() => { G.AbandonStory(); State(Game.GameState.Hub); Set(G,"level",0); Set(G,"wave",3); Set(G,"DefeatPending",false); Set(G,"InWilds",scope=="wilds"); if(scope=="town") Call(G,"BuildHub"); else if(scope=="floor") Call(G,"BuildFloor",0,3); else Call(G,"BuildWilds"); P.Revive(new Vector3(0,0.1f,-37),false); });
                Do(1.4f,() => ResourceSample(c,scope));
            }
        }
        Do(0.1f,() => { Set(G,"InWilds",false); Call(G,"BuildHub"); State(Game.GameState.Hub); frames.Clear(); allocation=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1); stressStarted=Time.realtimeSinceStartupAsDouble; soakNative=new[]{GeneratedResources.LiveAssets,Resources.FindObjectsOfTypeAll<Mesh>().Length,Resources.FindObjectsOfTypeAll<Material>().Length}; soakDay=Save.Day; Debug.Log("PRODUCTION SOAK start day="+Save.Day+" hour="+Save.Hour); stress=true; });
        for(int i=0;i<stressSeconds*2;i++) Do(0.5f,() => { Fx.Ghost(P.GetComponentInChildren<SkinnedMeshRenderer>().transform.parent,UI.Blue); Fx.Burst(P.transform.position+Vector3.up,UI.Blue,12); for(int n=0;n<16;n++) GameAudio.Play(n%4==0?"roar":"hit",position:P.transform.position+Vector3.up); PlaytestLog.Emit("stress_probe",value:Time.frameCount); });
        Do(0.6f,Finish);
    }
    static string N(double n)=>n.ToString("F4",CultureInfo.InvariantCulture);
    static void Replay()
    {
        State(Game.GameState.Hub); ProductionRandom.BeginScene("replay-fixture");
        var a=Enemy.Create(Enemy.Kind.Minion,new Vector3(4,0.1f,6),0); a.enabled=false;
        string identity=a.SpawnId; string model=string.Join("|",a.GetComponentsInChildren<Renderer>().Select(r=>r.name)); float cooldown=(float)Get(a,"cooldown"); uint ai=((RandomStream)Get(a,"aiRandom")).Next(); uint drop=((RandomStream)Get(a,"dropRandom")).Next();
        UnityEngine.Object.Destroy(a.gameObject);
        ProductionRandom.BeginScene("replay-fixture");
        for(int i=0;i<30;i++) { Fx.Burst(Vector3.up,UI.Blue,8); GameAudio.Play("hit",position:Vector3.up); }
        var b=Enemy.Create(Enemy.Kind.Minion,new Vector3(4,0.1f,6),0); b.enabled=false;
        Check(b.SpawnId==identity && (float)Get(b,"cooldown")==cooldown && string.Join("|",b.GetComponentsInChildren<Renderer>().Select(r=>r.name))==model && ((RandomStream)Get(b,"aiRandom")).Next()==ai && ((RandomStream)Get(b,"dropRandom")).Next()==drop,"real enemy appearance/AI/drop state replays despite extra production effects and sounds");
        File.WriteAllText("Reports/seed-replay.csv","scope,actor,initial_cooldown,next_ai,next_drop,model\nreplay-fixture,"+identity+","+N(cooldown)+","+ai+","+drop+",\""+model+"\"\n"); UnityEngine.Object.Destroy(b.gameObject);
    }
    static void Telemetry()
    {
        State(Game.GameState.Playing); Set(G,"awakening",false); Set(G,"waveActive",false); Save.BeginExpedition(false); P.NewRun();
        var log=PlaytestLog.Begin("acceptance",Path.GetFullPath("Reports/telemetry"));
        var big=Enemy.Create(Enemy.Kind.BigSlime,new Vector3(10,0.1f,5),0); big.enabled=false; P.BeginAction("fixture-heavy");
        var result=big.ReceiveHit(new DamageRequest(10000,P.transform.position,0,source:P));
        Check(result.outcome==HitOutcome.Killed && log.Kills==1,"split parent is recorded as one authoritative kill despite growing enemy list");
        G.OnEnemyKilled(big); Check(log.Kills==1,"replayed kill callback produces no duplicate telemetry reward");
        var children=Enemy.All.Where(e=>e.SpawnId.StartsWith(big.SpawnId+":split:")).ToArray(); Check(children.Length==2,"split children inherit stable parent-based spawn identities");
        foreach(var child in children) { child.enabled=false; child.Vanish(); }
        Check(log.Kills==1,"silent child despawns produce zero kill rewards");
        var retry=Enemy.Create(Enemy.Kind.Slime,new Vector3(12,0.1f,6),0); retry.enabled=false;
        int kills=log.Kills; int gameKills=(int)Get(G,"kills"); int daily=Save.DailyCount;
        JourneyStore.BeforeReplaceForTest=path=>{ if(path==JourneyStore.FilePath) throw new IOException("injected kill write"); };
        try { G.OnEnemyKilled(retry); throw new Exception("expected failed kill transaction"); } catch(TargetInvocationException e) when(e.InnerException is IOException) { } catch(IOException) { }
        JourneyStore.BeforeReplaceForTest=null; JourneyStore.ReloadForTest();
        Check(log.Kills==kills && (int)Get(G,"kills")==gameKills && Save.DailyCount==daily && !retry.KillRewardsCommitted,"interrupted reward publishes no successful kill event or runtime kill count");
        G.OnEnemyKilled(retry); G.OnEnemyKilled(retry); Check(log.Kills==kills+1,"successful retry emits exactly one committed reward"); retry.Vanish();
        P.NewRun(); P.stats.secondWinds=1; P.hp=1; Set(P,"invuln",0f);
        result=P.ReceiveHit(new DamageRequest(100,P.transform.position,0));
        Check(result.outcome==HitOutcome.Revived && log.HitsTaken==1 && log.Falls==0,"Second Wind reports actual lethal contact as revival without a fall");
        P.NewRun(); P.stats.secondWinds=0; P.hp=1; Set(P,"invuln",0f);
        result=P.ReceiveHit(new DamageRequest(100,P.transform.position,0));
        Check(result.outcome==HitOutcome.Killed && log.HitsTaken==2 && log.Falls==1,"lethal player contact is counted alongside one authoritative defeat");
        Set(G,"DefeatPending",false); ((System.Collections.IList)Get(G,"timers")).Clear(); P.NewRun(); State(Game.GameState.Hub); Juice.Reset();
        for(int i=0;i<512;i++) PlaytestLog.Emit("batch_probe",value:i,note:"Unicode café, \"quoted\"\nsecond line");
        Check(log.Pending<PlaytestLog.BatchSize && log.Failure==null,"append batches bound buffered memory under sustained events");
        log.Flush(); Check(File.ReadAllText(log.FilePath).Contains("\"\"quoted\"\""),"CSV preserves escaped quoted Unicode and newlines");
    }
    static void DenseCamera()
    {
        var root=new GameObject("Dense camera fixture"); Vector3 origin=new Vector3(1000,50,0);
        for(int i=0;i<72;i++) { var go=new GameObject("Ignored actor "+i); go.transform.SetParent(root.transform); go.transform.position=origin+Vector3.forward*(0.4f+i*0.12f); go.AddComponent<ProductionProbeActor>(); go.AddComponent<SphereCollider>().radius=0.08f; }
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform); wall.transform.position=origin+Vector3.forward*9.5f; wall.transform.localScale=new Vector3(3,3,0.2f); Physics.SyncTransforms();
        int before=G.Cam.SaturatedQueries; float safe=G.Cam.SafeBoom(origin,Vector3.forward*12);
        Check(G.Cam.SaturatedQueries>before && safe>8.5f && safe<9.3f,"72 ignored actors cannot hide the nearest wall when camera buffer saturates"); UnityEngine.Object.Destroy(root);
    }
    static Projectile pauseBolt;
    static Pickup pausePickup;
    static Vector3 boltAt,pickupAt,lumiAt,cameraAt;
    static float simulationAt;
    static Enemy pauseEnemy;
    static Vector3 enemyAt;
    static float enemyCooldown;
    static int pausedTimer;
    static double heroClipAt;
    static PlayableGraph heroGraph;
    static void AddPauseChecks()
    {
        Do(0.1f,() => { State(Game.GameState.Playing); Set(G,"waveActive",false); P.Revive(new Vector3(0,0.1f,-20),false); Projectile.Fire(new Vector3(0,10,-15),Vector3.forward); pauseBolt=UnityEngine.Object.FindObjectsByType<Projectile>().First(); Pickup.Spawn(Pickup.Kind.Coin,new Vector3(5,4,-20),1); pausePickup=UnityEngine.Object.FindObjectsByType<Pickup>().First(); });
        Do(0.1f,() => { pauseEnemy=Enemy.Create(Enemy.Kind.Spitter,new Vector3(8,0.1f,-17),0); Set(pauseEnemy,"cooldown",-1f); Call(G,"After",0f,(Action)(()=>pausedTimer++)); Call(G,"SetMap",true); });
        Do(0.1f,() => { boltAt=pauseBolt.transform.position; pickupAt=pausePickup.transform.position; lumiAt=UnityEngine.Object.FindAnyObjectByType<Lumi>().transform.position; cameraAt=G.Cam.transform.position; simulationAt=GameClocks.SimulationTime; heroGraph=(PlayableGraph)Get(P.GetComponent<CharAnim>(),"graph"); heroClipAt=heroGraph.GetRootPlayable(0).GetTime(); enemyAt=pauseEnemy.transform.position; enemyCooldown=(float)Get(pauseEnemy,"cooldown"); });
        Do(1.5f,() => { Check(GameClocks.HardPause && GameClocks.SimulationDelta==0 && GameClocks.WorldDelta==0 && GameClocks.PresentationDelta==0,"map hard pause suspends all named world clocks"); Check(pauseBolt.transform.position==boltAt && pausePickup.transform.position==pickupAt && UnityEngine.Object.FindAnyObjectByType<Lumi>().transform.position==lumiAt && G.Cam.transform.position==cameraAt && GameClocks.SimulationTime==simulationAt,"real projectile pickup companion-light camera and simulation time hold through pause"); Check(pauseEnemy.transform.position==enemyAt && (float)Get(pauseEnemy,"cooldown")==enemyCooldown && pausedTimer==0,"ready enemy actions and zero-delay simulation callbacks remain held by map pause"); Check(heroGraph.GetRootPlayable(0).GetTime()==heroClipAt,"actual character Playables graph remains on its retained pose during hard pause"); Call(G,"SetMap",false); });
        Do(0.2f,() => { Check(pausedTimer==1,"held zero-delay callback executes once after resume"); UnityEngine.Object.Destroy(pauseEnemy.gameObject); Check(pauseBolt!=null && pauseBolt.transform.position!=boltAt,"projectile resumes from its retained position"); UnityEngine.Object.Destroy(pauseBolt.gameObject); UnityEngine.Object.Destroy(pausePickup.gameObject); State(Game.GameState.Hub); });
    }
    static StoryScene story;
    static double storyTime;
    static Animator storyActor;
    static bool storyDone;
    static float idlePoseAt;
    static void AddStoryChecks()
    {
        Do(0.1f,() => { Call(G,"BuildFloor",0,3); var stone=G.LevelRoot.GetComponentInChildren<Drained>(); storyActor=stone.GetComponentInChildren<Animator>(); story=StoryScene.Stage(stone.transform,storyActor,P.transform.position); State(Game.GameState.Story); story.Play("Rescue",()=>storyDone=true); Juice.Slow(5f); });
        Do(0.4f,() => { storyTime=story.GetComponent<PlayableDirector>().time; Check(storyTime>0.25,"Timeline presentation advances independently of slow world time"); Call(G,"OpenPause",0); });
        Do(0.2f,() => storyTime=story.GetComponent<PlayableDirector>().time);
        Do(1.6f,() => { Check(!storyDone && story.Now=="Rescue" && Math.Abs(story.GetComponent<PlayableDirector>().time-storyTime)<0.001,"intentional Story pause retains Timeline and does not trigger the stall watchdog"); Call(G,"ClosePause"); });
        Do(0.3f,() => { Check(story.GetComponent<PlayableDirector>().time>storyTime && story.Playing,"Timeline resumes at retained presentation time"); story.Skip(); Check(storyDone,"skipping a resumed Timeline runs its completion once"); storyActor.Play("Idle",0,0f); P.GetComponent<CharAnim>().Once("Hit_A",0.4f); Juice.Slow(5f); });
        Do(0.6f,() => { Check(!P.GetComponent<CharAnim>().Busy,"presentation gesture commitment expires on the same unscaled clock as its clip"); Call(G,"OpenPause",0); });
        Do(0.2f,() => idlePoseAt=storyActor.GetCurrentAnimatorStateInfo(0).normalizedTime);
        Do(1.3f,() => { Check(storyActor.speed==0f && storyActor.GetCurrentAnimatorStateInfo(0).normalizedTime==idlePoseAt,"dialogue actor controller retains its pose while paused between Timelines"); Call(G,"ClosePause"); });
        Do(0.3f,() => { Check(storyActor.speed>0f && storyActor.GetCurrentAnimatorStateInfo(0).normalizedTime>idlePoseAt,"dialogue actor controller resumes its retained animation clock"); Call(G,"OpenPause",0); });
        Do(0.2f,() => { story.Close(); Check(storyActor.updateMode==AnimatorUpdateMode.Normal && storyActor.speed==1f,"closing paused Story restores original actor clock and speed"); State(Game.GameState.Hub); Juice.Reset(); });
    }
    static void ResourceSample(int cycle,string scope)
    {
        int[] values={GeneratedResources.LiveAssets,Resources.FindObjectsOfTypeAll<Mesh>().Length,Resources.FindObjectsOfTypeAll<Material>().Length};
        cycles.AppendLine(string.Join(",",cycle,scope,values[0],values[1],values[2],NavField.Agents.Count,GameAudio.ActiveThreats));
        File.WriteAllText("Reports/production-transitions.csv",cycles.ToString());
        File.WriteAllLines("Reports/native-"+cycle+"-"+scope+".csv", Resources.FindObjectsOfTypeAll<Mesh>().Select(m=>"mesh,"+m.name).Concat(Resources.FindObjectsOfTypeAll<Material>().Select(m=>"material,"+m.name)).GroupBy(n=>n).OrderBy(g=>g.Key).Select(g=>g.Key+","+g.Count()));
        Debug.Log("PRODUCTION SAMPLE "+cycle+"/"+scope+" owned="+values[0]+" meshes="+values[1]+" materials="+values[2]);
        Check(UnityEngine.Object.FindObjectsByType<DashGhostPool>().Length==0,"transition "+cycle+"/"+scope+" disposes the old dash scope");
        if(cycle==1) warmed[scope]=values;
        if(cycle>=2) { var baseline=warmed[scope]; Check(values[0]==baseline[0],scope+" cycle "+cycle+" owns the same bounded native asset count"); Check(values[1]<=baseline[1]+3 && values[2]<=baseline[2]+3,scope+" cycle "+cycle+" has no accumulating native mesh/material growth"); }
        meshPeak=Math.Max(meshPeak,values[1]);
    }
    static void Finish()
    {
        stress=false; allocation.Dispose();
        Check(GeneratedResources.LiveAssets<=soakNative[0]+DashGhostPool.Capacity && Resources.FindObjectsOfTypeAll<Mesh>().Length<=soakNative[1]+DashGhostPool.Capacity+3 && Resources.FindObjectsOfTypeAll<Material>().Length<=soakNative[2]+3,"sustained session retains bounded native mesh/material ownership");
        Debug.Log("PRODUCTION SOAK end day="+Save.Day+" hour="+Save.Hour);
        if(stressSeconds>=600) Check(Save.Day>soakDay,"ten-minute soak crosses the saved world-day boundary");
        Check(frames.Count>100,"long-session stress captured actual rendered editor frames");
        Check(G.LevelRoot.GetComponentInChildren<DashGhostPool>().Created<=DashGhostPool.Capacity && GameAudio.WorldVoiceCount==12,"long-session dash and audio pools retain their fixed bounds");
        var log=PlaytestLog.Active; log.End(); Check(log.Failure==null && log.Dropped==0,"local telemetry completes stress without lost buffered events");
        File.Copy(log.FilePath,"Reports/production-events.csv",true); UnityEngine.Object.DestroyImmediate(log.gameObject);
        var limit=PlaytestLog.Begin("limit",Path.GetFullPath("Reports/telemetry")); Set(limit,"bytes",PlaytestLog.MaximumBytes-1); PlaytestLog.Emit("limit_probe"); limit.Flush(); Check(limit.Failure!=null && limit.Dropped==1 && limit.Pending==0,"file-size limit drops a disclosed bounded batch and stops diagnostics"); UnityEngine.Object.DestroyImmediate(limit.gameObject);
        var failed=PlaytestLog.Begin("failure",Path.GetFullPath("Reports/production-manifest.txt")); Check(failed.Failure!=null,"unwritable telemetry destination is contained"); int carried=Save.CarriedSparks; G.Collect(Pickup.Kind.Coin,1); Check(Save.CarriedSparks>carried && JourneyStore.Error==null,"failed optional telemetry cannot block a committed gameplay reward"); UnityEngine.Object.DestroyImmediate(failed.gameObject);
        File.WriteAllText("Reports/production-transitions.csv",cycles.ToString()); frames.Sort();
        File.WriteAllText("Reports/production-stress.csv","duration_seconds,frames,median_frame_ms,p95_frame_ms,p99_frame_ms,peak_gc_allocated_frame_bytes,peak_native_meshes,owned_assets,dash_slots,world_voice_capacity\n"+N(Time.realtimeSinceStartupAsDouble-stressStarted)+","+frames.Count+","+N(frames[frames.Count/2])+","+N(frames[(int)(frames.Count*.95)])+","+N(frames[(int)(frames.Count*.99)])+","+gcPeak+","+meshPeak+","+GeneratedResources.LiveAssets+","+G.LevelRoot.GetComponentInChildren<DashGhostPool>().Created+",12\n");
        Check(errors.Count==0,"production scenarios contain no unexpected runtime errors"+(errors.Count>0?": "+errors[0]:"")); Check(JourneyStore.Error==null,"production leaves isolated journey healthy");
        Debug.Log("PRODUCTION DONE "+passed+" checks passed"); SessionState.SetBool(Session,false); EditorApplication.Exit(0);
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || G==null || P==null) return;
        try
        {
            if(deadline==0) { Runtime(); deadline=EditorApplication.timeSinceStartup+300+stressSeconds; next=EditorApplication.timeSinceStartup+1; }
            if(stress && frame!=Time.frameCount) { frame=Time.frameCount; frames.Add(Time.unscaledDeltaTime*1000); if(allocation.Valid) gcPeak=Math.Max(gcPeak,allocation.LastValue); }
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("production scenario deadline");
            if(EditorApplication.timeSinceStartup<next) return;
            steps[at].run(); at++; if(at<steps.Count) next=EditorApplication.timeSinceStartup+steps[at].wait;
        }
        catch(Exception e) { Debug.LogError("PRODUCTION FAILED step="+at+" "+e); SessionState.SetBool(Session,false); EditorApplication.Exit(1); }
    }
}
public sealed class ProductionProbeActor : Actor { protected override void Die() { } }
