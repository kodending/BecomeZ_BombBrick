using UnityEngine;

// Individual sprites let Unity sort each leaf in front of or behind the actors.
public sealed class MeadowAmbientParticles : MonoBehaviour
{
    private const int LeafCount=4,AmbientCount=10,ImpactCount=48;
    private sealed class Fleck
    {
        public SpriteRenderer renderer;
        public Vector3 position,velocity;
        public Color tint;
        public float age,lifetime,size,angle,spin,phase;
        public float swaySpeed,swayWidth,flutterSpeed;
        public bool dust;
    }
    private readonly Fleck[] flecks=new Fleck[AmbientCount+ImpactCount];
    // Cosmetic randomness must not consume the battle's random sequence.
    private readonly System.Random random=new System.Random(7319);
    private Camera view;
    private float ambientDepth;
    private Sprite leaf,menuLeaf,mote,dust;
    private readonly Sprite[] leafFrames=new Sprite[4];
    private readonly Texture2D[] leafTextures=new Texture2D[4];
    private Texture2D menuLeafTexture,moteTexture,dustTexture;
    private float clock;
    private int nextImpact;
    private string regionId;
    private static MeadowAmbientParticles current;
    public string RegionId=>regionId;
    private Color ambientLow=new Color(.25f,.46f,.13f,.8f);
    private Color ambientHigh=new Color(.69f,.72f,.2f,.85f);
    private Color pollenTint=new Color(1f,.91f,.65f,1f);
    private Color groundTint=new Color(1f,.95f,.8f,.9f);
    private Color dustTint=new Color(.72f,.64f,.46f,1f);

    // Only callers emitting terrain use this. Spell, feather, horn and summoned-wall colours stay authored.
    public static Color TerrainColor(Color original)
    {
        if(current==null||current.regionId=="prototype_region")return original;
        Color low,high;
        switch(current.regionId)
        {
            case "poison_fog_swamp":low=new Color(.16f,.22f,.10f);high=new Color(.54f,.63f,.29f);break;
            case "crimson_jungle":low=new Color(.22f,.12f,.16f);high=new Color(.61f,.34f,.27f);break;
            case "frozen_highlands":low=new Color(.25f,.48f,.71f);high=new Color(.89f,.96f,1f);break;
            case "sky_sanctuary":low=new Color(.49f,.44f,.32f);high=new Color(.96f,.88f,.62f);break;
            default:return original;
        }
        Color color=Color.Lerp(low,high,Mathf.InverseLerp(.15f,1f,Mathf.Max(original.r,Mathf.Max(original.g,original.b))));
        color.a=original.a;
        return color;
    }

