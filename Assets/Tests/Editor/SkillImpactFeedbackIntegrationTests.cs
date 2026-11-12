using System;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    [TestCase("rank3_hit")]
    [TestCase("rank3_area")]
    [TestCase("king_roar")]
    [TestCase("mushroom_landing")]
    public void DirectRank3DamageContactsSlideTheHitBodyWithoutAutomaticCameraShake(string contact)
    {
        WithSkillImpactCamera(director=>
        {
            var camera=director.GetComponent<Camera>();
            Vector3 point=Vector3.forward*10f;
            Vector3 screenBefore=camera.WorldToScreenPoint(point);
            Vector3 bodyBefore=Model(target).transform.position;
            float hpBefore=Hp(target);
            float dealt;
            if(contact=="rank3_hit")dealt=(float)SkillImpactCall("Rank3Hit",source,target,.5f,0f,0f);
            else if(contact=="rank3_area")dealt=(float)SkillImpactCall("Rank3AreaDamage",source,bodyBefore,1f,.5f,0f,null);
            else if(contact=="mushroom_landing")dealt=(float)SkillImpactCall("ApplyMushroomHit",null,0,source,target,50f,true);
            else
            {
                var hits=Activator.CreateInstance(typeof(System.Collections.Generic.HashSet<>).MakeGenericType(unitType));
                dealt=(float)SkillImpactCall("ApplyBearKingRoarWave",source,bodyBefore,1f,50f,false,hits);
            }

            Assert.That(dealt,Is.GreaterThan(0f));
            Assert.That(Hp(target),Is.LessThan(hpBefore));
            if(contact!="mushroom_landing")Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
            Assert.That(((Vector3)Get(target,"hitSlideVelocity")).sqrMagnitude,Is.GreaterThan(0f),
                "Authored rank-3 contacts disable trait recursion but remain direct skill impacts.");
            float start=SkillImpactCameraField<float>(director,"shakeStartedAt");
            float until=SkillImpactCameraField<float>(director,"shakeUntil");
            Assert.That(start,Is.Zero);Assert.That(until,Is.Zero);
            typeof(CombatCameraDirector).GetMethod("ApplyShakeProjection",Private)
                .Invoke(director,new object[]{Time.unscaledTime});
            Assert.That(Vector3.Distance(screenBefore,camera.WorldToScreenPoint(point)),Is.LessThan(.001f));

            SkillImpactCall("AdvanceHitSlide",target,.05f);
            Assert.That(Model(target).transform.position.x,Is.GreaterThan(bodyBefore.x));
        });
    }

    [TestCase("poison")]
    [TestCase("bleed")]
    [TestCase("burning")]
    [TestCase("trait_secondary")]
    [TestCase("basic")]
    [TestCase("critical_basic")]
    public void PeriodicTraitSecondaryAndBasicDamageDoNotStartSkillImpactFeedback(string kind)
    {
        WithSkillImpactCamera(director=>
        {
            float hpBefore=Hp(target);
            if(kind=="trait_secondary")SkillImpactCall("ApplyTraitSkillDamage",source,target,25f);
            else if(kind=="basic"||kind=="critical_basic")
            {
                Set(source,"lastBasicAttackCritical",kind=="critical_basic");
                SkillImpactCall("ApplyDamage",source,target,25f,false,true,true,false,
                    0f,0f,1f,false,false,false);
            }
            else
            {
                var status=kind=="poison"?CombatStatusKind.Poison:
                    kind=="bleed"?CombatStatusKind.Bleed:CombatStatusKind.Burning;
                typeof(MonsterAutoBattlePrototype).GetField("periodicStatusUnit",Private).SetValue(battle,target);
                SkillImpactCall("HandlePeriodicStatusTick",new CombatStatusEffect
                    {kind=status,source=source,magnitude=25f,stacks=1});
            }

            Assert.That(Hp(target),Is.LessThan(hpBefore),"The excluded damage must still resolve.");
            Assert.That(SkillImpactCameraField<float>(director,"shakeUntil"),Is.Zero);
            Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
            Assert.That(Get(target,"hitSlideNextAt"),Is.EqualTo(0f));
        });
    }

    [TestCase(false)] [TestCase(true)]
    public void FocusedCastersActualBasicHitsDoNotRefreshAnExistingAuthoredShake(bool critical)
    {
        WithSkillImpactCamera(director=>
        {
            var driver=Model(source).AddComponent<SpriteMonsterAnimationDriver>();
            var renderer=driver.GetComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
            Set(source,"spriteAnimation",driver);Set(source,"lastBasicAttackCritical",critical);
            Assert.That(director.TryBeginAnticipationPan(renderer,true),Is.True);
            for(int hit=0;hit<6;hit++)
            {
                float now=Time.unscaledTime;
                typeof(CombatCameraDirector).GetField("shakeStartedAt",Private).SetValue(director,now-.05f);
                float authoredEnd=now+.01f;
                typeof(CombatCameraDirector).GetField("shakeUntil",Private).SetValue(director,authoredEnd);
                float hpBefore=Hp(target);
                SkillImpactCall("ApplyDamage",source,target,25f,false,true,true,false,0f,0f,1f,false,false,false);
                Assert.That(Hp(target),Is.LessThan(hpBefore));
                Assert.That(SkillImpactCameraField<float>(director,"shakeUntil"),Is.EqualTo(authoredEnd));
                Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
            }
        });
    }

    [TestCase("ground_skill",false)] [TestCase("ground_skill",true)]
    [TestCase("rank3_ground",false)] [TestCase("rank3_ground",true)]
    [TestCase("gas",false)] [TestCase("gas",true)]
    [TestCase("resolved_gas",false)] [TestCase("resolved_gas",true)]
    public void GroundDamageKeepsItsDamageAndDoesNotAddOrResetExistingSlide(string kind,bool alreadySliding)
    {
        WithSkillImpactCamera(director=>
        {
            Vector3 carried=alreadySliding?Vector3.left*.7f:Vector3.zero;
            Set(target,"hitSlideVelocity",carried);Set(target,"hitSlideNextAt",.4f);
            Vector3 position=Model(target).transform.position;float before=Hp(target);float dealt;
            if(kind=="ground_skill")dealt=(float)SkillImpactCall("ApplyGroundSkillDamage",source,target,50f,false);
            else if(kind=="rank3_ground")dealt=(float)SkillImpactCall("Rank3GroundHit",source,target,.5f,0f,0f);
            else dealt=(float)SkillImpactCall("ApplyGroundMushroomHit",null,0,source,target,50f,kind=="resolved_gas");
            Assert.That(dealt,Is.GreaterThan(0f));Assert.That(Hp(target),Is.EqualTo(before-dealt).Within(.001f));
            Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(carried));
            Assert.That(Get(target,"hitSlideNextAt"),Is.EqualTo(.4f));
            Assert.That(Model(target).transform.position,Is.EqualTo(position));
            Assert.That(SkillImpactCameraField<float>(director,"shakeUntil"),Is.Zero);
        });
    }

    [Test]
    public void ADirectHitAfterAGroundTickStillSlidesAndAnAuthoredShakeStillWorks()
    {
        WithSkillImpactCamera(director=>
        {
            float before=Hp(target);
            float ground=(float)SkillImpactCall("Rank3GroundHit",source,target,.5f,0f,0f);
            Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
            Set(target,"hp",before);
            float direct=(float)SkillImpactCall("Rank3Hit",source,target,.5f,0f,0f);
            Assert.That(direct,Is.EqualTo(ground).Within(.001f));
            Assert.That(((Vector3)Get(target,"hitSlideVelocity")).sqrMagnitude,Is.GreaterThan(0f));
            Assert.That(SkillImpactCameraField<float>(director,"shakeUntil"),Is.Zero);
            director.ReportGroundSlam(true);
            Assert.That(SkillImpactCameraField<float>(director,"shakeUntil"),Is.GreaterThan(Time.unscaledTime));
            float authoredEnd=SkillImpactCameraField<float>(director,"shakeUntil");
            SkillImpactCall("Rank3GroundHit",source,target,.2f,0f,0f);
            Assert.That(SkillImpactCameraField<float>(director,"shakeUntil"),Is.EqualTo(authoredEnd));
        });
    }

    [TestCase("tyranno_lava")] [TestCase("kong_lava")]
    [TestCase("wyvern_fire")] [TestCase("griffin_hellfire")]
    public void ReleasedGroundFieldTicksStillDealDamageWithoutRepeatedSlideOrAutomaticShake(string field)
    {
        WithSkillImpactCamera(director=>
        {
            cast=(System.Collections.IEnumerator)(field=="wyvern_fire"
                ?SkillImpactCall("ResolveWyvernFireLine",source,Vector3.zero,Vector3.right,6f,2f,1f,0)
                :SkillImpactCall(field=="tyranno_lava"?"ResolveTyrannoLavaZone":
                    field=="kong_lava"?"ResolveLavaFissure":"ResolveGriffinHellfire",source,Vector3.zero,6f,0));
            Assert.That(cast.MoveNext(),Is.True);
            for(int tick=0;tick<3;tick++)
            {
                float before=Hp(target);Assert.That(cast.MoveNext(),Is.True);
                Assert.That(Hp(target),Is.LessThan(before));
                Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
                Assert.That(Get(target,"hitSlideNextAt"),Is.EqualTo(0f));
                Assert.That(SkillImpactCameraField<float>(director,"shakeUntil"),Is.Zero);
            }
        });
    }

    private void WithSkillImpactCamera(Action<CombatCameraDirector> check)
    {
        float originalTimeScale=Time.timeScale;
        try
        {
            Time.timeScale=1f;
            var host=new GameObject("Skill impact integration camera");
            var camera=host.AddComponent<Camera>();
            var director=host.AddComponent<CombatCameraDirector>();
            director.Initialize(camera,false);director.SetShakeStrength(1f);director.BeginBattle();
            typeof(MonsterAutoBattlePrototype).GetField("combatCameraDirector",Private).SetValue(battle,director);
            typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
                .SetValue(battle,new Bounds(Vector3.zero,new Vector3(24f,10f,18f)));
            check(director);
        }
        finally{Time.timeScale=originalTimeScale;}
    }

    private object SkillImpactCall(string name,params object[] arguments)=>
        typeof(MonsterAutoBattlePrototype).GetMethod(name,Private).Invoke(battle,arguments);
    private static T SkillImpactCameraField<T>(CombatCameraDirector director,string name)=>
        (T)typeof(CombatCameraDirector).GetField(name,Private).GetValue(director);
}
