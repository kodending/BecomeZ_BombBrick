using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class TyrannoGodImpactVisualsTests
{
    [TestCase(false)][TestCase(true)]
    public void FrozenBodiesCompressAndShudderWithoutMovingTheirCombatRootsOrShadows(bool mirrored)
    {
        var root=new GameObject("Held impact reaction test");
        var bodies=new Transform[8];var shadows=new Transform[8];
        var modelPositions=new Vector3[8];var originalPositions=new Vector3[8];var originalScales=new Vector3[8];
        TyrannoGodImpactVisuals visual=null;CombatSkillClock clock=null;
        try
        {
            for(int i=0;i<8;i++)
            {
                var model=new GameObject("Untouched body root").transform;model.SetParent(root.transform,false);
                model.position=new Vector3((mirrored?-1f:1f)*i,0f,i*.35f);model.localScale=Vector3.one*(.8f+i*.1f);
                bodies[i]=new GameObject("Visual only").transform;bodies[i].SetParent(model,false);
                bodies[i].localPosition=new Vector3(.02f,.1f,.03f);bodies[i].localScale=new Vector3(.9f,1.2f,1f);
                shadows[i]=new GameObject("Untouched ground shadow").transform;shadows[i].SetParent(model,false);
                modelPositions[i]=model.position;originalPositions[i]=bodies[i].localPosition;originalScales[i]=bodies[i].localScale;
            }
            clock=new CombatSkillClock(root.transform);clock.Hold();visual=new TyrannoGodImpactVisuals(bodies);
            visual.Pose(.02f);
            for(int i=0;i<8;i++)
            {
                Assert.That(bodies[i].localScale.y,Is.LessThan(originalScales[i].y*.85f));
                Assert.That(bodies[i].localScale.x,Is.GreaterThan(originalScales[i].x));
                Assert.That(bodies[i].localPosition,Is.Not.EqualTo(originalPositions[i]));
                Assert.That(bodies[i].parent.position,Is.EqualTo(modelPositions[i]));
                Assert.That(shadows[i].position,Is.EqualTo(modelPositions[i]));
            }
            visual.Pose(TyrannoGodImpactVisuals.Duration);
            for(int i=0;i<8;i++)
            {
                Assert.That(bodies[i].localPosition,Is.EqualTo(originalPositions[i]));
                Assert.That(bodies[i].localScale,Is.EqualTo(originalScales[i]));
            }
            visual.Pose(.02f);Assert.That(bodies[0].localScale,Is.EqualTo(originalScales[0]));
        }
        finally{visual?.Dispose();clock?.End();Object.DestroyImmediate(root);}
    }

    [Test]public void CancellationAndDestroyedTargetsRestoreEverySurvivingLocalPoseExactly()
    {
        var root=new GameObject("Cancelled held impact");var body=new GameObject("Survivor").transform;var lost=new GameObject("Lost target").transform;
        body.SetParent(root.transform,false);lost.SetParent(root.transform,false);
        Vector3 position=new Vector3(.15f,.27f,.34f),scale=new Vector3(.73f,1.17f,.95f);body.localPosition=position;body.localScale=scale;
        var visual=new TyrannoGodImpactVisuals(new[]{body,lost});
        try
        {
            visual.Pose(.03f);Object.DestroyImmediate(lost.gameObject);visual.Dispose();visual.Dispose();
            Assert.That(body.localPosition,Is.EqualTo(position));Assert.That(body.localScale,Is.EqualTo(scale));
        }
        finally{visual.Dispose();Object.DestroyImmediate(root);}
    }
}
