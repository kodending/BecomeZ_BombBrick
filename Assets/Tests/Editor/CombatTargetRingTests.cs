using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is=NUnit.Framework.Is;
using Object=UnityEngine.Object;

public sealed class CombatTargetRingTests
{
    private GameObject host;
    private CombatTargetRing ring;
    private Material material;
    private Renderer shadow;
    private Transform Indicator=>host.transform.Find("Combat Target Ring");
    private Transform Pivot=>Indicator.Find("Target Direction");
    private MeshFilter Circle=>Indicator.Find("Circle").GetComponent<MeshFilter>();
    private MeshFilter Point=>Pivot.Find("Point").GetComponent<MeshFilter>();

    [SetUp] public void Setup()
    {
        host=GameObject.CreatePrimitive(PrimitiveType.Cylinder);shadow=host.GetComponent<Renderer>();
        host.transform.position=new Vector3(2f,.025f,-3f);
        host.transform.localScale=new Vector3(.6f,.012f,.41f);
        material=new Material(Shader.Find("Sprites/Default"));
        ring=host.AddComponent<CombatTargetRing>();ring.Configure(.65f,Color.cyan,material);
        ring.SetVisible(true);
    }

    [TearDown] public void Cleanup()
    {
        if(ring!=null)Lifecycle("OnDestroy");
        Object.DestroyImmediate(host);Object.DestroyImmediate(material);
    }

