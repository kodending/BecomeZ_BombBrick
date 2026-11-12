using NUnit.Framework;
using UnityEngine;

public sealed class FrogTongueWhipTests
{
    [TestCase(false)] [TestCase(true)]
    public void BattleEndFinishesTongueWithoutRepeatingImpactOrFollowingTheActor(bool held)
    {
        var frog=new GameObject("Ending tongue source");var target=new GameObject("Ending tongue target");
        var scene=frog.scene;
        try
        {
            var whip=frog.AddComponent<FrogTongueWhip>();target.transform.position=Vector3.right*3f;
            int hits=0;
            var advance=typeof(FrogTongueWhip).GetMethod("Advance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            whip.Play(target.transform,()=>hits++,true,1f,0f,held);
            if(held)advance.Invoke(whip,new object[]{.13f});
            int previousHits=hits;
            whip.FinishAtBattleEnd();
            Assert.That(whip.IsPlaying,Is.False);
            FrogTongueWhip tail=null;
            foreach(var candidate in Resources.FindObjectsOfTypeAll<FrogTongueWhip>())
                if(candidate.name=="Finishing Frog Tongue"&&candidate.gameObject.scene==scene)tail=candidate;
            Assert.That(tail,Is.Not.Null);
            target.SetActive(false);frog.transform.position=Vector3.one*10f;
            advance.Invoke(tail,new object[]{.04f});
            Assert.That(tail.IsPlaying,Is.True,"Keep the existing visual through its normal retraction.");
            Assert.That(tail.ActiveSegmentCount,Is.GreaterThan(0));
            advance.Invoke(tail,new object[]{1f});
            Assert.That(tail.IsPlaying,Is.False);
            Assert.That(tail.ActiveSegmentCount,Is.Zero);
            Assert.That(hits,Is.EqualTo(previousHits));
            FrogTongueWhip.ClearBattleEndTails(scene);
            Assert.That(tail==null||!tail.gameObject.activeInHierarchy,Is.True);
        }
        finally
        {
            FrogTongueWhip.ClearBattleEndTails(scene);
            Object.DestroyImmediate(frog);Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void BranchStartsAtThePrimaryVictimAndTracksItThroughLiftAndRelease()
    {
        var frog=new GameObject("Branch Frog");var primary=new GameObject("Branch Origin");
        var secondary=new GameObject("Branch Victim");
        try
        {
            primary.transform.position=new Vector3(3,0,0);secondary.transform.position=new Vector3(3,0,2);
            var whip=frog.AddComponent<FrogTongueWhip>();
            var advance=typeof(FrogTongueWhip).GetMethod("Advance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var origin=typeof(FrogTongueWhip).GetMethod("SourcePosition",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            whip.Play(primary.transform,null,true,1,0,true);advance.Invoke(whip,new object[]{.13f});
            var branch=whip.Fork(secondary.transform);advance.Invoke(branch,new object[]{.13f});
            Assert.That((Vector3)origin.Invoke(branch,null),Is.EqualTo(primary.transform.position+Vector3.up*.35f));
            primary.transform.position+=Vector3.up*2.4f;advance.Invoke(branch,new object[]{.2f});
            Assert.That((Vector3)origin.Invoke(branch,null),Is.EqualTo(primary.transform.position+Vector3.up*.35f));
            whip.Release();advance.Invoke(branch,new object[]{.12f});Assert.That(branch.IsPlaying,Is.False);
            branch=whip.Fork(secondary.transform);whip.Cancel();Assert.That(branch.ActiveSegmentCount,Is.Zero);
        }
        finally{Object.DestroyImmediate(frog);Object.DestroyImmediate(primary);Object.DestroyImmediate(secondary);}
    }

    [Test]
    public void Play_SelectsSkillPixelsAndShowsBodyAndTip()
    {
        GameObject frog=new GameObject("Frog Tongue Test");
        GameObject target=new GameObject("Tongue Target");
        try
        {
            target.transform.position=Vector3.right*2f;
            FrogTongueWhip whip=frog.AddComponent<FrogTongueWhip>();
            whip.Play(target.transform,null,true,1f,5f);

            Assert.IsTrue(whip.IsPlaying);
            Assert.That(whip.ActiveSegmentCount,Is.GreaterThan(0));
            SpriteRenderer[] renderers=
                frog.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(renderers.Length,Is.GreaterThan(0));
            Assert.That(renderers[0].sprite.name,Is.EqualTo("FrogTonguePixel"));
            Assert.That(renderers[0].sprite.texture.width,Is.EqualTo(1));
            Assert.That(renderers[0].transform.localScale.x,
                Is.EqualTo(renderers[0].transform.localScale.y));
            Assert.That(whip.EndpointDistance,
                Is.EqualTo(Vector3.Distance(Vector3.up*0.4f,
                    target.transform.position+Vector3.up*0.35f)).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(frog);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void HarpoonStaysAttachedThroughMotionAndReleasesWithoutRepeatingImpact()
    {
        var frog=new GameObject("Held Frog Tongue Test");
        var target=new GameObject("Moving Tongue Target");
        try
        {
            var whip=frog.AddComponent<FrogTongueWhip>();
            var advance=typeof(FrogTongueWhip).GetMethod("Advance",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            int hits=0;
            target.transform.position=Vector3.right*2f;
            whip.Play(target.transform,()=>hits++,true,1f,5f,true);
            advance.Invoke(whip,new object[]{0.13f});
            frog.transform.position=Vector3.left*2.55f+Vector3.up*0.55f;
            target.transform.position=Vector3.right*4f;
            advance.Invoke(whip,new object[]{0.5f});
            Assert.That(hits,Is.EqualTo(1));
            Assert.That(whip.IsPlaying,Is.True);
            Assert.That(whip.EndpointDistance,Is.GreaterThan(5f));
            var pixels=frog.GetComponentsInChildren<SpriteRenderer>();
            var tip=System.Array.FindLast(pixels,p=>p.enabled);
            // No camera is required for the endpoint contract; compare screen positions if one exists.
            Vector3 end=target.transform.position+Vector3.up*0.35f;
            if(Camera.main==null)Assert.That(Vector3.Distance(tip.transform.position,end),Is.LessThan(0.001f));
            else Assert.That(Vector2.Distance(Camera.main.WorldToScreenPoint(tip.transform.position),
                Camera.main.WorldToScreenPoint(end)),Is.LessThan(0.01f));
            whip.Release();
            advance.Invoke(whip,new object[]{0.12f});
            Assert.That(whip.IsPlaying,Is.False);
            Assert.That(whip.ActiveSegmentCount,Is.Zero);
            Assert.That(hits,Is.EqualTo(1));
        }
        finally{Object.DestroyImmediate(frog);Object.DestroyImmediate(target);}
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CancelOrMissingTargetDoesNotDealPendingDamage(bool missingTarget)
    {
        var frog=new GameObject("Cancelled Tongue Test");
        var target=new GameObject("Cancelled Target");
        try
        {
            int hits=0;
            var whip=frog.AddComponent<FrogTongueWhip>();
            whip.Play(target.transform,()=>hits++,true,1f,0f,true);
            if(missingTarget)Object.DestroyImmediate(target);else whip.Cancel();
            typeof(FrogTongueWhip).GetMethod("Advance",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                .Invoke(whip,new object[]{1f});
            Assert.That(hits,Is.Zero);
            Assert.That(whip.IsPlaying,Is.False);
            Assert.That(whip.ActiveSegmentCount,Is.Zero);
        }
        finally{Object.DestroyImmediate(frog);if(target!=null)Object.DestroyImmediate(target);}
    }

    [Test]
    public void BasicTongueStillHitsOnceAndRetractsOnItsOriginalTiming()
    {
        var frog=new GameObject("Basic Tongue Test");
        var target=new GameObject("Basic Tongue Target");
        try
        {
            int hits=0;
            var whip=frog.AddComponent<FrogTongueWhip>();
            var advance=typeof(FrogTongueWhip).GetMethod("Advance",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            whip.Play(target.transform,()=>hits++);
            advance.Invoke(whip,new object[]{0.12f});
            Assert.That(hits,Is.Zero);
            advance.Invoke(whip,new object[]{0.02f});
            Assert.That(hits,Is.EqualTo(1));
            advance.Invoke(whip,new object[]{0.11f});
            Assert.That(whip.IsPlaying,Is.False);
            Assert.That(whip.ActiveSegmentCount,Is.Zero);
            Assert.That(hits,Is.EqualTo(1));
        }
        finally{Object.DestroyImmediate(frog);Object.DestroyImmediate(target);}
    }

    [Test]
    public void FrogTraitEffects_ShowRoyalAuraAndExplosiveGroundArea()
    {
        GameObject anchor=new GameObject("Frog Trait Effect Test");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        Material material=new Material(Shader.Find("Sprites/Default"));
        ParticleSystem aura=PixelCombatEffect.PlayKingpaRoyalAura(
            anchor.transform,sorting,FrogCombatRules.RoyalBuffDuration);
        PixelGroundCastTelegraph field=PixelCombatEffect.PlayGroundAreaTelegraph(
            "Rokrok Explosive Saliva Area",Vector3.zero,2f,0.4f,
            material,sorting,new Color(0.48f,0.9f,0.08f,0.4f));
        field.HoldFilled(0.4f);
        try
        {
            Assert.That(aura.name,Is.EqualTo("Kingpa Royal Decree Aura"));
            Assert.That(aura.main.duration,
                Is.EqualTo(FrogCombatRules.RoyalBuffDuration));
            Assert.That(aura.main.loop,Is.False);
            Assert.That(field.name,Is.EqualTo("Rokrok Explosive Saliva Area"));
            Assert.That(field.IsHolding,Is.True);
        }
        finally
        {
            Object.DestroyImmediate(aura.gameObject);
            Object.DestroyImmediate(field.gameObject);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(anchor);
        }
    }
}
