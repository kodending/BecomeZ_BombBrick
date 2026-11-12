using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class TyrannoGodFootstepsTests
{
    [Test]
    public void ThreeHeavierFootstepsBounceOnlyAfterTheirSeparateContacts()
    {
        Assert.That(TyrannoGodFootstepsRules.CompletedStepsAt(0f),Is.Zero);
        float previousHeight=0f;
        for(int step=0;step<TyrannoGodFootstepsRules.StepCount;step++)
        {
            float contact=TyrannoGodFootstepsRules.StepTimeAt(step);
            Assert.That(TyrannoGodFootstepsRules.CompletedStepsAt(contact-.001f),Is.EqualTo(step));
            Assert.That(TyrannoGodFootstepsRules.CompletedStepsAt(contact),Is.EqualTo(step+1));
            Assert.That(TyrannoGodFootstepsRules.BounceHeightAt(contact-.001f),Is.Zero);
            Assert.That(TyrannoGodFootstepsRules.BounceHeightAt(contact),Is.Zero);
            float peak=TyrannoGodFootstepsRules.BounceHeightAt(contact+TyrannoGodFootstepsRules.BounceDuration*.5f);
            Assert.That(peak,Is.GreaterThan(previousHeight));previousHeight=peak;
            Assert.That(TyrannoGodFootstepsRules.BounceHeightAt(contact+TyrannoGodFootstepsRules.BounceDuration+.001f),Is.Zero);
        }
        Assert.That(TyrannoGodFootstepsRules.CompletedStepsAt(100f),Is.EqualTo(3));
        Assert.That(TyrannoGodFootstepsRules.BounceHeightAt(TyrannoGodFootstepsRules.FieldPreludeEndTime),Is.Zero);
    }

    [Test]
    public void CameraShakesOnContactsAndSettlesBeforeTheGroundLevelReveal()
    {
        Assert.That(TyrannoGodFootstepsRules.CameraJoltAt(.30f),Is.EqualTo(Vector2.zero));
        float previous=0f;
        for(int step=0;step<TyrannoGodFootstepsRules.StepCount;step++)
        {
            float contact=TyrannoGodFootstepsRules.StepTimeAt(step);
            float force=TyrannoGodFootstepsRules.CameraJoltAt(contact).magnitude;
            Assert.That(force,Is.GreaterThan(previous));previous=force;
            Assert.That(TyrannoGodFootstepsRules.CameraJoltAt(contact+.10f).y,Is.LessThan(0f));
            Assert.That(TyrannoGodFootstepsRules.CameraJoltAt(contact+.201f),Is.EqualTo(Vector2.zero));
        }
        Assert.That(TyrannoGodFootstepsRules.CameraJoltAt(TyrannoGodFootstepsRules.FieldPreludeEndTime),Is.EqualTo(Vector2.zero));
    }

    [TestCase(false)][TestCase(true)]
    public void EightFrozenSpritesBounceTogetherWithoutMovingTheirModelsOrFloorShadows(bool mirrored)
    {
        var host=new GameObject("God footsteps visual ownership test");
        var models=new Transform[8];var bodies=new Transform[8];var shadows=new Transform[8];
        var modelPositions=new Vector3[8];var bodyPositions=new Vector3[8];var shadowPositions=new Vector3[8];
        TyrannoGodFootstepsVisuals visuals=null;CombatSkillClock clock=null;
        try
        {
            for(int i=0;i<8;i++)
            {
                models[i]=new GameObject("Untouched combat model "+i).transform;models[i].SetParent(host.transform,false);
                models[i].position=new Vector3((mirrored?-1f:1f)*(i+1),i*.02f,i*.3f);
                models[i].rotation=Quaternion.Euler(0f,i*11f,0f);models[i].localScale=new Vector3(.7f+i*.1f,1.3f,.9f);
                bodies[i]=new GameObject("Frozen sprite "+i).transform;bodies[i].SetParent(models[i],false);
                bodies[i].localPosition=new Vector3(.1f,.2f,.03f);bodies[i].gameObject.AddComponent<SpriteRenderer>();
                bodies[i].gameObject.AddComponent<SpriteMonsterAnimationDriver>();
                shadows[i]=new GameObject("Ground shadow "+i).transform;shadows[i].SetParent(models[i],false);
                shadows[i].localPosition=new Vector3(.02f,-.1f,.04f);
                modelPositions[i]=models[i].position;bodyPositions[i]=bodies[i].position;shadowPositions[i]=shadows[i].position;
            }
            clock=new CombatSkillClock(host.transform);clock.Hold();visuals=new TyrannoGodFootstepsVisuals(bodies);
            float time=TyrannoGodFootstepsRules.StepTimeAt(2)+TyrannoGodFootstepsRules.BounceDuration*.5f;
            for(int frame=0;frame<3;frame++)
            {
                visuals.Pose(time);
                for(int i=0;i<8;i++)
                {
                    var driver=bodies[i].GetComponent<SpriteMonsterAnimationDriver>();
                    Assert.That(CombatSkillClock.ActorFrozen(driver),Is.True);
                    typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(driver,null);
                    Assert.That(Vector3.Distance(bodies[i].position,bodyPositions[i]+Vector3.up*.48f),Is.LessThan(.00001f));
                    Assert.That(models[i].position,Is.EqualTo(modelPositions[i]));
                    Assert.That(shadows[i].position,Is.EqualTo(shadowPositions[i]));
                }
            }
            visuals.Pose(TyrannoGodFootstepsRules.FieldPreludeEndTime);
            for(int i=0;i<8;i++)Assert.That(Vector3.Distance(bodies[i].position,bodyPositions[i]),Is.LessThan(.00001f));
            visuals.Pose(time);
            for(int i=0;i<8;i++)Assert.That(Vector3.Distance(bodies[i].position,bodyPositions[i]),Is.LessThan(.00001f));
        }
        finally{visuals?.Dispose();clock?.End();Object.DestroyImmediate(host);}
    }

    [TestCase(.44f)][TestCase(.89f)][TestCase(1.35f)]
    public void CancellationRestoresTheExactLocalPoseEvenWhenAnotherVictimWasDestroyed(float time)
    {
        var host=new GameObject("Cancelled god footsteps");var body=new GameObject("Surviving body");var lost=new GameObject("Destroyed body");
        body.transform.SetParent(host.transform,false);lost.transform.SetParent(host.transform,false);
        var original=new Vector3(.123f,.456f,.789f);body.transform.localPosition=original;
        var visuals=new TyrannoGodFootstepsVisuals(new[]{body.transform,lost.transform});
        try
        {
            visuals.Pose(time);Assert.That(body.transform.localPosition.y,Is.GreaterThan(original.y));
            Object.DestroyImmediate(lost);visuals.Dispose();visuals.Dispose();visuals.Pose(time);
            Assert.That(body.transform.localPosition,Is.EqualTo(original));
        }
        finally{visuals.Dispose();Object.DestroyImmediate(host);}
    }
}
