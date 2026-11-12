using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class WolfHuntEffectTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private HashSet<GameObject> existing;
    [SetUp] public void SetUp()=>existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
    [TearDown] public void TearDown()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)
                Object.DestroyImmediate(go);
    }

    [TestCase("029_nukgu")]
    [TestCase("030_seollang")]
    [TestCase("031_honga")]
    [TestCase("032_wolyeong")]
    public void PreparationCrouchesWithoutMovingFeetThenReleasesOnceAndRestoresScale(string prefab)
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+prefab));
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Action<float> step=dt=>
        {
            typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
        };
        step(0f);Vector3 scale=driver.transform.localScale,foot=driver.transform.position;
        float time=0f,release=-1f;int hits=0;
        driver.PlayWolfHuntAttack(1f,()=>{hits++;release=time;});
        float skillScale=(float)typeof(SpriteMonsterAnimationDriver).GetMethod("ScaleFor",Private)
            .Invoke(driver,new object[]{"SkillAttack"});
        Assert.That(driver.transform.localScale.y,Is.EqualTo(skillScale*.82f).Within(.001f));
        for(int i=0;i<90;i++){time=(i+1)/120f;step(1f/120f);}
        Assert.That(hits,Is.EqualTo(1));Assert.That(release,Is.InRange(.09f,.12f));
        Assert.That(driver.transform.position,Is.EqualTo(foot));
        Assert.That(driver.transform.localScale,Is.EqualTo(scale));
        driver.PlayWolfHuntAttack(1f,()=>hits++);driver.InterruptCurrentAction();step(.5f);
        Assert.That(hits,Is.EqualTo(1));Assert.That(driver.transform.localScale,Is.EqualTo(scale));
    }

    [TestCase(2f)]
    [TestCase(6f)]
    public void AmbushStopsAtCasterDistanceIdlesThenScatters(float approachDistance)
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/029_nukgu"));
        var source=body.GetComponentInChildren<SpriteRenderer>();
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Vector3 target=Vector3.right,start=target-Vector3.right*approachDistance;
        Vector3 stop=target+Vector3.right*1.2f;
        var ghost=PixelCombatEffect.PlayWolfHuntAfterimage(source,start,target,Color.cyan,.18f);
        var effect=ghost.GetComponent<PixelWolfHuntAfterimage>();
        effect.PrepareAmbush(source,()=>true,driver.GetSkillAttackFrame(2),driver);
        var animate=typeof(PixelWolfHuntAfterimage).GetMethod("AnimateAmbush",Private);
        float waitingAlpha=ghost.color.a;animate.Invoke(effect,new object[]{.1f});
        Assert.That(ghost.transform.position,Is.EqualTo(start));
        effect.LaunchAmbush(target,1.2f);animate.Invoke(effect,new object[]{.09f});
        Assert.That(ghost.transform.position.x,Is.EqualTo((start.x+target.x)*.5f).Within(.001f));
        animate.Invoke(effect,new object[]{.09f});
        Assert.That(ghost.color.a,Is.GreaterThan(waitingAlpha));
        Assert.That(ghost.color.a,Is.EqualTo(1f).Within(.001f));
        effect.ContactAmbush(true);animate.Invoke(effect,new object[]{.03f});
        Assert.That(ghost.transform.position,Is.EqualTo(Vector3.right));
        animate.Invoke(effect,new object[]{.08f});
        Assert.That(ghost.transform.position.x,Is.GreaterThan(1f));
        Assert.That(ghost.GetComponentInChildren<ParticleSystem>(),Is.Null);
        animate.Invoke(effect,new object[]{.16f});
        Assert.That(ghost.transform.position,Is.EqualTo(stop));
        Assert.That(ghost.sprite,Is.SameAs(driver.GetIdleFrame(.05f)));
        Assert.That(ghost.GetComponentInChildren<ParticleSystem>(),Is.Null);
        animate.Invoke(effect,new object[]{.15f});
        Assert.That(ghost.transform.position,Is.EqualTo(stop));
        Assert.That(ghost.sprite,Is.SameAs(driver.GetIdleFrame(.2f)));
        Assert.That(ghost.GetComponentInChildren<ParticleSystem>(),Is.Null);
        var properties=new MaterialPropertyBlock();ghost.GetPropertyBlock(properties);
        Assert.That(properties.GetTexture("_MainTex"),Is.SameAs(ghost.sprite.texture));
        Sprite idle=ghost.sprite;
        Vector2 minimum=(Vector2)idle.bounds.min*idle.pixelsPerUnit+idle.pivot+idle.rect.position;
        Vector2 size=(Vector2)idle.bounds.size*idle.pixelsPerUnit;
        Assert.That(properties.GetVector("_SpriteUV"),Is.EqualTo(new Vector4(
            minimum.x/idle.texture.width,minimum.y/idle.texture.height,
            size.x/idle.texture.width,size.y/idle.texture.height)));
        animate.Invoke(effect,new object[]{.12f});
        Assert.That(ghost.transform.position,Is.EqualTo(stop));
        var scatter=ghost.GetComponentInChildren<ParticleSystem>();
        Assert.That(scatter,Is.Not.Null);
        Assert.That(scatter.particleCount,Is.EqualTo(24));
        ghost.GetPropertyBlock(properties);
        Assert.That(properties.GetFloat("_TailDissolve"),Is.GreaterThan(0f));
        animate.Invoke(effect,new object[]{.55f});Assert.That(effect==null,Is.True);
        Assert.That(scatter==null,Is.True);
    }

    [Test]
    public void HuntVerticesFormAnEquilateralTriangleCenteredOnTheTarget()
    {
        Vector3 target=new Vector3(4,0,-3),caster=new Vector3(1,0,-2);
        Vector3 a=WolfCombatRules.HuntVertex(target,caster,0);
        Vector3 b=WolfCombatRules.HuntVertex(target,caster,1);
        Vector3 c=WolfCombatRules.HuntVertex(target,caster,2);
        Assert.That(Vector3.Distance(a,caster),Is.LessThan(.001f));
        Assert.That(Vector3.Distance((a+b+c)/3f,target),Is.LessThan(.001f));
        Assert.That(Vector3.Distance(a,b),Is.EqualTo(Vector3.Distance(b,c)).Within(.001f));
        Assert.That(Vector3.Distance(a,b),Is.EqualTo(Vector3.Distance(c,a)).Within(.001f));
    }

    [TestCase("029_nukgu",1f)]
    [TestCase("030_seollang",1f)]
    [TestCase("031_honga",1f)]
    [TestCase("032_wolyeong",1f)]
    [TestCase("029_nukgu",2f)]
    public void EclipseCompanionUsesTheOwnersFullBasicAttackFramesScaleAndSpeed(string prefab,float speed)
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+prefab));
        var source=body.GetComponentInChildren<SpriteRenderer>();
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Vector3 home=Vector3.left;
        var ghost=PixelCombatEffect.PlayWolfHuntAfterimage(source,home,home,Color.cyan,4f);
        var fx=ghost.GetComponent<PixelWolfHuntAfterimage>();
        fx.PrepareCompanion(source,driver,()=>home,()=>true,Color.cyan);
        var animate=typeof(PixelWolfHuntAfterimage).GetMethod("AnimateCompanion",Private);
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        var late=typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private);
        driver.PlayBasicAttack(speed,null);fx.StrikeCompanion(source,0f,speed);
        var frames=new HashSet<Sprite>();
        for(float age=0f;age<driver.BasicAttackDuration/speed;age+=.005f)
        {
            advance.Invoke(driver,new object[]{.005f});late.Invoke(driver,null);
            animate.Invoke(fx,new object[]{.005f});
            Assert.That(ghost.transform.position,Is.EqualTo(home));
            if(driver.CurrentMotionName!="Attack")continue;
            frames.Add(source.sprite);
            Assert.That(ghost.sprite,Is.SameAs(source.sprite));
            Assert.That(Vector3.Distance(ghost.transform.lossyScale,source.transform.lossyScale),Is.LessThan(.001f));
        }
        Assert.That(frames.Count,Is.EqualTo(driver.AttackFrameCount));
    }

    [TestCase("029_nukgu")]
    [TestCase("030_seollang")]
    [TestCase("031_honga")]
    [TestCase("032_wolyeong")]
    public void AlphaHunterUsesEveryBasicFrameHitsOnceThenScatters(string prefab)
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+prefab));
        var owner=body.GetComponentInChildren<SpriteRenderer>();
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var victim=Object.Instantiate(body).GetComponentInChildren<SpriteRenderer>();
        var ghost=PixelCombatEffect.PlayWolfHuntAfterimage(owner,Vector3.left,Vector3.left,Color.white,2f);
        var fx=ghost.GetComponent<PixelWolfHuntAfterimage>();int hits=0;float hitAge=0f,age=0f;
        fx.PrepareAlphaStrike(owner,driver,victim,()=>true,()=>{hits++;hitAge=age;});
        var animate=typeof(PixelWolfHuntAfterimage).GetMethod("AnimateAlphaStrike",Private);
        var frames=new HashSet<Sprite>();bool facing=ghost.flipX;bool scatter=false;
        while(fx!=null&&age<2f)
        {
            age+=.005f;animate.Invoke(fx,new object[]{.005f});
            if(fx==null)break;
            Assert.That(ghost.flipX,Is.EqualTo(facing));
            if(ghost.sprite.name.Contains("Attack"))frames.Add(ghost.sprite);
            scatter|=ghost.GetComponentInChildren<ParticleSystem>()!=null;
        }
        Assert.That(frames.Count,Is.EqualTo(driver.AttackFrameCount));
        Assert.That(hits,Is.EqualTo(1));Assert.That(hitAge,Is.EqualTo(.06f+driver.BasicAttackHitTime).Within(.006f));
        Assert.That(scatter,Is.True);Assert.That(fx==null,Is.True);
    }

    [Test]
    public void InvalidatedAlphaHunterCancelsBeforeDamage()
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/029_nukgu"));
        var owner=body.GetComponentInChildren<SpriteRenderer>();
        var ghost=PixelCombatEffect.PlayWolfHuntAfterimage(owner,Vector3.left,Vector3.left,Color.white,2f);
        var fx=ghost.GetComponent<PixelWolfHuntAfterimage>();int hits=0;
        fx.PrepareAlphaStrike(owner,body.GetComponentInChildren<SpriteMonsterAnimationDriver>(),owner,()=>false,()=>hits++);
        typeof(PixelWolfHuntAfterimage).GetMethod("AnimateAlphaStrike",Private).Invoke(fx,new object[]{1f});
        Assert.That(fx==null,Is.True);Assert.That(hits,Is.Zero);
    }

    [Test]
    public void EclipseCompanionStopsIdlesBlinksFasterAndBurstsAtItsOwnPosition()
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/029_nukgu"));
        var source=body.GetComponentInChildren<SpriteRenderer>();
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Vector3 home=Vector3.left;
        var ghost=PixelCombatEffect.PlayWolfHuntAfterimage(source,home,home,Color.cyan,4f);
        var fx=ghost.GetComponent<PixelWolfHuntAfterimage>();
        fx.PrepareCompanion(source,driver,()=>home,()=>true,Color.cyan);
        var animate=typeof(PixelWolfHuntAfterimage).GetMethod("AnimateCompanion",Private);
        animate.Invoke(fx,new object[]{.01f});fx.FinishCompanion();
        Vector3 stopped=ghost.transform.position;bool facing=ghost.flipX;
        home+=Vector3.right*3f;
        animate.Invoke(fx,new object[]{.19f});
        Assert.That(ghost.transform.position,Is.EqualTo(stopped));
        Assert.That(ghost.sprite,Is.SameAs(driver.GetIdleFrame(.19f)));
        Assert.That(ghost.flipX,Is.EqualTo(facing));Assert.That(ghost.color.a,Is.EqualTo(1f));
        float age=.19f;bool bright=true;
        var changes=new List<float>();
        while(age<WolfCombatRules.EclipseIdleDuration+WolfCombatRules.EclipseBlinkDuration-.01f)
        {
            animate.Invoke(fx,new object[]{.005f});age+=.005f;
            Assert.That(ghost.transform.position,Is.EqualTo(stopped));
            Assert.That(ghost.sprite.name,Does.Contain("Idle"));
            Assert.That(ghost.flipX,Is.EqualTo(facing));
            bool now=ghost.color.a>.5f;
            if(now!=bright){changes.Add(age);bright=now;}
        }
        Assert.That(changes.Count,Is.GreaterThanOrEqualTo(7));
        Assert.That(changes[changes.Count-1]-changes[changes.Count-2],
            Is.LessThan(changes[1]-changes[0]));
        animate.Invoke(fx,new object[]{.02f});Assert.That(fx==null,Is.True);
        var burst=Array.Find(Object.FindObjectsOfType<ParticleSystem>(),p=>p.name=="Wolf Eclipse Dissolve Burst");
        Assert.That(burst,Is.Not.Null);Assert.That(burst.particleCount,Is.EqualTo(32));
        Assert.That(burst.transform.parent,Is.Null);
        var particles=new ParticleSystem.Particle[32];burst.GetParticles(particles);
        float radius=MonsterAutoBattlePrototype.TileSize*WolfCombatRules.EclipseBurstRadiusTiles;
        float farthest=0f;
        foreach(var particle in particles)
        {
            Assert.That(particle.velocity.y,Is.EqualTo(0f).Within(.001f));
            float reach=(particle.position-burst.transform.position+particle.velocity*particle.startLifetime).magnitude;
            Assert.That(reach,Is.LessThanOrEqualTo(radius+.001f));
            farthest=Mathf.Max(farthest,reach);
        }
        Assert.That(farthest,Is.EqualTo(radius).Within(.001f));
    }

    [Test]
    public void InvalidatedAmbushDisappearsBeforeItCanStrike()
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/029_nukgu"));
        var source=body.GetComponentInChildren<SpriteRenderer>();
        var ghost=PixelCombatEffect.PlayWolfHuntAfterimage(source,Vector3.left,Vector3.right,Color.cyan,.18f);
        var effect=ghost.GetComponent<PixelWolfHuntAfterimage>();
        effect.PrepareAmbush(source,()=>false);
        typeof(PixelWolfHuntAfterimage).GetMethod("AnimateAmbush",Private).Invoke(effect,new object[]{.01f});
        Assert.That(effect==null,Is.True);
    }

    [Test]
    public void ContactFlashUsesCachedPixelFramesAndStaysAtContactWhenVictimMoves()
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/029_nukgu"));
        var target=body.GetComponentInChildren<SpriteRenderer>();
        var first=PixelCombatEffect.PlayWolfHuntImpact(target.transform,target,Vector3.right,Color.white,false);
        var final=PixelCombatEffect.PlayWolfHuntImpact(target.transform,target,Vector3.left,Color.red,true);
        var frames=(Sprite[])typeof(PixelCombatEffect).GetField("frames",Private).GetValue(first);
        Assert.That(typeof(PixelCombatEffect).GetField("frames",Private).GetValue(final),Is.SameAs(frames));
        Assert.That(frames[0].name,Does.StartWith("Wolf Contact Splinters"));
        Assert.That(frames[0].texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(final.transform.localScale.x/first.transform.localScale.x,Is.EqualTo(1.15f).Within(.001f));
        Vector3 impact=first.transform.position;body.transform.position+=Vector3.right*3f;
        typeof(PixelCombatEffect).GetMethod("LateUpdate",Private).Invoke(first,null);
        Assert.That(first.transform.position,Is.EqualTo(impact));
    }

    [Test]
    public void SpectralHitBurstsOutwardAtContactAndClearsQuickly()
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/029_nukgu"));
        var target=body.GetComponentInChildren<SpriteRenderer>();
        var burst=PixelCombatEffect.PlayWolfSpectralImpact(target.transform,target,false);
        Assert.That(burst.particleCount,Is.EqualTo(32));
        Assert.That(burst.transform.parent,Is.Null);
        var renderer=burst.GetComponent<ParticleSystemRenderer>();
        Assert.That(renderer.sortingOrder,Is.GreaterThan(target.sortingOrder));
        Assert.That(renderer.sharedMaterial.mainTexture.filterMode,Is.EqualTo(FilterMode.Point));
        var particles=new ParticleSystem.Particle[32];burst.GetParticles(particles);
        foreach(var p in particles)
        {
            Assert.That(p.velocity.magnitude,Is.InRange(5.99f,10.01f));
            Assert.That(p.startLifetime,Is.LessThanOrEqualTo(.221f));
            Assert.That(Vector3.Dot(p.position-burst.transform.position,p.velocity),Is.GreaterThan(0f));
        }
        Vector3 impact=burst.transform.position;body.transform.position+=Vector3.right*3f;
        Assert.That(burst.transform.position,Is.EqualTo(impact));
        burst.Simulate(.3f,true,false,false);Assert.That(burst.particleCount,Is.Zero);
    }
}
