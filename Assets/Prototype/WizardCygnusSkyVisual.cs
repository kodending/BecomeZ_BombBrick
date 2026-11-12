using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Both sides of the cinematic handoff sample the same four authored wing drawings.
internal sealed class WizardCygnusSkyVisual:IDisposable
{
    private const int Columns=20,Rows=12;
    private static readonly Dictionary<Texture2D,Vector4> Framing=new Dictionary<Texture2D,Vector4>();
    private readonly GameObject root;
    private readonly Mesh[] meshes=new Mesh[2];
    private readonly MeshRenderer[] renderers=new MeshRenderer[2];
    private readonly MaterialPropertyBlock[] properties={new MaterialPropertyBlock(),new MaterialPropertyBlock()};
    private readonly Vector3[] vertices=new Vector3[(Columns+1)*(Rows+1)];
    private readonly Texture2D atlas;
    private readonly Material material;
    private readonly Vector4 framing;
    private float pointWidth,pointHeight,pointClock;
    private bool disposed;
    public bool Available=>atlas!=null&&material!=null;
    public float VisibleAspect=>framing.z/Mathf.Max(.02f,framing.w)*(atlas==null?1f:atlas.width/(float)atlas.height);

    public WizardCygnusSkyVisual(Transform parent,int layer,int order,int sortingLayer=0)
    {
        root=new GameObject("Cygnus authored sky wings"){layer=layer,hideFlags=HideFlags.DontSave};root.transform.SetParent(parent,false);
        atlas=Resources.Load<Texture2D>(WizardCygnusCinematicStage.ResourceRoot+"CygnusSkyAction");
        Shader shader=Resources.Load<Shader>(WizardCygnusCinematicStage.ResourceRoot+"CygnusSkySurface");
        if(shader!=null&&atlas!=null)
            material=new Material(shader){name="Cygnus authored nebula wings",mainTexture=atlas,hideFlags=HideFlags.DontSave};
        framing=Measure(atlas);
        for(int i=0;i<2;i++)
        {
            var host=new GameObject(i==0?"Cygnus sky current drawing":"Cygnus sky drawing bridge"){layer=layer,hideFlags=HideFlags.DontSave};
            host.transform.SetParent(root.transform,false);meshes[i]=Grid();host.AddComponent<MeshFilter>().sharedMesh=meshes[i];
            renderers[i]=host.AddComponent<MeshRenderer>();renderers[i].sharedMaterial=material;renderers[i].sortingLayerID=sortingLayer;renderers[i].sortingOrder=order+i;
            renderers[i].shadowCastingMode=ShadowCastingMode.Off;renderers[i].receiveShadows=false;renderers[i].enabled=false;
        }
    }

