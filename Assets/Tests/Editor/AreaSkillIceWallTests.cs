using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    [TestCase(0)] [TestCase(1)]
    public void LandedGasTicksDamageAcrossTheCastersIceWallOnEitherTeam(int team)
    {
        var random=UnityEngine.Random.state;
        try
        {
            GasReady();AreaWallReady(team);AssertOriginalProjectileIsBlocked();
            float hp=Hp(target);GasCreate();GasTick(.5f);
            float dealt=hp-Hp(target);
            Assert.That(dealt,Is.GreaterThan(0f));
            Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(dealt).Within(.001f));
            Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
            AssertOriginalProjectileIsBlocked();
        }
        finally{UnityEngine.Random.state=random;}
    }

    [TestCase(0)] [TestCase(1)]
    public void AttachedCrownErosionAndBloomKeepTheirDamageAcrossAnIceWallOnEitherTeam(int team)
    {
        var random=UnityEngine.Random.state;
        try
        {
            CrownReady();AreaWallReady(team);AssertOriginalProjectileIsBlocked();
            float hp=Hp(target);CrownCreate();CrownTick(1f);
            float first=hp-Hp(target);Assert.That(first,Is.GreaterThan(0f));
            CrownTick(2f);
            float total=hp-Hp(target);
            Assert.That(total,Is.GreaterThan(first*3f),"The three erosion ticks also keep the final local bloom.");
            Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(total).Within(.001f));
            AssertOriginalProjectileIsBlocked();
        }
        finally{UnityEngine.Random.state=random;}
    }

    [TestCase(0)] [TestCase(1)]
    public void WholeFieldCocoonsOpenCompressAndBurstAcrossAnIceWallOnEitherTeam(int team)
    {
        var random=UnityEngine.Random.state;
        IEnumerator cocoons=null;
        try
        {
            Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_aranea"));
            Set(source,"skillId",SpiderCombatRules.SkillId);
            AreaWallReady(team);AssertOriginalProjectileIsBlocked();
            float hp=Hp(target);
            cocoons=(IEnumerator)ReaperCall("ResolveSilkQueenCocoons",source,target,
                Model(target).transform.position,1f,1f,0);
            Assert.That(cocoons.MoveNext(),Is.True);
            Assert.That(hp-Hp(target),Is.EqualTo(100f).Within(.001f),"The overhead field must open its damaging shell behind the wall.");
            while(cocoons.MoveNext()){}
            float total=hp-Hp(target);
            Assert.That(total,Is.EqualTo(500f).Within(.001f),"Opening, three compressions and one burst keep their existing ratios.");
            Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(total).Within(.001f));
            Assert.That(cocoons.MoveNext(),Is.False);
            Assert.That(hp-Hp(target),Is.EqualTo(total));
            AssertOriginalProjectileIsBlocked();
        }
        finally{(cocoons as IDisposable)?.Dispose();UnityEngine.Random.state=random;}
    }

    private void AreaWallReady(int team)
    {
        Type teamType=unitType.GetField("team").FieldType;
        Set(source,"team",Enum.ToObject(teamType,team));Set(target,"team",Enum.ToObject(teamType,1-team));
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        ReaperCall("RegisterRank3IceWall",target,
            Vector3.Lerp(Model(source).transform.position,Model(target).transform.position,.5f),
            Vector3.right,Vector3.forward,10f,10f);
    }

    private void AssertOriginalProjectileIsBlocked()
    {
        float hp=Hp(target);
        MethodInfo apply=typeof(MonsterAutoBattlePrototype).GetMethods(Private)
            .Single(method=>method.Name=="ApplySkillDamage"&&method.GetParameters().Length==4&&
                method.GetParameters()[2].ParameterType==typeof(float));
        Assert.That(apply.Invoke(battle,new object[]{source,target,100f,false}),Is.EqualTo(0f));
        Assert.That(Hp(target),Is.EqualTo(hp),"The bypass cannot leak into the next ordinary projectile packet.");
    }
}