    public static MeadowAmbientParticles Create(Transform parent,Camera camera,Bounds bounds,
        string regionId="prototype_region")
    {
        var effect=new GameObject("Meadow Ambient Particles").AddComponent<MeadowAmbientParticles>();
        effect.transform.SetParent(parent,false);effect.view=camera;
        effect.regionId=regionId;
        current=effect;
        switch(regionId)
        {
            case "poison_fog_swamp":
                effect.ambientLow=new Color(.32f,.48f,.24f,.7f);
                effect.ambientHigh=new Color(.63f,.76f,.35f,.8f);
                effect.pollenTint=new Color(.68f,.87f,.4f,1f);
                effect.groundTint=new Color(.52f,.59f,.28f,.9f);
                effect.dustTint=new Color(.4f,.46f,.26f,1f);break;
            case "crimson_jungle":
                effect.ambientLow=new Color(.55f,.3f,.26f,.65f);
                effect.ambientHigh=new Color(.94f,.51f,.24f,.8f);
                effect.pollenTint=new Color(1f,.55f,.26f,1f);
                effect.groundTint=new Color(.59f,.35f,.32f,.9f);
                effect.dustTint=new Color(.45f,.31f,.3f,1f);break;
            case "frozen_highlands":
                effect.ambientLow=new Color(.64f,.78f,1f,.65f);
                effect.ambientHigh=new Color(.85f,.93f,1f,.8f);
                effect.pollenTint=new Color(.76f,.89f,1f,1f);
                effect.groundTint=new Color(.69f,.83f,1f,.9f);
                effect.dustTint=new Color(.78f,.86f,1f,1f);break;
            case "sky_sanctuary":
                effect.ambientLow=new Color(.85f,.76f,.5f,.65f);
                effect.ambientHigh=new Color(1f,.94f,.73f,.8f);
                effect.pollenTint=new Color(1f,.93f,.7f,1f);
                effect.groundTint=new Color(.86f,.79f,.58f,.9f);
                effect.dustTint=new Color(.84f,.8f,.66f,1f);break;
        }
        effect.ambientDepth=camera==null?20f:Mathf.Max(camera.nearClipPlane+8f,camera.transform.InverseTransformPoint(bounds.center).z);
        string[][] shapes={
            new[]{"...o.","..oho",".omho",".omo.","..o.."},
            new[]{".....","..ooo",".omho","omho.",".oo.."},
            new[]{".....","oooo.","omhmo",".ooo.","....."},
            new[]{"..o..",".omo.",".ohmo",".oho.","..o.."}};
        switch(regionId)
        {
            case "poison_fog_swamp":shapes=new[]{
                new[]{".....",".omo.",".mhmo",".omo.","....."},
                new[]{"..o..","..m..",".mh..",".mo..",".o..."},
                new[]{".....","..om.",".mhmo",".moo.","....."},
                new[]{".....","...om","..mho",".omo.","....."}};break;
            case "crimson_jungle":shapes=new[]{
                new[]{".....",".omo.","omhoo",".moo.","..o.."},
                new[]{"..o..",".omo.","omh..",".mo..","....."},
                new[]{".....",".oom.",".mhmo","..oo.","....."},
                new[]{".....","..omo",".mhmo",".oo..","....."}};break;
            case "frozen_highlands":shapes=new[]{
                new[]{"..h..",".hm..","hmmo.",".mo..","..o.."},
                new[]{".....","...h.",".hhm.",".mmo.",".o..."},
                new[]{".....",".hhh.","mmmo.",".oo..","....."},
                new[]{"..h..",".hmm.",".mmo.","..o..","....."}};break;
            case "sky_sanctuary":shapes=new[]{
                new[]{".....",".hhh.",".hmm.",".moo.","....."},
                new[]{".....","..hh.",".hmm.","..oo.","....."},
                new[]{".....",".hhh.",".mmo.","..oo.","....."},
                new[]{".....",".hh..",".hmm.",".oo..","....."}};break;
        }
        for(int i=0;i<shapes.Length;i++)effect.leafFrames[i]=effect.MakeSprite(regionId+" Ground Fleck "+i,shapes[i],out effect.leafTextures[i]);
        effect.leaf=effect.leafFrames[0];
        // Same three square blocks and alternating lean as GameFrontend.DrawMenuLeaves.
        string[] ambientShape=regionId=="poison_fog_swamp"?new[]{".w.","w.w",".w."}:
            regionId=="crimson_jungle"?new[]{"w.",".w"}:
            regionId=="frozen_highlands"?new[]{".w.","www",".w."}:
            regionId=="sky_sanctuary"?new[]{".w.",".w.","..."}:new[]{".w.",".ww"};
        effect.menuLeaf=effect.MakeSprite(regionId+" Ambient Fleck",ambientShape,out effect.menuLeafTexture);
        effect.mote=effect.MakeSprite("Ambient Mote",new[]{regionId=="prototype_region"?"h":"w"},out effect.moteTexture);
        effect.dust=effect.MakeSprite("Meadow Footstep Dust",new[]{"...xx....","..xxxx...",".xxxxxxx.","xxxxxxxxx","xxxxxxxx.",".xxxxxx..","...xx...."},out effect.dustTexture);
        for(int i=0;i<effect.flecks.Length;i++)
        {
            var renderer=new GameObject(i<LeafCount?"Drifting Leaf":i<AmbientCount?"Floating Pollen":"Impact Grass").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(effect.transform,false);
            renderer.sprite=i<LeafCount||i>=AmbientCount?effect.leaf:effect.mote;
            renderer.sortingOrder=10;renderer.enabled=false;
            var fleck=new Fleck{renderer=renderer};effect.flecks[i]=fleck;
            if(i<AmbientCount)
            {
                effect.ResetAmbient(fleck,i<LeafCount);
                // Start across the whole view, including sky, tree edges and foreground.
                fleck.position.x=(i-LeafCount+effect.Range(.2f,.8f))/(AmbientCount-LeafCount);
                fleck.position.y=Mathf.Repeat(i*.618034f+.13f,1f);
                // Interleave leaves across both screen halves, independently of the pollen.
                if(i<LeafCount)fleck.position.x=(i+effect.Range(.2f,.8f))/LeafCount;
                fleck.age=effect.Range(2f,5f);
                effect.Draw(fleck,i,true);
            }
        }
        return effect;
    }

