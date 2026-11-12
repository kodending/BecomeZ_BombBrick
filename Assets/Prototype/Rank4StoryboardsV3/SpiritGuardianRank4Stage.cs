using UnityEngine;

// One water gesture changes the viewpoint, wraps the actual party, then returns the same praying guardian.
public sealed class SpiritGuardianRank4Stage:SanctuarySkyV3ArtStage
{
    private const string ArtRoot="CombatCinematics/Rank4V3/spirit_deer/WaterMantleV02/";
    public const float FieldRevealTime=3.35f,FieldReturnTime=3.50f;
    public static readonly float[] BoardTimes={.28f,.80f,1.60f,2.50f,3.10f,3.495f};
    public override string Family=>"spirit_deer";
    public override float Duration=>FieldReturnTime;
    private readonly Sprite[] guardianFrames,sleeveFrames,waterFrames,hoofFrames;
    private readonly SpriteRenderer background,backgroundBlend,waterCrossing,guardian,sleeve,hoof,nativeGuardian;
    private readonly SpriteRenderer[] allies=new SpriteRenderer[8];
    private readonly MeshRenderer[] veils=new MeshRenderer[3];
    private readonly Mesh[] veilMeshes=new Mesh[3];
    private readonly Vector3[][] veilVertices=new Vector3[3][];
    private readonly Color[][] veilColors=new Color[3][];
    private readonly Material nativeVeil;
    private const int VeilSegments=64;
    private float filmTime;

    public SpiritGuardianRank4Stage(Transform parent):base(parent,"spirit_deer",Color.clear,
        new[]{"holydeer","lunadeer","forestdeer","eclipsedeer"},1280,720)
    {
        // Small droplets lose their viewport span at the legacy stage's 52,000-unit offset.
        Root.position=new Vector3(960f,320f,960f);
        guardianFrames=LoadAtlas(ArtRoot+"Guardian8",4,2);
        sleeveFrames=LoadAtlas(ArtRoot+"Sleeve4",2,2);
        waterFrames=LoadAtlas(ArtRoot+"WaterShots12",4,3);
        hoofFrames=LoadAtlas(ArtRoot+"Hoof20",4,5);
        background=MakeSprite("Water mantle authored sanctuary",-100);
        backgroundBlend=MakeSprite("Water mantle sanctuary frame blend",-99);
        waterCrossing=MakeSprite("Refracted water camera crossing",-98);
        guardian=MakeSprite("Water mantle guardian",20);
        sleeve=MakeSprite("Near shoulder and flowing sleeve",80);
        hoof=MakeSprite("Authored hoof contact",40);
        nativeGuardian=MakeSprite("Native praying guardian handoff",20);
        nativeVeil=OwnMaterial(Resources.Load<Shader>("CombatEffects/SpiritDeer/GuardianVeil"));
        nativeGuardian.sharedMaterial=nativeVeil;
        for(int i=0;i<allies.Length;i++)allies[i]=MakeSprite("Protected ally "+i,45+i);
        var cloth=OwnMaterial(Shader.Find("Sprites/Default"));
        for(int layer=0;layer<veils.Length;layer++)
        {
            var vertices=new Vector3[(VeilSegments+1)*2];
            var colors=new Color[vertices.Length];var triangles=new int[VeilSegments*6];
            for(int i=0;i<VeilSegments;i++)
            {
                int v=i*2,k=i*6;
                triangles[k]=v;triangles[k+1]=v+1;triangles[k+2]=v+2;
                triangles[k+3]=v+1;triangles[k+4]=v+3;triangles[k+5]=v+2;
            }
            veils[layer]=Geometry("Flowing translucent water veil "+layer,vertices,triangles,colors);
            veils[layer].sharedMaterial=cloth;veils[layer].sortingOrder=70+layer;
            veilMeshes[layer]=veils[layer].GetComponent<MeshFilter>().sharedMesh;veilMeshes[layer].MarkDynamic();
            veilVertices[layer]=vertices;veilColors[layer]=colors;veils[layer].enabled=false;
        }
    }

    private int SpeciesRow()
    {
        if(Context.source.shiny&&Context.source.species=="forestdeer")return 4;
        return Context.source.species=="lunadeer"?1:Context.source.species=="forestdeer"?2:Context.source.species=="eclipsedeer"?3:0;
    }

