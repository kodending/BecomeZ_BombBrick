using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatEffectPopupTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;

    // Exercise the real OnGUI/GUI.Button hot-control path, including covered SFX buttons.
    public sealed class PopupHarness:EditorWindow
    {
        public CombatEffectTestSceneController controller;
        public string mouseTrace="";
        public Rect sidebar,content;
        private void OnGUI()
        {
            EventType type=Event.current.type;
            Vector2 pointer=Event.current.mousePosition;
            sidebar=CombatEffectTestSceneController.TestSidebarRect(Screen.width,Screen.height,Collapsed);
            content=CombatEffectTestSceneController.TestSidebarContentRect(Screen.width,Screen.height);
            if(controller!=null)
                typeof(CombatEffectTestSceneController).GetMethod("OnGUI",Private)
                    .Invoke(controller,null);
            if(type==EventType.MouseDown||type==EventType.MouseUp)
                mouseTrace+=type+" "+pointer+" -> "+Event.current.type+
                    " hot="+GUIUtility.hotControl+" sidebar="+sidebar+"; ";
        }
    }

    private static object Get(object target,string name)=>
        target.GetType().GetField(name,Private).GetValue(target);
    private static void Set(object target,string name,object value)=>
        target.GetType().GetField(name,Private).SetValue(target,value);

    private static void Click(EditorWindow window,Vector2 point)
    {
        window.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=point});
        window.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=point});
    }

    // SendEvent includes the EditorWindow's 21 px tab, unlike Event.current.mousePosition.
    private static void ClickGui(EditorWindow window,Vector2 point)=>Click(window,point+Vector2.up*21f);
    private static Rect PopupRect=>(Rect)typeof(CombatEffectTestSceneController).GetField("activePopupRect",Static).GetValue(null);
    private static Vector2 SecondPopupRow(Rect popup)=>new Vector2(popup.center.x,popup.y+24f+28f+12f);
    private static void SetCollapsed(bool value)=>typeof(CombatEffectTestSceneController).GetField("sidebarCollapsed",Static).SetValue(null,value);
    private static bool Collapsed=>(bool)typeof(CombatEffectTestSceneController).GetField("sidebarCollapsed",Static).GetValue(null);

    private static CombatEffectTestSceneController CreateController(GameObject root)
    {
        const string family="spirit_deer";
        var controller=root.AddComponent<CombatEffectTestSceneController>();
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        List<MonsterDefinition> members=CombatEffectTestSceneController.GetFamilyMembers(database,family);
        ((List<MonsterDefinition>)Get(controller,"families")).Add(members[0]);
        ((Dictionary<string,List<MonsterDefinition>>)Get(controller,"familyMembers")).Add(family,members);
        ((List<PersonalityMasterRow>)Get(controller,"personalities"))
            .Add(GameMasterData.Database.personalities.First(row=>row!=null&&row.enabled));
        return controller;
    }

    private static Action SaveGlobals()
    {
        var values=new Dictionary<FieldInfo,object>();
        foreach(Type type in new[]{typeof(CombatEffectTestConfiguration),typeof(CombatReadabilityPreview),typeof(CombatEffectTestSceneController)})
            foreach(var field in type.GetFields(Static))
                if(!field.IsLiteral&&!field.IsInitOnly)values.Add(field,field.GetValue(null));
        string[] allies=CombatEffectTestConfiguration.AllyMonsterIds.ToArray(),enemies=CombatEffectTestConfiguration.EnemyMonsterIds.ToArray();
        float volume=AudioListener.volume,speed=Time.timeScale;var random=UnityEngine.Random.state;
        return ()=>
        {
            foreach(var pair in values)pair.Key.SetValue(null,pair.Value);
            CombatEffectTestConfiguration.AllyMonsterIds.Clear();CombatEffectTestConfiguration.AllyMonsterIds.AddRange(allies);
            CombatEffectTestConfiguration.EnemyMonsterIds.Clear();CombatEffectTestConfiguration.EnemyMonsterIds.AddRange(enemies);
            AudioListener.volume=volume;Time.timeScale=speed;UnityEngine.Random.state=random;
        };
    }

    [UnityTest]
    public IEnumerator DropdownsReceiveClicksBeforeCoveredButtonsAndOutsideClickOnlyCloses()
    {
        Action restore=SaveGlobals();
        GameObject root=new GameObject("Popup input regression test");
        root.SetActive(false);
        PopupHarness window=null;
        try
        {
            SetCollapsed(false);
            var controller=CreateController(root);
            var deer=((Dictionary<string,List<MonsterDefinition>>)Get(controller,"familyMembers"))["spirit_deer"];
            Set(controller,"allyRank",ExpeditionMonsterRank.Rank3);
            var choices=MonsterTraitCatalog.TestSelectable(deer[0],true);
            Assert.That(choices.Count,Is.GreaterThan(1));
            Set(controller,"allyRank3TraitId",choices[0].id);
            Set(controller,"openTraitList",13);
            CombatEffectTestConfiguration.Active=true;

            window=ScriptableObject.CreateInstance<PopupHarness>();
            window.controller=controller;
            window.position=new Rect(80,80,960,720);
            window.Show();
            window.Focus();
            yield return null;
            Assert.That(CombatEffectTestSceneController.PointerOverControls(new Vector2(950,680)),Is.True);

            // Both dropdowns cover scrollable formation controls; only the popup receives the click.
            Rect popup=PopupRect;Assert.That(popup.width,Is.GreaterThan(0f));
            ClickGui(window,SecondPopupRow(popup));
            yield return null;
            Assert.That(Get(controller,"allyRank3TraitId"),Is.EqualTo(choices[1].id),window.mouseTrace);
            Assert.That(Get(controller,"openTraitList"),Is.EqualTo(0));
            Assert.That(Get(controller,"allySoundsOnly"),Is.False);

            Set(controller,"openMonsterList",1);
            window.Repaint();
            yield return null;
            popup=PopupRect;Assert.That(popup.width,Is.GreaterThan(0f));
            ClickGui(window,SecondPopupRow(popup));
            yield return null;
            Assert.That(Get(controller,"allyMemberIndex"),Is.EqualTo(1));
            Assert.That(Get(controller,"openMonsterList"),Is.EqualTo(0));

            Set(controller,"openTraitList",13);
            window.Repaint();
            yield return null;
            int count=(int)Get(controller,"allyCount");
            bool prepared=(bool)Get(controller,"deploymentPrepared");
            Rect panel=CombatEffectTestSceneController.TestSidebarRect(Screen.width,Screen.height);
            float width=panel.width-16f;
            ClickGui(window,new Vector2(panel.x+8f+(width-4f)*.25f,panel.y+50f));
            yield return null;
            Assert.That(Get(controller,"openTraitList"),Is.EqualTo(0));
            Assert.That(Get(controller,"allyCount"),Is.EqualTo(count));
            Assert.That(Get(controller,"deploymentPrepared"),Is.EqualTo(prepared),"The covered prepare button must not run.");
            Assert.That(CombatEffectTestSceneController.PointerOverControls(new Vector2(950,680)),Is.False);
        }
        finally
        {
            if(window!=null)window.Close();
            UnityEngine.Object.DestroyImmediate(root);
            restore();
        }
    }

    [UnityTest]
    public IEnumerator SidebarTabsAndCollapseButtonRespondToRealMouseDownAndUp()
    {
        Action restore=SaveGlobals();var root=new GameObject("Sidebar click regression test");root.SetActive(false);
        PopupHarness window=null;
        try
        {
            SetCollapsed(false);CombatEffectTestConfiguration.Active=true;
            var controller=CreateController(root);
            window=ScriptableObject.CreateInstance<PopupHarness>();window.controller=controller;
            window.position=new Rect(80,80,960,720);window.Show();window.Focus();yield return null;
            Rect panel=window.sidebar;
            float width=panel.width-16f;
            Assert.That(Get(controller,"sidebarTab"),Is.EqualTo(0));
            foreach(int tab in new[]{1,2,0})
            {
                ClickGui(window,new Vector2(panel.x+8f+tab*(width+4f)/3f+(width-8f)/6f,panel.y+201f));
                yield return null;
                Assert.That(Get(controller,"sidebarTab"),Is.EqualTo(tab),window.mouseTrace);
                Assert.That(Get<Vector2>(controller,"sidebarScroll"),Is.EqualTo(Vector2.zero));
                if(tab==1)
                {
                    Rect viewport=window.content;
                    bool before=Get<bool>(controller,"allySoundsOnly");
                    ClickGui(window,new Vector2(viewport.x+(viewport.width-18f)*.5f,viewport.y+271f));
                    yield return null;
                    Assert.That(Get<bool>(controller,"allySoundsOnly"),Is.EqualTo(!before),window.mouseTrace);
                    Assert.That(CombatEffectTestSceneController.MuteEnemySounds,Is.EqualTo(!before));
                }
            }
            Vector2 fold=new Vector2(panel.xMax-39f,panel.y+16f);
            ClickGui(window,fold);yield return null;
            Assert.That(Collapsed,Is.True,window.mouseTrace);
            Assert.That(CombatEffectTestSceneController.PointerOverControls(fold),Is.True);
            Rect content=window.content;
            Assert.That(CombatEffectTestSceneController.PointerOverControls(content.center),Is.False);
            ClickGui(window,fold);yield return null;
            Assert.That(Collapsed,Is.False,window.mouseTrace);
            Assert.That(CombatEffectTestSceneController.PointerOverControls(content.center),Is.True);
            Assert.That(Get(controller,"sidebarTab"),Is.EqualTo(0));
        }
        finally
        {
            if(window!=null)window.Close();UnityEngine.Object.DestroyImmediate(root);restore();
        }
    }

    private static T Get<T>(object target,string name)=>(T)Get(target,name);
}
