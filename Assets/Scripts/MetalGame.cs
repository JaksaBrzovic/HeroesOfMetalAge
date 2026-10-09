using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HeroesOfMetalAge
{
    public class MetalGame : MonoBehaviour
    {
        const int W=18,H=11,BW=10,BH=7;
        const float TW=58,TH=54;
        readonly Color ink=new Color(.045f,.055f,.062f), panel=new Color(.075f,.087f,.094f), gold=new Color(.86f,.68f,.34f), cream=new Color(.90f,.88f,.80f), muted=new Color(.51f,.58f,.57f), red=new Color(.82f,.29f,.22f), teal=new Color(.33f,.69f,.61f);
        Campaign c;
        MetalAudio audioDirector;
        string screen="map",modal="",notice="",battleId="",resultText="";
        float noticeUntil;
        bool busy,settings,dev,showIntro=true;
        int[,] terrain=new int[W,H];
        List<Vector2Int> path=new List<Vector2Int>();
        Vector2Int destination=new Vector2Int(-1,-1);
        Texture2D hex,heroIcon,townIcon,axeIcon,bowIcon,riderIcon,mountainIcon,treeIcon;
        GUIStyle normal,small,title,heading,button,center,huge;
        Font font;
        int selectedArtifact=-1,qty=1;
        List<Fighter> fighters=new List<Fighter>();
        List<Fighter> order=new List<Fighter>();
        Fighter active;
        int turn,round;
        HashSet<Vector2Int> reachable=new HashSet<Vector2Int>();
        List<string> battleLog=new List<string>();
        string hitText="";
        float hitUntil;
        int lost,earnedXP,earnedGold,rewardArtifact=-1;
        bool smokeMode;
        string SavePath=>Path.Combine(Application.persistentDataPath,smokeMode?"smoke-campaign.json":"campaign.json");
        public bool Booted {get;private set;}
        public string CurrentScreen=>screen;

        class Fighter
        {
            public int kind,totalHp,startCount,x,y; public bool player,defend,retaliated;
            public int Count=>Mathf.Max(0,Mathf.CeilToInt(totalHp/(float)Rules.Units[kind].hp));
            public bool Alive=>totalHp>0;
        }
        void Awake()
        {
            Application.targetFrameRate=60;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            c=Campaign.New(); GenerateMap(); MakeArt();
            audioDirector=gameObject.AddComponent<MetalAudio>(); audioDirector.Initialize();
            audioDirector.musicVolume=PlayerPrefs.GetFloat("music",.52f); audioDirector.sfxVolume=PlayerPrefs.GetFloat("sfx",.7f); audioDirector.voiceVolume=PlayerPrefs.GetFloat("voice",.65f);
            Booted=true;
            smokeMode=Environment.GetCommandLineArgs().Contains("--smoke-test");
            if(smokeMode) StartCoroutine(Smoke());
        }
        IEnumerator Smoke()
        {
            Time.timeScale=12; showIntro=false; UnityEngine.Random.InitState(42);
            var reports=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","..","Reports"));Directory.CreateDirectory(reports);
            yield return new WaitForSecondsRealtime(1f);
            var report=new List<string>();
            Check(Booted && c.army.Count==1,"Runtime bootstrap",report);
            Check(FindPath(new Vector2Int(2,5),new Vector2Int(6,5)).Count>0,"World path",report);
            Check(FindPath(new Vector2Int(2,5),new Vector2Int(7,5)).Count>0&&FindPath(new Vector2Int(2,5),new Vector2Int(9,5)).Count==0,"Sentinel chokepoint",report);
            Plan(4,5);ScreenCapture.CaptureScreenshot(Path.Combine(reports,"01-world.png")); yield return new WaitForSecondsRealtime(.4f);
            Travel();while(busy)yield return new WaitForSecondsRealtime(.01f);
            Check(modal=="friendly"&&c.x==3&&c.movement==15,"Travel stops for friendly encounter",report);
            c.joined=true; Rules.Add(c.army,1,6);modal="";Plan(7,5);Travel();while(busy)yield return new WaitForSecondsRealtime(.01f);
            Check(modal=="sentinel"&&c.x==6,"Travel stops before hostile stack",report);BeginBattle("sentinel");
            Check(fighters.Count==3 && active!=null,"Battle bootstrap",report);
            yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(reports,"02-battle.png"));yield return new WaitForSecondsRealtime(.4f);
            yield return AutoBattle();
            Check(modal=="victory"&&c.sentinelDead&&c.xp==180&&c.gold==2100&&c.bag.Contains(0),"Sentinel victory and rewards",report);
            modal="";c.weapon=0;c.bag.Remove(0);screen="hero";yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(reports,"03-hero.png"));yield return new WaitForSecondsRealtime(.4f);
            screen="map";Plan(11,5);Travel();while(busy)yield return new WaitForSecondsRealtime(.01f);
            Check(screen=="town"&&c.x==11&&c.y==5,"Town reached through cleared pass",report);
            Check(Rules.Buy(c,0,10)&&Rules.Buy(c,1,5)&&Rules.Buy(c,2,3),"Town recruitment",report);
            yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(reports,"04-town.png"));yield return new WaitForSecondsRealtime(.4f);
            int stock=c.stock[0];for(int i=0;i<7;i++)Rules.EndDay(c);Check(c.day==8&&c.stock[0]==stock+14,"Weekly refresh in runtime",report);
            BeginBattle("town");yield return AutoBattle();Check(modal=="victory"&&c.townOwned,"Town conquest",report);modal="";
            Check(Rules.StationHero(c)&&c.heroHome&&c.army.Count==0&&Rules.Valid(c),"Hero home slot and army",report);
            yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(reports,"04b-owned-town.png"));yield return new WaitForSecondsRealtime(.4f);
            Check(Rules.StationHero(c)&&!c.heroHome&&c.army.Count>0,"Hero returns to visiting slot",report);
            Save(false);var restored=JsonUtility.FromJson<Campaign>(File.ReadAllText(SavePath));Check(Rules.Valid(restored)&&restored.army.Count==3,"Runtime save round trip",report);
            BeginBattle("boss");yield return AutoBattle();Check(modal=="victory"&&c.bossDead,"Recruited army defeats boss",report);
            yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(reports,"05-victory.png"));yield return new WaitForSecondsRealtime(.4f);
            modal="";c=Campaign.New();BeginBattle("boss");yield return AutoBattle();Check(modal=="defeat","Weak army defeat panel",report);
            yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(reports,"06-defeat.png"));yield return new WaitForSecondsRealtime(.4f);
            if(File.Exists(SavePath))File.Delete(SavePath);
            File.WriteAllText(Path.Combine(reports,"runtime-smoke.txt"),string.Join("\n",report));
            Application.Quit(report.Any(s=>s.StartsWith("FAIL"))?1:0);
        }
        IEnumerator AutoBattle()
        {
            int ticks=0;
            while(screen=="battle"&&modal==""&&ticks++<3000) {
                if(!busy&&active!=null&&active.player) {
                    var targets=fighters.Where(f=>f.Alive&&!f.player).OrderBy(f=>f.totalHp).ToList();
                    var target=targets.FirstOrDefault(f=>Rules.Units[active.kind].ranged||Adjacent(active,f)||Neighbors(new Vector2Int(f.x,f.y)).Any(p=>reachable.Contains(p)&&At(p.x,p.y)==null));
                    if(target!=null)BattleClick(target.x,target.y);
                    else if(targets.Count>0) { var nearest=targets.OrderBy(f=>HexDistance(new Vector2Int(active.x,active.y),new Vector2Int(f.x,f.y))).First();var p=reachable.OrderBy(q=>HexDistance(q,new Vector2Int(nearest.x,nearest.y))).First();if(At(p.x,p.y)==null)BattleClick(p.x,p.y);else Advance(); }
                }
                yield return new WaitForSecondsRealtime(.01f);
            }
        }
        void Check(bool ok,string label,List<string> report) { report.Add((ok?"PASS ":"FAIL ")+label); }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.F3)) dev=!dev;
            if(Input.GetKeyDown(KeyCode.Escape)) { if(settings) settings=false; else if(modal!="") { if(modal!="victory"&&modal!="defeat") modal=""; } else if(screen=="hero") screen=c.heroHome?"town":"map"; else if(screen=="town") LeaveTown(); else settings=true; }
            if(Input.GetKeyDown(KeyCode.M)) audioDirector.muted=!audioDirector.muted;
            if(!busy && !settings && modal=="" && !showIntro && screen=="map")
            {
                if(Input.GetKeyDown(KeyCode.Space)) Travel();
                if(Input.GetKeyDown(KeyCode.E)) NextDay();
                if(Input.GetKeyDown(KeyCode.H)) screen="hero";
            }
        }
        void GenerateMap()
        {
            for(int x=0;x<W;x++) for(int y=0;y<H;y++)
            {
                terrain[x,y]=(x*19+y*11)%9<3?1:0;
                if(y==5 || (y==4&&x>9)) terrain[x,y]=2;
                if(x==7&&y!=5) terrain[x,y]=3;
                if((x<3&&y<3)||(x>14&&y>8)) terrain[x,y]=4;
                if((x==5&&y==2)||(x==12&&y==8)||(x==13&&y==2)) terrain[x,y]=3;
            }
        }
        bool Inside(int x,int y)=>x>=0&&x<W&&y>=0&&y<H;
        bool Walkable(int x,int y)=>Inside(x,y)&&terrain[x,y]!=3&&terrain[x,y]!=4;
        int StepCost(Vector2Int p)=>terrain[p.x,p.y]==1?2:1;
        string Site(int x,int y)
        {
            if(x==4&&y==5&&!c.joined) return "friendly";
            if(x==7&&y==5&&!c.sentinelDead) return "sentinel";
            if(x==11&&y==5) return "town";
            if(x==15&&y==5&&!c.bossDead) return "boss";
            return "";
        }
        List<Vector2Int> FindPath(Vector2Int from,Vector2Int to)
        {
            var result=new List<Vector2Int>(); if(!Walkable(to.x,to.y)||from==to) return result;
            var open=new List<Vector2Int>{from}; var prev=new Dictionary<Vector2Int,Vector2Int>(); var cost=new Dictionary<Vector2Int,int>{{from,0}};
            while(open.Count>0)
            {
                var current=open.OrderBy(p=>cost[p]).First(); open.Remove(current); if(current==to) break;
                if(current!=from && Site(current.x,current.y)!="") continue;
                for(int dx=-1;dx<=1;dx++) for(int dy=-1;dy<=1;dy++)
                {
                    if(dx==0&&dy==0) continue; var n=current+new Vector2Int(dx,dy);
                    if(!Walkable(n.x,n.y)) continue;
                    if(dx!=0&&dy!=0 && (!Walkable(current.x+dx,current.y)||!Walkable(current.x,current.y+dy))) continue;
                    int nc=cost[current]+StepCost(n);
                    if(!cost.ContainsKey(n)||nc<cost[n]) { cost[n]=nc; prev[n]=current; if(!open.Contains(n)) open.Add(n); }
                }
            }
            if(!prev.ContainsKey(to)) return result;
            for(var p=to;p!=from;p=prev[p]) result.Add(p);
            result.Reverse(); return result;
        }
        void Plan(int x,int y)
        {
            if(busy||showIntro) return;
            if(x==c.x&&y==c.y) { if(Site(x,y)=="town") OpenTown(); return; }
            var target=new Vector2Int(x,y);
            if(destination==target&&path.Count>0) { Travel(); return; }
            destination=target; path=FindPath(new Vector2Int(c.x,c.y),target); audioDirector.Cue("click");
            if(path.Count==0) Notify("No route. Mountains and water block movement.");
        }
        void Travel()
        {
            if(busy||path.Count==0||screen!="map"||modal!="") return;
            if(c.heroHome) { screen="town";Notify("Move Kael to the visiting slot before riding.");return; }
            if(c.army.Count==0) { Notify("Your hero needs an army. Return to the town."); return; }
            if(c.movement<StepCost(path[0])) { Notify("Not enough movement. End the day [E]."); return; }
            StartCoroutine(MoveHero());
        }
        IEnumerator MoveHero()
        {
            busy=true; audioDirector.riding=true;
            var steps=new List<Vector2Int>(path); path.Clear();
            foreach(var p in steps)
            {
                if(c.movement<StepCost(p)) { Notify("Movement spent. End the day to continue."); break; }
                string site=Site(p.x,p.y);
                if(site=="friendly" || site=="sentinel" || site=="boss")
                {
                    audioDirector.riding=false; modal=site; audioDirector.Cue(site=="friendly"?"friendly":"enemy"); break;
                }
                c.movement-=StepCost(p); c.x=p.x; c.y=p.y;
                yield return new WaitForSeconds(.15f);
                if(site=="town") { OpenTown(); break; }
            }
            audioDirector.riding=false; busy=false; destination=new Vector2Int(-1,-1); Save(false);
        }
        void NextDay()
        {
            if(busy||screen=="battle") return;
            bool week=Rules.EndDay(c); path.Clear(); destination=new Vector2Int(-1,-1);
            Notify(week?"A new week begins. Town recruitment stocks have grown.":"Day "+c.day+". Your movement is restored.");
            audioDirector.Cue(week?"friendly":"click"); Save(false);
        }
        void OpenTown() { screen="town"; modal=""; qty=1; audioDirector.Cue("friendly"); }
        void LeaveTown() { if(c.heroHome)Notify("Kael guards the town. Click HOME / KAEL to move him to visiting.");else screen="map"; }
        void Notify(string text) { notice=text; noticeUntil=Time.unscaledTime+5; }
        void Save(bool notify=true)
        {
            if(screen=="battle" || modal=="defeat" || (c.army.Count==0&&c.garrison.Count==0)) { if(notify) Notify("Save is available on the adventure map and in town."); return; }
            try { string tmp=SavePath+".tmp"; File.WriteAllText(tmp,JsonUtility.ToJson(c,true)); File.Copy(tmp,SavePath,true); File.Delete(tmp); if(notify) Notify("Campaign saved."); }
            catch(Exception e) { Debug.LogException(e); Notify("Save failed. See the player log."); }
        }
        void Load()
        {
            if(busy||screen=="battle") return;
            try {
                var loaded=JsonUtility.FromJson<Campaign>(File.ReadAllText(SavePath));
                if(!Rules.Valid(loaded)) throw new Exception("Invalid campaign data");
                c=loaded; screen=c.heroHome?"town":"map"; modal=""; showIntro=false; path.Clear(); destination=new Vector2Int(-1,-1); audioDirector.StopBattle(); Notify("Campaign loaded.");
            } catch(Exception e) { Debug.LogWarning(e.Message); Notify("No valid saved campaign found."); }
        }
        void Restart()
        {
            StopAllCoroutines(); c=Campaign.New(); screen="map"; modal=""; busy=false; path.Clear(); destination=new Vector2Int(-1,-1); fighters.Clear(); audioDirector.StopBattle(); showIntro=true;
        }
        void BeginBattle(string id)
        {
            if(c.army.Count==0) { Notify("Recruit an army before fighting."); return; }
            battleId=id; screen="battle"; modal=""; fighters.Clear(); battleLog.Clear(); round=0; busy=false;
            for(int i=0;i<c.army.Count;i++) AddFighter(c.army[i],true,1,i);
            var enemies=id=="sentinel"?new[]{new Troop(0,10)}:id=="town"?new[]{new Troop(0,13),new Troop(1,5)}:new[]{new Troop(0,18),new Troop(1,9),new Troop(2,3)};
            for(int i=0;i<enemies.Length;i++) AddFighter(enemies[i],false,8,Mathf.Min(6,1+i*2));
            audioDirector.riding=false; audioDirector.fighting=true; audioDirector.Cue("intro");
            Log(id=="boss"?"The Black Banner answers your challenge.":id=="town"?"The Ashen Clan defends its stronghold.":"The pass wardens draw their axes."); NewRound();
        }
        void AddFighter(Troop t,bool player,int x,int y) { fighters.Add(new Fighter{kind=t.kind,totalHp=t.count*Rules.Units[t.kind].hp,startCount=t.count,player=player,x=x,y=y}); }
        int Speed(Fighter f)=>Rules.Units[f.kind].speed+(f.player?Rules.SpeedBonus(c):0);
        void NewRound()
        {
            round++; foreach(var f in fighters) { f.retaliated=false; f.defend=false; }
            order=fighters.Where(f=>f.Alive).OrderByDescending(Speed).ThenByDescending(f=>f.player).ToList(); turn=0; SetActive();
        }
        void SetActive()
        {
            while(turn<order.Count&&!order[turn].Alive) turn++;
            if(turn>=order.Count) { NewRound(); return; }
            active=order[turn]; active.defend=false; reachable=Reachable(active); audioDirector.Voice(active.kind);
            if(!active.player&&!busy) StartCoroutine(EnemyTurn());
        }
        void Advance()
        {
            if(CheckBattleEnd()) return; turn++; SetActive();
        }
        bool CheckBattleEnd()
        {
            bool own=fighters.Any(f=>f.Alive&&f.player), foe=fighters.Any(f=>f.Alive&&!f.player);
            if(own&&foe) return false;
            busy=false; audioDirector.StopBattle();
            if(!own) { modal="defeat"; audioDirector.Cue("defeat"); return true; }
            lost=fighters.Where(f=>f.player).Sum(f=>f.startCount-f.Count);
            c.army=fighters.Where(f=>f.player&&f.Alive).Select(f=>new Troop(f.kind,f.Count)).ToList();
            earnedXP=battleId=="boss"?450:battleId=="town"?250:180;
            earnedGold=battleId=="boss"?900:battleId=="town"?0:500;
            rewardArtifact=battleId=="sentinel"?0:battleId=="boss"?2:-1;
            int oldLevel=Rules.Level(c); c.xp+=earnedXP; c.gold+=earnedGold; if(rewardArtifact>=0)c.bag.Add(rewardArtifact);
            if(battleId=="sentinel")c.sentinelDead=true; if(battleId=="boss")c.bossDead=true; if(battleId=="town")c.townOwned=true;
            resultText=Rules.Level(c)>oldLevel?"LEVEL UP  /  Your hero is now level "+Rules.Level(c):"The fallen are remembered. The road is yours.";
            modal="victory"; audioDirector.Cue("victory");
            // Saving a battle outcome uses an adventure-state snapshot.
            screen=battleId=="town"?"town":"map"; Save(false); return true;
        }
        IEnumerable<Vector2Int> Neighbors(Vector2Int p)
        {
            int shift=p.y%2==0?-1:0;
            var offsets=new[]{new Vector2Int(-1,0),new Vector2Int(1,0),new Vector2Int(shift,-1),new Vector2Int(shift+1,-1),new Vector2Int(shift,1),new Vector2Int(shift+1,1)};
            foreach(var o in offsets) { var n=p+o; if(n.x>=0&&n.x<BW&&n.y>=0&&n.y<BH) yield return n; }
        }
        int HexDistance(Vector2Int a,Vector2Int b)
        {
            int ax=a.x-(a.y-(a.y&1))/2, bx=b.x-(b.y-(b.y&1))/2;
            return (Mathf.Abs(ax-bx)+Mathf.Abs(a.y-b.y)+Mathf.Abs((-ax-a.y)-(-bx-b.y)))/2;
        }
        Fighter At(int x,int y)=>fighters.Find(f=>f.Alive&&f.x==x&&f.y==y);
        HashSet<Vector2Int> Reachable(Fighter f)
        {
            var start=new Vector2Int(f.x,f.y); var dist=new Dictionary<Vector2Int,int>{{start,0}}; var queue=new Queue<Vector2Int>(); queue.Enqueue(start);
            while(queue.Count>0) {
                var p=queue.Dequeue(); if(dist[p]>=Speed(f)) continue;
                foreach(var n in Neighbors(p)) if(!dist.ContainsKey(n)&&At(n.x,n.y)==null) { dist[n]=dist[p]+1; queue.Enqueue(n); }
            }
            return new HashSet<Vector2Int>(dist.Keys);
        }
        bool Adjacent(Fighter a,Fighter b)=>HexDistance(new Vector2Int(a.x,a.y),new Vector2Int(b.x,b.y))==1;
        void Attack(Fighter attacker,Fighter target,bool retaliation=false)
        {
            bool melee=Adjacent(attacker,target); int attack=attacker.player?Rules.Attack(c):1, defense=target.player?Rules.Defense(c):1;
            int damage=Rules.Damage(attacker.kind,attacker.Count,attack,target.kind,defense,target.defend,melee,UnityEngine.Random.value);
            int before=target.Count; target.totalHp=Mathf.Max(0,target.totalHp-damage);
            Log(Rules.Units[attacker.kind].name+(retaliation?" retaliates":" strikes")+": "+damage+" damage / "+(before-target.Count)+" lost.");
            hitText=(target.player?"RECEIVED ":"DEALT ")+damage+" DAMAGE"; hitUntil=Time.unscaledTime+1.5f; audioDirector.Hit(damage,target.player);
            if(!retaliation&&melee&&target.Alive&&!target.retaliated) { target.retaliated=true; Attack(target,attacker,true); }
        }
        void BattleClick(int x,int y)
        {
            if(busy||active==null||!active.player||modal!="") return;
            var target=At(x,y);
            if(target!=null&&!target.player)
            {
                if(Adjacent(active,target)||Rules.Units[active.kind].ranged) { Attack(active,target); Advance(); }
                else {
                    var choices=Neighbors(new Vector2Int(target.x,target.y)).Where(p=>reachable.Contains(p)&&At(p.x,p.y)==null).OrderBy(p=>HexDistance(new Vector2Int(active.x,active.y),p)).ToList();
                    if(choices.Count>0) { active.x=choices[0].x; active.y=choices[0].y; Attack(active,target); Advance(); }
                    else Notify("Enemy is beyond your movement range.");
                }
            }
            else if(target==null&&reachable.Contains(new Vector2Int(x,y))) { active.x=x; active.y=y; Log(Rules.Units[active.kind].name+" advances."); Advance(); }
        }
        IEnumerator EnemyTurn()
        {
            busy=true; yield return new WaitForSeconds(.7f);
            if(screen!="battle"||active==null||active.player) { busy=false; yield break; }
            var target=fighters.Where(f=>f.player&&f.Alive).OrderBy(f=>HexDistance(new Vector2Int(active.x,active.y),new Vector2Int(f.x,f.y))).ThenBy(f=>f.totalHp).FirstOrDefault();
            if(target!=null) {
                if(Adjacent(active,target)||Rules.Units[active.kind].ranged) Attack(active,target);
                else {
                    var best=reachable.OrderBy(p=>HexDistance(p,new Vector2Int(target.x,target.y))).ThenBy(p=>p.x).First(); active.x=best.x; active.y=best.y;
                    if(Adjacent(active,target)) Attack(active,target); else Log(Rules.Units[active.kind].name+" closes in.");
                }
            }
            busy=false; Advance();
        }
        void Log(string text) { battleLog.Add(text); if(battleLog.Count>8) battleLog.RemoveAt(0); }

        void Styles()
        {
            if(normal!=null)return;
            normal=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true}; normal.normal.textColor=cream;
            small=new GUIStyle(normal){fontSize=14}; small.normal.textColor=muted;
            heading=new GUIStyle(normal){fontSize=24,fontStyle=FontStyle.Bold}; heading.normal.textColor=gold;
            title=new GUIStyle(heading){fontSize=34};
            huge=new GUIStyle(title){fontSize=58};
            center=new GUIStyle(normal){alignment=TextAnchor.MiddleCenter};
            button=new GUIStyle(center){fontSize=16,fontStyle=FontStyle.Bold};
        }
        void OnGUI()
        {
            Styles(); GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
            Box(new Rect(0,0,1600,900),ink); GUI.enabled=modal==""&&!settings&&!dev&&!showIntro; Header();
            if(screen=="map") World(); else if(screen=="battle") BattleView(); else if(screen=="town") TownView(); else HeroView();
            Footer(); GUI.enabled=true;
            if(Time.unscaledTime<noticeUntil) { Box(new Rect(310,795,980,42),new Color(.16f,.19f,.18f)); Label(new Rect(325,801,950,30),notice,center); }
            if(modal!="") Modal();
            if(showIntro) Intro();
            if(settings) SettingsView();
            if(dev) DevView();
        }
        void Header()
        {
            Box(new Rect(0,0,1600,84),panel); Box(new Rect(0,82,1600,2),gold);
            Label(new Rect(32,12,560,40),"HEROES OF METAL AGE",title); Label(new Rect(34,53,530,20),"THE ASHEN PASS   /   A SOUND-DRIVEN STRATEGY PROTOTYPE",small);
            Label(new Rect(760,19,220,30),"DAY "+c.day+"  /  WEEK "+((c.day-1)/7+1),normal);
            Label(new Rect(1000,19,210,30),"GOLD  "+c.gold,heading);
            if(Btn(new Rect(1230,20,130,40),"HERO [H]",screen!="battle"&&!busy)) screen=screen=="hero"?(c.heroHome?"town":"map"):"hero";
            if(Btn(new Rect(1380,20,185,40),"AUDIO / OPTIONS")) settings=true;
        }
        void Footer()
        {
            Box(new Rect(0,849,1600,51),panel);
            Label(new Rect(30,863,1050,24),screen=="map"?"CLICK TO PLAN  /  CLICK AGAIN OR SPACE TO RIDE  /  E END DAY  /  H HERO  /  M MUTE":screen=="battle"?"CLICK BLUE HEX TO MOVE  /  CLICK ENEMY TO ATTACK  /  ARCHERS CAN SHOOT AT RANGE":"ESC RETURN TO MAP  /  F3 DEVELOPMENT PANEL  /  M MUTE",small);
            Label(new Rect(1220,863,360,24),audioDirector.muted?"AUDIO MUTED":"AUDIO  /  "+(audioDirector.fighting?"BATTLE":audioDirector.riding?"GALLOP":"ASHEN AMBIENT"),small);
        }
        void World()
        {
            Label(new Rect(34,102,950,40),"THE ASHEN PASS",heading);
            Label(new Rect(34,142,1000,30),"Across the pass, the Ashen Clan sells steel. Beyond it, the Black Banner waits.",small);
            Box(new Rect(29,184,1054,607),new Color(.15f,.19f,.16f));
            for(int x=0;x<W;x++) for(int y=0;y<H;y++)
            {
                var r=new Rect(34+x*TW,190+y*TH,TW-1,TH-1); int t=terrain[x,y];
                Color color=t==0?new Color(.16f,.23f,.17f):t==1?new Color(.105f,.18f,.135f):t==2?new Color(.29f,.26f,.20f):t==3?new Color(.19f,.20f,.22f):new Color(.10f,.19f,.24f);
                color*=1+((x*7+y*3)%5)*.025f; color.a=1; Box(r,color);
                if(t==1) Image(new Rect(r.x+8,r.y+3,43,46),treeIcon,new Color(.31f,.39f,.28f));
                if(t==3) Image(new Rect(r.x+2,r.y+1,54,50),mountainIcon,new Color(.41f,.44f,.43f));
                if(t==4) { Line(new Vector2(r.x+8,r.y+19),new Vector2(r.x+43,r.y+19),new Color(.22f,.34f,.38f),1); Line(new Vector2(r.x+18,r.y+32),new Vector2(r.x+50,r.y+32),new Color(.22f,.34f,.38f),1); }
                string site=Site(x,y);
                if(site!="") { Texture2D icon=site=="town"?townIcon:site=="friendly"?bowIcon:site=="boss"?riderIcon:axeIcon; Image(new Rect(r.x+6,r.y+1,46,47),icon,site=="friendly"?teal:site=="town"?gold:red); }
                if(x==c.x&&y==c.y) { Box(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(.34f,.33f,.18f)); Image(new Rect(r.x+7,r.y+1,44,48),heroIcon,gold); }
                if(GUI.Button(r,"",GUIStyle.none)&&!busy&&modal==""&&!settings&&!showIntro) Plan(x,y);
            }
            for(int i=0,cost=0;i<path.Count;i++) {
                cost+=StepCost(path[i]); var p=path[i]; Vector2 pos=new Vector2(34+p.x*TW+TW/2,190+p.y*TH+TH/2); Color col=cost<=c.movement?gold:red;
                var previous=i>0?path[i-1]:new Vector2Int(c.x,c.y); Line(new Vector2(34+previous.x*TW+TW/2,190+previous.y*TH+TH/2),pos,col,3);
                if(i==path.Count-1) { Vector2 direction=i>0?pos-new Vector2(34+path[i-1].x*TW+TW/2,190+path[i-1].y*TH+TH/2):pos-new Vector2(34+c.x*TW+TW/2,190+c.y*TH+TH/2); direction.Normalize(); Vector2 side=new Vector2(-direction.y,direction.x); Line(pos,pos-direction*15+side*9,col,4); Line(pos,pos-direction*15-side*9,col,4); }
                else Box(new Rect(pos.x-3,pos.y-3,6,6),col);
            }
            SidePanel();
        }
        void SidePanel()
        {
            Box(new Rect(1110,100,456,691),panel); Image(new Rect(1135,119,69,85),heroIcon,gold);
            Label(new Rect(1220,123,320,35),"KAEL IRONSONG",heading); Label(new Rect(1220,161,320,30),"WARLORD  /  LEVEL "+Rules.Level(c),small);
            Label(new Rect(1135,220,390,30),"MOVEMENT   "+c.movement+" / "+Rules.DailyMovement,normal); Bar(new Rect(1135,260,402,7),c.movement/(float)Rules.DailyMovement,gold);
            Label(new Rect(1135,285,400,30),"YOUR WARHOST",heading);
            for(int i=0;i<c.army.Count;i++) { var t=c.army[i]; UnitRow(new Rect(1135,329+i*49,400,43),t); }
            int yy=510; Label(new Rect(1135,yy,390,28),"MISSION",heading);
            string[] objectives={"Gather the willing","Defeat the pass wardens","Recruit at Ashen Hold","Break the Black Banner"};
            bool[] done={c.joined,c.sentinelDead,c.recruited,c.bossDead};
            for(int i=0;i<4;i++) Label(new Rect(1135,yy+42+i*32,400,30),(done[i]?"[+] ":"[ ] ")+objectives[i],done[i]?small:normal);
            if(Btn(new Rect(1135,710,195,51),"RIDE [SPACE]",path.Count>0&&!busy)) Travel();
            if(Btn(new Rect(1340,710,197,51),"END DAY [E]",!busy)) NextDay();
        }
        void BattleView()
        {
            Label(new Rect(35,105,900,42),battleId=="boss"?"THE BLACK BANNER":battleId=="town"?"SIEGE OF ASHEN HOLD":"THE PASS WARDENS",heading);
            Label(new Rect(35,153,1050,30),"ROUND "+round+"  /  "+(active==null?"":(active.player?"YOUR TURN":"ENEMY TURN")+"  /  "+Rules.Units[active.kind].name),normal);
            Box(new Rect(29,210,1070,565),new Color(.13f,.15f,.14f));
            for(int y=0;y<BH;y++)for(int x=0;x<BW;x++)
            {
                Rect r=HexRect(x,y); var f=At(x,y); bool can=active!=null&&active.player&&reachable.Contains(new Vector2Int(x,y));
                Color col=can?new Color(.17f,.32f,.32f):new Color(.22f,.24f,.22f); if(f==active) col=new Color(.46f,.38f,.20f);
                Image(r,hex,col);
                if(f!=null) { Image(new Rect(r.x+22,r.y+8,50,46),UnitIcon(f.kind),f.player?gold:red); Label(new Rect(r.x+8,r.y+46,r.width-16,24),f.Count.ToString(),center); }
                if(GUI.Button(r,"",GUIStyle.none)&&modal==""&&!settings&&!dev) BattleClick(x,y);
            }
            if(Time.unscaledTime<hitUntil) Label(new Rect(100,740,940,34),hitText,center);
            Box(new Rect(1130,105,436,670),panel);
            Label(new Rect(1155,125,380,34),"WAR COUNCIL",heading);
            if(active!=null) {
                UnitRow(new Rect(1155,178,380,55),new Troop(active.kind,active.Count));
                var u=Rules.Units[active.kind]; Label(new Rect(1155,252,380,90),"HP "+active.totalHp+"  /  SPEED "+Speed(active)+"\nDAMAGE "+u.min+"-"+u.max+" PER CREATURE\n"+(u.ranged?"RANGED ATTACK":"MELEE / ONE RETALIATION PER ROUND"),small);
            }
            if(Btn(new Rect(1155,350,180,44),"DEFEND",!busy&&active!=null&&active.player)) { active.defend=true; Log(Rules.Units[active.kind].name+" defends (-30% damage)."); Advance(); }
            if(Btn(new Rect(1345,350,195,44),"PASS TURN",!busy&&active!=null&&active.player)) { Log("Turn passed."); Advance(); }
            Label(new Rect(1155,420,380,30),"BATTLE CHRONICLE",heading);
            for(int i=0;i<battleLog.Count;i++) Label(new Rect(1155,462+i*31,380,31),battleLog[i],small);
        }
        Rect HexRect(int x,int y)=>new Rect(55+x*96+(y%2)*48,225+y*72,92,80);
        void TownView()
        {
            Label(new Rect(35,108,900,45),"ASHEN HOLD",title);
            Label(new Rect(35,158,1100,30),c.townOwned?"YOUR STRONGHOLD  /  Production at clan cost  /  Transfer troops between both rows":"ASHEN CLAN TERRITORY  /  Visiting warhosts may purchase recruits",small);
            Box(new Rect(35,207,500,570),new Color(.13f,.15f,.15f));
            Image(new Rect(110,245,340,290),townIcon,gold);
            Label(new Rect(60,566,450,38),"THE FORGES ARE ALREADY BUILT",heading);
            Label(new Rect(60,615,445,75),"The Axe Hall, Cinder Range and Iron Stables are operational. Stocks accumulate every new week, on days 8, 15, 22...",normal);
            if(Btn(new Rect(60,713,445,42),c.townOwned?"CLAN OATH HONOURED":"CLAIM THE HOLD / FIGHT GARRISON",!c.townOwned&&c.army.Count>0)) modal="claim";
            for(int i=0;i<3;i++)
            {
                int kind=i; var u=Rules.Units[i]; float yy=207+i*150; Box(new Rect(563,yy,1003,139),panel); Image(new Rect(583,yy+21,83,89),UnitIcon(i),gold);
                Label(new Rect(692,yy+12,430,35),u.name,heading); Label(new Rect(692,yy+49,470,29),u.subtitle+"  /  HP "+u.hp+"  /  DMG "+u.min+"-"+u.max,small);
                Label(new Rect(692,yy+85,490,30),"STOCK "+c.stock[i]+"  /  "+Rules.Price(c,i)+" GOLD EACH",normal);
                int amount=Mathf.Min(qty,c.stock[i]);if(qty==99)amount=Mathf.Min(amount,c.gold/Rules.Price(c,i)); if(Btn(new Rect(1225,yy+38,309,57),"RECRUIT "+amount+"  /  "+(amount*Rules.Price(c,i))+" GOLD",!c.heroHome&&amount>0&&c.gold>=amount*Rules.Price(c,i)&&Rules.CanAdd(c.army,i))) {
                    if(Rules.Buy(c,kind,amount)) { audioDirector.Cue("gold"); audioDirector.Voice(kind); Notify(amount+" "+u.name+" join your warhost."); Save(false); }
                }
            }
            Label(new Rect(568,659,250,30),"RECRUIT QUANTITY",small);
            if(Btn(new Rect(790,654,70,37),"-"))qty=Mathf.Max(1,qty-1);
            Label(new Rect(865,655,90,37),qty==99?"MAX":qty.ToString(),center);
            if(Btn(new Rect(960,654,70,37),"+"))qty=Mathf.Min(99,qty+1);
            if(Btn(new Rect(1040,654,110,37),"MAX"))qty=99;
            DrawArmySlots(new Rect(563,700,1003,56),c.townOwned?c.garrison:new List<Troop>{new Troop(0,13),new Troop(1,5)},true,c.townOwned?(c.heroHome?"HOME / KAEL":"HOME / YOUR GARRISON"):"HOME / ASHEN CLAN");
            DrawArmySlots(new Rect(563,776,1003,56),c.army,false,c.heroHome?"VISITING / EMPTY":"VISITING / KAEL");
            if(Btn(new Rect(35,799,500,34),"RETURN TO THE PASS"))LeaveTown();
        }
        void DrawArmySlots(Rect area,List<Troop> troops,bool home,string label)
        {
            Label(new Rect(area.x,area.y-17,260,20),label,small);
            if(screen=="town") {
                if(!c.townOwned&&home) { Image(new Rect(area.x+4,area.y+4,30,38),heroIcon,red);Label(new Rect(area.x+42,area.y+8,193,39),"LORD VARG",small); }
                else if(c.townOwned&&((home&&c.heroHome)||(!home&&!c.heroHome))) {
                    if(Btn(new Rect(area.x,area.y+6,229,area.height-12),home?"KAEL -> VISITING":"KAEL -> HOME")) { if(Rules.StationHero(c)) { audioDirector.Cue("click");Save(false); } }
                }
            }
            float start=area.x+244, width=(area.width-244)/7;
            for(int i=0;i<7;i++) {
                Rect r=new Rect(start+i*width,area.y,width-5,area.height-4); Box(r,new Color(.14f,.17f,.17f));
                if(i<troops.Count) {
                    var t=troops[i]; Image(new Rect(r.x+4,r.y+4,34,35),UnitIcon(t.kind),home&&!c.townOwned?red:gold); Label(new Rect(r.x+40,r.y+13,r.width-44,26),t.count.ToString(),center);
                    if(GUI.Button(r,"",GUIStyle.none)&&c.townOwned&&screen=="town"&&!c.heroHome) { if(Rules.Transfer(c,home,i)) { Save(false); audioDirector.Cue("click"); } else Notify("The destination army has no free slot."); }
                }
            }
        }
        void HeroView()
        {
            Label(new Rect(35,107,900,47),"KAEL IRONSONG",title); Label(new Rect(37,161,900,28),"WARLORD OF THE IRON OATH",small);
            Box(new Rect(35,215,430,556),panel); Image(new Rect(145,242,198,228),heroIcon,gold);
            Label(new Rect(60,483,380,40),"LEVEL "+Rules.Level(c)+"  /  XP "+c.xp,heading); Bar(new Rect(60,532,380,9),(c.xp%300)/300f,gold);
            Label(new Rect(60,562,380,115),"ATTACK     "+Rules.Attack(c)+"\nDEFENSE   "+Rules.Defense(c)+"\nSPEED BONUS   +"+Rules.SpeedBonus(c),normal);
            Label(new Rect(60,693,380,64),"Every 300 XP grants a level. Attack improves each level; defense every second level.",small);
            Box(new Rect(494,215,1072,310),panel); Label(new Rect(521,235,900,35),"EQUIPMENT",heading);
            Equipment(new Rect(523,291,482,111),"WEAPON",c.weapon); Equipment(new Rect(1032,291,507,111),"CHARM",c.charm);
            Label(new Rect(521,437,1000,62),"Select an artifact below to equip it. Click an occupied equipment slot to return it to your inventory.",normal);
            Box(new Rect(494,552,1072,220),panel); Label(new Rect(521,572,950,35),"INVENTORY",heading);
            if(c.bag.Count==0) Label(new Rect(521,627,960,54),c.sentinelDead?"No unequipped artifacts carried.":"No artifacts carried. Defeat the pass wardens to claim the Fang of Thunder.",normal);
            for(int i=0;i<c.bag.Count;i++) {
                int artifact=c.bag[i]; Rect r=new Rect(521+(i%3)*337,624+(i/3)*65,322,58);
                if(Btn(r,Rules.ArtifactNames[artifact])) {
                    if(artifact==0) { if(c.weapon>=0)c.bag.Add(c.weapon); c.weapon=artifact; }
                    else { if(c.charm>=0)c.bag.Add(c.charm); c.charm=artifact; }
                    c.bag.RemoveAt(i); audioDirector.Cue("equip"); Notify(Rules.ArtifactDescriptions[artifact]); Save(false); break;
                }
            }
            if(Btn(new Rect(35,796,430,38),c.heroHome?"RETURN TO TOWN":"RETURN TO MAP"))screen=c.heroHome?"town":"map";
            DrawArmySlots(new Rect(494,797,1072,39),c.heroHome?c.garrison:c.army,false,c.heroHome?"HOME / KAEL'S WARHOST":"YOUR WARHOST");
        }
        void Equipment(Rect r,string label,int artifact)
        {
            Box(r,new Color(.16f,.17f,.15f)); Label(new Rect(r.x+15,r.y+12,r.width-30,23),label,small);
            Label(new Rect(r.x+15,r.y+39,r.width-30,29),artifact<0?"Empty":Rules.ArtifactNames[artifact],heading);
            if(artifact>=0) { Label(new Rect(r.x+15,r.y+78,r.width-30,25),Rules.ArtifactDescriptions[artifact],small); if(GUI.Button(r,"",GUIStyle.none)) { c.bag.Add(artifact); if(label=="WEAPON")c.weapon=-1; else c.charm=-1; audioDirector.Cue("equip"); Save(false); } }
        }
        void Intro()
        {
            Overlay(); Rect r=new Rect(320,165,960,570); Box(r,panel); Box(new Rect(r.x,r.y,r.width,3),gold);
            Label(new Rect(360,204,880,70),"THE ASHEN PASS",huge);
            Label(new Rect(360,292,870,157),"You are Kael Ironsong, a warlord without a stronghold.\nGather the willing, break the wardens, and hire the Ashen Clan's warriors. Beyond their gates, the Black Banner waits.",normal);
            Label(new Rect(360,464,870,79),"Sound follows your journey: ambient at rest, a metal gallop while riding, and riffs for encounters, attacks and victory. Headphones recommended.",small);
            Label(new Rect(360,553,880,40),"Click a tile to plan. Click again to ride. Days restore movement.",normal);
            if(Btn(new Rect(360,637,540,55),"TAKE THE IRON OATH")) { showIntro=false; audioDirector.Cue("friendly"); }
            if(Btn(new Rect(920,637,320,55),"LOAD CAMPAIGN",File.Exists(SavePath)))Load();
        }
        void Modal()
        {
            Overlay(); Rect r=new Rect(360,205,880,490); Box(r,panel); Box(new Rect(r.x,r.y,r.width,3),modal=="defeat"?red:gold);
            string h="",body="";
            if(modal=="friendly") { h="THE WILLING"; body="Six Ash Archers lower their bows.\n\"We heard the iron oath. Let us ride with you.\"\n\nThey ask for no gold. Their arrows will help against the pass wardens."; }
            if(modal=="sentinel") { h="THE PASS WARDENS"; body="10 Ironbound block the only passage through the ridge.\nThey refuse to join your cause.\n\nVictory rewards: 180 XP, 500 gold and the Fang of Thunder."; }
            if(modal=="boss") { h="THE BLACK BANNER"; body="18 Ironbound, 9 Ash Archers and 3 Doom Riders.\nThis warhost is stronger than the pass wardens.\n\nRecruit in Ashen Hold and equip your artifact before challenging them.\nVictory: 450 XP, 900 gold and the Warborn Sigil."; }
            if(modal=="claim") { h="CLAIM ASHEN HOLD"; body="The Ashen Clan will not surrender its ancestral forges.\nTheir garrison fields 13 Ironbound and 5 Ash Archers.\n\nDefeat them to own the town, transfer troops and recruit at 55% of the market price. Existing buildings remain operational."; }
            if(modal=="victory") { h=c.bossDead&&battleId=="boss"?"THE PASS IS YOURS":"VICTORY"; body="REWARDS   "+earnedXP+" XP  /  "+earnedGold+" GOLD\n"+(rewardArtifact>=0?"ARTIFACT   "+Rules.ArtifactNames[rewardArtifact]+"\n":"")+"LOSSES   "+lost+" CREATURES\n\n"+resultText+(battleId=="town"?"\nAshen Hold is now yours.":battleId=="boss"?"\nFirst mission complete. Continue exploring or start a new oath.":"\nThe road to Ashen Hold is open."); }
            if(modal=="defeat") { h="YOUR OATH FALLS SILENT"; body="Your warhost has been defeated.\n\nRetry with the army you brought into this battle, return to prepare a stronger force, or begin the mission again."; }
            Label(new Rect(399,236,800,58),h,title); Label(new Rect(399,320,800,254),body,normal);
            if(modal=="victory") { if(Btn(new Rect(400,611,800,49),"CONTINUE THE OATH")) { modal=""; if(resultText.StartsWith("LEVEL"))audioDirector.Cue("level"); } }
            else if(modal=="defeat") {
                if(Btn(new Rect(400,611,252,49),"RETRY BATTLE"))BeginBattle(battleId);
                if(Btn(new Rect(674,611,252,49),"RETURN / PREPARE")) { modal=""; screen=battleId=="town"?"town":"map"; }
                if(Btn(new Rect(947,611,252,49),"NEW MISSION"))Restart();
            }
            else {
                if(Btn(new Rect(400,611,490,49),modal=="friendly"?"WELCOME THEM":"TO BATTLE",modal!="friendly"||Rules.CanAdd(c.army,1))) {
                    if(modal=="friendly") { Rules.Add(c.army,1,6); c.joined=true; modal=""; audioDirector.Voice(1); Save(false); }
                    else BeginBattle(modal=="claim"?"town":modal);
                }
                if(Btn(new Rect(911,611,288,49),"LEAVE"))modal="";
            }
        }
        void SettingsView()
        {
            Overlay(); Box(new Rect(400,140,800,630),panel); Label(new Rect(439,176,720,43),"AUDIO / CAMPAIGN",title);
            Label(new Rect(440,240,720,30),"MUSIC  /  Ambient, gallop and battle layers",normal); audioDirector.musicVolume=GUI.HorizontalSlider(new Rect(440,280,720,25),audioDirector.musicVolume,0,1);
            Label(new Rect(440,322,720,30),"EFFECTS  /  Riffs, impacts and rewards",normal); audioDirector.sfxVolume=GUI.HorizontalSlider(new Rect(440,362,720,25),audioDirector.sfxVolume,0,1);
            Label(new Rect(440,404,720,30),"VOICES  /  Creature calls",normal); audioDirector.voiceVolume=GUI.HorizontalSlider(new Rect(440,444,720,25),audioDirector.voiceVolume,0,1);
            audioDirector.muted=GUI.Toggle(new Rect(440,482,720,30),audioDirector.muted," Mute all audio [M]");
            bool safe=screen!="battle"&&!busy&&modal!="defeat";
            if(Btn(new Rect(440,534,225,45),"SAVE",safe))Save();
            if(Btn(new Rect(680,534,225,45),"LOAD",safe&&File.Exists(SavePath))) { Load(); settings=false; }
            if(Btn(new Rect(920,534,240,45),"NEW MISSION",safe)) { Restart(); settings=false; }
            Label(new Rect(440,597,720,64),"Original procedural audio and offline synthetic voice placeholders.\nF3 opens the audio audition and development panel.",small);
            if(Btn(new Rect(440,688,470,44),"BACK TO THE OATH")) { settings=false; PlayerPrefs.SetFloat("music",audioDirector.musicVolume); PlayerPrefs.SetFloat("sfx",audioDirector.sfxVolume); PlayerPrefs.SetFloat("voice",audioDirector.voiceVolume); PlayerPrefs.Save(); }
            if(Btn(new Rect(930,688,230,44),"QUIT"))Application.Quit();
        }
        void DevView()
        {
            Overlay(); Box(new Rect(200,100,1200,705),panel); Label(new Rect(235,131,1110,44),"DEVELOPMENT / AUDIO AUDITION",title);
            string[] cues={"friendly","enemy","intro","hit","heavy","hurt","victory","defeat","gold","equip","level","click"};
            for(int i=0;i<cues.Length;i++) { string cue=cues[i]; if(Btn(new Rect(235+(i%4)*285,210+(i/4)*66,265,49),cue.ToUpperInvariant()))audioDirector.Cue(cue); }
            for(int i=0;i<3;i++) { int kind=i; if(Btn(new Rect(235+i*380,435,360,49),Rules.Units[i].name+" VOICE"))audioDirector.Voice(kind); }
            bool safe=screen!="battle"&&!busy;
            if(Btn(new Rect(235,526,265,49),"+ 2000 GOLD",safe))c.gold+=2000;
            if(Btn(new Rect(520,526,265,49),"NEXT DAY",safe))NextDay();
            if(Btn(new Rect(805,526,265,49),"TELEPORT TO TOWN",safe)) { c.sentinelDead=true;c.x=11;c.y=5;modal="";showIntro=false;OpenTown(); }
            if(Btn(new Rect(1090,526,265,49),"GRANT ARTIFACTS",safe)) { c.bag.Add(0);c.bag.Add(1);c.bag.Add(2); }
            Label(new Rect(235,602,1110,75),"Development actions modify the current campaign. Start a new mission for an unmodified playthrough.\nSave: "+SavePath,small);
            if(Btn(new Rect(235,725,1120,44),"CLOSE [F3]"))dev=false;
        }
        void Overlay() { Box(new Rect(0,0,1600,900),new Color(0,0,0,.84f)); }
        bool Btn(Rect r,string text,bool enabled=true)
        {
            bool old=GUI.enabled; GUI.enabled=old&&enabled; Box(r,enabled?new Color(.22f,.24f,.21f):new Color(.10f,.12f,.12f));
            Box(new Rect(r.x,r.y,r.width,1),enabled?gold:muted*.4f); var previous=GUI.color; GUI.color=enabled?Color.white:new Color(.55f,.55f,.55f);
            bool clicked=GUI.Button(r,text,button); GUI.color=previous;GUI.enabled=old;
            return clicked;
        }
        void Label(Rect r,string text,GUIStyle style) { GUI.Label(r,text,style); }
        void Box(Rect r,Color color) { var prev=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=prev; }
        void Image(Rect r,Texture2D texture,Color color) { var prev=GUI.color;GUI.color=color;GUI.DrawTexture(r,texture,ScaleMode.StretchToFill,true);GUI.color=prev; }
        void Bar(Rect r,float ratio,Color color) { Box(r,new Color(.2f,.22f,.21f));Box(new Rect(r.x,r.y,r.width*Mathf.Clamp01(ratio),r.height),color); }
        void Line(Vector2 a,Vector2 b,Color color,float width) { var matrix=GUI.matrix; GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a); Box(new Rect(a.x,a.y-width/2,Vector2.Distance(a,b),width),color);GUI.matrix=matrix; }
        Texture2D UnitIcon(int kind)=>kind==0?axeIcon:kind==1?bowIcon:riderIcon;
        void UnitRow(Rect r,Troop t) { Box(r,new Color(.115f,.135f,.13f)); Image(new Rect(r.x+7,r.y+3,34,r.height-6),UnitIcon(t.kind),gold); Label(new Rect(r.x+52,r.y+8,r.width-125,r.height-10),Rules.Units[t.kind].name,small); Label(new Rect(r.x+r.width-68,r.y+5,62,r.height-7),t.count.ToString(),center); }
        void MakeArt()
        {
            hex=Shape((x,y)=>Mathf.Abs(x-.5f)<(.5f-Mathf.Max(0,Mathf.Abs(y-.5f)-.25f)*1.6f));
            mountainIcon=Shape((x,y)=>y<.93f&&y>.12f&&Mathf.Abs(x-.5f)<y*.48f);
            treeIcon=Shape((x,y)=>(y>.73f&&Mathf.Abs(x-.5f)<.055f)||(y>.08f&&y<.82f&&Mathf.Abs(x-.5f)<(y%.24f)*1.55f+.055f));
            heroIcon=Shape((x,y)=> (y>.20f&&y<.44f&&Mathf.Abs(x-.5f)<.16f)||(y>.43f&&y<.75f&&Mathf.Abs(x-.5f)<.26f)||(y>.73f&&y<.94f&&(Mathf.Abs(x-.32f)<.09f||Mathf.Abs(x-.68f)<.09f))||(x>.73f&&x<.78f&&y>.04f&&y<.91f)||(y>.06f&&y<.27f&&x>.77f&&x<.99f));
            townIcon=Shape((x,y)=>(y>.50f&&y<.96f&&x>.08f&&x<.92f&&!(y>.78f&&Mathf.Abs(x-.5f)<.07f))||(y>.20f&&y<.72f&&(x>.1f&&x<.25f||x>.75f&&x<.9f))||(y>.13f&&y<.22f&&(x>.07f&&x<.13f||x>.17f&&x<.23f||x>.77f&&x<.83f||x>.87f&&x<.93f))||(y>.30f&&y<.55f&&Mathf.Abs(x-.5f)<.19f)||(y>.05f&&y<.30f&&Mathf.Abs(x-.5f)<y*.68f));
            axeIcon=Shape((x,y)=>(Mathf.Abs(x-(.30f+y*.4f))<.05f&&y>.13f&&y<.96f)||(y>.12f&&y<.40f&&x>.28f&&x<.82f&&Mathf.Abs(x-.55f)<.29f-Mathf.Abs(y-.26f)*.8f));
            bowIcon=Shape((x,y)=>(Mathf.Abs(x-(.28f+Mathf.Sin(y*Mathf.PI)*.39f))<.05f&&y>.08f&&y<.92f)||(Mathf.Abs(x-.28f)<.016f&&y>.08f&&y<.92f)||(Mathf.Abs(y-.5f)<.025f&&x>.15f&&x<.94f)||(x>.8f&&Mathf.Abs(y-.5f)<(x-.78f)*.65f));
            riderIcon=Shape((x,y)=>(y>.47f&&y<.70f&&x>.1f&&x<.77f)||(y>.67f&&y<.92f&&(Mathf.Abs(x-.22f)<.04f||Mathf.Abs(x-.68f)<.04f))||(x>.65f&&x<.86f&&y>.28f&&y<.58f)||(x>.73f&&x<.97f&&y>.22f&&y<.36f)||(x>.38f&&x<.52f&&y>.14f&&y<.51f)||(x>.48f&&x<.52f&&y>.01f&&y<.35f));
        }
        Texture2D Shape(Func<float,float,bool> inside)
        {
            const int size=96; var texture=new Texture2D(size,size,TextureFormat.RGBA32,false); var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[(size-1-y)*size+x]=inside((x+.5f)/size,(y+.5f)/size)?Color.white:Color.clear;
            texture.SetPixels(pixels);texture.Apply();texture.filterMode=FilterMode.Bilinear;return texture;
        }
    }
}
