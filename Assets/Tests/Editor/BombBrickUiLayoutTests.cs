using System.Reflection;
using NUnit.Framework;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BombBrickUiLayoutTests
{
    public sealed class ResponsiveGuiHarness : EditorWindow
    {
        public float displayScale = 1f;
        public Vector2 buttonInput;
        public Vector2 dragInput;
        public Vector2 logicalPointer;
        public bool clicked;
        public bool draggedInside;
        public bool restored;
        public int fontSize;

        private void OnGUI()
        {
            Matrix4x4 original = GUI.matrix;
            bool mouse = Event.current.isMouse;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * (displayScale / BombBrickUiLayout.Scale));
            Matrix4x4 parent = GUI.matrix;
            using (new BombBrickUiLayout.GuiScope(true))
            {
                buttonInput = GUIUtility.GUIToScreenPoint(new Vector2(115, 65)) - position.position;
                dragInput = GUIUtility.GUIToScreenPoint(new Vector2(115, 135)) - position.position;
                if (mouse) logicalPointer = Event.current.mousePosition;
                fontSize = BombBrickUiTheme.BodyStyle(14).fontSize;
                if (GUI.Button(new Rect(40, 40, 150, 50), "UI")) clicked = true;
                if (Event.current.type == EventType.MouseDrag)
                    draggedInside = new Rect(40, 110, 150, 50).Contains(Event.current.mousePosition);
            }
            restored = GUI.matrix == parent && !BombBrickUiLayout.ReferenceGuiActive;
            GUI.matrix = original;
        }
    }

    [UnityTest]
    public IEnumerator ScaledGuiKeepsButtonDragAndFontInTheSameCoordinateSpace()
    {
        var window = ScriptableObject.CreateInstance<ResponsiveGuiHarness>();
        try
        {
            window.position = new Rect(80, 80, 500, 380);
            window.Show();
            yield return null;
            foreach (float scale in new[] { 2f / 3f, 1f, 2f })
            {
                window.displayScale = scale;
                window.clicked = window.draggedInside = false;
                window.Repaint();
                yield return null;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0,
                    mousePosition = window.buttonInput });
                Assert.That(new Rect(40, 40, 150, 50).Contains(window.logicalPointer), Is.True);
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0,
                    mousePosition = window.buttonInput });
                Assert.That(window.clicked, Is.True, "Button at scale " + scale);
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0,
                    mousePosition = window.dragInput });
                Assert.That(window.draggedInside, Is.True, "Drag target at scale " + scale);
                Assert.That(window.fontSize, Is.EqualTo(16));
                Assert.That(window.restored, Is.True);
            }
        }
        finally { window.Close(); }
    }

    [TestCase(1280,720)]
    [TestCase(1600,900)]
    [TestCase(1620,905)]
    [TestCase(1920,1080)]
    [TestCase(1440,900)]
    [TestCase(1920,1200)]
    [TestCase(2560,1080)]
    [TestCase(3840,2160)]
    public void PrimaryPanelsStayInsideSafeAreaAndDoNotOverlap(
        int width,int height)
    {
        Rect full=new Rect(0f,0f,width,height);
        BombBrickUiLayoutSnapshot layout=
            BombBrickUiLayout.ForScreen(width,height,full);

        AssertInside(layout.topBar,layout.safeArea,"top bar");
        AssertInside(layout.runHud,layout.safeArea,"run HUD");
        AssertInside(layout.monsterDetails,layout.safeArea,"monster details");
        AssertInside(layout.itemBar,layout.safeArea,"item bar");
        AssertInside(layout.developerPanel,layout.safeArea,"developer panel");
        Assert.That(layout.runHud.Overlaps(layout.monsterDetails),Is.False);
        Assert.That(layout.developerPanel.Overlaps(layout.monsterDetails),Is.False);
        Assert.That(layout.topBar.Overlaps(layout.runHud),Is.False);
        Assert.That(layout.topBar.Overlaps(layout.monsterDetails),Is.False);
        Assert.That(layout.monsterDetails.Overlaps(layout.itemBar),Is.False);
    }

    [Test]
    public void CoreHudUsesCompactWholePixelGeometry()
    {
        BombBrickUiLayoutSnapshot layout=BombBrickUiLayout.ForScreen(
            1920,1080,new Rect(0f,0f,1920f,1080f));

        Assert.That(layout.topBar.height,Is.EqualTo(80f));
        Assert.That(layout.runHud.height,Is.EqualTo(154f));
        Assert.That(layout.monsterDetails.height,Is.EqualTo(470f));
        Assert.That(layout.itemBar.height,Is.EqualTo(78f));
        Assert.That(BombBrickUiTheme.PixelRect(
            new Rect(10.4f,20.6f,99.5f,30.49f)),
            Is.EqualTo(new Rect(10f,20f,100f,30f)));
    }

    [TestCase(1280,720,8)]
    [TestCase(1280,720,10)]
    [TestCase(1920,1080,8)]
    [TestCase(2560,1080,10)]
    [TestCase(3440,1440,8)]
    public void AugmentTargetListFitsCardsWithoutEmptyFooterSpace(
        int width,int height,int rosterCount)
    {
        var layout=BombBrickUiLayout.ForScreen(
            width,height,new Rect(0f,0f,width,height));
        Rect panel=BombBrickUiLayout.AugmentTargetPanel(
            layout.safeArea,layout.monsterDetails,rosterCount,out int columns);

        AssertInside(panel,layout.safeArea,"augment target list");
        Assert.That(panel.Overlaps(layout.monsterDetails),Is.False);
        Assert.That(panel.center.y,Is.EqualTo(layout.safeArea.center.y));
        if(width>=1920)
            Assert.That(panel.center.x,Is.EqualTo(layout.safeArea.center.x));
        Assert.That(panel.width,Is.LessThanOrEqualTo(780f));
        Assert.That(columns,Is.EqualTo(4));
        int rows=Mathf.CeilToInt(rosterCount/(float)columns);
        float lastCardBottom=panel.y+98f+rows*122f+(rows-1)*8f;
        Assert.That(panel.yMax-46f-lastCardBottom,Is.EqualTo(12f));
        Assert.That(panel.height,Is.EqualTo(rosterCount<=8?408f:538f));
    }

    [Test]
    public void FixedContentFitsItsResponsiveContainer()
    {
        BombBrickUiLayoutSnapshot layout=BombBrickUiLayout.ForScreen(
            1280,720,new Rect(0f,0f,1280f,720f));

        Assert.That(405f+52f,Is.LessThanOrEqualTo(
            layout.monsterDetails.height),"monster item slots");
        Assert.That(8f+54f+8f,Is.LessThanOrEqualTo(
            layout.itemBar.height),"inventory icons");
        Assert.That(layout.itemBar.yMax+4f,Is.LessThanOrEqualTo(
            layout.safeArea.yMax),"pixel shadow");
    }

    [Test]
    public void KoreanPixelFontIsAvailableToEveryImmediateModeScreen()
    {
        Assert.That(Resources.Load<Font>("UI/Fonts/Galmuri11-Bold"),Is.Not.Null);
    }

    [TestCase("UI/SystemIcons/gold")]
    [TestCase("UI/ItemIcons/base_fang")]
    [TestCase("UI/AugmentIcons/augment_early_awakening")]
    public void UiArtworkUsesLowResolutionPointSampling(string resourcePath)
    {
        Texture2D texture=Resources.Load<Texture2D>(resourcePath);
        Assert.That(texture,Is.Not.Null,resourcePath);
        Assert.That(texture.width,Is.LessThanOrEqualTo(64),resourcePath);
        Assert.That(texture.height,Is.LessThanOrEqualTo(64),resourcePath);
        Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point),resourcePath);
        Assert.That(texture.mipmapCount,Is.EqualTo(1),resourcePath);
    }

    [TestCase(14f,1280,720,14f)]
    [TestCase(14f,2560,1080,18.9f)]
    [TestCase(82f,3440,1440,110.7f)]
    public void InteractionDistanceNeverShrinksAndScalesOnLargeDisplays(
        float basePixels,int width,int height,float expected)
    {
        Assert.That(BombBrickUiLayout.InteractionDistance(
            basePixels,width,height),Is.EqualTo(expected).Within(0.15f));
    }

    [TestCase(9,10,720,10)]
    [TestCase(12,10,900,12)]
    [TestCase(20,13,1080,23)]
    public void AccessibleFontsRespectMinimumAndResolutionScale(
        int requested,int minimum,int height,int expected)
    {
        Assert.That(BombBrickUiTheme.AccessibleFontSize(
            requested,minimum,height),Is.EqualTo(expected));
    }

    [Test]
    public void SystemSafeAreaIsConvertedToTopLeftGuiCoordinates()
    {
        BombBrickUiLayoutSnapshot layout=BombBrickUiLayout.ForScreen(
            1200,800,new Rect(20f,10f,1160f,740f));

        float scale=BombBrickUiLayout.ScaleForScreen(1200,800);
        Assert.That(layout.safeArea,Is.EqualTo(new Rect(
            new Vector2(20f,50f)/scale,new Vector2(1160f,740f)/scale)));
    }

    [TestCase(1280,720)]
    [TestCase(1366,768)]
    [TestCase(1600,900)]
    [TestCase(2560,1440)]
    [TestCase(3840,2160)]
    public void ResolutionChangesPreserveHudProportionsAndPointerTargets(int width,int height)
    {
        float scale=BombBrickUiLayout.ScaleForScreen(width,height);
        var layout=BombBrickUiLayout.ForScreen(width,height,new Rect(0,0,width,height));
        Assert.That(layout.monsterDetails.width*scale/width,
            Is.EqualTo(480f/1920f).Within(0.001f));
        Assert.That(layout.monsterDetails.height*scale/height,
            Is.EqualTo(470f/1080f).Within(0.001f));
        Assert.That(layout.safeArea.width,Is.GreaterThanOrEqualTo(1919.9f));
        foreach(Rect target in new[]{layout.monsterDetails,layout.itemBar,layout.runHud})
        {
            Vector2 screen=BombBrickUiLayout.GuiToScreenPoint(target.center,width,height);
            Vector2 pointer=BombBrickUiLayout.ScreenToGuiPoint(screen,width,height);
            Assert.That(Vector2.Distance(pointer,target.center),Is.LessThan(0.001f));
            Assert.That(target.Contains(pointer),Is.True);
        }
    }

    [Test]
    public void PopupFlipsAndClampsAtEveryScreenEdge()
    {
        Rect safe=new Rect(20f,30f,1160f,740f);
        Vector2 popupSize=new Vector2(326f,174f);
        Vector2[] pointers=
        {
            new Vector2(safe.x,safe.y),
            new Vector2(safe.xMax,safe.y),
            new Vector2(safe.x,safe.yMax),
            new Vector2(safe.xMax,safe.yMax)
        };

        foreach(Vector2 pointer in pointers)
            AssertInside(BombBrickUiLayout.PlacePopup(
                pointer,popupSize,safe),safe,"popup");
    }

    [Test]
    public void AbilityTooltipUsesAuthoredCombatDescriptions()
    {
        SkillMasterRow skill=new SkillMasterRow
        {
            damageFormulaNote="공격력의 160% 피해",
            baseCooldown=7f,
            castRangeTiles=3f
        };
        TraitMasterRow trait=new TraitMasterRow
        {
            effectSummary="공격력 +25%",
            triggerRule="항상"
        };
        PersonalityMasterRow personality=new PersonalityMasterRow
        {
            behaviorSummary="가장 가까운 적을 집요하게 추적"
        };

        Assert.That(MonsterAbilityTooltipContent.SkillSummary(skill),
            Does.Contain("공격력의 160% 피해").And.Contain("쿨 7초"));
        Assert.That(MonsterAbilityTooltipContent.TraitSummary(trait),
            Does.Contain("공격력 +25%").And.Contain("조건  항상"));
        Assert.That(MonsterAbilityTooltipContent.PersonalitySummary(personality),
            Is.EqualTo("가장 가까운 적을 집요하게 추적"));
    }

    [Test]
    public void Rank3TraitTooltipUsesConcisePlayerFacingSummary()
    {
        TraitMasterRow trait=new TraitMasterRow
        {
            id="trait_bear_mountain_colossus",
            triggerRule="곰의 투지 사용",
            effectSummary=new string('긴',400)
        };

        string summary=MonsterAbilityTooltipContent.TraitSummary(trait);

        Assert.That(summary,Does.Not.Contain("긴"));
        Assert.That(summary,Does.Contain("투지 3초").And.Contain("최대 체력 15% 보호막"));
        Assert.That(summary,Does.Not.Contain("조건"));
    }

    [Test]
    public void GroundLootUsesSharedLowDensityPointFilteredSprites()
    {
        MethodInfo factory=typeof(MonsterAutoBattlePrototype).GetMethod(
            "LootPixelSprite",BindingFlags.NonPublic|BindingFlags.Static);

        Assert.That(factory,Is.Not.Null);
        Sprite gold=(Sprite)factory.Invoke(null,new object[]{false});
        Sprite item=(Sprite)factory.Invoke(null,new object[]{true});

        Assert.That(gold.texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(item.texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(gold.rect.width,Is.LessThanOrEqualTo(11f));
        Assert.That(item.rect.width,Is.LessThanOrEqualTo(11f));
        Assert.That(item,Is.Not.SameAs(gold));
    }

    [Test]
    public void RegionBackgroundAssetsAreLoadable()
    {
        string[] regionIds=
        {
            "prototype_region","poison_fog_swamp","crimson_jungle",
            "frozen_highlands","sky_sanctuary"
        };
        for(int i=0;i<regionIds.Length;i++)
        {
            string root=MonsterAutoBattlePrototype.EnvironmentResourceRoot(regionIds[i])+"/";
            Assert.That(Resources.Load<Texture2D>(root+"BattleBackground"),Is.Not.Null,root);
        }
    }

    [Test]
    public void PrimordialMeadowPixelMotionShaderIsAvailable()
    {
        Shader shader=Shader.Find("BecomeZ/Environment/Primordial Meadow Pixel Motion");
        Assert.That(shader,Is.Not.Null);
        Material material=new Material(shader);
        Assert.That(material.HasProperty("_SwampMode"),Is.True);
        Assert.That(material.HasProperty("_JungleMode"),Is.True);
        Assert.That(material.HasProperty("_FrozenMode"),Is.True);
        Assert.That(material.HasProperty("_SkyMode"),Is.True);
        Assert.That(material.HasProperty("_FrozenMotionMask"),Is.True);
        Assert.That(material.HasProperty("_SkyMotionMask"),Is.True);
        Assert.That(material.HasProperty("_SkyCleanBackground"),Is.True);
        Assert.That(Resources.Load<Texture2D>(
            "Environment/FrozenHighlands/BattleMotionMask"),Is.Not.Null);
        Assert.That(Resources.Load<Texture2D>(
            "Environment/SkySanctuary/BattleMotionMask"),Is.Not.Null);
        Assert.That(Resources.Load<Texture2D>(
            "Environment/SkySanctuary/BattleCleanBackground"),Is.Not.Null);
        Object.DestroyImmediate(material);
    }

    [Test]
    public void NineSlotBenchSharesCombatCenter()
    {
        var rawGridWorld=typeof(MonsterAutoBattlePrototype).GetMethod(
            "RawGridWorld",System.Reflection.BindingFlags.Static|
            System.Reflection.BindingFlags.NonPublic);
        Assert.That(rawGridWorld,Is.Not.Null);
        Vector3 first=(Vector3)rawGridWorld.Invoke(
            null,new object[]{new Vector2Int(-1,-1),true});
        Vector3 last=(Vector3)rawGridWorld.Invoke(
            null,new object[]{new Vector2Int(7,-1),true});
        float combatCenter=(MonsterAutoBattlePrototype.HexDeploymentColumns-0.5f)*
            MonsterAutoBattlePrototype.HexDeploymentTileSize*
            MonsterAutoBattlePrototype.HexDeploymentSpacingScale*0.5f;
        Assert.That((first.x+last.x)*0.5f,Is.EqualTo(combatCenter).Within(0.001f));
    }

    private static void AssertInside(Rect child,Rect parent,string label)
    {
        const float tolerance=0.01f;
        Assert.That(child.xMin,Is.GreaterThanOrEqualTo(parent.xMin-tolerance),label);
        Assert.That(child.yMin,Is.GreaterThanOrEqualTo(parent.yMin-tolerance),label);
        Assert.That(child.xMax,Is.LessThanOrEqualTo(parent.xMax+tolerance),label);
        Assert.That(child.yMax,Is.LessThanOrEqualTo(parent.yMax+tolerance),label);
    }
}