    private Color Light(float alpha=1f)=>Context.enemy?new Color(.83f,.78f,1f,alpha):new Color(1f,.88f,.56f,alpha);

    private void FixedBody(SpriteRenderer renderer,Sprite sprite,Sprite anchor,Vector2 center,float height,int order,Color color,float depth)
    {
        // Authored frames share a cell pivot; refitting each silhouette would cancel the hoof descent and pump the robe.
        Body(renderer,anchor,center,height,order,color,0f,depth);renderer.sprite=sprite;
    }

    private void Water(int row,float progress,float alpha=1f)
    {
        float frame=Mathf.Clamp01(progress)*3f;int index=Mathf.Min(3,Mathf.FloorToInt(frame));
        Color tint=new Color(.72f,.79f,.88f,alpha);
        Art(background,waterFrames[row*4+index],Vector2.one*.5f,new Vector2(1.025f,1.025f),-100,tint,24f);
        if(index<3&&frame-index>0f)
        {
            tint.a*=frame-index;
            Art(backgroundBlend,waterFrames[row*4+index+1],Vector2.one*.5f,new Vector2(1.025f,1.025f),-99,tint,24f);
        }
        float cut=filmTime<1.15f?.45f:filmTime<1.95f?1.15f:filmTime<2.85f?1.95f:2.85f;
        int previousRow=cut==.45f?1:cut==1.15f?0:cut==1.95f?1:2;
        float crossing=1f-Ease(cut,cut+.12f,filmTime);
        if(filmTime>=cut&&crossing>0f&&filmTime<FieldRevealTime)
            Art(waterCrossing,waterFrames[previousRow*4+3],Vector2.one*.5f,new Vector2(1.025f,1.025f),-98,
                new Color(.72f,.79f,.88f,crossing*alpha),24f);
    }

    private void Goddess(float pose,Vector2 center,float height,float alpha)
    {
        int index=Mathf.Clamp(Mathf.FloorToInt(pose+.25f),0,7);
        Color color=Context.enemy?new Color(.88f,.84f,1f,alpha):new Color(1f,1f,1f,alpha);
        FixedBody(guardian,guardianFrames[index],guardianFrames[0],center,height,20,color,16f);
    }

    private void Sleeve(float pose,Vector2 center,Vector2 size,float alpha)
    {
        int index=Mathf.Clamp(Mathf.FloorToInt(pose+.15f),0,3);
        Color color=Context.enemy?new Color(.88f,.84f,1f,alpha):new Color(1f,1f,1f,alpha);
        Art(sleeve,sleeveFrames[index],center,size,80,color,8f);
    }

    private void Ripple(Vector2 center,float age,float radius,float alpha,float depth=11f)
    {
        if(age<0f)return;
        for(int ring=0;ring<3;ring++)
        {
            float r=radius*(.34f+ring*.22f+Mathf.Clamp01(age)*.5f);
            for(int i=0;i<92;i++)
            {
                float angle=i/92f*Mathf.PI*2f;
                float wave=1f+Mathf.Sin(angle*7f-age*4f+ring)*.025f;
                Vector2 at=center+new Vector2(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r*.28f)*wave;
                Color color=Light(alpha*(.45f+Noise(i*17+ring)*.40f));
                Pixel(Point(at.x,at.y,depth),Span(.0024f+Noise(i*13)*.0017f,depth),color);
            }
        }
    }

    private void Droplets(float time,float alpha,bool underwater)
    {
        int count=underwater?64:88;
        for(int i=0;i<count;i++)
        {
            float fall=Mathf.Repeat(time*(underwater?.19f:.14f)+Noise(i*29),1f);
            float x=Noise(i*47+3)+Mathf.Sin(time*1.4f+i)*.014f;
            float y=underwater?1.14f-fall*1.35f:fall*1.15f-.10f;
            float depth=6f+Noise(i*71)*12f;
            Color color=Color.Lerp(new Color(.55f,.83f,1f),Light(),Noise(i*19));
            color.a=alpha*Mathf.Sin(fall*Mathf.PI)*(.25f+Noise(i*13)*.45f);
            float size=Span(.0018f+Noise(i*37)*.0026f,depth);
            Pixel(Point(x,y,depth),size,color,45f);
            if(i%5==0)Pixel(Point(x,y-.007f,depth),size*.6f,color);
        }
    }

