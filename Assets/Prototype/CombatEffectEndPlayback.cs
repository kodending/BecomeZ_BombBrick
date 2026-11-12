using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// The battle can finish while its existing visuals use their own, harmless playback clock.
[DisallowMultipleComponent]
[DefaultExecutionOrder(10100)]
public sealed partial class CombatEffectEndPlayback : MonoBehaviour
{
    private sealed class Playback
    {
        public MonoBehaviour effect;
        public Action<float> step;
        public float remaining;
    }

    private struct DisabledBehaviour
    {
        public MonoBehaviour component;
        public bool enabled;
    }

    private struct FieldRestore
    {
        public MonoBehaviour component;
        public FieldInfo field;
        public object value;
    }

    private static readonly HashSet<CombatEffectEndPlayback> Live=new HashSet<CombatEffectEndPlayback>();
    private static readonly Dictionary<Type,FieldInfo[]> CallbackFields=new Dictionary<Type,FieldInfo[]>();
    private static readonly Dictionary<Type,Dictionary<string,MethodInfo>> NativeMethods=new Dictionary<Type,Dictionary<string,MethodInfo>>();
    private static readonly Func<bool> Valid=()=>true,NotPaused=()=>false;
    private readonly List<Playback> playbacks=new List<Playback>();
    private readonly List<DisabledBehaviour> disabled=new List<DisabledBehaviour>();
    private readonly List<FieldRestore> restoreFields=new List<FieldRestore>();
    private readonly List<GameObject> referenceSnapshots=new List<GameObject>();
    private ISet<Type> effectTypes;
    private Scene ownerScene;
    private GameObject frozenAnchor;
    private float elapsed;
    private bool advancing,finished;

    public static bool IsPreserving(Component component)
    {
        if(component==null)return false;
        var playback=component.GetComponentInParent<CombatEffectEndPlayback>(true);
        return playback!=null&&!playback.finished&&!playback.advancing;
    }

    public static void Begin(GameObject root,ISet<Type> effectTypes)
    {
        if(root==null||!root.activeInHierarchy||effectTypes==null||
           root.GetComponentInParent<CombatEffectEndPlayback>(true)!=null)return;
        var playback=root.AddComponent<CombatEffectEndPlayback>();
        playback.ownerScene=root.scene;
        Live.Add(playback);
        try{playback.Initialize(effectTypes);}
        catch
        {
            playback.Finish();
            throw;
        }
    }

    private void Initialize(ISet<Type> effectTypes)
    {
        this.effectTypes=effectTypes;
        var components=GetComponentsInChildren<MonoBehaviour>(true);
        foreach(var effect in components)
        {
            if(effect==null||effect is CombatEffectEndPlayback||effect is CombatEffectEndFade)continue;
            disabled.Add(new DisabledBehaviour{component=effect,enabled=effect.enabled});
            if(!effect.enabled||!effect.gameObject.activeInHierarchy||!effectTypes.Contains(effect.GetType()))continue;
            DisarmCallbacks(effect);
            Action<float> step;
            float remaining;
            advancing=true;
            bool driven;
            try{driven=TryCreateDrivenStep(effect,out step,out remaining);}
            finally{advancing=false;}
            if(this==null||finished)return;
            if(!driven)
            {
                Action update=Bind(effect,"Update"),lateUpdate=Bind(effect,"LateUpdate");
                if(update==null&&lateUpdate==null)continue;
                step=dt=>{update?.Invoke();if(effect!=null&&effect.gameObject.activeInHierarchy)lateUpdate?.Invoke();};
                remaining=float.PositiveInfinity;
            }
            playbacks.Add(new Playback{effect=effect,step=step,remaining=remaining});
        }
        foreach(var behaviour in disabled)
            if(behaviour.component!=null)behaviour.component.enabled=false;
        FreezeParent();
    }

