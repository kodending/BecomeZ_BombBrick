using System.Collections.Generic;
using UnityEngine;

// Keeps only presentation alive while battle-owned callbacks and timers are cleared.
[DisallowMultipleComponent]
[DefaultExecutionOrder(10100)]
public sealed class CombatEffectEndFade : MonoBehaviour
{
    public const float Duration=.45f;
    private readonly List<MonoBehaviour> disabled=new List<MonoBehaviour>();
    private readonly Dictionary<SpriteRenderer,Color> sprites=new Dictionary<SpriteRenderer,Color>();
    private readonly List<LineFade> lines=new List<LineFade>();
    private readonly List<MeshFade> meshes=new List<MeshFade>();
    private readonly List<ParticleFade> particles=new List<ParticleFade>();
    private float elapsed,previousAlpha=1f;
    private bool finished;

    public static bool IsFading(Component component)
    {
        if(component==null)return false;
        if(CombatEffectEndPlayback.IsPreserving(component))return true;
        var fade=component.GetComponentInParent<CombatEffectEndFade>(true);
        return fade!=null&&!fade.finished;
    }

    public static void Begin(GameObject root)
    {
        if(root==null||!root.activeInHierarchy||IsFading(root.transform))return;
        var fade=root.AddComponent<CombatEffectEndFade>();
        root.transform.SetParent(null,true);
        foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(behaviour==null||behaviour is CombatEffectEndFade||!behaviour.enabled||
               behaviour.GetComponentInParent<CombatEffectEndFade>(true)!=fade)continue;
            fade.disabled.Add(behaviour);
            behaviour.enabled=false;
        }
        foreach(var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if(!renderer.enabled||renderer.forceRenderingOff||
               renderer.GetComponentInParent<CombatEffectEndFade>()!=fade)continue;
            if(renderer is SpriteRenderer sprite)fade.sprites.Add(sprite,sprite.color);
            else if(renderer is LineRenderer line)fade.lines.Add(new LineFade(line));
            else if(renderer is MeshRenderer mesh)fade.meshes.Add(new MeshFade(mesh));
        }
        foreach(var system in root.GetComponentsInChildren<ParticleSystem>())
        {
            if(system.GetComponentInParent<CombatEffectEndFade>()!=fade)continue;
            fade.particles.Add(new ParticleFade(system));
        }
    }

    private void LateUpdate()=>Advance(Time.unscaledDeltaTime);

    private void Advance(float deltaTime)
    {
        if(finished)return;
        elapsed+=Mathf.Max(0f,deltaTime);
        float alpha=1f-Mathf.SmoothStep(0f,1f,elapsed/Duration);
        foreach(var entry in sprites)
        {
            if(entry.Key==null)continue;
            Color color=entry.Value;color.a*=alpha;entry.Key.color=color;
        }
        foreach(var line in lines)line.Apply(alpha);
        foreach(var mesh in meshes)mesh.Apply(alpha);
        foreach(var particle in particles)particle.Apply(previousAlpha>0f?alpha/previousAlpha:0f);
        previousAlpha=alpha;
        if(elapsed<Duration)return;

        var pooled=GetComponent<PixelCombatEffect>();
        if(pooled!=null)
        {
            foreach(var entry in sprites)if(entry.Key!=null)entry.Key.color=entry.Value;
            foreach(var line in lines)line.Restore();
            foreach(var mesh in meshes)mesh.Restore();
            foreach(var particle in particles)particle.Restore();
            foreach(var behaviour in disabled)if(behaviour!=null)behaviour.enabled=true;
            finished=true;
            pooled.ReleaseAfterBattleEndFade();
            Remove(this);
        }
        else
        {
            finished=true;
            Remove(gameObject);
        }
    }

    private void OnDestroy()
    {
        foreach(var mesh in meshes)mesh.Dispose();
    }

    private static void Remove(Object value)
    {
        if(value==null)return;
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }

    private sealed class LineFade
    {
        private readonly LineRenderer renderer;
        private readonly Gradient original,working=new Gradient();
        private readonly GradientAlphaKey[] alphaKeys,originalAlpha;
        private readonly GradientColorKey[] colorKeys;

        public LineFade(LineRenderer renderer)
        {
            this.renderer=renderer;original=renderer.colorGradient;
            originalAlpha=original.alphaKeys;alphaKeys=original.alphaKeys;colorKeys=original.colorKeys;
            working.mode=original.mode;
        }
        public void Apply(float alpha)
        {
            if(renderer==null)return;
            for(int i=0;i<alphaKeys.Length;i++)alphaKeys[i].alpha=originalAlpha[i].alpha*alpha;
            working.SetKeys(colorKeys,alphaKeys);renderer.colorGradient=working;
        }
        public void Restore(){if(renderer!=null)renderer.colorGradient=original;}
    }

    private sealed class MeshFade
    {
        private static readonly int FadeProperty=Shader.PropertyToID("_BattleEndFade");
        private static readonly int ColorProperty=Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty=Shader.PropertyToID("_BaseColor");
        private readonly MeshRenderer renderer;
        private readonly MeshFilter filter;
        private readonly Mesh original,copy;
        private readonly Color32[] originalColors,colors;
        private readonly MaterialPropertyBlock originalBlock,workingBlock;
        private readonly int colorProperty;
        private readonly Color originalColor;
        private readonly bool opacityProperty;
        private readonly float originalOpacity;

        public MeshFade(MeshRenderer renderer)
        {
            this.renderer=renderer;
            var material=renderer.sharedMaterial;
            opacityProperty=material!=null&&material.HasProperty(FadeProperty);
            bool colorTint=material!=null&&(material.HasColor(BaseColorProperty)||material.HasColor(ColorProperty));
            if(opacityProperty||colorTint)
            {
                originalBlock=new MaterialPropertyBlock();workingBlock=new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalBlock);renderer.GetPropertyBlock(workingBlock);
                if(opacityProperty)
                    originalOpacity=originalBlock.HasFloat(FadeProperty)
                        ?originalBlock.GetFloat(FadeProperty):material.GetFloat(FadeProperty);
                else
                {
                    colorProperty=material.HasColor(BaseColorProperty)?BaseColorProperty:ColorProperty;
                    originalColor=originalBlock.HasColor(colorProperty)
                        ?originalBlock.GetColor(colorProperty):material.GetColor(colorProperty);
                }
                return;
            }
            filter=renderer.GetComponent<MeshFilter>();
            if(filter==null||filter.sharedMesh==null)return;
            original=filter.sharedMesh;copy=Instantiate(original);
            copy.name=original.name+" Battle End Fade";
            originalColors=copy.colors32;
            if(originalColors.Length!=copy.vertexCount)
            {
                originalColors=new Color32[copy.vertexCount];
                for(int i=0;i<originalColors.Length;i++)originalColors[i]=new Color32(255,255,255,255);
            }
            colors=(Color32[])originalColors.Clone();filter.sharedMesh=copy;
        }
        public void Apply(float alpha)
        {
            if(renderer==null)return;
            if(workingBlock!=null)
            {
                if(opacityProperty)workingBlock.SetFloat(FadeProperty,originalOpacity*alpha);
                else
                {
                    Color color=originalColor;color.a*=alpha;workingBlock.SetColor(colorProperty,color);
                }
                renderer.SetPropertyBlock(workingBlock);return;
            }
            if(copy==null)return;
            for(int i=0;i<colors.Length;i++)colors[i].a=(byte)Mathf.RoundToInt(originalColors[i].a*alpha);
            copy.colors32=colors;
        }
        public void Restore()
        {
            if(renderer!=null&&originalBlock!=null)renderer.SetPropertyBlock(originalBlock);
            if(filter!=null)filter.sharedMesh=original;
        }
        public void Dispose()
        {
            Remove(copy);
        }
    }

    private sealed class ParticleFade
    {
        private readonly ParticleSystem system;
        private readonly ParticleSystem.Particle[] buffer;
        private readonly ParticleSystemStopAction stopAction;
        private readonly bool emissionEnabled;
        public ParticleFade(ParticleSystem system)
        {
            this.system=system;
            var main=system.main;stopAction=main.stopAction;main.stopAction=ParticleSystemStopAction.None;
            var emission=system.emission;emissionEnabled=emission.enabled;emission.enabled=false;
            system.Stop(false,ParticleSystemStopBehavior.StopEmitting);
            buffer=new ParticleSystem.Particle[system.particleCount];
        }
        public void Apply(float ratio)
        {
            if(system==null)return;
            int count=system.GetParticles(buffer);
            for(int i=0;i<count;i++)
            {
                Color color=buffer[i].startColor;color.a*=ratio;buffer[i].startColor=color;
            }
            system.SetParticles(buffer,count);
        }
        public void Restore()
        {
            if(system==null)return;
            system.Clear(false);var main=system.main;main.stopAction=stopAction;
            var emission=system.emission;emission.enabled=emissionEnabled;
        }
    }
}