    private void Veil(float time,float spread,float alpha,float centerY=.38f)
    {
        for(int layer=0;layer<veils.Length;layer++)
        {
            var vertices=veilVertices[layer];var colors=veilColors[layer];
            float reach=Mathf.Lerp(.18f,1.38f,spread);
            for(int i=0;i<=VeilSegments;i++)
            {
                float u=i/(float)VeilSegments;
                float x=-.15f+u*reach;
                float y=centerY-Mathf.Sin(u*Mathf.PI)*(.22f+layer*.025f)+layer*.105f+
                    Mathf.Sin(u*9f-time*2.7f+layer)*.016f;
                float width=(.018f+layer*.004f)*Mathf.Sin(u*Mathf.PI);
                vertices[i*2]=Point(x,y-width,9f+layer*.25f);
                vertices[i*2+1]=Point(x,y+width,9f+layer*.25f);
                Color edge=Light(alpha*Mathf.Sin(u*Mathf.PI)*.62f);
                colors[i*2]=edge;edge.a*=.10f;colors[i*2+1]=edge;
            }
            veilMeshes[layer].vertices=vertices;veilMeshes[layer].colors=colors;veilMeshes[layer].RecalculateBounds();
            veils[layer].enabled=alpha>.001f;
        }
    }

    private void ActualAllies()
    {
        int count=Mathf.Min(8,Context.allies.Length);
        for(int i=0;i<count;i++)
        {
            var actor=Context.allies[i];
            int column=count<=3?i:i%4,row=count<=3?0:i/4;
            float x=count<=3?.61f+i*.135f:.49f+column*.135f;
            float y=count<=3?.33f+i*.055f:.29f+row*.16f+column*.012f;
            float height=count<=3?.14f:.105f;
            Body(allies[i],actor.sprite,new Vector2(x,y),height,45+i,actor.color,0f,13f,actor.flipX);
            Ripple(new Vector2(x,y+height*.48f),.25f,.10f,.55f,13.2f);
        }
        Body(Lead,Pose(7),new Vector2(.46f,.42f),.14f,42,Context.source.color);
        Ripple(new Vector2(.46f,.49f),.4f,.10f,.6f);
    }

    private void DissolvingStatus(float time)
    {
        float dissolve=Ease(3.00f,3.28f,time);
        for(int i=0;i<52;i++)
        {
            float angle=i/52f*Mathf.PI*2f;
            Vector2 glyph=new Vector2(.77f,.34f)+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.055f;
            if(i%3==0)glyph+=Vector2.up*.012f;
            Vector2 outward=new Vector2(Mathf.Cos(angle*1.7f),-.6f-Mathf.Sin(angle));
            Vector2 point=glyph+outward*dissolve*.16f;
            Color color=Color.Lerp(new Color(.70f,.30f,.95f),Light(),dissolve);
            color.a=1f-Ease(.70f,1f,dissolve);
            Pixel(Point(point.x,point.y,10f),Span(.004f*(1f-dissolve*.55f),10f),color,45f);
        }
    }