    public void Impact(Vector3 center,float radius,SpriteRenderer reference)
    {
        if(!isActiveAndEnabled||view==null||radius<=0f)return;
        int count=Mathf.Clamp(Mathf.RoundToInt(radius*3f),8,18);
        for(int i=0;i<count;i++)
        {
            Fleck fleck=flecks[AmbientCount+nextImpact];
            nextImpact=(nextImpact+1)%ImpactCount;
            fleck.dust=false;fleck.renderer.sprite=leaf;fleck.renderer.name="Impact Grass";
            float angle=(i+Range(.1f,.9f))*Mathf.PI*2f/count;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            fleck.position=center+direction*radius*Range(.2f,.8f);
            fleck.position.y=.08f;
            fleck.velocity=direction*Range(1.2f,2.5f)*Mathf.Clamp(radius*.4f,.8f,1.8f)+Vector3.up*Range(1.8f,3.2f);
            fleck.age=0f;fleck.lifetime=Range(.8f,1.25f);fleck.size=Range(.22f,.38f);
            fleck.angle=Range(0f,360f);fleck.spin=Range(-280f,280f);fleck.phase=Range(0f,6.28f);
            fleck.tint=groundTint;
            fleck.renderer.sortingLayerID=reference==null?0:reference.sortingLayerID;
            fleck.renderer.sortingOrder=reference==null?10:reference.sortingOrder;
            Draw(fleck,AmbientCount,false);
        }
    }

    public void Footstep(Vector3 ground,Vector3 direction,float footRadius,SpriteRenderer reference)
    {
        if(!isActiveAndEnabled||view==null||direction.sqrMagnitude<.0001f)return;
        direction.y=0f;direction.Normalize();
        Vector3 side=new Vector3(-direction.z,0f,direction.x);
        int emitted=0;
        // Walking only uses free slots, so it cannot erase an active landing burst.
        for(int n=0;n<ImpactCount&&emitted<2;n++)
        {
            Fleck fleck=flecks[AmbientCount+nextImpact];nextImpact=(nextImpact+1)%ImpactCount;
            if(fleck.lifetime>0f)continue;
            fleck.dust=true;fleck.renderer.sprite=dust;fleck.renderer.name="Footstep Dust";
            fleck.position=ground-direction*Mathf.Clamp(footRadius*.9f,.16f,.65f)+side*Range(-.12f,.12f);
            fleck.position.y=.06f;
            fleck.velocity=-direction*Range(.25f,.5f)+side*Range(-.15f,.15f)+Vector3.up*Range(.25f,.4f);
            fleck.age=0f;fleck.lifetime=Range(.38f,.52f);
            fleck.size=Range(.26f,.38f)*Mathf.Clamp(footRadius/.5f,.8f,1.4f);
            fleck.angle=Range(-25f,25f);fleck.spin=Range(-20f,20f);
            fleck.tint=dustTint;fleck.tint.a=Range(.42f,.58f);
            fleck.renderer.sortingLayerID=reference==null?0:reference.sortingLayerID;
            fleck.renderer.sortingOrder=reference==null?10:reference.sortingOrder;
            Draw(fleck,AmbientCount,false);emitted++;
        }
    }

    public void ClearImpacts()
    {
        for(int i=AmbientCount;i<flecks.Length;i++)
            if(flecks[i]!=null){flecks[i].lifetime=0f;flecks[i].renderer.enabled=false;}
    }

