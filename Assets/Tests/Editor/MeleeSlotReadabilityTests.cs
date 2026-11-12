using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is=NUnit.Framework.Is;
using Object=UnityEngine.Object;

public sealed class MeleeSlotReadabilityTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Rect Body=new Rect(-1.2f,0f,2.4f,3.2f);
    private readonly List<Object> objects=new List<Object>();
    private MonsterAutoBattlePrototype battle;
    private Camera camera;
    private Type unitType;
    private IList units;
    private object enemy;
    private MonsterDefinition definition;

    [SetUp] public void Setup()
    {
        var host=Go("Melee readability test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)BattleField("units").GetValue(battle);
        BattleField("combatBounds").SetValue(battle,new Bounds(Vector3.zero,new Vector3(24,10,18)));
        var run=BattleField("expeditionRun");run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        definition=Resources.LoadAll<MonsterDefinition>("MonsterData").First(d=>d.monsterId=="velos");
        camera=Go("Readability camera").AddComponent<Camera>();
        camera.fieldOfView=36f;camera.aspect=16f/9f;camera.nearClipPlane=.1f;camera.farClipPlane=100f;
        camera.transform.rotation=Quaternion.Euler(58f,0f,0f);
        camera.transform.position=camera.transform.rotation*(Vector3.back*30f);
        BattleField("mainCamera").SetValue(battle,camera);
        enemy=Unit(1,Vector3.zero);
    }

    [TearDown] public void Cleanup()
    {
        var cache=(IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        for(int i=objects.Count-1;i>=0;i--)
        {
            Object asset=objects[i];if(asset==null)continue;
            if(asset is Sprite sprite)cache.Remove(sprite);
            Object.DestroyImmediate(asset);
        }
        objects.Clear();
    }

    [Test] public void VerticalAndDiagonalBodiesCostMoreThanSideBySideBodies()
    {
        var source=Attacker(Vector3.left*6f);Prepare();
        float side=Penalty(source,Vector3.right*2.8f);
        float top=Penalty(source,Vector3.forward*1.4f);
        float bottom=Penalty(source,Vector3.back*1.4f);
        float diagonal=Penalty(source,new Vector3(1.4f,0f,1.4f));
        Assert.That(side,Is.EqualTo(0f));
        Assert.That(diagonal,Is.GreaterThan(side));
        Assert.That(top,Is.GreaterThan(diagonal));
        Assert.That(bottom,Is.GreaterThan(diagonal));
    }

    [Test] public void CameraAngleDoesNotChangeTheClosestPhysicalAttackPosition()
    {
        var source=Attacker(Vector3.back*6f);
        float reach=(float)Call("AttackDistance",source,enemy);
        Vector3 cameraOrigin=camera.transform.position;
        foreach(float offset in new[]{-8f,-4f,4f,8f})
        {
            camera.transform.position=cameraOrigin+Vector3.right*offset;
            BattleField("mainCamera").SetValue(battle,null);
            Vector3 withoutCamera=(Vector3)Call("ClosestMeleeAttackPosition",source,enemy);
            BattleField("mainCamera").SetValue(battle,camera);
            Vector3 withCamera=(Vector3)Call("ClosestMeleeAttackPosition",source,enemy);
            Assert.That(withCamera,Is.EqualTo(withoutCamera));
            Assert.That(Vector3.Distance(withCamera,Position(enemy)),Is.LessThanOrEqualTo(reach));
            Assert.That((float)Call("AttackDistance",source,enemy),Is.EqualTo(reach),
                "Rendering changes must not change the authored damage reach.");
        }
    }

    [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
    [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
    public void MutualMeleeContactStartsFightingWithoutDraggingBothActorsUpTheScreen(int fps,bool reverseUpdate)
    {
        Model(enemy).transform.position=Vector3.right*6f;
        var source=Attacker(Vector3.left*6f);Set(enemy,"target",source);
        foreach(object unit in units)
        {Set(unit,"damage",20f);Set(unit,"attackInterval",.8f);Set(unit,"cooldown",0f);}
        BattleField("mlEpisodeActive").SetValue(battle,true);
        float dt=1f/fps;
        float firstContact=-1f,firstSourceAttack=-1f,firstEnemyAttack=-1f,maxCentroidZ=0f;
        var random=UnityEngine.Random.state;
        try
        {
            for(int frame=0;frame<fps*5;frame++)
            {
                Call("UpdateUnit",reverseUpdate?enemy:source,dt);
                Call("UpdateUnit",reverseUpdate?source:enemy,dt);
                float time=(frame+1)*dt,distance=Vector3.Distance(Position(source),Position(enemy));
                if(firstContact<0f&&distance<=(float)Call("MeleeArrivalDistance",source,enemy)+.0001f)firstContact=time;
                if(firstSourceAttack<0f&&(int)Get(source,"basicAttackCount")>0)firstSourceAttack=time;
                if(firstEnemyAttack<0f&&(int)Get(enemy,"basicAttackCount")>0)firstEnemyAttack=time;
                maxCentroidZ=Mathf.Max(maxCentroidZ,Mathf.Abs((Position(source).z+Position(enemy).z)*.5f));
                Assert.That(distance,Is.GreaterThanOrEqualTo(.6f-.001f),"Actual body collision must remain intact.");
            }
        }
        finally{UnityEngine.Random.state=random;}
        Assert.That(firstContact,Is.GreaterThanOrEqualTo(0f).And.LessThan(3f));
        Assert.That(firstSourceAttack,Is.GreaterThanOrEqualTo(firstContact-dt).And.LessThanOrEqualTo(firstContact+.35f));
        Assert.That(firstEnemyAttack,Is.GreaterThanOrEqualTo(firstContact-dt).And.LessThanOrEqualTo(firstContact+.35f));
        Assert.That(maxCentroidZ,Is.LessThanOrEqualTo(.25f),
            "A direct horizontal engagement must not make both fighters chase upward together.");
        Assert.That((float)Get(source,"hp"),Is.LessThan(5000f));Assert.That((float)Get(enemy,"hp"),Is.LessThan(5000f));
        Assert.That(Call("IsImmediatelyAttackable",source,enemy),Is.True);
        Assert.That(Call("IsImmediatelyAttackable",enemy,source),Is.True);
        Assert.That(Vector3.Distance(Position(source),Position(enemy)),
            Is.EqualTo((float)Call("MeleeContactDistance",source,enemy)).Within(.12f));
    }

    [TestCase("missing")] [TestCase("disabled")] [TestCase("empty")] [TestCase("behind")]
    public void InvalidProjectionFallsBackToZeroCost(string failure)
    {
        var source=Attacker(Vector3.left*6f);Vector3 candidate=Vector3.zero;
        if(failure=="missing")BattleField("mainCamera").SetValue(battle,null);
        if(failure=="disabled")camera.enabled=false;
        if(failure=="empty")Set(source,"meleeBodyShape",new Rect());
        if(failure=="behind")candidate=camera.transform.position-camera.transform.forward*3f;
        Prepare();object[] args={source,candidate,null};
        Assert.That(Call("TryProjectMeleeBody",args),Is.False);
        Assert.That(Penalty(source,candidate),Is.EqualTo(0f));
        Assert.That((Rect)args[2],Is.EqualTo(new Rect()));
    }

    [Test] public void VisualHeightOffsetIsIncludedInTheProjectedBody()
    {
        var source=Attacker(Vector3.left*6f);
        object[] before={source,Vector3.zero,null};Assert.That(Call("TryProjectMeleeBody",before),Is.True);
        Set(source,"visualHeightOffset",2f);
        object[] after={source,Vector3.zero,null};Assert.That(Call("TryProjectMeleeBody",after),Is.True);
        Assert.That(((Rect)after[2]).yMin,Is.GreaterThan(((Rect)before[2]).yMin));
    }

    [Test] public void CachedIdleFootprintIgnoresAttackPoseAndFacingButTracksRankScale()
    {
        var source=Attacker(Vector3.left*6f);var visual=Go("Stable idle visual");visual.transform.SetParent(Model(source).transform,false);
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite idle=Sprite(8,12),attack=Sprite(32,32);
        driver.Configure(new[]{idle},new[]{idle},new[]{attack},new[]{attack},new[]{idle},new[]{idle},new[]{idle});
        Set(source,"spriteAnimation",driver);var renderer=visual.GetComponent<SpriteRenderer>();renderer.sprite=idle;
        Call("CacheMeleeReadabilityBody",source);Rect original=(Rect)Get(source,"meleeBodyShape");
        Assert.That(original.width,Is.GreaterThan(0f));Assert.That(original.height,Is.GreaterThan(0f));
        Assert.That(original.center.x,Is.EqualTo(0f));
        renderer.sprite=attack;renderer.flipX=true;visual.transform.localScale=new Vector3(-1.8f,.7f,1f);
        Call("CacheMeleeReadabilityBody",source);
        Assert.That(Get(source,"meleeBodyShape"),Is.EqualTo(original));
        Set(source,"baseModelScale",Vector3.one);Set(source,"meleeBodyShape",new Rect());
        Type rankType=typeof(MonsterAutoBattlePrototype).GetMethod("ApplyRankVisualScale",Private).GetParameters()[2].ParameterType;
        Call("ApplyRankVisualScale",source,null,Enum.ToObject(rankType,0),2f);
        Rect ranked=(Rect)Get(source,"meleeBodyShape");Vector3 scale=Model(source).transform.lossyScale;
        Assert.That(ranked.width,Is.EqualTo(original.width*Mathf.Abs(scale.x)).Within(.0001f));
        Assert.That(ranked.height,Is.EqualTo(original.height*Mathf.Abs(scale.y)).Within(.0001f));
    }

    [Test] public void WarmedPenaltyDoesNotAllocateManagedMemory()
    {
        var source=Attacker(Vector3.left*6f);Prepare();
        GetType().GetMethod(nameof(CheckPenaltyAllocations),Private).MakeGenericMethod(unitType).Invoke(this,new[]{source});
    }

    private void CheckPenaltyAllocations<TUnit>(object value)
    {
        var penalty=(Func<TUnit,Vector3,float>)Delegate.CreateDelegate(typeof(Func<TUnit,Vector3,float>),battle,
            typeof(MonsterAutoBattlePrototype).GetMethod("MeleeReadabilityPenalty",Private));
        TUnit source=(TUnit)value;float total=0f;
        TestDelegate work=()=>{for(int i=0;i<128;i++)total+=penalty(source,new Vector3(.1f,0f,.1f));};
        work();Assert.That(work,UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        Assert.That(total,Is.GreaterThan(0f));
    }

    private GameObject Go(string name){var go=new GameObject(name);objects.Add(go);return go;}
    private Sprite Sprite(int width,int height)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);objects.Add(texture);
        var pixels=Enumerable.Repeat(new Color32(255,255,255,255),width*height).ToArray();
        texture.SetPixels32(pixels);texture.Apply(false,false);
        var sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,0f),10f,0,SpriteMeshType.FullRect);
        objects.Add(sprite);return sprite;
    }
    private object Unit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);var model=Go("Readability unit");model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",Go("Ring"));Set(unit,"id",units.Count);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"attackRange",1);
        Set(unit,"collisionRadius",.3f);Set(unit,"skillTimer",100f);Set(unit,"skillId","focus_chain");Set(unit,"cooldown",100f);
        Set(unit,"speedTilesPerSecond",2f);Set(unit,"definition",definition);Set(unit,"targetLockTimer",2.4f);
        Set(unit,"meleeBodyShape",Body);units.Add(unit);return unit;
    }
    private object Attacker(Vector3 position){var unit=Unit(0,position);Set(unit,"target",enemy);return unit;}
    private FieldInfo BattleField(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private);
    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private).Invoke(battle,args);
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private Vector3 Position(object unit)=>Model(unit).transform.position;
    private void Prepare()=>Call("PrepareMeleeReadability",enemy);
    private float Penalty(object unit,Vector3 candidate)=>(float)Call("MeleeReadabilityPenalty",unit,candidate);
}
