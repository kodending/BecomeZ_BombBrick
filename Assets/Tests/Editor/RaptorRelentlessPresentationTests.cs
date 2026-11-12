using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BombBrick.Combat;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class RaptorRelentlessPresentationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> owned=new List<GameObject>();
    private Dictionary<PixelCombatEffect,bool> previousEffects;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private IList units;
    private MonsterDatabase database;
    private Camera camera;
    private UnityEngine.Random.State random;
    private object frontend;
    private bool effectTest;
    private float timeScale;

    [SetUp] public void Setup()
    {
        random=UnityEngine.Random.state;timeScale=Time.timeScale;Time.timeScale=1f;
        effectTest=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=false;
        frontend=typeof(GameFrontend).GetField("instance",Private).GetValue(null);
        typeof(GameFrontend).GetField("instance",Private).SetValue(null,null);
        previousEffects=Resources.FindObjectsOfTypeAll<PixelCombatEffect>()
            .ToDictionary(effect=>effect,effect=>effect.gameObject.activeSelf);
        var host=Go("Relentless integration battle");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)BattleField("units").GetValue(battle);
        database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        BattleField("monsterDatabase").SetValue(battle,database);
        BattleField("playerProfile").SetValue(battle,new PlayerProfileSave());
        BattleField("expeditionRun").SetValue(battle,new ExpeditionRunSave
            {runId="relentless_presentation",regionId=HunterRegionCatalog.FirstRegionId});
        BattleField("combatBounds").SetValue(battle,new Bounds(Vector3.zero,new Vector3(30f,10f,20f)));
        BattleField("mlEpisodeActive").SetValue(battle,true);
        BattleField("battleStarted").SetValue(battle,true);
        camera=Go("Relentless integration camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(0f,10f,-16f);camera.transform.LookAt(Vector3.zero);
        camera.cullingMask=0;
    }

    [TearDown] public void Cleanup()
    {
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
        {
            if(effect==null||!effect.gameObject.scene.IsValid())continue;
            if(!previousEffects.TryGetValue(effect,out bool wasActive))
            {effect.ReleaseAfterBattleEndFade();Object.DestroyImmediate(effect.gameObject);}
            else if(!wasActive&&effect.gameObject.activeSelf&&effect.name=="Raptor Contact Claws")
                effect.ReleaseAfterBattleEndFade();
        }
        for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);
        owned.Clear();CombatEffectTestConfiguration.Active=effectTest;
        Time.timeScale=timeScale;UnityEngine.Random.state=random;
        typeof(GameFrontend).GetField("instance",Private).SetValue(null,frontend);
    }

    [TestCase("velos",0)] [TestCase("velos",1)]
    [TestCase("velok",0)] [TestCase("velok",1)]
    public void RealLockedSkillFacesItsVictimAndThrustsOnEveryStrikeIncludingWhenTheVictimChangesSides(string monsterId,int team)
    {
        float side=team==0?1f:-1f;
        object source=Actor(monsterId,team,Vector3.left*(3f*side));
        object target=Actor("velos",1-team,Vector3.zero);
        var driver=Driver(source);var billboard=Billboard(source);
        Vector3 initialLocal=driver.transform.localPosition;
        Set(source,"target",target);Set(target,"target",source);
        Vector3 direction=Model(target).transform.position-Model(source).transform.position;
        billboard.SetFacingDirection(-direction,true);
        driver.PlaySkillAttack(1f,null);
        Assert.That(driver.LocksHorizontalFacing,Is.True);
        bool opposite=driver.GetComponent<SpriteRenderer>().flipX;
        billboard.SetFacingDirection(direction);
        Assert.That(driver.GetComponent<SpriteRenderer>().flipX,Is.EqualTo(opposite),"Exercise the actual skill-facing lock.");

        Assert.That(Call("StartAssassinLeap",source,target,1f),Is.True);
        AssertFacing(source,target);
        Assert.That(driver.GetComponent<SpriteRenderer>().flipX,Is.Not.EqualTo(opposite));
        Call("PlayAssassinLeapAnimation",source);
        Call("UpdateAssassinLeap",source,.04f);AssertFacing(source,target);
        Call("UpdateAssassinLeap",source,.03f);AssertFacing(source,target);
        Call("UpdateAssassinLeap",source,.3f);
        Assert.That(Get(source,"leapComboStrikes"),Is.EqualTo(1));
        AssertFacing(source,target);AssertVisibleThrust(source,target,initialLocal);

        for(int strike=1;strike<3;strike++)
        {
            Model(target).transform.position=Model(source).transform.position+
                new Vector3((strike==1?-side:side)*1.1f,0f,.3f);
            Vector3 body=Model(source).transform.position;
            Call("UpdateRelentlessHunt",source,strike==1?.14f:.18f);
            Assert.That(Get(source,"leapComboStrikes"),Is.EqualTo(strike+1));
            Assert.That(Model(source).transform.position,Is.EqualTo(body),"Strike poses must not teleport the combat body.");
            AssertFacing(source,target);AssertVisibleThrust(source,target,initialLocal);
        }
        Call("UpdateRelentlessHunt",source,.09f);
        Assert.That(Get(source,"leapReturning"),Is.True);
        Model(target).transform.position=Model(source).transform.position+Vector3.left*(1.2f*side);
        Call("UpdateAssassinLeap",source,.045f);AssertFacing(source,target);
        Call("UpdateAssassinLeap",source,.2f);AssertFacing(source,target);
        Assert.That(Get(source,"leaping"),Is.False);
        Assert.That(Get(source,"target"),Is.SameAs(target));
        DriverField(driver,"raptorStrikeStartedFrame").SetValue(driver,-1);
        DriverCall(driver,"Advance",1f);DriverCall(driver,"LateUpdate");
        Assert.That(DriverField(driver,"raptorStrikeRemaining").GetValue(driver),Is.EqualTo(0f));
        Assert.That(DriverField(driver,"appliedRecoilOffset").GetValue(driver),Is.EqualTo(Vector3.zero));
        Assert.That(Vector3.Distance(driver.transform.localPosition,initialLocal),Is.LessThan(.025f),
            "The strike must restore its offset; the existing .018 held-frame quiver is separate.");
    }

    [TestCase("velos",0)] [TestCase("velos",1)]
    [TestCase("velok",0)] [TestCase("velok",1)]
    public void ThreeActualSkillHitsKeepTheirDamageAndSlideTheVictimWithoutLosingTheComboOrReturn(string monsterId,int team)
    {
        float side=team==0?1f:-1f;
        object source=Actor(monsterId,team,Vector3.left*(3f*side));
        object target=Actor("velos",1-team,Vector3.zero);
        Set(source,"target",target);Set(target,"target",source);
        float fullHp=Number(target,"hp");
        Call("ApplyRaptorLeapHit",source,target,1f,-1);
        float baseline=fullHp-Number(target,"hp");Assert.That(baseline,Is.GreaterThan(0f));
        Set(target,"hp",fullHp);Set(target,"hitSlideVelocity",Vector3.zero);Set(target,"hitSlideNextAt",0f);
        Set(source,"skillHits",0);Set(source,"skillDamage",0f);
        Assert.That(Call("StartAssassinLeap",source,target,1f),Is.True);
        Call("PlayAssassinLeapAnimation",source);
        float previousHp=fullHp,previousGate=0f;int observed=0,frames=0;
        const float dt=1f/120f;
        while((bool)Get(source,"leaping")&&frames++<240)
        {
            BattleField("battleElapsed").SetValue(battle,frames*dt);
            Call("UpdateAssassinLeap",source,dt*CombatStatFormula.CombatAnimationRate);
            AdvanceDriver(Driver(source),dt);AdvanceDriver(Driver(target),dt);
            int hits=(int)Get(source,"leapHitCount");
            if(hits>observed)
            {
                Assert.That(hits,Is.EqualTo(observed+1),"Each impact must resolve once at its own time.");
                float expected=baseline*(observed<2?.6f:1f);
                Assert.That(previousHp-Number(target,"hp"),Is.EqualTo(expected).Within(.02f));
                Assert.That(((Vector3)Get(target,"hitSlideVelocity")).sqrMagnitude,Is.GreaterThan(0f));
                Assert.That(Number(target,"hitSlideNextAt"),Is.GreaterThan(previousGate));
                previousGate=Number(target,"hitSlideNextAt");previousHp=Number(target,"hp");observed=hits;
                AssertFacing(source,target);
            }
            Call("AdvanceHitSlide",target,dt);
        }
        Assert.That(observed,Is.EqualTo(3));
        Assert.That(fullHp-Number(target,"hp"),Is.EqualTo(baseline*2.2f).Within(.03f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));
        Assert.That(Number(source,"skillDamage"),Is.EqualTo(baseline*2.2f).Within(.03f));
        Assert.That(Model(target).transform.position.x*side,Is.GreaterThan(.5f));
        Assert.That(Get(source,"leaping"),Is.False);Assert.That(Get(source,"leapReturning"),Is.False);
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Get(source,"positionRevision"),Is.EqualTo(0));
        Call("UpdateAssassinLeap",source,0f);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));
    }

    private object Actor(string monsterId,int team,Vector3 position)
    {
        var definition=database.Get(monsterId);Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null,definition.resourcePath);
        var model=Object.Instantiate(prefab);owned.Add(model);model.transform.position=position;
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var billboard=model.GetComponentInChildren<SpriteMonsterBillboard>(true);
        Assert.That(driver,Is.Not.Null);Assert.That(billboard,Is.Not.Null);
        DriverCall(driver,"Awake");driver.ResetForReuse();driver.SetCombatPlaybackSpeed(CombatStatFormula.CombatAnimationRate);
        typeof(SpriteMonsterBillboard).GetMethod("Awake",Private).Invoke(billboard,null);
        typeof(SpriteMonsterBillboard).GetField("cachedCamera",Private).SetValue(billboard,camera);
        typeof(SpriteMonsterBillboard).GetMethod("LateUpdate",Private).Invoke(billboard,null);
        object unit=Activator.CreateInstance(unitType,true);
        Set(unit,"id",units.Count);Set(unit,"stableId","relentless_"+units.Count);
        Set(unit,"model",model);Set(unit,"ring",Go("Relentless floor shadow"));Set(unit,"definition",definition);
        Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",10000f);Set(unit,"maxHp",10000f);Set(unit,"damage",100f);
        Set(unit,"attackRange",1);Set(unit,"collisionRadius",definition.collisionRadius*.84f);
        Set(unit,"skillId",definition.skillId);Set(unit,"skillTraitDamageMultiplier",1f);
        ((List<string>)Get(unit,"activeTraitIds")).Add(RaptorCombatRules.RelentlessHuntTraitId);
        units.Add(unit);return unit;
    }

    private void AssertFacing(object source,object target)
    {
        var billboard=Billboard(source);
        typeof(SpriteMonsterBillboard).GetMethod("LateUpdate",Private).Invoke(billboard,null);
        Vector3 direction=Model(target).transform.position-Model(source).transform.position;direction.y=0f;
        float horizontal=Vector3.Dot(direction.normalized,camera.transform.right);
        Assert.That(Mathf.Abs(horizontal),Is.GreaterThan(.1f));
        bool inverted=(bool)typeof(SpriteMonsterBillboard).GetField("invertHorizontalFacing",Private).GetValue(billboard);
        Assert.That(Driver(source).GetComponent<SpriteRenderer>().flipX,Is.EqualTo(inverted?horizontal<0f:horizontal>0f));
    }
    private void AssertVisibleThrust(object source,object target,Vector3 initialLocal)
    {
        var driver=Driver(source);
        // Sample the thrust peak so the independent held-frame quiver cannot cancel its opening pose.
        float duration=(float)DriverField(driver,"raptorStrikeDuration").GetValue(driver);
        DriverField(driver,"raptorStrikeRemaining").SetValue(driver,duration*.82f);
        DriverCall(driver,"LateUpdate");
        Vector3 direction=Vector3.ProjectOnPlane(Model(target).transform.position-Model(source).transform.position,camera.transform.forward).normalized;
        Vector3 offset=driver.transform.position-Model(source).transform.TransformPoint(initialLocal);
        Assert.That(Vector3.Dot(offset,direction),Is.GreaterThan(.035f),"The rendered sprite must visibly thrust toward this hit's current victim.");
        Assert.That(driver.IsActionLocked,Is.True);
    }
    private void AdvanceDriver(SpriteMonsterAnimationDriver driver,float dt)
    {
        DriverField(driver,"raptorStrikeStartedFrame").SetValue(driver,-1);
        DriverCall(driver,"Advance",dt);DriverCall(driver,"LateUpdate");
    }
    private SpriteMonsterAnimationDriver Driver(object unit)=>(SpriteMonsterAnimationDriver)Get(unit,"spriteAnimation");
    private SpriteMonsterBillboard Billboard(object unit)=>(SpriteMonsterBillboard)Get(unit,"spriteBillboard");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private GameObject Go(string name){var value=new GameObject(name);owned.Add(value);return value;}
    private FieldInfo BattleField(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private).Invoke(battle,args);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private float Number(object unit,string name)=>(float)Get(unit,name);
    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private FieldInfo DriverField(SpriteMonsterAnimationDriver driver,string name)=>typeof(SpriteMonsterAnimationDriver).GetField(name,Private);
    private void DriverCall(SpriteMonsterAnimationDriver driver,string name,params object[] args)=>typeof(SpriteMonsterAnimationDriver).GetMethod(name,Private).Invoke(driver,args);
}