    private void ResetAmbient(Fleck fleck,bool isLeaf)
    {
        bool fromSide=Range(0f,1f)<.5f;
        fleck.velocity=new Vector3(isLeaf?Range(-.025f,.035f):Range(.018f,.034f),isLeaf?Range(-.065f,-.035f):Range(-.025f,-.012f),0f);
        if(regionId=="poison_fog_swamp")fleck.velocity=new Vector3(Range(-.012f,.018f),Range(.012f,.025f),0f);
        else if(regionId=="crimson_jungle")fleck.velocity=new Vector3(Range(.012f,.028f),Range(.02f,.042f),0f);
        else if(regionId=="frozen_highlands")fleck.velocity=new Vector3(Range(.012f,.035f),Range(-.06f,-.035f),0f);
        else if(regionId=="sky_sanctuary")fleck.velocity=new Vector3(Range(.025f,.048f),Range(-.012f,.006f),0f);
        fleck.position=new Vector3(fromSide?(fleck.velocity.x<0f?1.04f:-.04f):Range(.05f,.95f),fromSide?Range(.05f,1f):1.04f,ambientDepth+Range(-3f,3f));
        if(!fromSide&&fleck.velocity.y>0f)fleck.position.y=-.04f;
        fleck.age=0f;fleck.lifetime=Range(18f,26f);
        fleck.size=1f;fleck.angle=0f;fleck.spin=0f;fleck.phase=Range(0f,6.28f);
        fleck.swaySpeed=Range(2.4f,3.8f);fleck.swayWidth=isLeaf?Range(.009f,.024f):.008f;
        if(regionId!="prototype_region")fleck.swaySpeed*=.5f;
        fleck.flutterSpeed=Range(4f,6f);
        fleck.tint=isLeaf?Color.Lerp(ambientLow,ambientHigh,Range(0f,1f)):pollenTint;
        if(!isLeaf)fleck.tint.a=Range(.5f,.8f);
    }

    private void Update()
    {
        if(view==null)return;
        float dt=Time.deltaTime;clock+=dt;
        for(int i=0;i<flecks.Length;i++)
        {
            Fleck fleck=flecks[i];
            if(fleck==null||fleck.lifetime<=0f)continue;
            bool ambient=i<AmbientCount;
            fleck.age+=dt;
            if(fleck.age>=fleck.lifetime||ambient&&
               (fleck.position.x>1.06f||fleck.position.x<-.06f||fleck.position.y<-.06f||fleck.position.y>1.06f))
            {
                if(ambient)ResetAmbient(fleck,i<LeafCount);
                else{fleck.lifetime=0f;fleck.renderer.enabled=false;continue;}
            }
            if(ambient)
            {
                float gust=1f+Mathf.Pow(Mathf.Max(0f,Mathf.Sin(clock*.65f+fleck.phase)),4f)*.9f;
                fleck.position+=fleck.velocity*(dt*gust);
            }
            else if(fleck.dust)
            {
                fleck.position+=fleck.velocity*dt;
                fleck.velocity*=Mathf.Exp(-3f*dt);
            }
            else
            {
                fleck.velocity.y-=(regionId=="poison_fog_swamp"?7.5f:
                    regionId=="crimson_jungle"||regionId=="sky_sanctuary"?7f:5.5f)*dt;
                fleck.position+=fleck.velocity*dt;
                if(fleck.position.y<.03f)
                {
                    fleck.position.y=.03f;fleck.velocity.y=0f;
                    fleck.velocity*=Mathf.Exp(-8f*dt);
                }
            }
            fleck.angle+=fleck.spin*dt;
            Draw(fleck,i,ambient);
        }
    }

