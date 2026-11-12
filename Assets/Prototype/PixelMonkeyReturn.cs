using System;
using UnityEngine;

public sealed class PixelMonkeyReturn:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private readonly SpriteRenderer[,] fruit=new SpriteRenderer[5,3];
    private readonly Vector3[] directions=new Vector3[5];
    private readonly Vector3[] origins=new Vector3[5];
    private readonly bool[] launched=new bool[5];
    private readonly bool[] deflected=new bool[5];
    private static Sprite bananaSprite;
    private Func<bool> valid;
    private Func<Vector3> catchPosition;
    private Func<Vector3> launchPosition;
    private float laneHeight;
    private Action<int> onLaunch;
    private Action<int,Vector3> onReturn;
    private Action<int,bool,Vector3,Vector3> onSweep;
    private Vector3 origin;
    private Quaternion billboard;
    private Color color;
    private float age;
    private bool cancelled;
    private CombatProjectileSource.Flight flight;
    private float previousClock;
    public bool Complete { get; private set; }

    public static PixelMonkeyReturn Play(Vector3 origin,Vector3 direction,SpriteRenderer sorting,Color color,
        Func<bool> valid,Func<Vector3> catchPosition,Action<int> launch,Action<int,bool,Vector3,Vector3> sweep,
        Func<Vector3> launchPosition=null,float laneHeight=float.NaN,Action<int,Vector3> returning=null)
    {
        var fx=new GameObject("Monkey Banana Boomerangs").AddComponent<PixelMonkeyReturn>();
        fx.origin=origin;fx.color=color;fx.valid=valid;fx.catchPosition=catchPosition;
        fx.onLaunch=launch;fx.onSweep=sweep;
        fx.onReturn=returning;
        fx.flight=CombatProjectileSource.Capture(sorting==null?null:sorting.transform);
        fx.previousClock=fx.flight.clock==null?0:fx.flight.clock();
        fx.launchPosition=launchPosition;fx.laneHeight=laneHeight;
        var camera=Camera.main;fx.billboard=camera==null?Quaternion.identity:camera.transform.rotation;
        direction.y=0f;if(direction.sqrMagnitude<.001f)direction=Vector3.right;direction.Normalize();
        int layer=sorting==null?0:sorting.sortingLayerID,order=(sorting==null?0:sorting.sortingOrder)+30;
        if(bananaSprite==null)bananaSprite=CreateBanana();
        for(int i=0;i<5;i++)
        {
            fx.origins[i]=origin;
            fx.directions[i]=Quaternion.AngleAxis(UnityEngine.Random.Range(-30f,30f),Vector3.up)*direction;
            for(int j=0;j<3;j++)
            {
                var renderer=new GameObject("Banana "+i+" Trail "+j).AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(fx.transform,false);renderer.sprite=bananaSprite;renderer.enabled=false;
                renderer.sortingLayerID=layer;renderer.sortingOrder=order-j;fx.fruit[i,j]=renderer;
            }
        }
        return fx;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(flight.valid!=null&&!flight.valid()){Cancel();return;}
            float dt=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(flight.clock!=null){float now=flight.clock();dt=Mathf.Max(0,now-previousClock);previousClock=now;}
            Advance(dt);
        }
    }

    private void Advance(float deltaTime)
    {
        if(cancelled||Complete)return;
        if(valid!=null&&!valid()){Cancel();return;}
        float end=age+Mathf.Max(0f,deltaTime);
        // Follow the curved path in short swept segments, including when a frame crosses the turn.
        while(age<end&&!Complete)
        {
            if(valid!=null&&!valid()){Cancel();return;}
            float previousAge=age;age=Mathf.Min(end,age+1f/60f);
            Vector3 catcher=catchPosition();
            for(int i=0;i<5;i++)
            {
                float previous=previousAge-i*MonkeyRank3Rules.ReturnThrowInterval;
                float local=age-i*MonkeyRank3Rules.ReturnThrowInterval;
                float turn=MonkeyRank3Rules.BoomerangOutDuration,total=turn+MonkeyRank3Rules.ReturnFlightDuration;
                if(local<0f||previous>=total||deflected[i])continue;
                if(!launched[i])
                {
                    launched[i]=true;origins[i]=launchPosition==null?origin:launchPosition();
                    onLaunch?.Invoke(i);
                }
                if(previous<turn)
                    Sweep(i,false,Point(i,Mathf.Max(0f,previous),catcher),Point(i,Mathf.Min(local,turn),catcher));
                if(!deflected[i]&&previous<=turn&&local>turn)
                    onReturn?.Invoke(i,Point(i,turn,catcher));
                if(local>turn)
                    Sweep(i,true,Point(i,Mathf.Max(previous,turn),catcher),Point(i,Mathf.Min(local,total),catcher));
                Draw(i,local,catcher);
            }
            if(age>=4f*MonkeyRank3Rules.ReturnThrowInterval+MonkeyRank3Rules.BoomerangOutDuration+MonkeyRank3Rules.ReturnFlightDuration)
            {Complete=true;onLaunch=null;onSweep=null;onReturn=null;}
        }
    }

    private void Sweep(int index,bool returning,Vector3 start,Vector3 end)
    {
        if(deflected[index])return;
        if(flight.deflect!=null&&flight.deflect(start,end,fruit[index,0]))
        {
            deflected[index]=true;
            for(int j=0;j<3;j++)fruit[index,j].enabled=false;
            return;
        }
        onSweep?.Invoke(index,returning,start,end);
    }

    private Vector3 Point(int index,float local,Vector3 catcher)
    {
        float turn=MonkeyRank3Rules.BoomerangOutDuration;
        float back=Mathf.Clamp01((local-turn)/MonkeyRank3Rules.ReturnFlightDuration);
        float t=local<=turn?.5f*Mathf.Clamp01(local/turn):.5f+.5f*back;
        Vector3 direction=directions[index],side=new Vector3(-direction.z,0f,direction.x);
        float arc=Mathf.Sin(t*Mathf.PI);
        Vector3 start=origins[index];
        Vector3 point=start+direction*(MonsterAutoBattlePrototype.TileSize*MonkeyRank3Rules.BoomerangRangeTiles*arc)+
            side*(MonsterAutoBattlePrototype.TileSize*MonkeyRank3Rules.BoomerangCurveTiles*Mathf.Sin(t*Mathf.PI*2f)*arc)+
            (catcher-start)*Mathf.SmoothStep(0f,1f,back);
        if(!float.IsNaN(laneHeight))
        {
            float descend=Mathf.SmoothStep(0f,1f,t/.28f);
            float rise=Mathf.SmoothStep(0f,1f,(t-.7f)/.3f);
            point.y=Mathf.Lerp(Mathf.Lerp(start.y,laneHeight,descend),catcher.y,rise);
        }
        return point;
    }

    private void Draw(int index,float local,Vector3 catcher)
    {
        if(deflected[index])return;
        float turn=MonkeyRank3Rules.BoomerangOutDuration,total=turn+MonkeyRank3Rules.ReturnFlightDuration;
        float rotation=local*1050f+Mathf.Max(0f,local-turn)*850f;
        float size=(index==4?.88f:.7f)*MonkeyCombatRules.SkillProjectileVisualMultiplier;
        float fade=Mathf.Clamp01((total-local)/.055f);
        for(int j=0;j<3;j++)
        {
            var renderer=fruit[index,j];renderer.enabled=local<total;
            if(!renderer.enabled)continue;
            renderer.transform.position=Point(index,Mathf.Max(0f,local-j*.014f),catcher);
            // Stepped sprite rotation keeps the silhouette crisp and animation-like.
            renderer.transform.rotation=billboard*Quaternion.Euler(0f,0f,Mathf.Round((rotation-j*18f)/30f)*30f);
            renderer.transform.localScale=Vector3.one*size*(1f-j*.12f);
            Color tint=j==0?Color.white:Color.Lerp(Color.white,color,.45f);
            tint.a=fade*(j==0?1f:.22f/j);renderer.color=tint;
        }
    }

    private static Sprite CreateBanana()
    {
        string[] rows={
            "........................","..DD....................","..DGBD..................",
            "...DYYD.................","...DYHYD................","...DYHHYD...............",
            "...DYHHHYD...........DD.","...DYHHHHYDD........DYBD",
            "...DMYHHHHYYDDD...DDYHYD","....DMYHHHHHYYYDDDHHHYD.",
            "....DMYYHHHHHHHHHHHHYD..",".....DMYYYHHHHHHHHYYD...",
            "......DMMYYYYYYYYYYD....",".......DMMMYYYYYMMD.....",
            "........DDMMMMMMDD......","..........DDDDDD........"};
        const int width=24;int height=rows.Length;
        var pixels=new Color32[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<rows[y].Length;x++)
        {
            char c=rows[y][x];
            pixels[(height-1-y)*width+x]=c=='D'?new Color32(83,51,27,255):
                c=='Y'?new Color32(255,210,55,255):c=='H'?new Color32(255,244,145,255):
                c=='M'?new Color32(201,136,32,255):c=='G'?new Color32(127,153,53,255):
                c=='B'?new Color32(133,80,33,255):new Color32(0,0,0,0);
        }
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {name="Pixel Banana Boomerang",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        var sprite=Sprite.Create(texture,new Rect(0,0,width,height),Vector2.one*.5f,16f);
        sprite.name=texture.name;sprite.hideFlags=HideFlags.HideAndDontSave;return sprite;
    }

    public void Cancel()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(this==null)return;
            if(CombatEffectEndFade.IsFading(this))return;
            if(cancelled)return;
            cancelled=true;flight.finished?.Invoke();onLaunch=null;onSweep=null;onReturn=null;
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
        }
    }
    private void OnDestroy()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            flight.finished?.Invoke();
        }
    }
}
