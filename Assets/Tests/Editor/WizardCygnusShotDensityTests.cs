using NUnit.Framework;
using UnityEngine;

public sealed class WizardCygnusShotDensityTests
{
    [TestCase(0,0)] [TestCase(1,5)] [TestCase(2,9)] [TestCase(3,14)]
    [TestCase(4,18)] [TestCase(5,23)] [TestCase(6,27)] [TestCase(7,32)] [TestCase(8,36)]
    public void ShotCountRoundsTheOriginalVolleyTimesOneAndAHalfUp(int targets,int expected)
    {
        Assert.That(WizardCygnusShowerRules.ShotCount(targets),Is.EqualTo(expected));
    }

    [TestCase(1)] [TestCase(3)] [TestCase(5)] [TestCase(7)] [TestCase(8)]
    public void IncreasedDensityPreservesTheOriginalFirstAndLastContactAndFitsTheSameLifetime(int targets)
    {
        int total=WizardCygnusShowerRules.ShotCount(targets);
        float first=WizardCygnusShowerRules.HitTime(0,0);
        float last=WizardCygnusShowerRules.HitTime(WizardCygnusShowerRules.WaveCount-1,targets-1);
        Assert.That(WizardCygnusShowerRules.ImpactTime(0,targets),Is.EqualTo(first));
        Assert.That(WizardCygnusShowerRules.ImpactTime(total-1,targets),Is.EqualTo(last).Within(.00001f));
        float previous=first;
        for(int i=1;i<total;i++)
        {
            float contact=WizardCygnusShowerRules.ImpactTime(i,targets);
            Assert.That(contact,Is.GreaterThan(previous));previous=contact;
        }
        Assert.That(WizardCygnusShowerRules.DurationForTargets(targets),
            Is.EqualTo(Mathf.Max(WizardCygnusShowerRules.Duration,last+.35f)));
    }

    [Test]
    public void OneTargetReceivesTwoInterpolatedContactsBetweenItsOriginalThreeImpacts()
    {
        float[] expected={.38f,.55f,.72f,.89f,1.06f};
        for(int i=0;i<expected.Length;i++)
            Assert.That(WizardCygnusShowerRules.ImpactTime(i,1),Is.EqualTo(expected[i]).Within(.00001f));
    }
}