    private void Draw(Fleck fleck,int index,bool ambient)
    {
        var renderer=fleck.renderer;renderer.enabled=true;
        Vector3 position=fleck.position;
        if(ambient)
        {
            position.x+=Mathf.Sin(fleck.age*fleck.swaySpeed+fleck.phase)*fleck.swayWidth;
            position.y+=Mathf.Sin(fleck.age*1.1f+fleck.phase)*.004f;
            DrawPixelFleck(fleck,position,index<LeafCount,true);
            return;
        }
        if(!fleck.dust)
        {
            if(view!=null)DrawPixelFleck(fleck,view.WorldToViewportPoint(position),true,false);
            return;
        }
        renderer.transform.position=position;
        renderer.transform.rotation=(view==null?Quaternion.identity:view.transform.rotation)*Quaternion.Euler(0f,0f,fleck.angle);
        float size=fleck.size*Mathf.Lerp(.65f,1.65f,fleck.age/fleck.lifetime);
        renderer.transform.localScale=new Vector3(size,size,1f);
        Color color=fleck.tint;
        color.a*=1f-fleck.age/fleck.lifetime;
        renderer.color=color;
    }

    private void DrawPixelFleck(Fleck fleck,Vector3 viewport,bool isLeaf,bool ambient)
    {
        if(view==null)return;
        var renderer=fleck.renderer;
        int frame=Mathf.FloorToInt(clock*5f+fleck.phase)%leafFrames.Length;
        bool menuStyle=ambient&&isLeaf;
        renderer.sprite=menuStyle?menuLeaf:isLeaf?leafFrames[frame]:mote;
        renderer.flipX=menuStyle&&Mathf.Sin(fleck.age*fleck.flutterSpeed+fleck.phase)<0f;
        // A fixed 270-row pixel grid keeps every texel square, without fractional rotation or scaling.
        float width=Mathf.Max(1f,Mathf.Round(270f*view.aspect));
        float pixelOffset=menuStyle?0f:.5f;
        viewport.x=(Mathf.Floor(viewport.x*width)+pixelOffset)/width;
        viewport.y=(Mathf.Floor(viewport.y*270f)+pixelOffset)/270f;
        renderer.transform.position=view.ViewportToWorldPoint(viewport);
        renderer.transform.rotation=view.transform.rotation;
        float viewHeight=view.orthographic?view.orthographicSize*2f:2f*viewport.z*Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad);
        renderer.transform.localScale=Vector3.one*viewHeight/270f*
            (menuStyle?(regionId=="prototype_region"?6f:3f):isLeaf?5f:1f);
        Color tint=fleck.tint;
        float fade=Mathf.Clamp01((fleck.lifetime-fleck.age)/(ambient?1f:.35f));
        if(ambient)fade*=Mathf.Clamp01(fleck.age);
        tint.a*=Mathf.Ceil(fade*4f)/4f;
        renderer.color=tint;
    }

    private Sprite MakeSprite(string name,string[] rows,out Texture2D texture)
    {
        int width=rows[0].Length,height=rows.Length;
        texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {name=name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            char pixel=rows[height-1-y][x];
            if(pixel=='w')pixels[y*width+x]=new Color32(255,255,255,255);
            else if(pixel=='o')pixels[y*width+x]=new Color32(62,83,32,255);
            else if(pixel=='m')pixels[y*width+x]=new Color32(137,164,65,255);
            else if(pixel=='h')pixels[y*width+x]=new Color32(226,218,121,255);
            else if(pixel=='x')pixels[y*width+x]=x>y?new Color32(255,255,255,255):new Color32(194,211,157,255);
            if(regionId!="prototype_region"&&pixel!='.')
            {
                byte shade=pixel=='o'?(byte)95:pixel=='m'?(byte)180:(byte)255;
                pixels[y*width+x]=new Color32(shade,shade,shade,255);
            }
        }
        texture.SetPixels32(pixels);texture.Apply(false,true);
        return Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),width,0,SpriteMeshType.FullRect);
    }

    private float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
    private void OnEnable(){if(regionId!=null)current=this;}
    private void OnDisable()
    {
        if(current==this)current=null;
        ClearImpacts();
        for(int i=0;i<AmbientCount;i++)if(flecks[i]!=null)flecks[i].renderer.enabled=false;
    }
    private void OnDestroy()
    {
        if(current==this)current=null;
        for(int i=0;i<leafFrames.Length;i++){Release(leafFrames[i]);Release(leafTextures[i]);}
        Release(menuLeaf);Release(menuLeafTexture);Release(mote);Release(dust);Release(moteTexture);Release(dustTexture);
    }
    private static void Release(Object value)
    {
        if(value==null)return;
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }
}