    // EditMode does not dispatch native lifetime messages for this runtime-only MonoBehaviour.
    private void Lifecycle(string method)=>typeof(CombatTargetRing)
        .GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ring,null);

    [TestCase(0f,1f)] [TestCase(1f,0f)] [TestCase(0f,-1f)] [TestCase(-1f,0f)]
    public void PointFacesTheWorldTargetOnAllGroundCardinals(float x,float z)
    {
        var direction=new Vector3(x,20f,z);ring.UpdateDirection(direction,true);
        Vector3 tip=Point.transform.TransformPoint(Point.sharedMesh.vertices[0])-Indicator.position;
        tip.y=0f;direction.y=0f;
        Assert.That(Vector3.Dot(tip.normalized,direction),Is.GreaterThan(.99999f));
        Assert.That(tip.magnitude,Is.EqualTo(.65f*1.38f).Within(.0001f));
        Assert.That(Pivot.gameObject.activeInHierarchy,Is.True);
    }

    [Test]
    public void GroundCircleKeepsWorldRadiusAfterNonuniformAndOffspringRescaling()
    {
        for(int pass=0;pass<2;pass++)
        {
            if(pass==1)host.transform.localScale*=.55f;
            ring.UpdateDirection(new Vector3(1f,0f,1f),true);
            Vector3[] vertices=Circle.sharedMesh.vertices;
            float smallest=float.PositiveInfinity,largest=0f;
            foreach(Vector3 vertex in vertices)
            {
                Vector3 world=Circle.transform.TransformPoint(vertex)-Indicator.position;
                Assert.That(world.y,Is.EqualTo(0f).Within(.0001f));
                smallest=Mathf.Min(smallest,world.magnitude);largest=Mathf.Max(largest,world.magnitude);
            }
            Assert.That(smallest,Is.EqualTo(.65f*.88f).Within(.0001f));
            Assert.That(largest,Is.EqualTo(.65f).Within(.0001f));
            Vector3 tip=Point.transform.TransformPoint(Point.sharedMesh.vertices[0])-Indicator.position;
            tip.y=0f;
            Assert.That(Vector3.Dot(tip.normalized,new Vector3(1f,0f,1f).normalized),Is.GreaterThan(.99999f));
        }
    }

    [TestCase(0f,0f)] [TestCase(float.NaN,1f)] [TestCase(1f,float.PositiveInfinity)]
    public void InvalidDirectionHidesOnlyThePointWithoutInvalidTransforms(float x,float z)
    {
        ring.UpdateDirection(Vector3.forward,true);ring.UpdateDirection(new Vector3(x,0f,z),true);
        Assert.That(Pivot.gameObject.activeInHierarchy,Is.False);
        Assert.That(Circle.gameObject.activeInHierarchy,Is.True);Assert.That(shadow.enabled,Is.True);
        Quaternion rotation=Pivot.rotation;
        Assert.That(float.IsNaN(rotation.x)||float.IsInfinity(rotation.x),Is.False);
        Assert.That(float.IsNaN(rotation.y)||float.IsInfinity(rotation.y),Is.False);
        Assert.That(float.IsNaN(rotation.z)||float.IsInfinity(rotation.z),Is.False);
        Assert.That(float.IsNaN(rotation.w)||float.IsInfinity(rotation.w),Is.False);
    }

    [Test]
    public void GrowthChangesTheRadiusWithoutHidingOrRebuildingTheLiveMarker()
    {
        ring.UpdateDirection(Vector3.forward,true);
        Mesh circle=Circle.sharedMesh,point=Point.sharedMesh;
        ring.SetRadius(1.3f);
        Assert.That(Indicator.gameObject.activeInHierarchy,Is.True);
        Assert.That(Pivot.gameObject.activeInHierarchy,Is.True);
        Assert.That(Circle.sharedMesh,Is.SameAs(circle));Assert.That(Point.sharedMesh,Is.SameAs(point));
        Assert.That(Vector3.Distance(Circle.transform.TransformPoint(Circle.sharedMesh.vertices[0]),Indicator.position),
            Is.EqualTo(1.3f).Within(.0001f));
        ring.SetVisible(false);ring.SetRadius(.65f);
        Assert.That(Indicator.gameObject.activeInHierarchy,Is.False);
    }

    [Test]
    public void ReconfigureAndPoolReuseKeepOneGeometrySetAndTheExistingShadow()
    {
        Mesh circle=Circle.sharedMesh,point=Point.sharedMesh;
        int children=host.GetComponentsInChildren<Transform>(true).Length;
        ring.UpdateDirection(Vector3.forward,true);ring.enabled=false;Lifecycle("OnDisable");
        Assert.That(Indicator.gameObject.activeInHierarchy,Is.False);Assert.That(shadow.enabled,Is.True);
        ring.enabled=true;ring.Configure(.4f,Color.red,material);
        Assert.That(host.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(children));
        Assert.That(Circle.sharedMesh,Is.SameAs(circle));Assert.That(Point.sharedMesh,Is.SameAs(point));
        Assert.That(Circle.GetComponent<Renderer>().sharedMaterial,Is.SameAs(material));
        ring.SetVisible(true);ring.UpdateDirection(Vector3.right,false);
        Assert.That(Circle.gameObject.activeInHierarchy,Is.True);Assert.That(Pivot.gameObject.activeInHierarchy,Is.False);
        ring.SetVisible(false);Assert.That(Indicator.gameObject.activeInHierarchy,Is.False);
        Assert.That(shadow.enabled,Is.True);Assert.That(host.activeSelf,Is.True);
    }

    [Test]
    public void DestroyingTheComponentReleasesOnlyItsOwnMeshesAndChildren()
    {
        Mesh circle=Circle.sharedMesh,point=Point.sharedMesh;Transform indicator=Indicator;
        Lifecycle("OnDestroy");
        Object.DestroyImmediate(ring);
        Assert.That(circle==null&&point==null&&indicator==null,Is.True);
        Assert.That(host!=null&&shadow!=null&&shadow.enabled,Is.True);
        Assert.That(material!=null,Is.True);
    }

    [Test]
    public void FollowingTargetsDoesNotAllocateAfterConfiguration()
    {
        ring.UpdateDirection(Vector3.forward,true);
        Assert.That(()=>
        {
            for(int i=0;i<120;i++)ring.UpdateDirection(i%2==0?Vector3.forward:Vector3.left,true);
        },Is.Not.AllocatingGCMemory());
    }
}
