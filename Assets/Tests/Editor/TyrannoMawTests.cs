using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class TyrannoMawTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private HashSet<GameObject> existing;
    [SetUp] public void Setup()=>existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
    [TearDown] public void Cleanup()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }

    [TestCase(1f)] [TestCase(2f)]
    public void MawPoseAndClockPauseTogetherAndDeathCancelsOnce(float speed)
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        float clock=0f;int ended=0;driver.SetGlobalSpeedMultiplier(speed);
        var fx=PixelTyrannoMaw.Open(Vector3.zero,MonsterAutoBattlePrototype.TileSize*2f,
            driver.GetComponent<SpriteRenderer>(),Vector3.right);
        driver.PlayTyrannoMaw(()=>true,t=>{clock=t;fx.Advance(t-TyrannoRank3Rules.MawWindup);},()=>{ended++;fx.Cancel();});
        var head=fx.GetComponentInChildren<SpriteRenderer>();
        Assert.That(head.sprite.texture.name,Is.EqualTo("PrimordialSummon16"));
        Assert.That(head.color.a,Is.EqualTo(.72f).Within(.001f));
        advance.Invoke(driver,new object[]{.9f/speed});
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(2));
        Assert.That(head.sprite.name,Is.EqualTo("Primordial Summon 04"));
        advance.Invoke(driver,new object[]{.101f/speed});
        Assert.That(clock,Is.EqualTo(1.001f).Within(.001f));Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
        driver.SetFrozen(true);advance.Invoke(driver,new object[]{1f});
        Assert.That(clock,Is.EqualTo(1.001f).Within(.001f));
        Assert.That(head.sprite.name,Is.EqualTo("Primordial Summon 05"));
        driver.SetFrozen(false);advance.Invoke(driver,new object[]{.35f/speed});
        Assert.That(clock,Is.EqualTo(1.351f).Within(.001f));
        Assert.That(head.sprite.name,Is.EqualTo("Primordial Summon 09"));
        driver.PlayDeath();advance.Invoke(driver,new object[]{1f});
        Assert.That(driver.TyrannoMawActive,Is.False);Assert.That(ended,Is.EqualTo(1));
        Assert.That(fx==null,Is.True);
    }

    [TestCase(1f,null)] [TestCase(1.5f,null)] [TestCase(1f,"death")] [TestCase(1f,"stun")] [TestCase(1f,"freeze")]
    public void MawPullIsSmoothThenBitesTheActualAreaWithCappedBonus(float radiusScale,string disabled)
    {
        var host=new GameObject("Maw battle");host.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();var type=typeof(MonsterAutoBattlePrototype);
        var ut=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,new Vector3(50,1,50)));
        var units=(IList)type.GetField("units",Private).GetValue(battle);
        float tile=MonsterAutoBattlePrototype.TileSize;
        float standOff=MonsterAutoBattlePrototype.AttackSlotDistanceForRing(
            MonsterAutoBattlePrototype.AttackDistanceForRange(1,.35f,.35f),.35f,.35f,0);
        float[] distance={0f,(standOff+tile*.125f)/tile,2.1f*radiusScale,2.75f*radiusScale,3.5f*radiusScale,1f,2.75f*radiusScale,.5f};
        for(int i=0;i<distance.Length;i++)
        {
            var unit=Activator.CreateInstance(ut,true);units.Add(unit);
            Action<string,object> set=(key,value)=>ut.GetField(key).SetValue(unit,value);
            var model=i==0?Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino")):new GameObject("Maw target");
            model.transform.position=Vector3.right*distance[i]*tile;
            set("model",model);set("ring",new GameObject("Ring"));set("id",i);set("stableId","maw-"+i);
            set("definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_magno"));
            set("alive",true);set("hp",4000f);set("maxHp",10000f);set("damage",100f);
            set("collisionRadius",.35f);set("attackRange",1);
            set("team",Enum.ToObject(ut.GetField("team").FieldType,i==0||i==5?0:1));
            if(i==0)set("spriteAnimation",model.GetComponentInChildren<SpriteMonsterAnimationDriver>());
        }
        ut.GetField("tyrannoTremorRadiusMultiplier").SetValue(units[0],radiusScale);
        ut.GetField("rank3ForceImmuneTimer").SetValue(units[3],10f);
        Func<int,float> hp=i=>(float)ut.GetField("hp").GetValue(units[i]);
        Func<int,float> x=i=>((GameObject)ut.GetField("model").GetValue(units[i])).transform.position.x;
        type.GetMethod("StartTyrannoMaw",Private).Invoke(battle,new[]{units[0],(object)0});
        var maw=ut.GetField("tyrannoMaw").GetValue(units[0]);
        var tick=type.GetMethod("AdvanceTyrannoMaw",Private);
        Action<float> advance=t=>tick.Invoke(battle,new[]{units[0],maw,(object)t});
        var immune=type.GetMethod("PreyDisplacementImmune",Private);
        Assert.That(immune.Invoke(null,new[]{units[0]}),Is.True);
        advance(.99f);Assert.That(hp(1),Is.EqualTo(4000f));
        advance(1f);Assert.That(hp(1),Is.EqualTo(4000f));Assert.That(x(2),Is.EqualTo(distance[2]*tile));
        Assert.That(immune.Invoke(null,new[]{units[0]}),Is.False);
        advance(1.175f);Assert.That(x(2),Is.EqualTo((distance[2]-.125f)*tile).Within(.001f));
        Assert.That(x(1),Is.EqualTo((distance[1]*tile+standOff)*.5f).Within(.001f));
        Assert.That(x(7),Is.EqualTo(distance[7]*tile),"An already close victim must not move outward or further inward.");
        Assert.That(hp(1),Is.EqualTo(4000f),"Pulling/open-mouth frames must not deal the early 120% hit.");
        if(disabled!=null)
        {
            var driver=(SpriteMonsterAnimationDriver)ut.GetField("spriteAnimation").GetValue(units[0]);
            if(disabled=="death"){ut.GetField("alive").SetValue(units[0],false);driver.PlayDeath();}
            else
            {
                if(disabled=="stun")ut.GetField("giraffeStunTimer").SetValue(units[0],5f);
                else ((CombatStatusState)ut.GetField("combatStatuses").GetValue(units[0])).ApplyTimed(CombatStatusKind.Freeze,5f,units[1]);
                driver.InterruptCurrentAction(true);
            }
            advance(1.5f);Assert.That(hp(1),Is.EqualTo(3430f));
            Assert.That(ut.GetField("tyrannoMaw").GetValue(units[0]),Is.Null);
        }
        else
        {
            advance(1.349f);
            Assert.That(hp(1),Is.EqualTo(4000f));Assert.That(hp(3),Is.EqualTo(4000f));
            Assert.That(PixelTyrannoMaw.FrameAt(1.349f-TyrannoRank3Rules.MawWindup),Is.EqualTo(8));
            advance(1.35f);advance(1.36f);
            Assert.That(PixelTyrannoMaw.FrameAt(1.35f-TyrannoRank3Rules.MawWindup),Is.EqualTo(9));
            Assert.That(x(2),Is.EqualTo((distance[2]-.25f)*tile).Within(.001f));
            Assert.That(x(1),Is.EqualTo(standOff).Within(.001f));
            Assert.That(x(7),Is.EqualTo(distance[7]*tile));
            Assert.That(x(6),Is.EqualTo((distance[6]-.25f)*tile).Within(.001f));
            Assert.That(hp(1),Is.EqualTo(3430f));Assert.That(hp(2),Is.EqualTo(3430f));
            Assert.That(hp(3),Is.EqualTo(3880f));Assert.That(hp(4),Is.EqualTo(4000f));Assert.That(hp(5),Is.EqualTo(4000f));
            Assert.That(hp(6),Is.EqualTo(3880f),"A quarter-tile pull must not collect the outer ring into the bite.");
            Assert.That(((CombatStatusState)ut.GetField("combatStatuses").GetValue(units[1])).Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(.8f));
            type.GetMethod("EndTyrannoMaw",Private).Invoke(battle,new[]{units[0],maw});
        }
        Assert.That(ut.GetField("tyrannoMawPulledBy").GetValue(units[1]),Is.Null);
        Assert.That(TyrannoRank3Rules.MawBonusDamage(100f,120f,1f),Is.EqualTo(12f));
        Assert.That(TyrannoRank3Rules.MawBonusDamage(100f,9000f,2f),Is.EqualTo(300f));
    }
}