    protected override void Animate(float time,float dt)
    {
        View.ResetProjectionMatrix();ActionTime=-1f;filmTime=time;
        SoundBeat=time<.18f?-1:time<1.15f?0:time<2.38f?1:2;
        Shot=time<.45f?0:time<1.15f?1:time<1.95f?2:time<2.85f?3:time<FieldRevealTime?4:5;
        Opacity=1f-Ease(FieldRevealTime,FieldReturnTime,time);
        foreach(var veil in veils)veil.enabled=false;
        View.backgroundColor=time<FieldRevealTime?new Color(.008f,.025f,.07f,1f):Color.clear;

        if(time<FieldRevealTime)
        {
            if(time<.45f)
            {
                CameraPose(new Vector3(0f,.9f,-3f),new Vector3(0f,0f,.5f),38f);
                Water(1,Ease(0f,.45f,time));
                int frame=Mathf.Min(3,Mathf.FloorToInt(time/.45f*4f));
                FixedBody(hoof,hoofFrames[SpeciesRow()*4+frame],hoofFrames[SpeciesRow()*4+2],new Vector2(.49f,.30f),1.03f,40,Color.white,10f);
                if(time>=.18f)Ripple(new Vector2(.49f,.80f),(time-.18f)/.27f,.23f,1f,9.8f);
                Droplets(time,Ease(.18f,.30f,time)*.5f,false);
            }
            else if(time<1.15f)
            {
                float rise=Ease(.45f,1.15f,time);
                CameraPose(Vector3.Lerp(new Vector3(-.1f,-3.8f,-2.2f),new Vector3(.12f,-2.9f,-1.9f),rise),
                    new Vector3(0f,2.6f,.3f),57f);
                Water(0,rise);
                Body(Lead,Pose(6),new Vector2(.5f,.24f),.15f,35,new Color(1f,.96f,.80f,.92f),0f,22f);
                Droplets(time,.9f,true);
                Ripple(new Vector2(.50f,.27f),rise,.30f,.65f,20f);
            }
            else if(time<1.95f)
            {
                float reveal=Ease(1.15f,1.95f,time);
                CameraPose(Vector3.Lerp(new Vector3(-.4f,-1.2f,-9f),new Vector3(.2f,-.6f,-10.8f),reveal),
                    new Vector3(0f,1.6f,0f),45f);
                Water(1,reveal);
                Goddess(reveal*4f,new Vector2(.58f,.48f+(1f-reveal)*.09f),.91f,Ease(1.15f,1.31f,time));
                Body(Lead,Pose(time<1.45f?4:time<1.70f?5:7),new Vector2(.15f,.82f),.23f,45,Context.source.color);
                Ripple(new Vector2(.57f,.94f),reveal,.31f,.78f);
                Droplets(time,.55f,false);
            }
            else if(time<2.85f)
            {
                float sweep=Ease(1.95f,2.57f,time);
                CameraPose(Vector3.Lerp(new Vector3(-3.8f,6.4f,-5.8f),new Vector3(-3.3f,6.0f,-5.5f),sweep),
                    new Vector3(.5f,0f,1.1f),48f);
                Water(2,Ease(1.95f,2.85f,time));
                ActualAllies();
                Sleeve(Mathf.Clamp01((time-1.95f)/.66f)*3f,new Vector2(.50f,.50f),new Vector2(1.02f,1f),1f);
                Veil(time,sweep,.72f);Droplets(time,.58f,false);
            }
            else
            {
                float close=Ease(2.85f,FieldRevealTime,time);
                CameraPose(new Vector3(.7f,.35f,-7.6f),new Vector3(.5f,.9f,1f),42f);
                Water(1,close);
                Goddess(4f+close*3f,new Vector2(-.22f,.16f),1.50f,.92f);
                Body(Lead,Pose(7),new Vector2(.22f,.78f),.19f,45,Context.source.color);
                if(Context.allies.Length>0)
                {
                    var actor=Context.allies[0];
                    Body(allies[0],actor.sprite,new Vector2(.61f,.67f),.43f,50,actor.color,0f,11f,actor.flipX);
                    Ripple(new Vector2(.61f,.87f),close,.23f,.80f);
                }
                DissolvingStatus(time);Veil(time,1f,.48f,.14f);Droplets(time,.50f,false);
            }
            return;
        }

        CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
        float curtain=1f-Ease(FieldRevealTime,FieldReturnTime,time);
        if(curtain>0f)Water(1,1f,curtain);
        PaintReturn(Lead,50);
        if(time<FieldReturnTime&&Context.guardianReturnVisible&&Context.guardianReturnSprite!=null)
        {
            nativeVeil.SetFloat("_RearEdge",Context.guardianReturnRearEdge);
            Paint(nativeGuardian,Context.guardianReturnSprite,Context.guardianReturnPosition,Context.guardianReturnHeight,
                Context.guardianReturnColor,20);
        }
        float tail=1f-Ease(FieldRevealTime,FieldReturnTime,time);
        Veil(time,1f,tail*.20f,.42f+Ease(FieldRevealTime,Duration,time)*.8f);
        Droplets(time,tail*.24f,false);
    }
}