    private void DisarmCallbacks(MonoBehaviour effect)
    {
        foreach(var field in FieldsForPlayback(effect.GetType()))
        {
            object original=field.GetValue(effect),replacement=original;
            if(field.FieldType==typeof(CombatProjectileSource.Flight))
            {
                var flight=(CombatProjectileSource.Flight)original;
                float start=flight.clock==null?0f:flight.clock();
                flight.clock=()=>start+elapsed;
                flight.valid=Valid;
                flight.deflect=null;
                Action complete=flight.finished;
                bool completed=false;
                flight.finished=()=>{if(completed)return;completed=true;complete?.Invoke();};
                replacement=flight;
            }
            else if(field.FieldType==typeof(Func<bool>))
                replacement=field.Name.IndexOf("paused",StringComparison.OrdinalIgnoreCase)>=0?NotPaused:Valid;
            else if(field.FieldType==typeof(Func<float>)&&original!=null)
            {
                float start=((Func<float>)original)();
                replacement=field.Name.IndexOf("clock",StringComparison.OrdinalIgnoreCase)>=0
                    ?(Func<float>)(()=>start+elapsed):(Func<float>)(()=>start);
            }
            else if(typeof(Delegate).IsAssignableFrom(field.FieldType)&&
                    field.FieldType.GetMethod("Invoke").ReturnType==typeof(void))replacement=null;
            else continue;
            field.SetValue(effect,replacement);
            restoreFields.Add(new FieldRestore{component=effect,field=field,value=original});
        }
    }

    private static FieldInfo[] FieldsForPlayback(Type effectType)
    {
        FieldInfo[] result;
        if(CallbackFields.TryGetValue(effectType,out result))return result;
        var fields=new List<FieldInfo>();
        for(Type type=effectType;type!=null&&type!=typeof(MonoBehaviour);type=type.BaseType)
        foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
        {
            if(field.IsInitOnly)continue;
            Type kind=field.FieldType;
            if(kind==typeof(CombatProjectileSource.Flight)||kind==typeof(Func<bool>)||kind==typeof(Func<float>)||
               typeof(Delegate).IsAssignableFrom(kind)&&kind.GetMethod("Invoke").ReturnType==typeof(void))fields.Add(field);
        }
        result=fields.ToArray();CallbackFields.Add(effectType,result);return result;
    }

    private void FreezeParent()
    {
        Transform original=transform.parent;
        if(original==null)return;
        var ancestors=new List<Transform>();
        for(var parent=original;parent!=null;parent=parent.parent)ancestors.Add(parent);
        Transform previous=null;
        for(int i=ancestors.Count-1;i>=0;i--)
        {
            var snapshot=new GameObject("Battle Effect Frozen Anchor").transform;
            if(previous==null)
            {
                frozenAnchor=snapshot.gameObject;
                if(ownerScene.IsValid()&&ownerScene.isLoaded)SceneManager.MoveGameObjectToScene(frozenAnchor,ownerScene);
            }
            else snapshot.SetParent(previous,false);
            snapshot.localPosition=ancestors[i].localPosition;
            snapshot.localRotation=ancestors[i].localRotation;
            snapshot.localScale=ancestors[i].localScale;
            previous=snapshot;
        }
        // Matching every ancestor's TRS also preserves local-space effects under scaled actors.
        transform.SetParent(previous,false);
    }

    private void LateUpdate()=>Advance(Time.deltaTime);

    private void Advance(float deltaTime)
    {
        if(finished)return;
        float dt=Mathf.Max(0f,deltaTime);
        elapsed+=dt;
        advancing=true;
        try
        {
            for(int i=playbacks.Count-1;i>=0;i--)
            {
                var playback=playbacks[i];
                if(playback.effect==null||!playback.effect.gameObject.activeInHierarchy)
                {playbacks.RemoveAt(i);continue;}
                playback.step?.Invoke(dt);
                if(this==null||finished)return;
                if(playback.effect!=null&&!playback.effect.gameObject.activeInHierarchy&&playback.effect is PixelCombatEffect completed)
                {
                    if(completed.gameObject==gameObject){Finish();return;}
                    RestoreAndRelease(completed);
                    playbacks.RemoveAt(i);
                    continue;
                }
                playback.remaining-=dt;
                if(playback.remaining>0f||playback.effect==null)continue;
                if(playback.effect.gameObject==gameObject){Finish();return;}
                if(playback.effect is PixelCombatEffect pooled)RestoreAndRelease(pooled);
                else
                {
                    playback.effect.gameObject.SetActive(false);
                    Remove(playback.effect.gameObject);
                }
                playbacks.RemoveAt(i);
            }
            if(!gameObject.activeInHierarchy)Finish();
        }
        catch
        {
            if(this!=null)Finish();
            throw;
        }
        finally{if(this!=null)advancing=false;}
    }