    public void Render(float clock,float pose,Vector3 center,Quaternion facing,float width,float alpha,float reveal)
    {
        if(disposed)return;
        root.transform.localPosition=center;root.transform.localRotation=facing;
        pointWidth=width/Mathf.Max(.02f,framing.z);
        pointHeight=pointWidth*(atlas==null?1f:atlas.height/(float)atlas.width);pointClock=clock;
        if(!Available||alpha<=.001f){Hide();return;}
        int next=Mathf.Clamp(Mathf.CeilToInt(pose),0,3),previous=Mathf.Clamp(next-1,0,3);
        float blend=next==0?1f:Mathf.SmoothStep(0f,1f,Mathf.Clamp01(pose-previous));
        for(int n=0;n<2;n++)
        {
            float opacity=alpha*(n==0?blend:1f-blend);renderers[n].enabled=opacity>.001f;if(!renderers[n].enabled)continue;
            int frame=n==0?next:previous;
            int left=Mathf.FloorToInt(frame%2*atlas.width/2f),right=Mathf.FloorToInt((frame%2+1)*atlas.width/2f);
            int bottom=Mathf.FloorToInt((1-frame/2)*atlas.height/2f),top=Mathf.FloorToInt((2-frame/2)*atlas.height/2f);
            var block=properties[n];block.SetVector("_CellUV",new Vector4(left/(float)atlas.width,bottom/(float)atlas.height,(right-left)/(float)atlas.width,(top-bottom)/(float)atlas.height));
            block.SetFloat("_Opacity",opacity);block.SetFloat("_Reveal",reveal);block.SetFloat("_Phase",clock);renderers[n].SetPropertyBlock(block);
            float fullWidth=width/Mathf.Max(.02f,framing.z);
            float height=fullWidth*(top-bottom)/(float)Mathf.Max(1,right-left);
            for(int y=0;y<=Rows;y++)for(int x=0;x<=Columns;x++)
            {
                float u=x/(float)Columns-framing.x,v=y/(float)Rows-framing.y;
                vertices[y*(Columns+1)+x]=SurfacePoint(u,v,fullWidth,height,clock);
            }
            meshes[n].vertices=vertices;meshes[n].RecalculateBounds();
        }
    }
    public Vector3 Point(Vector2 normalizedVisiblePoint)
    {
        if(disposed)return Vector3.zero;
        float u=(Mathf.Clamp01(normalizedVisiblePoint.x)-.5f)*framing.z;
        float v=(Mathf.Clamp01(normalizedVisiblePoint.y)-.5f)*framing.w;
        return root.transform.TransformPoint(SurfacePoint(u,v,pointWidth,pointHeight,pointClock));
    }
    private static Vector3 SurfacePoint(float u,float v,float width,float height,float clock)
    {
        float edge=Mathf.Abs(u)*2f;
        return new Vector3(u*width,v*height+Mathf.Sin(clock*2.7f+edge*3f)*edge*.10f,
            Mathf.Sin(clock*2.1f+u*5f+v*3f)*edge*.16f);
    }
    private static Vector4 Measure(Texture2D texture)
    {
        if(texture==null)return new Vector4(.5f,.5f,1f,1f);
        Vector4 cached;if(Framing.TryGetValue(texture,out cached))return cached;
        float minX=1f,minY=1f,maxX=0f,maxY=0f;
        for(int i=0;i<4;i++)
        {
            int left=Mathf.FloorToInt(i%2*texture.width/2f),right=Mathf.FloorToInt((i%2+1)*texture.width/2f);
            int bottom=Mathf.FloorToInt((1-i/2)*texture.height/2f),top=Mathf.FloorToInt((2-i/2)*texture.height/2f);
            var sprite=Sprite.Create(texture,new Rect(left,bottom,right-left,top-bottom),Vector2.zero,128f,0,SpriteMeshType.FullRect);
            Bounds bounds=PixelMantisReaper.VisibleBody(sprite);
            minX=Mathf.Min(minX,bounds.min.x*128f/(right-left));maxX=Mathf.Max(maxX,bounds.max.x*128f/(right-left));
            minY=Mathf.Min(minY,bounds.min.y*128f/(top-bottom));maxY=Mathf.Max(maxY,bounds.max.y*128f/(top-bottom));
            Destroy(sprite);
        }
        cached=new Vector4((minX+maxX)*.5f,(minY+maxY)*.5f,Mathf.Max(.02f,maxX-minX),Mathf.Max(.02f,maxY-minY));
        Framing.Add(texture,cached);return cached;
    }
    private Mesh Grid()
    {
        var mesh=new Mesh{name="Cygnus breathing feather mesh",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();
        var uv=new Vector2[vertices.Length];var triangles=new int[Columns*Rows*6];
        for(int y=0;y<=Rows;y++)for(int x=0;x<=Columns;x++)uv[y*(Columns+1)+x]=new Vector2(x/(float)Columns,y/(float)Rows);
        for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
        {
            int v=y*(Columns+1)+x,t=(y*Columns+x)*6;
            triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+Columns+1;
            triangles[t+3]=v+1;triangles[t+4]=v+Columns+2;triangles[t+5]=v+Columns+1;
        }
        mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;return mesh;
    }
    public void Hide(){for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=false;}
    public void Dispose()
    {if(disposed)return;disposed=true;for(int i=0;i<meshes.Length;i++)Destroy(meshes[i]);Destroy(material);Destroy(root);}
    private static void Destroy(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
