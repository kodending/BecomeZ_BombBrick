using NUnit.Framework;
using UnityEngine;

public sealed class MammothTraitEffectTests
{
    [Test]
    public void TraitPaths_CreateVisibleGroundAndLowMotes()
    {
        GameObject sortingObject=new GameObject("Mammoth Trait Sorting");
        SpriteRenderer sorting=sortingObject.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        GameObject glacier=PixelCombatEffect.PlayMammothGlacierTrail(
            Vector3.zero,Vector3.forward*3.5f,1.1f,4f,sorting);
        GameObject ancestor=PixelCombatEffect.PlayMammothAncestorTrail(
            Vector3.right*2f,Vector3.right*2f+Vector3.forward*3.5f,
            1.1f,MammothCombatRules.AncestorBuffDuration,sorting);
        try
        {
            Assert.That(glacier,Is.Not.Null);
            Assert.That(ancestor,Is.Not.Null);
            Assert.That(glacier.GetComponentInChildren<SpriteRenderer>(),Is.Not.Null);
            Assert.That(glacier.GetComponentInChildren<ParticleSystem>(),Is.Not.Null);
            Assert.That(ancestor.GetComponentInChildren<SpriteRenderer>(),Is.Not.Null);
            ParticleSystem ancestorMotes=
                ancestor.GetComponentInChildren<ParticleSystem>();
            Assert.That(ancestorMotes,Is.Not.Null);
            Assert.That(ancestorMotes.main.duration,
                Is.EqualTo(MammothCombatRules.AncestorBuffDuration));
        }
        finally
        {
            Object.DestroyImmediate(glacier);
            Object.DestroyImmediate(ancestor);
            Object.DestroyImmediate(sortingObject);
        }
    }
}
