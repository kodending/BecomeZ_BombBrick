using NUnit.Framework;
using UnityEngine;

public sealed class SpiritDeerTraitEffectTests
{
    [Test]
    public void HolydeerMasterDataDescribesOneBlessingPerSanctuary()
    {
        TraitMasterRow holy=MonsterAbilityTooltipContent.Trait(
            SpiritDeerCombatRules.HolydeerTraitId);

        Assert.That(holy,Is.Not.Null);
        Assert.That(holy.triggerRule,Does.Contain("최초 진입"));
        Assert.That(holy.effectSummary,Does.Contain("체력 10% 회복"));
        Assert.That(holy.effectSummary,Does.Contain("면역 2초"));
    }

    [Test]
    public void TraitEffectsUseDistinctHolyMoonForestAndEclipseShapes()
    {
        GameObject anchor=new GameObject("Spirit Deer Trait Test");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        ParticleSystem holy=PixelCombatEffect.PlaySpiritDeerCleanse(
            anchor.transform,sorting);
        ParticleSystem moon=PixelCombatEffect.PlaySpiritDeerMoonHeal(
            anchor.transform,sorting);
        ParticleSystem forest=PixelCombatEffect.PlaySpiritDeerForestHaste(
            anchor.transform,sorting);
        ParticleSystem eclipse=PixelCombatEffect.PlaySpiritDeerEclipseCurse(
            anchor.transform,sorting);
        try
        {
            Assert.That(holy.name,Is.EqualTo("Holydeer Purifying Light"));
            Assert.That(moon.GetComponent<ParticleSystemRenderer>()
                .sharedMaterial.name,Does.Contain("Crescent"));
            Assert.That(forest.GetComponent<ParticleSystemRenderer>()
                .sharedMaterial.name,Does.Contain("Leaf"));
            Assert.That(eclipse.velocityOverLifetime.y.constant,
                Is.LessThan(0f));
            Assert.That(holy.particleCount,Is.GreaterThan(moon.particleCount));
        }
        finally
        {
            Object.DestroyImmediate(holy.gameObject);
            Object.DestroyImmediate(moon.gameObject);
            Object.DestroyImmediate(forest.gameObject);
            Object.DestroyImmediate(eclipse.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }
}
