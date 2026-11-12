using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class TyrannoReignTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private HashSet<GameObject> existing;
    private MonsterAutoBattlePrototype battle;
    private Type type,unitType;
    private IList units;
    private object state;
    private object Call(string name,params object[] args)=>type.GetMethod(name,Private).Invoke(battle,args);
    private void Set(int i,string field,object value)=>unitType.GetField(field).SetValue(units[i],value);
    private T Get<T>(int i,string field)=>(T)unitType.GetField(field).GetValue(units[i]);
    private T State<T>(string field)=>(T)state.GetType().GetField(field).GetValue(state);
    private void Tick(float dt)=>Call("TickTyrannoReign",units[0],state,dt);
    private void Queue()=>Call("QueueTyrannoReign",units[0],0);
    private static Bounds VisibleBody(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Private).Invoke(null,new object[]{sprite});

    [SetUp] public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Reign test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();type=battle.GetType();
        unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,new Vector3(50,1,50)));
        units=(IList)type.GetField("units",Private).GetValue(battle);
        for(int i=0;i<6;i++)
        {
            units.Add(Activator.CreateInstance(unitType,true));
            var model=new GameObject("Reign unit "+i);model.transform.position=new Vector3(i*4,0,i*4);
            Set(i,"model",model);Set(i,"ring",new GameObject("Ring"));Set(i,"id",i);Set(i,"stableId","reign-"+i);
            Set(i,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_magno"));
            Set(i,"alive",true);Set(i,"hp",1000f);Set(i,"maxHp",1000f);Set(i,"damage",100f);
            Set(i,"collisionRadius",.35f);Set(i,"attackRange",1);
            Set(i,"team",Enum.ToObject(unitType.GetField("team").FieldType,i==0||i==4?0:1));
        }
        Set(0,"augmentTranscended",true);Set(0,"skillUses",1);Set(5,"inStorage",true);
        Get<CombatStatusState>(2,"combatStatuses").ControlImmune=true;Set(3,"statusResistance",100f);
        state=Call("FamilyTranscendenceStateFor",units[0],true);
    }

    [TearDown] public void Cleanup()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }

    [TestCase("tino",0,TyrannoRank3Rules.PrimordialMaw)]
    [TestCase("tino",1,TyrannoRank3Rules.PrimordialMaw)]
    [TestCase("magno",0,TyrannoRank3Rules.MassExtinction)]
    [TestCase("magno",1,TyrannoRank3Rules.MassExtinction)]
    [TestCase("cheolno",0,TyrannoRank3Rules.TyrantMarch)]
    [TestCase("cheolno",1,TyrannoRank3Rules.TyrantMarch)]
    [TestCase("bonrex",0,"")] [TestCase("bonrex",1,"")]
    public void RankFourCastStartsReignDirectlyWithoutBasicOrRankThreeAttacks(string species,int team,string rankThree)
    {
        Set(0,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(0,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        for(int i=1;i<units.Count;i++)Set(i,"team",Enum.ToObject(unitType.GetField("team").FieldType,i==4?team:1-team));
        Set(0,"skillId",TyrannoCombatRules.SkillId);Set(0,"skillUses",0);Set(0,"skillCooldownDuration",8.5f);
        var scaling=GameMasterData.MonsterGrowth(species);
        if(scaling.skillScalingStat=="MaxHealth")
        {Set(0,"maxHp",100f/scaling.skillScalingRatio);Set(0,"hp",Get<float>(0,"maxHp"));}
        else if(scaling.skillScalingStat=="Defense")Set(0,"defense",100f/scaling.skillScalingRatio);
        Assert.That((float)Call("SkillPower",units[0]),Is.EqualTo(100f).Within(.001f));
        Set(0,"target",units[1]);Get<GameObject>(1,"model").transform.position=Vector3.right*500f;
        if(!string.IsNullOrEmpty(rankThree))Get<List<string>>(0,"activeTraitIds").Add(rankThree);
        var skill=GameMasterData.Skill(TyrannoCombatRules.SkillId);
        Assert.That((bool)Call("CanStartFocusedAreaSkill",units[0],units[1],skill,false),Is.True);
        Assert.That((float)Call("SkillActivationDistance",units[0],units[1],skill),Is.EqualTo(float.PositiveInfinity));
        Assert.That(((SkillUseSnapshot)Call("BuildSkillSnapshot",units[0])).enemiesInEffect,Is.EqualTo(3));
        Assert.That((bool)Call("ExecuteReadySkill",units[0],false),Is.True);
        Assert.That(Get<int>(0,"skillUses"),Is.EqualTo(1));Assert.That(Get<float>(0,"skillTimer"),Is.EqualTo(8.5f));
        Assert.That(State<float>("tyrannoStompTime"),Is.Zero);
        Assert.That(Get<bool>(0,"tyrannoTremorPending"),Is.False);
        Assert.That(unitType.GetField("tyrannoMarch").GetValue(units[0]),Is.Null);
        Assert.That(unitType.GetField("tyrannoMaw").GetValue(units[0]),Is.Null);
        Assert.That((bool)Call("TryUseSkill",units[0],true),Is.False,"Combined cast cannot restart an active summon.");
        Assert.That((bool)Call("BeginTyrantTremor",units[0],skill),Is.False);
        Assert.That((bool)Call("ResolveTyrantTremor",units[0],skill,1f,true,0),Is.False,"An echo cannot restore the old attack.");
        Tick(.499f);Assert.That(Get<float>(1,"hp"),Is.EqualTo(1000f));
        Tick(.0011f);Tick(.6f);
        for(int i=1;i<=3;i++)Assert.That(Get<float>(i,"hp"),Is.EqualTo(800f));
        Assert.That(Get<float>(4,"hp"),Is.EqualTo(1000f));Assert.That(Get<float>(5,"hp"),Is.EqualTo(1000f));
        Assert.That(Get<int>(0,"skillHits"),Is.EqualTo(3));Assert.That(Get<int>(0,"traitActivations"),Is.EqualTo(1));
        Tick(.7f);Set(0,"skillTimer",0f);
        Assert.That((bool)Call("BeginTyrantTremor",units[0],skill),Is.True);
        Tick(.5f);Assert.That(Get<float>(1,"hp"),Is.EqualTo(600f));
        Assert.That(Get<int>(0,"skillUses"),Is.EqualTo(2));Assert.That(Get<int>(0,"skillHits"),Is.EqualTo(6));
    }

    [TestCase("")] [TestCase(TyrannoRank3Rules.MassExtinction)]
    [TestCase(TyrannoRank3Rules.TyrantMarch)] [TestCase(TyrannoRank3Rules.PrimordialMaw)]
    public void NontranscendedUnitsKeepTheirAuthoredBasicAndRankThreePreparation(string rankThree)
    {
        Set(0,"augmentTranscended",false);Set(0,"skillUses",0);Set(0,"skillCooldownDuration",8.5f);
        Get<GameObject>(1,"model").transform.position=Vector3.right;
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        Set(0,"spriteAnimation",model.GetComponentInChildren<SpriteMonsterAnimationDriver>());
        if(!string.IsNullOrEmpty(rankThree))Get<List<string>>(0,"activeTraitIds").Add(rankThree);
        var skill=GameMasterData.Skill(TyrannoCombatRules.SkillId);
        Assert.That((bool)Call("BeginTyrantTremor",units[0],skill),Is.True);
        Assert.That(Get<bool>(0,"tyrannoTremorPending"),Is.True);
        Assert.That(Get<int>(0,"skillUses"),Is.EqualTo(1));
        Assert.That(State<float>("tyrannoStompTime"),Is.EqualTo(-1f));Assert.That(State<int>("tyrannoPending"),Is.Zero);
        Assert.That(Get<string>(0,"currentAction"),Is.EqualTo("tyrant_tremor_prepare"));
    }

    [Test] public void LandingHitsWholeBoardOnceAndRespectsTeamsStorageAndControlResistance()
    {
        Call("TriggerFamilyTranscendenceSkill",units[0]);
        Assert.That(Get<CombatStatusState>(1,"combatStatuses").Count,Is.Zero,"No legacy fear/slow at cast start.");
        Queue();Queue();Assert.That(State<int>("tyrannoPending"),Is.EqualTo(1));
        Set(0,"tyrannoTremorPending",true);Tick(1f);Assert.That(State<float>("tyrannoStompTime"),Is.EqualTo(-1f));
        Set(0,"tyrannoTremorPending",false);Tick(0f);Tick(.49f);
        Assert.That(Get<float>(1,"hp"),Is.EqualTo(1000f));
        Tick(.01f);
        for(int i=1;i<=3;i++)Assert.That(Get<float>(i,"hp"),Is.EqualTo(800f));
        Assert.That(Get<float>(4,"hp"),Is.EqualTo(1000f));Assert.That(Get<float>(5,"hp"),Is.EqualTo(1000f));
        Assert.That(Get<CombatStatusState>(1,"combatStatuses").Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(1.2f).Within(.001));
        Assert.That(Get<CombatStatusState>(2,"combatStatuses").Has(CombatStatusKind.Knockdown),Is.False);
        Assert.That(Get<CombatStatusState>(3,"combatStatuses").Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(.6f).Within(.001));
        Tick(.6f);Tick(.6f);Assert.That(Get<float>(1,"hp"),Is.EqualTo(800f));
        Assert.That(Get<int>(0,"traitActivations"),Is.EqualTo(1));
        Tick(.5f);Assert.That(State<float>("tyrannoStompTime"),Is.EqualTo(-1f));
    }

    [Test] public void NextCastQueuesWithoutRefreshingAnActiveStomp()
    {
        Queue();Tick(0f);Tick(.5f);Set(0,"skillUses",2);Queue();Queue();
        Tick(.4f);Assert.That(State<float>("tyrannoStompTime"),Is.EqualTo(.9f).Within(.001));
        Assert.That(State<int>("tyrannoPending"),Is.EqualTo(1));
        Tick(1.3f);Tick(0f);Tick(.5f);
        Assert.That(Get<float>(1,"hp"),Is.EqualTo(600f));Assert.That(Get<int>(0,"traitActivations"),Is.EqualTo(2));
    }

    [TestCase(false)] [TestCase(true)] public void DeathOrBattleEndCancelsBeforeLanding(bool battleEnd)
    {
        Queue();Tick(0f);Tick(.2f);
        if(battleEnd)type.GetField("battleEnding",Private).SetValue(battle,true);else Set(0,"alive",false);
        Tick(1f);Assert.That(Get<float>(1,"hp"),Is.EqualTo(1000f));
        Assert.That(State<int>("tyrannoPending"),Is.Zero);Assert.That(State<float>("tyrannoStompTime"),Is.EqualTo(-1f));
    }

    [TestCase("tino",false,"ImpactMotionTinoV3")] [TestCase("magno",false,"ImpactMotionMagno")]
    [TestCase("cheolno",false,"ImpactMotionCheolno")] [TestCase("bonrex",false,"ImpactMotionBonrex")]
    [TestCase("bonrex",true,"ImpactMotionBonrexShiny")]
    public void LandingUsesTheAuthoredSpeciesAndShinyLowerLegAnimation(string species,bool shiny,string atlas)
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var fx=PixelTyrannoReignStomp.Create(new Bounds(Vector3.zero,new Vector3(20,1,20)),model.GetComponentInChildren<SpriteRenderer>(),100,species,shiny);
        var foot=fx.transform.Find("Animated tyrant foot").GetComponent<SpriteRenderer>();
        var shadow=fx.transform.Find("Growing foot shadow").GetComponent<SpriteRenderer>();
        fx.Advance(.5f);
        var expected=Resources.Load<Texture2D>(PixelTyrannoReignStomp.ResourceRoot+atlas);
        if(expected==null&&atlas=="ImpactMotionTinoV3")expected=Resources.Load<Texture2D>(PixelTyrannoReignStomp.ResourceRoot+"ImpactMotionTino");
        Assert.That(foot.sprite.texture,Is.SameAs(expected));
        float cell=foot.sprite.texture.width/4f;
        Assert.That(foot.sprite.rect.x,Is.EqualTo(cell));Assert.That(foot.sprite.rect.y,Is.EqualTo(cell));
        Assert.That(foot.sprite.pivot,Is.EqualTo(new Vector2(.5f,.21875f)*cell));
        Assert.That(foot.color.a,Is.GreaterThan(.8f));
        Assert.That(shadow.sprite,Is.Not.SameAs(foot.sprite),"The warning shadow belongs to the ground, not the whole upright shin.");
        Assert.That(DistinctAnatomyShapes(foot.sprite.texture),Is.GreaterThanOrEqualTo(8),
            "Twelve cells must contain actual toe/ankle deformation, not copies of one sprite shifted or enlarged.");
        fx.Cancel();
    }

    private static int DistinctAnatomyShapes(Texture2D texture)
    {
        var pixels=texture.GetPixels32();int w=texture.width/4,h=texture.height/3;var shapes=new HashSet<ulong>();
        for(int frame=0;frame<12;frame++)
        {
            int x0=frame%4*w,y0=(2-frame/4)*h,minX=w,minY=h,maxX=-1,maxY=-1;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[(y0+y)*texture.width+x0+x].a>128)
            {minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
            Assert.That(maxX,Is.GreaterThanOrEqualTo(minX),"Authored foot frame "+frame+" is empty.");
            ulong hash=14695981039346656037UL;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                int px=minX+x*(maxX-minX+1)/64,py=minY+y*(maxY-minY+1)/64;
                hash=unchecked((hash^(pixels[(y0+py)*texture.width+x0+px].a>128?1UL:0UL))*1099511628211UL);
            }
            shapes.Add(hash);
        }
        return shapes.Count;
    }

    [Test] public void ARealGroundCraterRemainsAfterTheAnimatedFootWithdrawsWhileKnockdownPersists()
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var fx=PixelTyrannoReignStomp.Create(new Bounds(Vector3.zero,new Vector3(20,1,20)),model.GetComponentInChildren<SpriteRenderer>(),100);
        Assert.That(fx,Is.Not.Null);
        var foot=fx.transform.Find("Animated tyrant foot").GetComponent<SpriteRenderer>();
        var shadow=fx.transform.Find("Growing foot shadow").GetComponent<SpriteRenderer>();
        var crater=fx.transform.Find("Authored asymmetric pixel ground fracture").GetComponent<MeshRenderer>();
        Assert.That(fx.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&!(r is ParticleSystemRenderer)),Is.False,
            "The summon frame must not leak its warning before the cinematic starts.");
        fx.Advance(.2f);Assert.That(foot.enabled,Is.False);Assert.That(shadow.enabled,Is.True);Assert.That(crater.enabled,Is.False);
        fx.Advance(.5f);Assert.That(foot.enabled,Is.True);
        Assert.That(PixelTyrannoReignStomp.FrameAt(.5f),Is.EqualTo(5));
        Queue();Tick(0f);Tick(.5f);
        fx.Advance(1.25f);Assert.That(foot.enabled||shadow.enabled,Is.False);Assert.That(crater.enabled,Is.True);
        var status=Get<CombatStatusState>(1,"combatStatuses");
        Assert.That(status.Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(1.2f).Within(.001));
        Assert.That(PixelTyrannoReignStomp.FrameAt(1.6f),Is.EqualTo(-1));
        fx.Advance(FamilyTranscendenceRules.TyrannoStompEnd);
        Assert.That(fx.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&!(r is ParticleSystemRenderer)),Is.False);
        Assert.That(fx.GetComponentsInChildren<ParticleSystem>().All(p=>p.particleCount==0),Is.True);
        fx.Cancel();fx.Cancel();Assert.That(fx==null,Is.True);
    }

    [TestCase(false)][TestCase(true)]
    public void TwelveToeAndAnkleFramesAnimateAtAFixedPlantAnchorFacingTheActualGameCamera(bool mirrored)
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var reference=model.GetComponentInChildren<SpriteRenderer>();reference.transform.rotation=Quaternion.Euler(58f,0f,0f);reference.flipX=!mirrored;
        var fx=PixelTyrannoReignStomp.Create(new Bounds(Vector3.zero,new Vector3(20,1,20)),reference,100,"tino",false,mirrored);
        var foot=fx.FootRenderer;Vector3 position=foot.transform.position,scale=foot.transform.localScale;
        float[] times={.365f,.398f,.425f,.453f,.485f,.515f,.575f,.68f,.78f,.9f,1.02f,1.15f};
        var poses=new HashSet<Sprite>();
        for(int i=0;i<times.Length;i++)
        {
            fx.Advance(times[i]);Assert.That(foot.enabled,Is.True);Assert.That(PixelTyrannoReignStomp.FrameAt(times[i]),Is.EqualTo(i));
            Assert.That(poses.Add(foot.sprite),Is.True,"Each phase changes authored anatomy rather than moving one flat stamp.");
            Assert.That(foot.transform.position,Is.EqualTo(position));Assert.That(foot.transform.localScale,Is.EqualTo(scale));
            Assert.That(foot.flipX,Is.EqualTo(mirrored),"The team's right foot cannot switch when the child's target facing changes.");
            Assert.That(Quaternion.Angle(foot.transform.rotation,reference.transform.rotation),Is.LessThan(.001f));
            Assert.That(Vector3.Dot(foot.transform.up,Vector3.up),Is.GreaterThan(.5f),"The authored shin faces the actual 58 degree battlefield camera.");
        }
        Vector3 contact=foot.transform.TransformPoint(new Vector3(fx.ContactBounds.center.x,fx.ContactBounds.center.y,0f));
        Assert.That(Vector3.Distance(fx.LandingAnchor,contact),Is.LessThan(.001f));Assert.That(scale.x,Is.EqualTo(scale.y));
        fx.Cancel();
    }

    [TestCase(false,false)] [TestCase(false,true)] [TestCase(true,false)] [TestCase(true,true)]
    public void TheVisibleSoleCoversOneHundredAndTenPercentOfTheActualArenaAtTheGameCamera(bool orthographic,bool mirrored)
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var reference=model.GetComponentInChildren<SpriteRenderer>();reference.transform.rotation=Quaternion.Euler(35f,0f,0f);
        var camera=new GameObject("Real 58 degree stomp camera").AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=orthographic;camera.orthographicSize=20f;camera.fieldOfView=36f;camera.aspect=16f/9f;
        var arena=new Bounds(new Vector3(2f,0f,3f),new Vector3(26.95f,1f,13.4351f));
        Quaternion facing=Quaternion.Euler(58f,0f,0f);
        camera.transform.SetPositionAndRotation(arena.center-facing*Vector3.forward*46f,facing);
        var fx=PixelTyrannoReignStomp.Create(arena,reference,100,"tino",false,mirrored,camera);fx.Advance(.5f);
        Rect contact=fx.ContactViewportRect(camera);
        for(int i=0;i<4;i++)
        {
            Vector3 world=fx.LandingAnchor+new Vector3((i<2?-1f:1f)*arena.extents.x*1.10f,0f,(i%2==0?-1f:1f)*arena.extents.z*1.05f);
            Vector3 p=camera.WorldToViewportPoint(world);
            Assert.That(p.x,Is.InRange(contact.xMin-.0001f,contact.xMax+.0001f));
            Assert.That(p.y,Is.InRange(contact.yMin-.0001f,contact.yMax+.0001f));
        }
        Assert.That(fx.FootprintWorldSize.x,Is.GreaterThanOrEqualTo(arena.size.x*1.10f-.001f));
        Assert.That(fx.FootprintWorldSize.y,Is.GreaterThanOrEqualTo(arena.size.z*1.05f-.001f));
        Assert.That(Quaternion.Angle(fx.FootRenderer.transform.rotation,camera.transform.rotation),Is.LessThan(.001f));
        Rect actual=VisibleContactViewport(fx,camera);
        Assert.That(actual.width,Is.GreaterThanOrEqualTo(contact.width*.95f),"Opaque forefoot pixels must fill the contact metadata, not a tall shin or transparent padding.");
        Assert.That(actual.height,Is.GreaterThanOrEqualTo(contact.height*.95f));
        Vector3 scale=fx.FootRenderer.transform.localScale;fx.Advance(.94f);
        Assert.That(fx.FootRenderer.transform.localScale,Is.EqualTo(scale));
        Assert.That(fx.ContactViewportRect(camera),Is.EqualTo(contact));
        fx.Cancel();
    }

    private static Rect VisibleContactViewport(PixelTyrannoReignStomp fx,Camera camera)
    {
        var renderer=fx.FootRenderer;Sprite sprite=renderer.sprite;Color32[] pixels=sprite.texture.GetPixels32();
        Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=-min;
        int x0=Mathf.RoundToInt(sprite.rect.x),y0=Mathf.RoundToInt(sprite.rect.y),w=Mathf.RoundToInt(sprite.rect.width),h=Mathf.RoundToInt(sprite.rect.height);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            if(pixels[(y0+y)*sprite.texture.width+x0+x].a<=32)continue;
            Vector2 p=(new Vector2(x+.5f,y+.5f)-sprite.pivot)/sprite.pixelsPerUnit;if(renderer.flipX)p.x=-p.x;
            if(!fx.ContactBounds.Contains(p))continue;
            Vector2 viewport=camera.WorldToViewportPoint(renderer.transform.TransformPoint(new Vector3(p.x,p.y,0f)));
            min=Vector2.Min(min,viewport);max=Vector2.Max(max,viewport);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    [Test] public void LayeredImpactDustCracksAndTerrainChunksFollowTheAbsoluteClockWithoutAccumulating()
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var fx=PixelTyrannoReignStomp.Create(new Bounds(Vector3.zero,new Vector3(20,1,20)),model.GetComponentInChildren<SpriteRenderer>(),100);
        var clouds=fx.GetComponentsInChildren<ParticleSystem>();Assert.That(clouds.Length,Is.EqualTo(3));
        Assert.That(clouds.Select(p=>p.GetComponent<ParticleSystemRenderer>().sortingOrder).Distinct().Count(),Is.EqualTo(3));
        Assert.That(clouds.All(p=>p.main.simulationSpeed==0f&&!p.emission.enabled),Is.True);
        Assert.That(clouds.All(p=>p.main.maxParticles<=8),Is.True,"Sparse brown grit supports the drawn dust, instead of a ring of tiny glitter particles.");
        Assert.That(fx.GetComponentsInChildren<LineRenderer>(),Is.Empty);
        var crater=fx.transform.Find("Authored asymmetric pixel ground fracture").GetComponent<MeshRenderer>();
        Assert.That(crater.sharedMaterial.mainTexture,Is.SameAs(Resources.Load<Texture2D>(PixelTyrannoReignStomp.ResourceRoot+"PixelCraterMotionV3")));
        Assert.That(crater.GetComponent<MeshFilter>().sharedMesh.vertices.All(p=>p.y==0f),Is.True);
        fx.Advance(.49f);Assert.That(crater.enabled,Is.False);
        fx.Advance(.59f);Assert.That(crater.enabled,Is.True);
        Assert.That(fx.transform.Find("Authored pixel earth shock").GetComponent<SpriteRenderer>().enabled,Is.True);
        var volumes=fx.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.Contains("authored pixel dust volume")).ToArray();
        Assert.That(volumes.Length,Is.EqualTo(8));Assert.That(volumes.All(r=>r.enabled),Is.True);
        var dust=Resources.Load<Texture2D>(PixelTyrannoReignStomp.ResourceRoot+"PixelDustBurst");Assert.That(dust,Is.Not.Null);
        Assert.That(volumes.All(r=>r.sprite.texture==dust),Is.True);
        Assert.That(volumes.Where(r=>r.name.StartsWith("Near")).All(r=>r.sortingOrder>fx.FootRenderer.sortingOrder),Is.True);
        Assert.That(volumes.Where(r=>r.name.StartsWith("Far")).All(r=>r.sortingOrder<fx.FootRenderer.sortingOrder),Is.True);
        Assert.That(volumes.All(r=>VisibleBody(r.sprite).size.y*r.transform.localScale.y>fx.FootprintWorldSize.y*.14f),Is.True,
            "Drawn pixels, rather than transparent atlas padding, give the foot's outer edge substantial dust volume.");
        Assert.That(volumes.All(r=>Mathf.Abs(r.transform.localPosition.z)>fx.FootprintWorldSize.y*.30f),Is.True,
            "Pressure dust begins at the forefoot's near/far perimeter instead of a small ring at the shin pivot.");
        Vector3[] volumePositions=volumes.Select(r=>r.transform.localPosition).ToArray();
        Sprite[] volumeFrames=volumes.Select(r=>r.sprite).ToArray();
        var chunks=fx.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.StartsWith("Burst terrain plate")).ToArray();
        Assert.That(chunks.Length,Is.EqualTo(12));Assert.That(chunks.All(r=>r.enabled),Is.True);
        Assert.That(chunks.Max(r=>r.transform.localScale.x),Is.GreaterThan(.9f));
        Assert.That(chunks.All(r=>r.sprite.texture==Resources.Load<Texture2D>(PixelTyrannoReignStomp.ResourceRoot+"PixelRubbleMotionV3")),Is.True);
        Assert.That(chunks.All(r=>r.sprite.rect.width==32f&&r.sprite.rect.height==32f),Is.True);
        Assert.That(chunks.All(r=>Vector3.Dot(r.transform.forward,fx.FootRenderer.transform.forward)>.9999f),Is.True,
            "Drawn pixel rubble turns only within the billboard plane.");
        Assert.That(fx.GetComponentsInChildren<MeshFilter>().Length,Is.EqualTo(1),"Only the textured ground is a mesh; flying chunks remain pixel art.");
        Vector3[] positions=chunks.Select(r=>r.transform.localPosition).ToArray();
        Vector3[] sizes=chunks.Select(r=>r.transform.localScale).ToArray();
        Vector2[] craterUV=crater.GetComponent<MeshFilter>().sharedMesh.uv;
        var samples=new Vector3[clouds.Length][];
        for(int i=0;i<clouds.Length;i++)
        {
            var particles=new ParticleSystem.Particle[160];int count=clouds[i].GetParticles(particles);
            Assert.That(count,Is.InRange(1,8));samples[i]=particles.Take(count).Select(p=>p.position).ToArray();
            Assert.That(particles.Take(count).All(p=>((Color)p.startColor).r<.9f),Is.True,"No golden spark core accompanies this physical stomp.");
        }
        fx.Advance(.95f);Assert.That(chunks.Select(r=>r.transform.localScale).ToArray(),Is.EqualTo(sizes));
        fx.Advance(.59f);fx.Advance(.59f);
        Assert.That(chunks.Select(r=>r.transform.localPosition).ToArray(),Is.EqualTo(positions));
        Assert.That(volumes.Select(r=>r.transform.localPosition).ToArray(),Is.EqualTo(volumePositions));
        Assert.That(volumes.Select(r=>r.sprite).ToArray(),Is.EqualTo(volumeFrames));
        Assert.That(crater.GetComponent<MeshFilter>().sharedMesh.uv,Is.EqualTo(craterUV));
        for(int i=0;i<clouds.Length;i++)
        {
            var particles=new ParticleSystem.Particle[160];int count=clouds[i].GetParticles(particles);
            Assert.That(particles.Take(count).Select(p=>p.position).ToArray(),Is.EqualTo(samples[i]));
        }
        Mesh ground=crater.GetComponent<MeshFilter>().sharedMesh;Material groundMaterial=crater.sharedMaterial;
        fx.Cancel();Assert.That(ground==null&&groundMaterial==null,Is.True);
    }

    [TestCase("tino",false)] [TestCase("magno",false)] [TestCase("cheolno",false)]
    [TestCase("bonrex",false)] [TestCase("bonrex",true)]
    public void EveryNativeSpriteEffectUsesAtLeastFourAuthoredDrawingsAndTheCraterHasFourGrowthFrames(string species,bool shiny)
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var fx=PixelTyrannoReignStomp.Create(new Bounds(Vector3.zero,new Vector3(27f,1f,13.5f)),model.GetComponentInChildren<SpriteRenderer>(),100,species,shiny);
        var renderers=fx.GetComponentsInChildren<SpriteRenderer>();
        var drawings=renderers.ToDictionary(r=>r,r=>new HashSet<ulong>());
        var spriteHashes=new Dictionary<Sprite,ulong>();var texturePixels=new Dictionary<Texture2D,Color32[]>();
        var shadowFrames=new HashSet<Sprite>();var groundFrames=new HashSet<Vector4>();
        var ground=fx.transform.Find("Authored asymmetric pixel ground fracture").GetComponent<MeshRenderer>();
        var mesh=ground.GetComponent<MeshFilter>().sharedMesh;
        for(int tick=0;tick<=170;tick++)
        {
            fx.Advance(tick*.01f);
            foreach(SpriteRenderer renderer in renderers)
            {
                if(!renderer.enabled||renderer.sprite==null)continue;
                Sprite sprite=renderer.sprite;
                if(!spriteHashes.TryGetValue(sprite,out ulong hash))
                {
                    hash=DrawingHash(sprite.texture,sprite.rect,texturePixels);spriteHashes[sprite]=hash;
                }
                drawings[renderer].Add(hash);
                if(renderer.name=="Growing foot shadow")shadowFrames.Add(sprite);
            }
            if(ground.enabled)
            {
                Vector2[] uv=mesh.uv;groundFrames.Add(new Vector4(uv[0].x,uv[0].y,uv[2].x,uv[2].y));
            }
        }
        foreach(var drawing in drawings)Assert.That(drawing.Value.Count,Is.GreaterThanOrEqualTo(4),
            drawing.Key.name+" must actually change its authored pixels; translating or scaling one drawing is insufficient.");
        Assert.That(shadowFrames.Count,Is.EqualTo(4));
        Assert.That(shadowFrames.All(s=>s.rect.height<=fx.FootRenderer.sprite.rect.height*.5f),Is.True,
            "Ground silhouettes crop the toes and sole, without projecting the whole upright shin onto the map.");
        Assert.That(groundFrames.Count,Is.EqualTo(4));
        var texture=(Texture2D)ground.sharedMaterial.mainTexture;var craterDrawings=new HashSet<ulong>();
        for(int i=0;i<4;i++)craterDrawings.Add(DrawingHash(texture,new Rect(i*256,0,256,256),texturePixels));
        Assert.That(craterDrawings.Count,Is.EqualTo(4),"Four genuinely different fissure drawings accompany four atlas UV frames.");
        var properties=new MaterialPropertyBlock();ground.GetPropertyBlock(properties);
        Assert.That(properties.GetVector(Shader.PropertyToID("_PixelGrid")),Is.EqualTo(new Vector4(1024f,256f,1f/1024f,1f/256f)));
        fx.Cancel();Assert.That(shadowFrames.All(s=>s==null),Is.True,"Per-cast cropped silhouettes are explicitly released on cancellation.");
    }

    private static ulong DrawingHash(Texture2D texture,Rect rect,Dictionary<Texture2D,Color32[]> pixels)
    {
        if(!pixels.TryGetValue(texture,out Color32[] data)){data=ReadPixels(texture);pixels[texture]=data;}
        ulong hash=14695981039346656037UL;
        int x0=Mathf.RoundToInt(rect.x),y0=Mathf.RoundToInt(rect.y),w=Mathf.RoundToInt(rect.width),h=Mathf.RoundToInt(rect.height);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            Color32 color=data[(y0+y)*texture.width+x0+x];
            uint visible=color.a>32?((uint)color.r<<24)|((uint)color.g<<16)|((uint)color.b<<8)|color.a:0u;
            hash=unchecked((hash^visible)*1099511628211UL);
        }
        return hash;
    }

    private static Color32[] ReadPixels(Texture2D texture)
    {
        if(texture.isReadable)return texture.GetPixels32();
        RenderTexture previous=RenderTexture.active,target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);
        var copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(texture,target);RenderTexture.active=target;
            copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();return copy.GetPixels32();
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(copy);}
    }

    [Test]public void APhysicalStompHasOneShortPressureRingAndItsDustFallsAwayBeforeTheResidualCrater()
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var fx=PixelTyrannoReignStomp.Create(new Bounds(Vector3.zero,new Vector3(20,1,20)),model.GetComponentInChildren<SpriteRenderer>(),100);
        var ring=fx.transform.Find("Authored pixel earth shock").GetComponent<SpriteRenderer>();
        var dust=fx.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.Contains("authored pixel dust volume")).ToArray();
        fx.Advance(.49f);Assert.That(ring.enabled||dust.Any(r=>r.enabled),Is.False);
        fx.Advance(.55f);Assert.That(ring.enabled&&dust.All(r=>r.enabled),Is.True);
        fx.Advance(.76f);Assert.That(ring.enabled,Is.False);Assert.That(dust.All(r=>r.enabled),Is.True);
        fx.Advance(1.10f);Assert.That(ring.enabled||dust.Any(r=>r.enabled),Is.False);
        Assert.That(fx.transform.Find("Authored asymmetric pixel ground fracture").GetComponent<MeshRenderer>().enabled,Is.True);
        fx.Cancel();
    }

    [Test] public void AReleasedBattleEndPlaybackKeepsTheNativeFootUntilItsNormalVisualTailCompletes()
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var fx=PixelTyrannoReignStomp.Create(new Bounds(Vector3.zero,new Vector3(20,1,20)),model.GetComponentInChildren<SpriteRenderer>(),100);
        fx.Advance(.59f);CombatEffectEndFade.Begin(fx.gameObject);fx.Cancel();
        Assert.That(fx!=null&&fx.gameObject.activeSelf,Is.True);Assert.That(CombatEffectEndFade.IsFading(fx),Is.True);
    }
}
