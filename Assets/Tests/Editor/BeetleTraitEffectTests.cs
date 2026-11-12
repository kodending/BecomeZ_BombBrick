using NUnit.Framework;
using UnityEngine;

public sealed class BeetleTraitEffectTests
{
    [Test]
    public void TraitEffects_CreateDistinctVisibleObjects()
    {
        GameObject anchor=new GameObject("Beetle Trait Test");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        PixelSpiritSanctuaryEffect horn=PixelCombatEffect.PlayBeetleHornShockwave(
            Vector3.zero,1f,sorting);
        ParticleSystem debris=PixelCombatEffect.PlayBeetleHornDebris(
            Vector3.zero,1f,sorting);
        GameObject lightning=PixelCombatEffect.PlayBeetleLightningArc(
            Vector3.up*1.5f,Vector3.zero,sorting);
        SpriteRenderer fortress=PixelCombatEffect.PlayBeetleFortressAura(
            anchor.transform,sorting,BeetleCombatRules.FortressDuration);
        ParticleSystem solar=PixelCombatEffect.PlayBeetleSolarBurst(
            Vector3.zero,1f,sorting);
        try
        {
            Assert.That(horn,Is.Not.Null);
            Assert.That(horn.GetComponent<SpriteRenderer>().sprite,Is.Not.Null);
            Assert.That(debris,Is.Not.Null);
            Assert.That(lightning,Is.Not.Null);
            Assert.That(
                lightning.GetComponentsInChildren<PixelNinjaLineSlash>(true).Length,
                Is.GreaterThanOrEqualTo(8));
            Assert.That(fortress,Is.Not.Null);
            Assert.That(fortress.sprite,Is.Not.Null);
            Assert.That(solar,Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(horn.gameObject);
            Object.DestroyImmediate(debris.gameObject);
            Object.DestroyImmediate(lightning);
            Object.DestroyImmediate(fortress.gameObject);
            Object.DestroyImmediate(solar.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }
}