    public static void Clear(Scene scene)
    {
        foreach(var playback in new List<CombatEffectEndPlayback>(Live))
            if(playback!=null&&playback.ownerScene==scene)playback.Finish();
    }

    private void Finish()
    {
        if(finished)return;
        finished=true;
        Live.Remove(this);
        if(frozenAnchor!=null)transform.SetParent(null,true);
        ReleaseAnchor();
        var pooled=GetComponent<PixelCombatEffect>();
        foreach(var nested in GetComponentsInChildren<PixelCombatEffect>(true))
            if(nested!=pooled)RestoreAndRelease(nested);
        if(pooled!=null)
        {
            RestoreAndRelease(pooled);
            Remove(this);
        }
        else
        {
            if(TryGetComponent<PixelSabertoothDevour>(out var devour))
            {
                devour.Cancel(true);
                return;
            }
            gameObject.SetActive(false);
            Remove(gameObject);
        }
    }

    private void RestoreAndRelease(PixelCombatEffect pooled)
    {
        if(pooled==null)return;
        foreach(var playback in playbacks)
            if(playback.effect!=null&&playback.effect.transform.IsChildOf(pooled.transform))playback.effect=null;
        for(int i=restoreFields.Count-1;i>=0;i--)
        {
            var restore=restoreFields[i];
            if(restore.component==null||!restore.component.transform.IsChildOf(pooled.transform))continue;
            restore.field.SetValue(restore.component,restore.value);
            restoreFields.RemoveAt(i);
        }
        for(int i=disabled.Count-1;i>=0;i--)
        {
            var behaviour=disabled[i];
            if(behaviour.component==null||!behaviour.component.transform.IsChildOf(pooled.transform))continue;
            behaviour.component.enabled=behaviour.enabled;
            disabled.RemoveAt(i);
        }
        pooled.transform.SetParent(null,true);
        pooled.ReleaseAfterBattleEndFade();
    }

    private void OnDestroy()
    {
        Live.Remove(this);
        ReleaseAnchor();
    }

    private void ReleaseAnchor()
    {
        var anchor=frozenAnchor;
        frozenAnchor=null;
        Remove(anchor);
        foreach(var snapshot in referenceSnapshots)Remove(snapshot);
        referenceSnapshots.Clear();
    }

    private static void Remove(UnityEngine.Object value)
    {
        if(value==null)return;
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }

    private static FieldInfo Field(object target,string name)
    {
        for(Type type=target.GetType();type!=null;type=type.BaseType)
        {
            var field=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            if(field!=null)return field;
        }
        throw new MissingFieldException(target.GetType().FullName,name);
    }

    private static T Get<T>(object target,string name)=>(T)Field(target,name).GetValue(target);
    private static void Set(object target,string name,object value)=>Field(target,name).SetValue(target,value);

    private static Action Bind(MonoBehaviour effect,string name)
    {
        Type type=effect.GetType();
        Dictionary<string,MethodInfo> methods;
        if(!NativeMethods.TryGetValue(type,out methods))
        {methods=new Dictionary<string,MethodInfo>();NativeMethods.Add(type,methods);}
        MethodInfo method;
        if(!methods.TryGetValue(name,out method))
        {
            method=type.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
            methods.Add(name,method);
        }
        return method==null?null:(Action)Delegate.CreateDelegate(typeof(Action),effect,method);
    }
}
