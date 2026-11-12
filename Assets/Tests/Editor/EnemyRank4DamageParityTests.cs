using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyRank4DamageParityTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly string[] Families={"raptor","sabertooth","frog","crocodile","kingkong","wizard_mouse",
        "bear","wolf","arachne","ninja_cat","tyranno","monkey","mantis","mushroom","mammoth","beetle",
        "spirit_deer","griffin","wyvern","haetae"};
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()=>random=UnityEngine.Random.state;
    [TearDown] public void Cleanup()=>UnityEngine.Random.state=random;

    private static IEnumerable<TestCaseData> FamilyCases()=>Families.Select(x=>new TestCaseData(x));

    [TestCaseSource(nameof(FamilyCases))]
    public void EveryFamilyResolvesTheSameDirectSkillDamageForEitherTeamAtIdenticalStats(string family)
    {
        var amounts=new float[2];
        for(int team=0;team<2;team++)
        using(var fixture=new ExistingFixture(typeof(MonkeyArcTests),team==1))
        {
            var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.First(x=>x.familyId==family);
            object source=Get<object>(fixture.Value,"source"),target=Get<object>(fixture.Value,"target");
            Set(source,"definition",definition);Set(source,"skillId",definition.skillId);Set(source,"augmentTranscended",true);
            Set(fixture.Battle,"mlEpisodeActive",true);Set(fixture.Battle,"playerProfile",new PlayerProfileSave());
            Set(fixture.Battle,"expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
            if(family!="raptor")Assert.That(Call(fixture.Battle,"HasFamilyTranscendence",source),Is.True,family);
            float hp=Get<float>(target,"hp");
            amounts[team]=SkillDamage(fixture.Battle,source,target,100f);
            Assert.That(amounts[team],Is.GreaterThan(0f),family+" team "+team);
            Assert.That(hp-Get<float>(target,"hp"),Is.EqualTo(amounts[team]).Within(.001f));
            Assert.That(Get<float>(source,"damageDealt"),Is.EqualTo(amounts[team]).Within(.001f));
            Assert.That(Get<float>(source,"skillDamage"),Is.Zero,"Skill callers own skillDamage aggregation.");
        }
        Assert.That(amounts[1],Is.EqualTo(amounts[0]).Within(.001f),family+" must not lose damage when the caster is an enemy.");
    }

    [TestCase("wizard_mouse")] [TestCase("monkey")] [TestCase("frog")] [TestCase("crocodile")]
    [TestCase("ninja_cat")] [TestCase("mammoth")] [TestCase("tyranno")]
    public void ActualRankFourContactCallbacksDamageOpponentsWithTheSameCreditOnBothTeams(string family)
    {
        var damage=new float[2];var credit=new float[2];var hits=new int[2];
        for(int team=0;team<2;team++)
        {
            UnityEngine.Random.InitState(7341);
            if(family=="ninja_cat")
            {
                using(var fixture=new NinjaSpacetimeFixture())
                {
                    SetTeam(fixture.source,team);SetTeam(fixture.enemy,1-team);
                    float before=fixture.Float(fixture.enemy,"hp");Assert.That(fixture.Begin(),Is.True);
                    Assert.That(fixture.Float(fixture.enemy,"hp"),Is.EqualTo(before));
                    fixture.Advance(NinjaSpacetimeRules.Windup+.001f);
                    damage[team]=before-fixture.Float(fixture.enemy,"hp");
                    credit[team]=fixture.Float(fixture.source,"skillDamage");hits[team]=fixture.Int(fixture.source,"skillHits");
                    fixture.Advance(.5f);Assert.That(before-fixture.Float(fixture.enemy,"hp"),Is.EqualTo(damage[team]));
                }
            }
            else
            using(var fixture=new ExistingFixture(FixtureType(family),team==1))
            {
                object source=family=="tyranno"?fixture.Units[0]:Get<object>(fixture.Value,"source");
                var targets=fixture.Units.Cast<object>().Where(x=>Team(x)!=Team(source)&&Get<bool>(x,"alive")&&!Get<bool>(x,"inStorage")).ToArray();
                float before=targets.Sum(x=>Get<float>(x,"hp"));
                RunRankFour(fixture,family,team);
                damage[team]=before-targets.Sum(x=>Get<float>(x,"hp"));
                credit[team]=Get<float>(source,"skillDamage");hits[team]=Get<int>(source,"skillHits");
            }
            Assert.That(damage[team],Is.GreaterThan(0f),family+" team "+team);
            Assert.That(credit[team],Is.EqualTo(damage[team]).Within(.01f),family+" damage credit");
            Assert.That(hits[team],Is.GreaterThan(0),family+" accepted contacts");
            if(family=="crocodile")
            {
                Assert.That(damage[team],Is.EqualTo(600f).Within(.01f),"A 100 ATK abyss strike deals 600% once to its primary target.");
                Assert.That(hits[team],Is.EqualTo(1),"The selected Rank 3 payload cannot add another contact.");
            }
        }
        Assert.That(damage[1],Is.EqualTo(damage[0]).Within(.01f),family+" damage parity");
        Assert.That(hits[1],Is.EqualTo(hits[0]),family+" contact parity");
    }

    [TestCase(0)] [TestCase(1)]
    public void CinematicHoldDefersTheCastersSkillCallbackAndResumeAppliesExactlyOneRealHit(int team)
    {
        using(var fixture=new ExistingFixture(typeof(MonkeyArcTests),team==1))
        {
            object source=Get<object>(fixture.Value,"source"),target=Get<object>(fixture.Value,"target");
            Set(fixture.Battle,"mlEpisodeActive",true);
            var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos");
            var model=UnityEngine.Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
            Set(source,"model",model);Set(source,"definition",definition);Set(source,"skillId",definition.skillId);
            var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();int callbacks=0;
            float before=Get<float>(target,"hp");driver.PlaySkillAttack(1f,()=>{callbacks++;SkillDamage(fixture.Battle,source,target,100f);});
            var clock=new CombatSkillClock(model.transform);
            try
            {
                clock.Hold();driver.NotifySkillAttackHitFrameFromAnimation();
                Assert.That(callbacks,Is.Zero);Assert.That(Get<float>(target,"hp"),Is.EqualTo(before));
                Assert.That(driver.HasPendingSkillAttackHit,Is.True);
            }
            finally{clock.End();}
            Call(driver,"Advance",driver.SkillAttackHitTime+.001f);Call(driver,"LateUpdate");
            Assert.That(callbacks,Is.EqualTo(1));Assert.That(Get<float>(target,"hp"),Is.LessThan(before));
            float after=Get<float>(target,"hp");Call(driver,"Advance",.2f);driver.NotifySkillAttackHitFrameFromAnimation();
            Assert.That(callbacks,Is.EqualTo(1));Assert.That(Get<float>(target,"hp"),Is.EqualTo(after));
        }
    }

    [TestCase("wizard_mouse",0)] [TestCase("wizard_mouse",1)]
    [TestCase("monkey",0)] [TestCase("monkey",1)]
    [TestCase("mushroom",0)] [TestCase("mushroom",1)]
    public void SkyDamageCrossesAnIceWallAndDeathBloomRemainsAnUnaffectedControl(string family,int team)
    {
        Type type=family=="mushroom"?typeof(MonkeyArcTests):FixtureType(family);
        using(var fixture=new ExistingFixture(type,team==1))
        {
            object source=Get<object>(fixture.Value,"source"),target=Get<object>(fixture.Value,"target");
            if(family=="mushroom")Call(fixture.Value,"DeathBloomReady",source);
            Vector3 start=Get<GameObject>(source,"model").transform.position,end=Get<GameObject>(target,"model").transform.position;
            Vector3 forward=(end-start).normalized;Assert.That(forward.sqrMagnitude,Is.GreaterThan(.5f));
            Call(fixture.Battle,"RegisterRank3IceWall",target,Vector3.Lerp(start,end,.5f),forward,Vector3.Cross(Vector3.up,forward),10f,10f);
            float before=Get<float>(target,"hp");
            Set(source,"augmentTranscended",false);Assert.That(SkillDamage(fixture.Battle,source,target,100f),Is.Zero);
            Assert.That(Get<float>(target,"hp"),Is.EqualTo(before));Set(source,"augmentTranscended",true);
            if(family=="mushroom")
            {
                object flower=Call(fixture.Value,"KillForDeathBloom",source,target);
                Assert.That(string.IsNullOrEmpty(Get<string>(flower,"skillId")),Is.True,
                    "The flower is a nonprojectile control, rather than a blocked sky-skill regression.");
                Call(fixture.Value,"DeathBloomTick",.5f);
            }
            else RunRankFour(fixture,family,team);
            float dealt=before-Get<float>(target,"hp");
            Assert.That(dealt,Is.GreaterThan(0f),family+" must hit from the sky or the existing ground cloud, not raycast from its caster.");
            Assert.That(Get<float>(source,"skillDamage"),Is.EqualTo(dealt).Within(.01f));
        }
    }

    [TestCase(0)] [TestCase(1)]
    public void BearRankFourFearControlsTheOtherTeamWithoutInventingDirectDamage(int team)
    {
        using(var fixture=new ExistingFixture(typeof(BearAvatarFearTests),team==1))
        {
            object bear=Get<object>(fixture.Value,"bear");
            var targets=fixture.Units.Cast<object>().Where(x=>Team(x)!=Team(bear)).ToArray();
            var health=targets.Select(x=>Get<float>(x,"hp")).ToArray();Call(fixture.Value,"Roar");
            for(int i=0;i<targets.Length;i++)
            {
                Assert.That(Get<CombatStatusState>(targets[i],"combatStatuses").Has(CombatStatusKind.Fear),Is.True);
                Assert.That(Get<float>(targets[i],"hp"),Is.EqualTo(health[i]));
            }
        }
    }

    [TestCase(0,"")] [TestCase(1,"")]
    [TestCase(0,BearCombatRules.MountainColossusTraitId)] [TestCase(1,BearCombatRules.MountainColossusTraitId)]
    [TestCase(0,BearCombatRules.RagingHundredStrikesTraitId)] [TestCase(1,BearCombatRules.RagingHundredStrikesTraitId)]
    public void BearTransformationResumesAndItsBuffedAttackContactsStillDamageTheOtherTeam(int team,string rankThree)
    {
        using(var fixture=new ExistingFixture(typeof(BearAvatarCinematicTests),team==1))
        {
            // Coroutines require an active host; disabling this non-ExecuteAlways component keeps the editor fixture manual.
            var camera=Get<Camera>(fixture.Value,"camera");int unitCount=fixture.Units.Count;
            fixture.Battle.enabled=false;fixture.Battle.gameObject.SetActive(true);
            Assert.That(fixture.Units.Count,Is.EqualTo(unitCount),"Activating the edit-mode coroutine host must not initialize a scene.");
            Assert.That(Get<Camera>(fixture.Battle,"mainCamera"),Is.SameAs(camera));
            Assert.That(fixture.Battle.IsSceneReady,Is.False);
            object source=Get<object>(fixture.Value,"source"),primary=Get<object>(fixture.Value,"primary"),secondary=Get<object>(fixture.Value,"secondary");
            var driver=Get<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
            if(rankThree.Length>0)Get<List<string>>(source,"activeTraitIds").Add(rankThree);
            float initial=Get<float>(primary,"hp");Call(fixture.Battle,"Attack",source,primary);FinishDriverAction(driver);
            float basic=initial-Get<float>(primary,"hp");Assert.That(basic,Is.GreaterThan(0f),"The same actor must have a working unbuffed contact first.");
            float beforeTransform=Get<float>(primary,"hp");
            Assert.That(Call(fixture.Battle,"BeginBearFightingSpirit",source,GameMasterData.Skill(BearCombatRules.SkillId)),Is.True);
            Call(fixture.Battle,"PlayExecutedSkillAction",source);
            for(int frame=0;frame<240&&!Get<bool>(source,"bearAvatarRoarPending");frame++)StepDriver(driver,1f/120f);
            Assert.That(Get<bool>(source,"bearAvatarRoarPending"),Is.True,"The actual fighting-spirit callback must reach transformation.");
            Call(fixture.Value,"StartStage");Call(fixture.Value,"CompleteStage");
            Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
            Assert.That(Get<bool>(source,"bearAvatarRoarPending"),Is.False);Assert.That(Call(fixture.Battle,"BearAvatarActive",source),Is.True);
            Assert.That(Get<float>(source,"bearResolveTimer"),Is.GreaterThan(0f));Assert.That(driver.BearAvatarActive,Is.True);
            FinishDriverAction(driver);Assert.That(Get<float>(primary,"hp"),Is.EqualTo(beforeTransform),"Transformation and its fear alone do not damage targets.");
            Assert.That(driver.GetComponent<SpriteRenderer>().sprite.name,Does.StartWith("OriginalBear"));
            float beforePrimary=Get<float>(primary,"hp"),beforeSecondary=Get<float>(secondary,"hp"),beforeCredit=Get<float>(source,"damageDealt");
            int attacks=Get<int>(source,"basicAttackCount");Call(fixture.Battle,"Attack",source,primary);FinishDriverAction(driver);
            float primaryDamage=beforePrimary-Get<float>(primary,"hp"),secondaryDamage=beforeSecondary-Get<float>(secondary,"hp");
            Assert.That(primaryDamage,Is.GreaterThan(basic),"The transformed, resolved attack must actually apply its stronger contact(s).");
            Assert.That(Get<float>(source,"damageDealt")-beforeCredit,Is.EqualTo(primaryDamage+secondaryDamage).Within(.01f));
            Assert.That(Get<int>(source,"basicAttackCount"),Is.EqualTo(attacks+1));
            Assert.That(driver.IsActionLocked,Is.False);Assert.That(driver.HasPendingBasicAttackHit,Is.False);
        }
    }

    private static Type FixtureType(string family)
    {
        switch(family)
        {
            case "wizard_mouse":return typeof(WizardCygnusShowerTests);
            case "monkey":return typeof(MonkeyStormTests);
            case "frog":return typeof(FrogGrandSlamTests);
            case "crocodile":return typeof(CrocodileAbyssDiveTests);
            case "mammoth":return typeof(MonkeyArcTests);
            case "tyranno":return typeof(TyrannoReignTests);
            default:throw new ArgumentOutOfRangeException(nameof(family));
        }
    }

    private static void RunRankFour(ExistingFixture fixture,string family,int team)
    {
        switch(family)
        {
            case "wizard_mouse":Call(fixture.Value,"Begin");Call(fixture.Value,"Release");Call(fixture.Value,"StepPastShower",1);break;
            case "monkey":Call(fixture.Value,"ConcentrateArena");Call(fixture.Value,"Begin");Call(fixture.Value,"Step",4.01f);break;
            case "frog":Call(fixture.Value,"Begin");Call(fixture.Value,"AdvanceTo",FrogGrandSlamRules.HitTime+.01f);break;
            case "crocodile":Call(fixture.Value,"Begin");Call(fixture.Value,"AdvanceTo",CrocodileAbyssDiveRules.HitTime+.01f);break;
            case "mammoth":Call(fixture.Value,"ChainReady","kogason",team,100f);Call(fixture.Value,"ChainBegin",null,null);break;
            case "tyranno":Call(fixture.Value,"Queue");Call(fixture.Value,"Tick",0f);Call(fixture.Value,"Tick",.501f);break;
        }
    }

    private sealed class ExistingFixture:IDisposable
    {
        public readonly object Value;
        public readonly MonsterAutoBattlePrototype Battle;
        public IList Units=>(IList)typeof(MonsterAutoBattlePrototype).GetField("units",Fields).GetValue(Battle);
        public ExistingFixture(Type type,bool invert)
        {
            Value=Activator.CreateInstance(type);Lifecycle(typeof(SetUpAttribute));Battle=Get<MonsterAutoBattlePrototype>(Value,"battle");
            if(invert)foreach(object unit in Units)SetTeam(unit,1-Team(unit));
        }
        public void Dispose()=>Lifecycle(typeof(TearDownAttribute));
        private void Lifecycle(Type attribute)
        {
            foreach(var method in Value.GetType().GetMethods(Fields).Where(x=>x.GetCustomAttributes(attribute,true).Length>0))
                method.Invoke(Value,null);
        }
    }

    private static float SkillDamage(MonsterAutoBattlePrototype battle,object source,object target,float damage)=>
        (float)typeof(MonsterAutoBattlePrototype).GetMethods(Fields).Single(x=>x.Name=="ApplySkillDamage"&&x.GetParameters().Length==4&&
            x.GetParameters()[2].ParameterType==typeof(float)).Invoke(battle,new object[]{source,target,damage,false});
    private static void StepDriver(SpriteMonsterAnimationDriver driver,float dt)
    {Call(driver,"ClearAppliedRecoil");Call(driver,"Advance",dt);Call(driver,"LateUpdate");}
    private static void FinishDriverAction(SpriteMonsterAnimationDriver driver)
    {
        for(int frame=0;frame<720&&driver.IsActionLocked;frame++)StepDriver(driver,1f/120f);
        Assert.That(driver.IsActionLocked,Is.False,"The real authored action must release naturally after its callback.");
    }
    private static int Team(object unit)=>Convert.ToInt32(Get<object>(unit,"team"));
    private static void SetTeam(object unit,int team)=>Set(unit,"team",Enum.ToObject(unit.GetType().GetField("team",Fields).FieldType,team));
    private static T Get<T>(object value,string field)=>(T)value.GetType().GetField(field,Fields).GetValue(value);
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Fields).SetValue(value,data);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Fields).Invoke(value,args);
}
