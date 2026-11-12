using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class TyrannoStompTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private HashSet<GameObject> existing;
    [SetUp] public void Setup()=>existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
    [TearDown] public void Cleanup()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }
    [TestCase("041_tino")] [TestCase("042_magno")] [TestCase("043_cheolno")] [TestCase("044_bonrex")]
    public void StompContactsAtOneSecondOnceAndDeathCancelsIt(string prefab)
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+prefab));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var type=typeof(SpriteMonsterAnimationDriver);
        var advance=type.GetMethod("Advance",Private);var draw=type.GetMethod("LateUpdate",Private);
        int hits=0;driver.PlayTyrannoStomp(()=>hits++);
        advance.Invoke(driver,new object[]{.91f});draw.Invoke(driver,null);
        Assert.That(hits,Is.Zero);Assert.That(driver.CurrentFrameIndex,Is.EqualTo(2));
        advance.Invoke(driver,new object[]{.091f});draw.Invoke(driver,null);
        Assert.That(hits,Is.EqualTo(1));Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
        advance.Invoke(driver,new object[]{.04f});draw.Invoke(driver,null);
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
        advance.Invoke(driver,new object[]{.4f});draw.Invoke(driver,null);
        Assert.That(hits,Is.EqualTo(1));
        driver.ResetForReuse();driver.PlayTyrannoStomp(()=>hits++);
        advance.Invoke(driver,new object[]{.5f});driver.PlayDeath();
        advance.Invoke(driver,new object[]{1f});draw.Invoke(driver,null);
        Assert.That(hits,Is.EqualTo(1));Assert.That(driver.HasPendingSkillAttackHit,Is.False);
    }
    [TestCase(1f)] [TestCase(2f)]
    public void ExtinctionWindupTracksSpeedAndFreezing(float speed)
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        var draw=typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private);
        int hits=0;driver.SetGlobalSpeedMultiplier(speed);driver.PlayTyrannoStomp(()=>hits++,true);
        advance.Invoke(driver,new object[]{1.1f/speed});draw.Invoke(driver,null);
        Assert.That(hits,Is.Zero);Assert.That(driver.TyrannoStompProgress,Is.EqualTo(1.1f/1.2f).Within(.001f));
        driver.SetFrozen(true);advance.Invoke(driver,new object[]{1f});draw.Invoke(driver,null);
        Assert.That(hits,Is.Zero);Assert.That(driver.TyrannoStompProgress,Is.EqualTo(1.1f/1.2f).Within(.001f));
        driver.SetFrozen(false);
        advance.Invoke(driver,new object[]{.101f/speed});draw.Invoke(driver,null);
        Assert.That(hits,Is.EqualTo(1));Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
        advance.Invoke(driver,new object[]{.06f/speed});draw.Invoke(driver,null);
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
    }

    [TestCase(1f)] [TestCase(2f)]
    public void ExtinctionWaveHitsOnArrivalOnceWithExactCoreAndOuterBounds(float radiusScale)
    {
        var host=new GameObject("Extinction battle");host.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();var type=typeof(MonsterAutoBattlePrototype);
        var ut=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        var units=(IList)type.GetField("units",Private).GetValue(battle);
        float tile=MonsterAutoBattlePrototype.TileSize*radiusScale;
        float[] distances={0f,1f,2f,2.01f,3.5f,3.51f,1f};
        for(int i=0;i<distances.Length;i++)
        {
            var unit=Activator.CreateInstance(ut,true);units.Add(unit);
            Action<string,object> set=(key,value)=>ut.GetField(key).SetValue(unit,value);
            var model=new GameObject("Extinction unit");model.transform.position=Vector3.right*distances[i]*tile;
            set("model",model);set("ring",new GameObject("Ring"));set("id",i);set("stableId","mass-"+i);
            set("definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_magno"));
            set("alive",true);set("hp",10000f);set("maxHp",10000f);set("damage",100f);
            set("team",Enum.ToObject(ut.GetField("team").FieldType,i==0||i==6?0:1));
        }
        var visited=Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(ut));
        ((IList)ut.GetField("activeTraitIds").GetValue(units[0])).Add(TyrannoRank3Rules.MassExtinction);
        ut.GetField("tyrannoTremorPending").SetValue(units[0],true);
        var immune=type.GetMethod("PreyDisplacementImmune",Private);
        Assert.That(immune.Invoke(null,new[]{units[0]}),Is.True);
        type.GetMethod("Rank3Hit",Private).Invoke(battle,new[]{units[1],units[0],(object)1f,0f,0f});
        Assert.That(ut.GetField("hp").GetValue(units[0]),Is.EqualTo(9950f));
        ut.GetField("tyrannoTremorPending").SetValue(units[0],false);
        Assert.That(immune.Invoke(null,new[]{units[0]}),Is.False);
        type.GetMethod("Rank3Hit",Private).Invoke(battle,new[]{units[1],units[0],(object)1f,0f,0f});
        Assert.That(ut.GetField("hp").GetValue(units[0]),Is.EqualTo(9850f));
        var hit=type.GetMethod("HitTyrannoExtinctionWave",Private);
        Action<float> wave=front=>hit.Invoke(battle,new[]{units[0],Vector3.zero,(object)(3.5f*tile),2f*tile,front*tile,1f,visited});
        Func<int,float> hp=i=>(float)ut.GetField("hp").GetValue(units[i]);
        wave(.9f);Assert.That(hp(1),Is.EqualTo(10000f));
        wave(1f);Assert.That(hp(1),Is.EqualTo(9580f));Assert.That(hp(2),Is.EqualTo(10000f));
        wave(2f);Assert.That(hp(2),Is.EqualTo(9580f));Assert.That(hp(3),Is.EqualTo(10000f));
        wave(3.5f);wave(3.5f);
        Assert.That(hp(1),Is.EqualTo(9580f));Assert.That(hp(2),Is.EqualTo(9580f));
        Assert.That(hp(3),Is.EqualTo(9700f));Assert.That(hp(4),Is.EqualTo(9700f));
        Assert.That(hp(5),Is.EqualTo(10000f));Assert.That(hp(6),Is.EqualTo(10000f));
        Assert.That(((CombatStatusState)ut.GetField("combatStatuses").GetValue(units[2])).Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(1f));
        Assert.That(((CombatStatusState)ut.GetField("combatStatuses").GetValue(units[4])).Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(.75f));
        ((GameObject)ut.GetField("model").GetValue(units[5])).transform.position=Vector3.right*tile;
        ut.GetField("alive").SetValue(units[0],false);wave(3.5f);
        Assert.That(hp(5),Is.EqualTo(9580f),"The released extinction wave finishes after caster death");
    }

    [Test] public void GroundFragmentsReachTheBoundaryInFourWavesAndCleanUp()
    {
        var source=new GameObject("Stomp source").AddComponent<SpriteRenderer>();
        var fx=PixelTyrannoStomp.Charge(Vector3.zero,4f,source,()=>true);
        var particles=fx.GetComponent<ParticleSystem>();
        var animate=typeof(PixelTyrannoStomp).GetMethod("Animate",Private);
        var values=new ParticleSystem.Particle[128];
        animate.Invoke(fx,new object[]{.4f});
        int count=particles.GetParticles(values);float max=0;
        for(int i=0;i<count;i++)if(values[i].startSize>.01f)
            max=Mathf.Max(max,new Vector2(values[i].position.x,values[i].position.z).magnitude);
        Assert.That(max,Is.EqualTo(2f).Within(.01f));
        animate.Invoke(fx,new object[]{.4f});count=particles.GetParticles(values);max=0;
        for(int i=0;i<count;i++)if(values[i].startSize>.01f)
            max=Mathf.Max(max,new Vector2(values[i].position.x,values[i].position.z).magnitude);
        Assert.That(max,Is.EqualTo(4f).Within(.01f));
        fx.Impact(4f);Assert.That(particles.particleCount,Is.EqualTo(16));
        typeof(PixelTyrannoStomp).GetMethod("Animate",Private).Invoke(fx,new object[]{.17f});
        Assert.That(particles.particleCount,Is.EqualTo(112));
        count=particles.GetParticles(values);
        max=0;for(int i=0;i<count;i++)max=Mathf.Max(max,new Vector2(values[i].position.x,values[i].position.z).magnitude);
        Assert.That(max,Is.EqualTo(4f).Within(.01f));
        typeof(PixelTyrannoStomp).GetMethod("Animate",Private).Invoke(fx,new object[]{.9f});
        Assert.That(fx==null,Is.True);
        bool valid=true;fx=PixelTyrannoStomp.Charge(Vector3.zero,4f,source,()=>valid);valid=false;
        typeof(PixelTyrannoStomp).GetMethod("Animate",Private).Invoke(fx,new object[]{.01f});
        Assert.That(fx==null,Is.True);
    }
    [Test] public void BaseDamageRangeAndRankThreeTimingStaySeparate()
    {
        var host=new GameObject("Tyranno battle");host.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();
        var type=typeof(MonsterAutoBattlePrototype);var unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        var units=(IList)type.GetField("units",Private).GetValue(battle);
        for(int i=0;i<3;i++)
        {
            var unit=Activator.CreateInstance(unitType,true);units.Add(unit);
            Action<string,object> set=(key,value)=>unitType.GetField(key).SetValue(unit,value);
            var model=new GameObject("Tyranno test unit");
            model.transform.position=Vector3.right*(i==0?0:i==1?1:20);
            set("model",model);set("ring",new GameObject("Ring"));set("id",i);
            set("definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_magno"));
            set("stableId","stomp-"+i);set("alive",true);set("hp",1000f);set("maxHp",1000f);set("damage",100f);
            set("team",Enum.ToObject(unitType.GetField("team").FieldType,i==0?0:1));
        }
        var skill=GameMasterData.Skill(TyrannoCombatRules.SkillId);
        Assert.That(skill.damageMultiplier,Is.EqualTo(2.4f));Assert.That(skill.baseCooldown,Is.EqualTo(9f));
        Assert.That(skill.radiusTiles,Is.EqualTo(2f));Assert.That(skill.statusDurationSeconds,Is.EqualTo(.75f));
        type.GetMethod("ResolveTyrantTremor",Private).Invoke(battle,new object[]{units[0],skill,1f,false,0});
        Assert.That((float)unitType.GetField("hp").GetValue(units[1]),Is.EqualTo(760f).Within(.01f));
        Assert.That((float)unitType.GetField("hp").GetValue(units[2]),Is.EqualTo(1000f));
        var cast=type.GetMethod("TyrannoWindupSeconds",Private);
        Assert.That(cast.Invoke(null,new[]{units[0]}),Is.EqualTo(1f));
        var traits=(IList)unitType.GetField("activeTraitIds").GetValue(units[0]);
        foreach(var trait in new[]{TyrannoRank3Rules.MassExtinction,TyrannoRank3Rules.TyrantMarch,TyrannoRank3Rules.PrimordialMaw})
        {
            traits.Clear();traits.Add(trait);Assert.That(cast.Invoke(null,new[]{units[0]}),
                Is.EqualTo(trait==TyrannoRank3Rules.MassExtinction?1.2f:trait==TyrannoRank3Rules.TyrantMarch?.6f:1f));
        }
    }
}
