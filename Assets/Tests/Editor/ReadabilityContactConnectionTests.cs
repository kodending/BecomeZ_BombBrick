using System;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityContactConnectionTests
{
    private const BindingFlags Fields=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private HashSet<GameObject> existing;
    private Dictionary<PixelCombatEffect,bool> effectStates;
    private readonly List<Object> art=new List<Object>();
    private bool enabled,active,reduced;
    private CombatReadabilityPreset preset;
    private SpriteRenderer previousRecipient;
    private Vector3 previousIncoming;

    [SetUp] public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        effectStates=new Dictionary<PixelCombatEffect,bool>();
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())effectStates[effect]=effect.gameObject.activeSelf;
        enabled=CombatReadabilityPreview.Enabled;active=CombatEffectTestConfiguration.Active;preset=CombatReadabilityPreview.Preset;
        previousRecipient=CombatReadabilityPreview.ProjectileContactRenderer;previousIncoming=CombatReadabilityPreview.ProjectileContactDirection;
        reduced=CombatFlashPresentation.Reduced;
        CombatEffectTestConfiguration.Active=CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        CombatReadabilityPreview.ProjectileContactRenderer=null;CombatReadabilityPreview.ProjectileContactDirection=Vector3.zero;
        CombatFlashPresentation.Apply(false);
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&effect.name=="Contact impact"&&effect.gameObject.scene.IsValid())effect.ReleaseAfterBattleEndFade();
    }

    [TearDown] public void Cleanup()
    {
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&effect.gameObject.activeSelf&&(!effectStates.TryGetValue(effect,out bool wasActive)||!wasActive))
                effect.ReleaseAfterBattleEndFade();
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&go.scene.IsValid()&&go.transform.parent==null&&!existing.Contains(go))Object.DestroyImmediate(go);
        foreach(var item in art)if(item is Sprite frame)
            ((System.Collections.IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",Fields).GetValue(null)).Remove(frame);
        foreach(var item in art)if(item!=null)Object.DestroyImmediate(item);
        art.Clear();
        CombatReadabilityPreview.Enabled=enabled;CombatEffectTestConfiguration.Active=active;CombatReadabilityPreview.Preset=preset;
        CombatReadabilityPreview.ProjectileContactRenderer=previousRecipient;CombatReadabilityPreview.ProjectileContactDirection=previousIncoming;
        CombatFlashPresentation.Apply(reduced);
    }

    [TestCase(false,false)] [TestCase(true,false)] [TestCase(false,true)] [TestCase(true,true)]
    public void VisibleCenterAndContactRespectTransparentPaddingFlipsAndNestedTransforms(bool flipX,bool flipY)
    {
        var body=Body("Asymmetric contact body",Vector3.zero);
        var root=body.transform.parent;
        root.SetPositionAndRotation(new Vector3(3f,2f,-1f),Quaternion.Euler(0f,37f,13f));
        root.localScale=new Vector3(1.3f,.7f,1f);
        body.transform.localPosition=new Vector3(.2f,.3f,0f);
        body.transform.localRotation=Quaternion.Euler(58f,0f,0f);
        body.transform.localScale=new Vector3(.8f,1.4f,1f);
        body.flipX=flipX;body.flipY=flipY;
        Vector3 local=new Vector3(flipX?.5f:-.5f,flipY?.0625f:-.0625f,0f);
        Vector3 expected=body.transform.TransformPoint(local);
        Assert.That(Vector3.Distance(CombatReadabilityPreview.VisibleBodyCenter(body),expected),Is.LessThan(.002f));
        Vector3 direction=new Vector3(1f,0f,.4f);
        var effect=PixelCombatEffect.PlayContactImpact(root,body,direction,false);
        var camera=Get<Camera>(effect,"effectCamera");
        Vector3 right=camera==null?body.transform.right:camera.transform.right;
        Vector3 up=camera==null?body.transform.up:camera.transform.up;
        Vector2 projected=new Vector2(Vector3.Dot(direction,right),Vector3.Dot(direction,up)).normalized;
        expected-=(right*projected.x+up*projected.y)*Mathf.Min(.18f,body.bounds.size.y*.18f);
        Assert.That(Vector3.Distance(effect.transform.position,expected),Is.LessThan(.002f));
        Assert.That(Get<Sprite[]>(effect,"frames").Length,Is.EqualTo(4));
    }

    [TestCase(CombatReadabilityPreset.Current,true)]
    [TestCase(CombatReadabilityPreset.Compact,true)]
    [TestCase(CombatReadabilityPreset.Local,false)]
    public void ExistingModesDoNotOptIntoBodyAlignedProjectileArt(CombatReadabilityPreset mode,bool selected)
    {
        CombatReadabilityPreview.Preset=mode;CombatReadabilityPreview.Enabled=selected;
        var source=Body("Legacy source",Vector3.zero);var target=Body("Legacy target",Vector3.right*4f);
        var shot=Shot(source,target,()=>{});Vector3 head=Head(shot).position;float duration=shot.FlightDuration;
        shot.UseReadabilityBodyAnchors(source);
        Assert.That(Get<bool>(shot,"readabilityBodyAnchors"),Is.False);
        Assert.That(Head(shot).position,Is.EqualTo(head));Assert.That(shot.FlightDuration,Is.EqualTo(duration));
    }

    [Test]
    public void OptedInBasicArtKeepsExactLegacyDeflectionSegmentsDurationAndCapturedVictim()
    {
        var source=Body("Moving attacker",Vector3.zero);var target=Body("Moving victim",Vector3.right*4f);
        var oldSegments=new List<Vector3>();var newSegments=new List<Vector3>();
        int oldArrivals=0,victimHp=100,otherHp=100;Vector3 contactPoint=Vector3.zero,incoming=Vector3.zero;
        var legacy=Shot(source,target,()=>oldArrivals++);
        PixelStarProjectile connected=null;
        connected=Shot(source,target,()=>
        {
            victimHp-=10;contactPoint=Head(connected).position;
            Assert.That(CombatReadabilityPreview.ProjectileContactRenderer,Is.SameAs(target));
            incoming=CombatReadabilityPreview.ProjectileContactDirection;
        });
        Set(legacy,"flight",new CombatProjectileSource.Flight{deflect=(a,b,r)=>{oldSegments.Add(a);oldSegments.Add(b);return false;}});
        Set(connected,"flight",new CombatProjectileSource.Flight{deflect=(a,b,r)=>{newSegments.Add(a);newSegments.Add(b);return false;}});
        float duration=legacy.FlightDuration;
        connected.UseReadabilityBodyAnchors(source);
        Assert.That(connected.FlightDuration,Is.EqualTo(duration));
        Call(legacy,"Advance",duration*.4f);Call(connected,"Advance",duration*.4f);
        source.transform.parent.position=Vector3.right*20f;
        target.transform.parent.position+=new Vector3(.2f,0f,.15f);target.flipX=true;
        Vector3 expected=CombatReadabilityPreview.VisibleBodyCenter(target);
        Call(legacy,"Advance",duration*.61f);Call(connected,"Advance",duration*.61f);
        Assert.That(newSegments.Count,Is.EqualTo(oldSegments.Count));
        for(int i=0;i<oldSegments.Count;i++)Assert.That(Vector3.Distance(newSegments[i],oldSegments[i]),Is.LessThan(.00001f));
        Assert.That(Vector3.Distance(contactPoint,expected),Is.LessThan(.002f));
        Assert.That(incoming.x,Is.GreaterThan(0f),"The projectile still arrives from its launch side after the attacker moves past its victim.");
        Assert.That(victimHp,Is.EqualTo(90));Assert.That(otherHp,Is.EqualTo(100));Assert.That(oldArrivals,Is.EqualTo(1));
        Assert.That(CombatReadabilityPreview.ProjectileContactRenderer,Is.Null);
        Assert.That(CombatReadabilityPreview.ProjectileContactDirection,Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void IncomingDirectionOnlyChangesItsMatchingVictimsVisualReaction()
    {
        var first=RealBody();var other=RealBody();
        var one=first.transform.root.GetComponent<VoxelEnemyReaction>()??first.transform.root.gameObject.AddComponent<VoxelEnemyReaction>();
        var two=other.transform.root.GetComponent<VoxelEnemyReaction>()??other.transform.root.gameObject.AddComponent<VoxelEnemyReaction>();
        Vector3 axis=first.transform.right;
        CombatReadabilityPreview.ProjectileContactRenderer=first;
        CombatReadabilityPreview.ProjectileContactDirection=axis;
        Vector3 position=first.transform.root.position;
        one.PlayHit(-axis,false,true);two.PlayHit(-axis,false,true);
        Assert.That(Vector3.Dot(WorldShake(first),axis),Is.GreaterThan(0f));
        Assert.That(Vector3.Dot(WorldShake(other),axis),Is.LessThan(0f),"Splash or reflection on another recipient cannot inherit the primary projectile's direction.");
        Assert.That(first.transform.root.position,Is.EqualTo(position));
    }

    [Test]
    public void ArrivalContextRestoresTheOuterContextEvenIfTheDamageCallbackThrows()
    {
        var source=Body("Context source",Vector3.zero);var target=Body("Context target",Vector3.right*4f);
        var sentinel=Body("Outer recipient",Vector3.back);
        CombatReadabilityPreview.ProjectileContactRenderer=sentinel;
        CombatReadabilityPreview.ProjectileContactDirection=Vector3.forward*3f;
        var shot=Shot(source,target,()=>
        {
            Assert.That(CombatReadabilityPreview.ProjectileContactRenderer,Is.SameAs(target));
            throw new InvalidOperationException("Contact scope test");
        });
        shot.UseReadabilityBodyAnchors(source);
        var error=Assert.Throws<TargetInvocationException>(()=>Call(shot,"Advance",shot.FlightDuration+.001f));
        Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());
        Assert.That(CombatReadabilityPreview.ProjectileContactRenderer,Is.SameAs(sentinel));
        Assert.That(CombatReadabilityPreview.ProjectileContactDirection,Is.EqualTo(Vector3.forward*3f));
    }

    [Test]
    public void AZeroLengthArrivalStepKeepsTheLastVisibleIncomingDirection()
    {
        var source=Body("Short final step source",Vector3.zero);
        var target=Body("Short final step target",Vector3.right*4f);
        Vector3 observed=Vector3.zero;
        var shot=Shot(source,target,()=>observed=CombatReadabilityPreview.ProjectileContactDirection);
        shot.UseReadabilityBodyAnchors(source);
        Call(shot,"Advance",shot.FlightDuration*.5f);
        Vector3 expected=Get<Vector3>(shot,"readabilityIncomingDirection");
        Assert.That(expected.sqrMagnitude,Is.GreaterThan(.000001f));
        Call(shot,"UpdateVisual",1f);Set(shot,"elapsed",shot.FlightDuration);
        Call(shot,"AdvanceFlight",0f);
        Assert.That(observed,Is.EqualTo(expected));
        Assert.That(CombatReadabilityPreview.ProjectileContactRenderer,Is.Null);
    }

    private SpriteRenderer Body(string name,Vector3 position)
    {
        var root=new GameObject(name);root.transform.position=position;
        var child=new GameObject("Padded sprite");child.transform.SetParent(root.transform,false);
        var renderer=child.AddComponent<SpriteRenderer>();
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        var pixels=new Color32[4096];
        for(int y=12;y<48;y++)for(int x=8;x<24;x++)pixels[y*64+x]=new Color32(255,255,255,255);
        texture.SetPixels32(pixels);texture.Apply();art.Add(texture);
        var sprite=Sprite.Create(texture,new Rect(0,0,64,64),Vector2.one*.5f,32f,0,SpriteMeshType.FullRect);
        art.Add(sprite);renderer.sprite=sprite;return renderer;
    }

    private static PixelStarProjectile Shot(SpriteRenderer source,SpriteRenderer target,Action arrival)
    {
        var shot=PixelStarProjectile.PlayStone(source.transform,source,target.transform,target,Color.yellow,arrival);
        shot.SetImpactEnabled(false);Set(shot,"curveSide",1f);Call(shot,"UpdateVisual",0f);return shot;
    }
    private static SpriteRenderer RealBody()
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos");
        return Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath)).GetComponentInChildren<SpriteRenderer>();
    }
    private static Vector3 WorldShake(SpriteRenderer body)
    {
        var driver=body.GetComponent<SpriteMonsterAnimationDriver>();
        Vector3 offset=Get<Vector3>(driver,"contactShakeOffset");
        return body.transform.parent==null?offset:body.transform.parent.TransformVector(offset);
    }
    private static Transform Head(PixelStarProjectile shot)=>Get<SpriteRenderer>(shot,"head").transform;
    private static T Get<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Fields).GetValue(obj);
    private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Fields).SetValue(obj,value);
    private static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Fields).Invoke(obj,args);
}
