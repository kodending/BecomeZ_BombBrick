using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private object RoyalValue(string name)
    {
        object state=Get(source,"griffinRoyal");
        return state==null?null:state.GetType().GetField(name).GetValue(state);
    }
    private void RoyalSetup(string species="pone")
    {GriffinSetup(species);Trait(GriffinRank3Rules.RoyalDescent);GriffinCall("GrantGriffinRoyal",source);}

    [TestCase("pone",170f,80f)]
    [TestCase("griphonas",170f,80f)]
    [TestCase("holophonas",170f,80f)]
    [TestCase("entpone",510f,240f)]
    public void GriffinRoyalUsesBaseLandingThenThreeSpeciesScaledWinds(string species,float landing,float wind)
    {
        GriffinSetup(species);Trait(GriffinRank3Rules.RoyalDescent);
        GriffinCall("BeginGriffinSkyfall",source,GameMasterData.Skill(GriffinCombatRules.SkillId));
        Assert.That(Get(source,"griffinRoyal"),Is.Null);
        GriffinCall("UpdateGriffinSkyfall",source,.75f);
        Assert.That(5000f-Hp(target),Is.EqualTo(landing).Within(.01f));
        Assert.That(Model(source).transform.position.x,Is.GreaterThan(3f));
        Assert.That(RoyalValue("charges"),Is.EqualTo(3));
        Assert.That(RoyalValue("remaining"),Is.EqualTo(6f));
        Assert.That(Get(source,"rank3AttackBonusTimer"),Is.EqualTo(0f));
        Assert.That(Get(source,"rank3AttackSpeedBonusTimer"),Is.EqualTo(0f));
        Assert.That(Get(target,"itemDefenseShredRatio"),Is.EqualTo(0f));
        Assert.That(Get(target,"familyResistanceShredRatio"),Is.EqualTo(0f));
        Assert.That(Get(target,"rank3SkillLockTimer"),Is.EqualTo(0f));
        Model(target).transform.position=Model(source).transform.position+Vector3.right*2f;
        Set(target,"displacing",false);((CombatStatusState)Get(target,"combatStatuses")).Clear();
        for(int i=0;i<4;i++)
        {
            // The common basic-hit hook must still let the normal attack resolve.
            Assert.That(GriffinCall("ResolveRank3BasicAttack",source,target,100f),Is.False);
            GriffinCall("TickGriffinRoyal",source,.5f);
            Assert.That(5000f-Hp(target),Is.EqualTo(landing+wind*Mathf.Min(3,i+1)).Within(.01f));
        }
        Assert.That(Get(source,"griffinRoyal"),Is.Null);
    }

    [Test]
    public void GriffinRoyalSweepsTheVisibleFrontOnceWithoutHittingBehindOrOutside()
    {
        RoyalSetup();Model(target).transform.position=Vector3.right*2f;
        object back=Unit(1,Vector3.left),outside=Unit(1,new Vector3(3,0,1.7f)),
            far=Unit(1,Vector3.right*5.5f),edge=Unit(1,new Vector3(3,0,1.2f)),
            ally=Unit(0,Vector3.right*2f);
        GriffinCall("FireGriffinRoyalWind",source,target);
        GriffinCall("TickGriffinRoyal",source,.04f);
        Assert.That(Hp(target),Is.EqualTo(5000f));
        for(int i=0;i<22;i++)GriffinCall("TickGriffinRoyal",source,.02f);
        Assert.That(Hp(target),Is.EqualTo(4920f).Within(.01f));
        Assert.That(Hp(edge),Is.EqualTo(4920f).Within(.01f));
        foreach(object u in new[]{back,outside,far,ally})Assert.That(Hp(u),Is.EqualTo(5000f));
        Assert.That(((IList)RoyalValue("waves")).Count,Is.EqualTo(0));
    }

    [Test]
    public void GriffinRoyalRefreshesWithoutStackingAndExpiresAfterSixSeconds()
    {
        RoyalSetup();GriffinCall("FireGriffinRoyalWind",source,target);
        Assert.That(RoyalValue("charges"),Is.EqualTo(2));
        GriffinCall("GrantGriffinRoyal",source);Assert.That(RoyalValue("charges"),Is.EqualTo(3));
        GriffinCall("TickGriffinRoyal",source,.5f);float hp=Hp(target);
        GriffinCall("TickGriffinRoyal",source,5.51f);
        Assert.That(Get(source,"griffinRoyal"),Is.Null);
        GriffinCall("FireGriffinRoyalWind",source,target);GriffinCall("TickGriffinRoyal",source,1f);
        Assert.That(Hp(target),Is.EqualTo(hp));
    }

    [Test]
    public void GriffinRoyalPauseHoldsWavesAndDeathPreservesAlreadyReleasedWaves()
    {
        RoyalSetup();Model(target).transform.position=Vector3.right*3f;
        GriffinCall("FireGriffinRoyalWind",source,target);
        GriffinCall("TickGriffinRoyal",source,0f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(RoyalValue("remaining"),Is.EqualTo(6f));
        Set(source,"alive",false);GriffinCall("CleanupRank3Unit",source);
        Assert.That(RoyalValue("charges"),Is.Zero);
        GriffinCall("TickGriffinRoyal",source,1f);Assert.That(Hp(target),Is.EqualTo(4920f));
        Assert.That(Get(source,"griffinRoyal"),Is.Null);
    }

    [Test]
    public void GriffinRoyalWingSamplesRegisteredSpriteClipAtTheActualHitAndHoldsWhenPaused()
    {
        var go=new GameObject("Wing timing source");
        var renderer=go.AddComponent<SpriteRenderer>();
        var texture=new Texture2D(2,2);texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,2f);
        var shifted=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.zero,2f);
        renderer.sprite=sprite;var fx=PixelGriffinRoyal.Ready(renderer,Color.white);
        try
        {
            var clip=Resources.Load<AnimationClip>("CombatEffects/Griffin/RoyalWingbeat");
            var spriteBindings=UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.That(spriteBindings.Length,Is.EqualTo(1));
            Assert.That(spriteBindings[0].type,Is.EqualTo(typeof(SpriteRenderer)));
            Assert.That(UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip,spriteBindings[0]).Length,Is.EqualTo(16));
            var poses=UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip,spriteBindings[0]);
            var raised=(Sprite)poses[4].value;var lowered=(Sprite)poses[9].value;
            Assert.That(raised.bounds.min.x,Is.LessThan(-.5f));
            Assert.That(raised.bounds.max.x,Is.GreaterThan(.5f),"Both wings must spread around the shoulder.");
            Assert.That(lowered.bounds.size.x,Is.GreaterThan(raised.bounds.size.x*.85f),"The downstroke must keep its broad fan.");
            Assert.That(lowered.bounds.center.y,Is.LessThan(raised.bounds.center.y-.7f));
            Assert.That(fx.transform.parent,Is.SameAs(renderer.transform));
            foreach(var binding in UnityEditor.AnimationUtility.GetCurveBindings(clip))
                Assert.That(binding.type,Is.EqualTo(typeof(SpriteRenderer)),"The clip must not rotate, move or scale the wing.");
            fx.Prepare(0f,.3f);fx.Pose(.15f,true);
            Assert.That(fx.WingFrame,Is.EqualTo(-1),"The wing must not linger through the full basic-attack windup.");
            fx.Pose(.17f,true);Assert.That(fx.WingFrame,Is.EqualTo(0));
            var wing=fx.transform.Find("Great Wings Animated Sprite").GetComponent<SpriteRenderer>();
            float entranceAlpha=wing.color.a;Vector3 shoulder=wing.transform.position;
            renderer.sprite=shifted;fx.Pose(.23f,true);
            Assert.That(wing.transform.position,Is.EqualTo(shoulder),"Changing attack sprite bounds must not move the shoulder.");
            go.transform.position+=Vector3.right*3f;fx.Pose(.23f,true);
            Assert.That(wing.transform.position,Is.EqualTo(shoulder+Vector3.right*3f),"The attached wing follows a moving caster.");
            Assert.That(fx.WingFrame,Is.EqualTo(3));
            fx.Pose(.27f,true);Assert.That(fx.WingFrame,Is.EqualTo(5));
            fx.Pose(.295f,true);Assert.That(fx.WingFrame,Is.EqualTo(6));
            fx.Pose(.8f,true);Assert.That(fx.WingFrame,Is.EqualTo(6),"A delayed hit must not release the downstroke early.");
            fx.Strike(.8f);fx.CancelPreparation();fx.Pose(.8f,true);
            Assert.That(fx.WingFrame,Is.EqualTo(7),"The hit callback starts the forward stroke.");
            Assert.That(wing.color.a,Is.InRange(.7f,.85f));
            Assert.That(entranceAlpha,Is.LessThan(wing.color.a));
            Assert.That(Quaternion.Angle(wing.transform.rotation,renderer.transform.rotation),Is.LessThan(.01f),
                "Battle playback must not turn the wing transform.");
            fx.Pose(.825f,true);Assert.That(fx.WingFrame,Is.EqualTo(8));
            fx.Pose(.85f,true);Assert.That(fx.WingFrame,Is.EqualTo(9));
            var heldSprite=wing.sprite;float heldAlpha=wing.color.a;
            fx.Pose(.85f,true);Assert.That(wing.sprite,Is.SameAs(heldSprite),"A paused battle clock holds the frame.");
            Assert.That(wing.color.a,Is.EqualTo(heldAlpha));
            Vector3 anchor=wing.transform.localPosition;
            renderer.flipX=true;fx.Pose(.85f,true);Assert.That(wing.flipX,Is.True);
            Assert.That(wing.transform.localPosition.x,Is.EqualTo(-anchor.x));
            fx.Pose(.875f,true);Assert.That(fx.WingFrame,Is.EqualTo(10));
            fx.Pose(.905f,true);Assert.That(fx.WingFrame,Is.EqualTo(12));
            fx.Pose(.965f,true);Assert.That(fx.WingFrame,Is.EqualTo(15));Assert.That(wing.color.a,Is.LessThan(.15f));
            fx.Pose(.985f,true);Assert.That(fx.WingFrame,Is.EqualTo(-1),"The drawn stroke and particle dissolve must finish within 0.18 seconds of the hit.");
            fx.Prepare(2f,.3f);fx.CancelPreparation();fx.Pose(2.1f,true);
            Assert.That(fx.WingFrame,Is.EqualTo(-1));
        }
        finally {fx.Cancel();Object.DestroyImmediate(go);Object.DestroyImmediate(sprite);Object.DestroyImmediate(shifted);Object.DestroyImmediate(texture);}
    }

    [Test]
    public void GriffinRoyalStandingBodyKeepsTheBasicHitClockAndRestoresNormalAttacks()
    {
        var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/PixelMonsters/069_pone.prefab");
        var instance=UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            var renderer=driver.GetComponent<SpriteRenderer>();
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",flags);
            var late=typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",flags);
            int hits=0;
            driver.PlayBasicAttack(1f,()=>hits++);driver.UseGriffinWingPose();
            Sprite standing=driver.GetIdleFrame(0f);
            Assert.That(renderer.sprite,Is.SameAs(standing));
            advance.Invoke(driver,new object[]{.1f});late.Invoke(driver,null);
            Assert.That(hits,Is.Zero);
            driver.SetFrozen(true);
            advance.Invoke(driver,new object[]{1f});late.Invoke(driver,null);
            Assert.That(hits,Is.Zero);Assert.That(renderer.sprite,Is.SameAs(standing));
            driver.SetFrozen(false);
            for(int i=0;i<3;i++){advance.Invoke(driver,new object[]{.05f});late.Invoke(driver,null);}
            Assert.That(hits,Is.EqualTo(1));Assert.That(renderer.sprite,Is.SameAs(standing));
            for(int i=0;i<20;i++){advance.Invoke(driver,new object[]{.1f});late.Invoke(driver,null);}
            Assert.That(hits,Is.EqualTo(1));
            driver.PlayBasicAttack(1f,()=>hits++);
            advance.Invoke(driver,new object[]{.25f});late.Invoke(driver,null);
            Assert.That(hits,Is.EqualTo(2));Assert.That(renderer.sprite,Is.Not.SameAs(standing));
            driver.InterruptCurrentAction();
            driver.PlayBasicAttack(1f,()=>hits++);driver.UseGriffinWingPose();
            driver.InterruptCurrentAction();
            for(int i=0;i<10;i++){advance.Invoke(driver,new object[]{.1f});late.Invoke(driver,null);}
            Assert.That(hits,Is.EqualTo(2),"Cancelling the wing attack must cancel its pending hit.");
        }
        finally{Object.DestroyImmediate(instance);}
    }
}
