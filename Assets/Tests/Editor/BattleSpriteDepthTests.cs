using NUnit.Framework;
using UnityEngine;

public sealed class BattleSpriteDepthTests
{
    [Test] public void FeetDetermineOverlapEvenWhenSpritesHaveDifferentHeights()
    {
        var camera=new GameObject("Depth test camera").AddComponent<Camera>();
        var front=new GameObject("Tall front monster");
        var back=new GameObject("Short back monster");
        var texture=new Texture2D(4,4);
        texture.SetPixels(new Color[16]);
        for(int x=0;x<4;x++)for(int y=0;y<4;y++)texture.SetPixel(x,y,Color.white);
        texture.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,4,4),new Vector2(.5f,0f),1f);
        var image=new Texture2D(128,128,TextureFormat.RGB24,false);
        var rt=new RenderTexture(128,128,24);
        var active=RenderTexture.active;
        try
        {
            MonsterAutoBattlePrototype.FrameCameraToBattleArena(camera,new Bounds(Vector3.zero,Vector3.one*10f));
            camera.orthographic=true;camera.orthographicSize=5f;
            camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-8f),Quaternion.Euler(45f,0f,0f));
            camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var red=front.AddComponent<SpriteRenderer>();red.sprite=sprite;red.color=Color.red;
            var blue=back.AddComponent<SpriteRenderer>();blue.sprite=sprite;blue.color=Color.blue;
            var awake=typeof(SpriteMonsterBillboard).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            awake.Invoke(front.AddComponent<SpriteMonsterBillboard>(),null);
            awake.Invoke(back.AddComponent<SpriteMonsterBillboard>(),null);
            Assert.That(red.spriteSortPoint,Is.EqualTo(SpriteSortPoint.Pivot));
            front.layer=back.layer=31;
            front.transform.rotation=back.transform.rotation=camera.transform.rotation;
            back.transform.localScale=Vector3.one*.5f;
            foreach(bool reverse in new[]{false,true})
            {
                front.transform.position=new Vector3(0f,0f,reverse?.8f:0f);
                back.transform.position=new Vector3(0f,0f,reverse?0f:.4f);
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                Vector3 sample=camera.WorldToViewportPoint(blue.bounds.center);
                Color pixel=image.GetPixel((int)(sample.x*128),(int)(sample.y*128));
                Assert.That(reverse?pixel.b:pixel.r,Is.GreaterThan(.7f));
                Assert.That(reverse?pixel.r:pixel.b,Is.LessThan(.1f));
            }
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=active;rt.Release();
            Object.DestroyImmediate(front);Object.DestroyImmediate(back);Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(rt);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);Object.DestroyImmediate(image);
        }
    }
}
