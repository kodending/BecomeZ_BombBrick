using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MoltColonyV3TimingTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;

    [Test]
    public void NurseryKeepsTheParentClosedAndRevealsOnlyTheThreeOriginalBirths()
    {
        Inspect(new MantisDeathMoltRank4Stage(null),"mantius",(stage,context,root)=>
        {
            float[] times={2.11f,2.14f,2.38f,2.62f};
            for(int i=0;i<times.Length;i++)
            {
                stage.Tick(times[i],0f);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.enabled&&r.name.StartsWith("Baby",StringComparison.Ordinal)),Is.EqualTo(i));
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.enabled&&r.name.StartsWith("Lead",StringComparison.Ordinal)).sprite.name,
                    Does.Contain("CocoonMotion"),"The adult must remain in its cocoon while offspring live.");
            }
            stage.Tick(2.12f,0f);Assert.That(stage.ActionTime,Is.EqualTo(.32f).Within(.0001f));
            stage.Tick(2.36f,0f);Assert.That(stage.ActionTime,Is.EqualTo(.44f).Within(.0001f));
            stage.Tick(2.60f,0f);Assert.That(stage.ActionTime,Is.EqualTo(.56f).Within(.0001f));
            context.returnBodyVisible=false;stage.Tick(4.02f,0f);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled),Is.False,
                "Actual hidden parent/cocoon state must be supplied by the battlefield, without a second overlay.");
        });
    }

    [Test]
    public void DeathBloomStartsItsOwnDamageClockOnlyAfterTheSporeCurtainReturnsToTheField()
    {
        Inspect(new MushroomColonyRank4Stage(null),"versus",(stage,context,root)=>
        {
            stage.Tick(MushroomColonyRank4Stage.FieldReturnTime-.001f,0f);Assert.That(stage.ActionTime,Is.LessThan(0f));
            stage.Tick(MushroomColonyRank4Stage.FieldReturnTime,0f);Assert.That(stage.ActionTime,Is.Zero);
            stage.Tick(stage.Duration,0f);Assert.That(stage.ActionTime,Is.EqualTo(stage.Duration-MushroomColonyRank4Stage.FieldReturnTime).Within(.0001f));
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled&&r.gameObject.activeInHierarchy),Is.False,"The native flower must be the only body after the curtain clears.");
            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true).Any(p=>p.gameObject.activeInHierarchy&&p.particleCount>0),Is.False);
        });
    }

    [Test]
    public void MammothChainHandsOffOneChargeToTheRealArenaAndNeverReplaysTheRejectedFlight()
    {
        Inspect(new MammothExpulsionRank4Stage(null),"kogason",(stage,context,root)=>
        {
            stage.Tick(MammothExpulsionRank4Stage.FieldReturnTime-.001f,0f);
            Assert.That(stage.ActionTime,Is.LessThan(MammothExpulsionRules.Contact),"The low charge shot must not launch a hidden arena hit.");
            stage.Tick(MammothExpulsionRank4Stage.FieldReturnTime,0f);
            Assert.That(stage.ActionTime,Is.EqualTo(MammothExpulsionRules.Contact+.001f).Within(.0001f));
            foreach(float time in new[]{2.20f,2.70f,3.70f,stage.Duration})
            {
                stage.Tick(time,0f);
                Assert.That(stage.ActionTime,Is.EqualTo(MammothExpulsionRules.Contact+.001f+time-MammothExpulsionRank4Stage.FieldReturnTime).Within(.0001f));
                Assert.That(stage.Opacity,Is.Zero,"Wall/body collisions must be the real arena actors, not a second cinematic flight.");
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled),Is.False);
            }
            var camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
            Assert.That(camera.orthographic,Is.True);
            Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled),Is.False);
        });
    }

    [TestCase("mantis","mantius")]
    [TestCase("mushroom","versus")]
    [TestCase("mammoth","kogason")]
    public void ASecondPlaybackRestoresAnOpaqueSetAndNeverMirrorsOnlyTheStageActor(string family,string species)
    {
        Rank4CinematicStage film=family=="mantis"?(Rank4CinematicStage)new MantisDeathMoltRank4Stage(null):family=="mushroom"?
            (Rank4CinematicStage)new MushroomColonyRank4Stage(null):new MammothExpulsionRank4Stage(null);
        Inspect(film,species,(stage,context,root)=>
        {
            stage.Tick(stage.Duration,0f);
            var source=context.source;source.flipX=true;context.source=source;context.enemy=true;
            Assert.That(stage.Begin(context),Is.True);stage.Tick(.28f,0f);
            var camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
            Assert.That(camera.backgroundColor.a,Is.EqualTo(family=="mushroom"?0f:1f));
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.enabled&&r.name.StartsWith("Lead",StringComparison.Ordinal)).flipX,Is.EqualTo(family=="mushroom"));
        });
    }

    private static void Inspect(Rank4CinematicStage stage,string species,Action<Rank4CinematicStage,Rank4StageContext,Transform> check)
    {
        var db=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var definition=db.monsters.First(d=>d!=null&&d.monsterId==species);
        var driver=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var actor=new FrogCatchCinematicStage.Actor{species=species,sprite=driver.IdleFrames[0],color=Color.white,size=1f};
        var context=new Rank4StageContext{source=actor,returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f};
        try
        {
            Assert.That(stage.Begin(context),Is.True);
            check(stage,context,(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage));
        }
        finally{stage.Dispose();}
    }
}
