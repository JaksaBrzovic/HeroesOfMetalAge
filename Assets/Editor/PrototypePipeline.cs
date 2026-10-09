using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using HeroesOfMetalAge;

public static class PrototypePipeline
{
    const string ScenePath="Assets/Scenes/AshenPass.unity";
    [MenuItem("Tools/Heroes of Metal Age/Generate prototype")]
    public static void Setup()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Camera").AddComponent<Camera>(); camera.orthographic=true;camera.backgroundColor=new Color(.04f,.05f,.06f);camera.clearFlags=CameraClearFlags.SolidColor;camera.gameObject.AddComponent<AudioListener>();
        camera.transform.position=new Vector3(0,0,-10);
        new GameObject("Heroes of Metal Age").AddComponent<MetalGame>();
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        PlayerSettings.companyName="Iron Oath Studio";PlayerSettings.productName="HeroesOfMetalAge";PlayerSettings.bundleVersion="0.1.0";
        PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
        // Legacy input is used deliberately: zero scene wiring and deterministic keyboard support.
        var serialized=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input=serialized.FindProperty("activeInputHandler");if(input!=null) { input.intValue=0;serialized.ApplyModifiedProperties(); }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh(); Debug.Log("PROTOTYPE_SETUP_OK");
    }
    [MenuItem("Tools/Heroes of Metal Age/Validate rules")]
    public static void Validate()
    {
        var results=new List<string>();
        Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("VALIDATION_FAILED: "+name);results.Add("PASS "+name);};
        var c=Campaign.New(); check(Rules.Valid(c),"Fresh campaign valid");
        int initial=c.gold; check(Rules.Buy(c,0,2),"Purchase succeeds");check(c.gold==initial-140&&c.stock[0]==12&&c.army[0].count==14,"Purchase debits gold and stock and merges army");
        var before=JsonUtility.ToJson(c);check(!Rules.Buy(c,2,99)&&JsonUtility.ToJson(c)==before,"Out-of-stock purchase atomic");
        c.gold=0;before=JsonUtility.ToJson(c);check(!Rules.Buy(c,1,1)&&JsonUtility.ToJson(c)==before,"Unaffordable purchase atomic");
        check(!Rules.Buy(c,-1,2)&&!Rules.Buy(c,0,-1),"Invalid purchase rejected");
        c=Campaign.New();int stock=c.stock[0];for(int i=0;i<6;i++)check(!Rules.EndDay(c),"No refresh before day 8 / "+i);check(c.day==7&&c.stock[0]==stock,"Day 7 stock unchanged");
        check(Rules.EndDay(c)&&c.day==8&&c.stock[0]==stock+14,"Day 8 weekly growth");for(int i=0;i<7;i++)Rules.EndDay(c);check(c.day==15&&c.stock[0]==stock+28,"Day 15 second growth");
        c.movement=0;Rules.EndDay(c);check(c.movement==16,"Movement restored");
        c=Campaign.New();check(!Rules.Transfer(c,false,0),"Foreign garrison protected");c.townOwned=true;
        check(Rules.Price(c,0)==39,"Owned-town production discount");check(Rules.Transfer(c,false,0)&&c.army.Count==0&&c.garrison[0].count==12,"Transfer to home garrison");
        check(Rules.Transfer(c,true,0)&&c.army[0].count==12&&c.garrison.Count==0,"Transfer to visitor");
        c.x=11;c.y=5;check(Rules.StationHero(c)&&c.heroHome&&c.army.Count==0&&c.garrison.Count==1&&Rules.Valid(c),"Station hero and army in home slot");
        check(!Rules.Transfer(c,true,0),"No visitor army without visiting hero");
        check(Rules.StationHero(c)&&!c.heroHome&&c.army[0].count==12&&c.garrison.Count==0,"Return home hero to visitor");
        c.weapon=0;c.charm=1;check(Rules.Attack(c)==4&&Rules.Defense(c)==4,"Equipment stat bonuses");c.xp=300;check(Rules.Level(c)==2&&Rules.Attack(c)==5,"XP level threshold");
        int hit=Rules.Damage(0,10,2,0,2,false,true,.5f);check(Rules.Damage(0,10,2,0,2,true,true,.5f)<hit,"Defend reduces damage");
        check(Rules.Damage(1,10,2,0,2,false,true,.5f)<Rules.Damage(1,10,2,0,2,false,false,.5f),"Archer melee penalty");
        check(Rules.Damage(0,1,0,2,100,false,true,0)>0,"Damage minimum one");
        var restored=JsonUtility.FromJson<Campaign>(JsonUtility.ToJson(c));check(Rules.Valid(restored)&&restored.weapon==0&&restored.xp==300,"Save round trip preserves state");
        check(Resources.Load<AudioClip>("Voices/ironbound")!=null&&Resources.Load<AudioClip>("Voices/archer")!=null&&Resources.Load<AudioClip>("Voices/rider")!=null,"All creature voices import");
        Directory.CreateDirectory("Reports");File.WriteAllLines("Reports/rules-validation.txt",results);Debug.Log("VALIDATION_OK "+results.Count+" checks");
    }
    [MenuItem("Tools/Heroes of Metal Age/Build Windows prototype")]
    public static void Build()
    {
        Setup();Validate();Directory.CreateDirectory("Builds/Windows");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Windows/HeroesOfMetalAge.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
        Debug.Log("PROTOTYPE_BUILD_OK / "+report.summary.totalSize+" bytes");
    }
}
