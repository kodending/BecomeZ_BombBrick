using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatEffectSidebarTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private MonsterRankUpFeedbackTests fixture;
    private MonsterAutoBattlePrototype battle;
    private CombatEffectTestSceneController controller;
    private object unit;
    private SpriteRenderer renderer;
    private ExpeditionRunSave run;
    private Dictionary<FieldInfo,object> configuration,comparison,controls;
    private float volume;

    [SetUp] public void Setup()
    {
        configuration=Snapshot(typeof(CombatEffectTestConfiguration));comparison=Snapshot(typeof(CombatReadabilityPreview));
        controls=Snapshot(typeof(CombatEffectTestSceneController));volume=AudioListener.volume;
        fixture=new MonsterRankUpFeedbackTests();fixture.Setup();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");unit=Get<object>(fixture,"unit");
        renderer=Get<SpriteRenderer>(fixture,"renderer");run=Get<ExpeditionRunSave>(fixture,"run");
        var host=new GameObject("Inactive sidebar input fixture");host.SetActive(false);
        controller=host.AddComponent<CombatEffectTestSceneController>();
        var definition=Get<MonsterDefinition>(unit,"definition");
        Get<List<MonsterDefinition>>(controller,"families").Add(definition);
        Get<Dictionary<string,List<MonsterDefinition>>>(controller,"familyMembers").Add(definition.familyId,new List<MonsterDefinition>{definition});
        Get<List<PersonalityMasterRow>>(controller,"personalities").Add(GameMasterData.Database.personalities.First(row=>row!=null&&row.enabled));
        Set(controller,"activeBattle",battle);Set(controller,"deploymentPrepared",true);
        Set(battle,"<IsSceneReady>k__BackingField",true);
        Set(null,typeof(CombatEffectTestSceneController),"sidebarCollapsed",false);
        Set(null,typeof(CombatEffectTestSceneController),"activePopupRect",new Rect());
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Enabled=false;
    }

    [TearDown] public void Cleanup()
    {
        try{if(battle!=null)Call(battle,"ResetMonsterRankUpFeedback");fixture?.Cleanup();}
        finally{Restore(configuration);Restore(comparison);Restore(controls);AudioListener.volume=volume;}
    }

    [TestCase(960f,720f)] [TestCase(1280f,720f)] [TestCase(1920f,1080f)]
    public void SidebarLeavesTheBattleCenterClearAndKeepsEvolutionAboveItsScrollableContent(float width,float height)
    {
        Rect panel=CombatEffectTestSceneController.TestSidebarRect(width,height);
        Assert.That(panel.x,Is.EqualTo(8f));Assert.That(panel.y,Is.EqualTo(8f));
        Assert.That(panel.width,Is.LessThanOrEqualTo(width*.24f));Assert.That(panel.width,Is.LessThanOrEqualTo(320f));
        Assert.That(panel.yMax,Is.EqualTo(height-8f));Assert.That(panel.Contains(new Vector2(width*.5f,height*.5f)),Is.False);
        Rect content=CombatEffectTestSceneController.TestSidebarContentRect(width,height);
        Assert.That(content.x,Is.EqualTo(panel.x+8f));Assert.That(content.y,Is.EqualTo(panel.y+222f));
        Assert.That(content.xMax,Is.EqualTo(panel.xMax-8f));Assert.That(content.yMax,Is.EqualTo(panel.yMax-8f));
        Rect previous=new Rect();
        for(int rank=2;rank<=4;rank++)
        {
            Rect button=CombatEffectTestSceneController.EvolutionPreviewButtonRect(panel,rank);
            Assert.That(panel.Contains(button.center),Is.True);Assert.That(button.height,Is.EqualTo(28f));
            Assert.That(button.yMax,Is.LessThan(content.yMin));
            if(rank>2)Assert.That(button.xMin-previous.xMax,Is.EqualTo(4f).Within(.0001f));
            previous=button;
        }
    }

    [Test] public void SmallScreensKeepUsableButtonWidthsAndCollapseReservesOnlyTheHeader()
    {
        Rect panel=CombatEffectTestSceneController.TestSidebarRect(480f,320f);
        Rect collapsed=CombatEffectTestSceneController.TestSidebarRect(480f,320f,true);
        Assert.That(panel.width,Is.EqualTo(150f));Assert.That(collapsed.width,Is.EqualTo(panel.width));
        Assert.That(collapsed.height,Is.EqualTo(32f));
        Assert.That(CombatEffectTestSceneController.EvolutionPreviewButtonRect(panel,2).width,Is.GreaterThan(40f));
    }

    [TestCase("deploymentPrepared")] [TestCase("sceneNotReady")] [TestCase("battleStarted")]
    public void PreviewRejectsUnavailableDeploymentWithAnExplanationAndNoQueuedEffects(string blocker)
    {
        if(blocker=="sceneNotReady")Set(battle,"<IsSceneReady>k__BackingField",false);
        else if(blocker=="battleStarted")Set(battle,blocker,true);
        else Set(controller,blocker,false);
        string data=JsonUtility.ToJson(run),config=Configuration();
        Assert.That(controller.PreviewEvolution(2),Is.False);
        Assert.That(Get<string>(controller,"evolutionPreviewMessage"),Does.Contain("배치"));Assert.That(Pulses.Count,Is.Zero);
        Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));Assert.That(Configuration(),Is.EqualTo(config));
    }

    [Test] public void SelectingAnUndeployedMonsterExplainsWhyPreviewCannotPlay()
    {
        var definition=Get<MonsterDefinition>(unit,"definition");
        var other=CombatEffectTestSceneController.GetFamilyMembers(Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase"),definition.familyId)
            .First(value=>value.monsterId!=definition.monsterId);
        Get<Dictionary<string,List<MonsterDefinition>>>(controller,"familyMembers")[definition.familyId].Add(other);
        Set(controller,"allyMemberIndex",1);
        Assert.That(controller.PreviewEvolution(3),Is.False);Assert.That(Pulses.Count,Is.Zero);
        Assert.That(Get<string>(controller,"evolutionPreviewMessage"),Does.Contain(other.displayName).And.Contain("배치"));
    }

    public sealed class SidebarHarness:EditorWindow
    {
        public CombatEffectTestSceneController controller;
        public Rect panel;
        public readonly Rect[] buttons=new Rect[3];
        public Vector2 pointer;
        public EventType pointerType;
        public int paints;
        public bool stateRestored=true;
        public bool panelCenterBlocked,outsideBlocked,evolutionBlocked,headerBlocked;
        private void OnGUI()
        {
            panel=CombatEffectTestSceneController.TestSidebarRect(Screen.width,Screen.height);
            for(int rank=2;rank<=4;rank++)buttons[rank-2]=CombatEffectTestSceneController.EvolutionPreviewButtonRect(panel,rank);
            if(Event.current.isMouse){pointer=Event.current.mousePosition;pointerType=Event.current.type;}
            bool enabled=GUI.enabled;int depth=GUI.depth;Matrix4x4 matrix=GUI.matrix;GUIStyle label=GUI.skin.label;
            Call(controller,"OnGUI");
            // Screen dimensions belong to this GUI host; sample drawing and hit bounds in the same context.
            panelCenterBlocked=CombatEffectTestSceneController.PointerOverControls(panel.center);
            outsideBlocked=CombatEffectTestSceneController.PointerOverControls(new Vector2(panel.xMax+30f,panel.y+200f));
            evolutionBlocked=CombatEffectTestSceneController.PointerOverControls(buttons[0].center);
            headerBlocked=CombatEffectTestSceneController.PointerOverControls(new Vector2(panel.x+20f,panel.y+16f));
            stateRestored&=GUI.enabled==enabled&&GUI.depth==depth&&GUI.matrix==matrix&&GUI.skin.label==label;
            if(Event.current.type==EventType.Repaint)paints++;
        }
    }

    [UnityTest] public IEnumerator AllThreeVisibleButtonsReachTheSelectedWorldBodyWithoutChangingItsRankOrConfiguration()
    {
        var window=ScriptableObject.CreateInstance<SidebarHarness>();window.controller=controller;
        try
        {
            window.position=new Rect(80f,80f,960f,720f);window.wantsMouseMove=true;window.Show();window.Focus();yield return null;
            window.Repaint();yield return null;Assert.That(window.paints,Is.GreaterThan(0));
            Vector2 offset=PointerOffset(window);
            string data=JsonUtility.ToJson(run),config=Configuration();
            float hp=Get<float>(unit,"hp"),damage=Get<float>(unit,"damage"),radius=Get<float>(unit,"collisionRadius");
            int range=Get<int>(unit,"attackRange");Vector3 position=renderer.transform.position,scale=renderer.transform.localScale;
            foreach(int rank in new[]{2,3,4})
            {
                Click(window,window.buttons[rank-2].center+offset);
                Assert.That(Pulses.Count,Is.EqualTo(1),"The actual GUI button must queue feedback.");
                object pulse=Pulses[Get<string>(unit,"stableId")];
                Assert.That(Get<int>(pulse,"rank"),Is.EqualTo(rank));Assert.That(Get<object>(pulse,"unit"),Is.SameAs(unit));
                Assert.That(Get<float>(pulse,"startedAt"),Is.EqualTo(-1f));
                Call(battle,"PresentMonsterRankUpFeedback",20f+rank);
                var effect=Get<MonsterRankUpFeedback>(pulse,"worldEffect");Assert.That(effect,Is.Not.Null);
                Assert.That(Get<SpriteRenderer>(effect,"reference"),Is.SameAs(renderer));
                Assert.That(effect.BodyRenderer,Is.Not.Null);
                Assert.That(Vector3.Distance(Get<Vector3>(effect,"center"),CombatReadabilityPresentation.VisibleBodyCenter(effect.BodyRenderer)),Is.LessThan(.00001f));
            }
            Assert.That(window.stateRestored,Is.True);Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));
            Assert.That(Configuration(),Is.EqualTo(config));Assert.That(Get<float>(unit,"hp"),Is.EqualTo(hp));
            Assert.That(Get<float>(unit,"damage"),Is.EqualTo(damage));Assert.That(Get<float>(unit,"collisionRadius"),Is.EqualTo(radius));
            Assert.That(Get<int>(unit,"attackRange"),Is.EqualTo(range));Assert.That(renderer.transform.position,Is.EqualTo(position));
            Assert.That(renderer.transform.localScale,Is.EqualTo(scale));
        }
        finally{window.Close();}
    }

    [UnityTest] public IEnumerator PopupDismissalCannotClickThroughToEvolutionAndCollapsedSidebarDoesNotBlockTheBattle()
    {
        var window=ScriptableObject.CreateInstance<SidebarHarness>();window.controller=controller;
        try
        {
            window.position=new Rect(80f,80f,960f,720f);window.wantsMouseMove=true;window.Show();window.Focus();yield return null;
            window.Repaint();yield return null;Vector2 offset=PointerOffset(window);
            Assert.That(window.panelCenterBlocked,Is.True);
            Assert.That(window.outsideBlocked,Is.False);
            Set(controller,"openMonsterList",1);window.Repaint();yield return null;
            Assert.That(window.outsideBlocked,Is.True,"An open dropdown remains modal.");
            Click(window,window.buttons[0].center+offset);
            Assert.That(Get<int>(controller,"openMonsterList"),Is.Zero);Assert.That(Pulses.Count,Is.Zero);
            Click(window,new Vector2(window.panel.xMax-39f,window.panel.y+16f)+offset);
            Assert.That((bool)typeof(CombatEffectTestSceneController).GetField("sidebarCollapsed",Fields).GetValue(null),Is.True);
            Assert.That(window.evolutionBlocked,Is.False);
            Assert.That(window.headerBlocked,Is.True);
            Assert.That(window.outsideBlocked,Is.False);Assert.That(window.stateRestored,Is.True);
        }
        finally{window.Close();}
    }

    private IDictionary Pulses=>Get<IDictionary>(battle,"monsterRankUpPulses");
    private static Vector2 PointerOffset(SidebarHarness window)
    {
        // EditorWindow tab heights differ from Game view; calibrate the real event coordinate path.
        Vector2 probe=new Vector2(4f,90f);window.SendEvent(new Event{type=EventType.MouseMove,mousePosition=probe});
        Assert.That(window.pointerType,Is.EqualTo(EventType.MouseMove));return probe-window.pointer;
    }
    private static void Click(EditorWindow window,Vector2 point)
    {
        window.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=point});
        window.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=point});
    }
    private static Dictionary<FieldInfo,object> Snapshot(Type type)=>type.GetFields(Fields).Where(field=>field.IsStatic)
        .ToDictionary(field=>field,field=>field.GetValue(null) is List<string> list?(object)list.ToArray():field.GetValue(null));
    private static void Restore(Dictionary<FieldInfo,object> values)
    {
        if(values==null)return;
        foreach(var pair in values)
            if(pair.Key.GetValue(null) is List<string> list&&pair.Value is string[] strings){list.Clear();list.AddRange(strings);}
            else if(!pair.Key.IsInitOnly&&!pair.Key.IsLiteral)pair.Key.SetValue(null,pair.Value);
    }
    private static string Configuration()=>string.Join("\n",Snapshot(typeof(CombatEffectTestConfiguration)).OrderBy(pair=>pair.Key.Name)
        .Select(pair=>pair.Key.Name+"="+(pair.Value is string[] list?string.Join(",",list):Convert.ToString(pair.Value))));
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Fields).GetValue(target);
    private static void Set(object target,string name,object value)=>Set(target,target.GetType(),name,value);
    private static void Set(object target,Type type,string name,object value)=>type.GetField(name,Fields).SetValue(target,value);
    private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Fields).Invoke(target,args);
}
