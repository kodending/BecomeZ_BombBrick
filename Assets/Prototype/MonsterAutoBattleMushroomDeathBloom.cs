using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MushroomDeathBloomRules
{
    public const string TraitId="trait_transcend_mushroom_endless_proliferation";
    public const float Duration=8f,TickInterval=.5f,DamageRatio=.6f,Coverage=.5f;
    public const int TickCount=16;

    public static float CoveredArea(Bounds arena,Vector3 center,float radius)
    {
        const int strips=128;
        float width=arena.size.x/strips,area=0f;
        for(int i=0;i<strips;i++)
        {
            float x=arena.min.x+(i+.5f)*width-center.x;
            if(Mathf.Abs(x)>=radius)continue;
            float reach=Mathf.Sqrt(radius*radius-x*x);
            area+=Mathf.Max(0f,Mathf.Min(arena.max.z,center.z+reach)-
                Mathf.Max(arena.min.z,center.z-reach))*width;
        }
        return area;
    }

    public static float CoverageRadius(Bounds arena,Vector3 center)
    {
        float low=0f,high=0f;
        for(int x=0;x<2;x++)for(int z=0;z<2;z++)
            high=Mathf.Max(high,Vector2.Distance(new Vector2(center.x,center.z),
                new Vector2(x==0?arena.min.x:arena.max.x,z==0?arena.min.z:arena.max.z)));
        float target=arena.size.x*arena.size.z*Coverage;
        for(int i=0;i<24;i++)
        {
            float middle=(low+high)*.5f;
            if(CoveredArea(arena,center,middle)<target)low=middle;else high=middle;
        }
        return (low+high)*.5f;
    }
}

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MushroomDeathBloom
    {
        public Unit source,flower;
        public Vector3 center;
        public float radius,age;
        public int generation,ticks;
        public bool awaitingPresentation,stopped;
        public PixelMushroomDeathBloom effect;
    }

    private sealed class MushroomDeathBloomTick
    {
        public MushroomDeathBloom bloom;
        public float at;
    }

    private readonly List<MushroomDeathBloom> mushroomDeathBlooms=new List<MushroomDeathBloom>();
    private readonly HashSet<Unit> mushroomDeathBloomUsed=new HashSet<Unit>();

    private MushroomDeathBloom MushroomDeathBloomFor(Unit source)=>
        mushroomDeathBlooms.Find(b=>b.source==source&&b.generation==combatGeneration&&!b.stopped);
    private Unit MushroomDeathBloomUnit(Unit source)=>MushroomDeathBloomFor(source)?.flower;
    private SpriteRenderer MushroomDeathBloomBodyRenderer(Unit source)=>Rank3Renderer(MushroomDeathBloomUnit(source));
    private float MushroomDeathBloomRadius(Unit source)=>MushroomDeathBloomFor(source)?.radius??0f;
    private bool IsMushroomDeathBloomPending(Unit source)=>MushroomDeathBloomFor(source)?.awaitingPresentation==true;
    private bool IsMushroomDeathBloomActive(Unit source)=>IsCombatActive(MushroomDeathBloomUnit(source));
    private bool IsMushroomDeathBloom(Unit unit)=>mushroomDeathBlooms.Any(b=>b.flower==unit&&!b.stopped&&
        b.generation==combatGeneration);

    private IEnumerable<GameObject> MushroomDeathBloomVisualRoots(Unit source)
    {
        var bloom=MushroomDeathBloomFor(source);if(bloom==null)yield break;
        yield return bloom.flower.model;yield return bloom.flower.ring;
        if(bloom.flower.mycokingFx!=null)yield return bloom.flower.mycokingFx.gameObject;
        if(bloom.effect!=null)yield return bloom.effect.gameObject;
    }

    private void TryBeginMushroomDeathBloom(Unit source)
    {
        if(source?.model==null||source.hp>0f||!IsMushroomFamily(source)||!HasFamilyTranscendence(source)||
           source.rank3MycokingSummon||mushroomDeathBloomUsed.Contains(source))return;
        mushroomDeathBloomUsed.Add(source);
        Vector3 center=Flat(source.model.transform.position);
        var model=new GameObject("Death Bloom - "+source.displayName);model.transform.SetParent(transform,false);
        model.transform.position=center;
        var reference=model.AddComponent<SpriteRenderer>();var original=Rank3Renderer(source);
        if(original!=null)
        {
            reference.sprite=original.sprite;reference.sortingLayerID=original.sortingLayerID;
            reference.sortingOrder=original.sortingOrder;model.transform.localScale=original.transform.lossyScale;
        }
        var ring=new GameObject("Death Bloom Target Ring");ring.transform.SetParent(transform,false);
        ring.transform.position=center+Vector3.up*.025f;
        var flower=new Unit{id=-1,stableId=source.stableId+"_death_bloom_"+combatGeneration,
            definition=source.definition,displayName=source.displayName,team=source.team,model=model,ring=ring,
            currentTile=source.currentTile,initialTile=source.currentTile,collisionRadius=source.collisionRadius,
            statusResistance=source.statusResistance,regionDamageMultiplier=source.regionDamageMultiplier,
            baseModelScale=model.transform.localScale,attackInterval=1f,attackRange=1,lastGroundPosition=center};
        flower.targetRing=ring.AddComponent<CombatTargetRing>();ConfigureTargetRing(flower);
        ConfigureMycoking(source,flower);
        flower.mycokingFx?.UseDeathBloomArtwork();
        flower.maxHp=flower.hp=source.maxHp;flower.defense=source.defense;flower.damage=source.damage;
        flower.rank3SummonTimer=MushroomDeathBloomRules.Duration;
        flower.mycokingFx?.Pose(.42f);
        var arena=combatBounds.size.x>0f&&combatBounds.size.z>0f?combatBounds:CenteredArenaBounds();
        var bloom=new MushroomDeathBloom{source=source,flower=flower,center=center,
            radius=MushroomDeathBloomRules.CoverageRadius(arena,center),generation=combatGeneration};
        mushroomDeathBlooms.Add(bloom);
        if(!mlEpisodeActive)
        {
            bloom.effect=PixelMushroomDeathBloom.Play(center,bloom.radius,MushroomDeathBloomRules.Duration+.65f,
                Rank3Renderer(flower),MushroomGasColor(source,false),arena);
            bloom.effect.transform.SetParent(transform,true);
            bloom.effect.Advance(.65f);
        }
        source.traitActivations++;
        bloom.awaitingPresentation=QueueRank4Cinematic(source);
        if(!bloom.awaitingPresentation)PlayMushroomDeathBloomOpening(bloom);
        RecordSkillEvent(source,MushroomDeathBloomRules.TraitId,0,0f);
    }

    private void ReleaseMushroomDeathBloom(Unit source)
    {
        var bloom=MushroomDeathBloomFor(source);
        if(bloom==null||!bloom.awaitingPresentation)return;
        bloom.awaitingPresentation=false;
        // The continuous film track owns the bloom at its native handoff.
    }

    private void PlayMushroomDeathBloomOpening(MushroomDeathBloom bloom)
    {
        PlayMushroomSound(bloom.source,MushroomSound.DeathBloom,bloom.center,.9f);
        PlayMushroomSound(bloom.source,MushroomSound.DeathBreath,bloom.center,.65f);
    }

    private void AdvanceMushroomDeathBloomCinematic(Unit source,float absoluteAge)
    {
        FlushMushroomDeathBlooms();
        var bloom=MushroomDeathBloomFor(source);
        if(bloom==null||bloom.awaitingPresentation||absoluteAge<=bloom.age)return;
        AdvanceMushroomDeathBlooms(new[]{bloom},absoluteAge-bloom.age);
    }

    private void UpdateMushroomDeathBlooms(float dt)
    {
        if(mushroomDeathBlooms.Count==0)return;
        FlushMushroomDeathBlooms();
        foreach(var bloom in mushroomDeathBlooms.ToArray())
        {
            if(bloom.generation!=combatGeneration||!IsCombatActive(bloom.flower))
            {EndMushroomDeathBloom(bloom,false);continue;}
            if(bloom.awaitingPresentation&&rank4Presentation?.request.source!=bloom.source&&
               !rank4PresentationQueue.Any(r=>r.source==bloom.source&&Rank4PresentationValid(r)))
                ReleaseMushroomDeathBloom(bloom.source);
        }
        if(dt>0f)AdvanceMushroomDeathBlooms(mushroomDeathBlooms.Where(b=>!b.awaitingPresentation).ToArray(),dt);
    }

    private void FlushMushroomDeathBlooms()
    {
        // Damage callers may be enumerating units when the death callback creates this flower.
        foreach(var bloom in mushroomDeathBlooms)
            if(!bloom.stopped&&bloom.generation==combatGeneration&&!units.Contains(bloom.flower))
            {bloom.flower.id=units.Count;units.Add(bloom.flower);}
    }

    private void AdvanceMushroomDeathBlooms(MushroomDeathBloom[] blooms,float dt)
    {
        var due=new List<MushroomDeathBloomTick>();
        foreach(var bloom in blooms)
        {
            if(bloom.stopped||bloom.generation!=combatGeneration||!IsCombatActive(bloom.flower))continue;
            float previous=bloom.age;bloom.age=Mathf.Min(MushroomDeathBloomRules.Duration,bloom.age+dt);
            // One audible exhalation for the visible cycle, never catch up missed poison ticks.
            if(bloom.age<MushroomDeathBloomRules.Duration&&Mathf.FloorToInt(bloom.age/2f)>Mathf.FloorToInt(previous/2f))
                PlayMushroomSound(bloom.source,MushroomSound.DeathBreath,bloom.center,.65f);
            while(bloom.ticks<MushroomDeathBloomRules.TickCount&&
                  (bloom.ticks+1)*MushroomDeathBloomRules.TickInterval<=bloom.age+.0001f)
            {
                bloom.ticks++;
                due.Add(new MushroomDeathBloomTick{bloom=bloom,
                    at=bloom.ticks*MushroomDeathBloomRules.TickInterval-previous});
            }
            bloom.flower.rank3SummonTimer=Mathf.Max(0f,MushroomDeathBloomRules.Duration-bloom.age);
            bloom.flower.mycokingFx?.Pose(.42f+bloom.age);bloom.effect?.Advance(.65f+bloom.age);
        }
        due.Sort((a,b)=>a.at.CompareTo(b.at));
        for(int i=0;i<due.Count;)
        {
            int end=i+1;while(end<due.Count&&Mathf.Abs(due[end].at-due[i].at)<.0001f)end++;
            // A flower destroyed by this simultaneous batch still delivers its already-due tick.
            var ready=due.GetRange(i,end-i).Where(t=>!t.bloom.stopped&&IsCombatActive(t.bloom.flower)).ToArray();
            foreach(var tick in ready)ApplyMushroomDeathBloomTick(tick.bloom);
            i=end;
        }
        foreach(var bloom in blooms)
            if(!IsCombatActive(bloom.flower))EndMushroomDeathBloom(bloom,false);
    }

    private void ApplyMushroomDeathBloomTick(MushroomDeathBloom bloom)
    {
        int hits=0;float dealt=0f;
        foreach(var target in units.ToArray())
        {
            if(target.team==bloom.flower.team||!IsCombatTargetable(target)||
               Vector3.Distance(bloom.center,Flat(target.model.transform.position))>bloom.radius)continue;
            float applied=ApplyDamageComponents(bloom.flower,target,bloom.flower.damage*MushroomDeathBloomRules.DamageRatio,
                true,false,false,false,0f,0f,1f,false,false,false,0f,skillImpact:false,allowHitSlide:false);
            if(applied>0f){hits++;dealt+=applied;}
        }
        bloom.source.skillHits+=hits;bloom.source.skillDamage+=dealt;bloom.source.damageDealt+=dealt;
        bloom.flower.damageDealt-=dealt;
        RecordSkillEvent(bloom.source,MushroomDeathBloomRules.TraitId+"_tick",hits,dealt);
    }

    private void StopMushroomDeathBloomDamage(Unit flower)
    {
        var bloom=mushroomDeathBlooms.Find(b=>b.flower==flower);
        if(bloom==null)return;
        bloom.stopped=true;RetireMushroomDeathBloomEffect(bloom,true);
    }

    private void EndMushroomDeathBloom(MushroomDeathBloom bloom,bool preserveVisualTail)
    {
        if(preserveVisualTail&&!bloom.stopped&&bloom.generation==combatGeneration&&
           !battleEnding&&bloom.age>=MushroomDeathBloomRules.Duration)
            PlayMushroomSound(bloom.source,MushroomSound.Transfer,bloom.center,.4f);
        RetireMushroomDeathBloomEffect(bloom,preserveVisualTail);bloom.stopped=true;
        DeactivateRank3Special(bloom.flower);mushroomDeathBlooms.Remove(bloom);
    }

    private void ExpireMushroomDeathBlooms()
    {
        foreach(var bloom in mushroomDeathBlooms.ToArray())
            if(bloom.age+.0001f>=MushroomDeathBloomRules.Duration)EndMushroomDeathBloom(bloom,true);
    }

    private void RetireMushroomDeathBloomEffect(MushroomDeathBloom bloom,bool preserveVisualTail)
    {
        if(bloom.effect==null)return;
        if(preserveVisualTail)
        {
            bloom.effect.StopEmission();
            CombatEffectEndPlayback.Begin(bloom.effect.gameObject,EndingEffectTypes);
        }
        else bloom.effect.Cancel();
        bloom.effect=null;
    }

    private void ClearMushroomDeathBlooms(bool preserveVisualTail=false)
    {
        foreach(var bloom in mushroomDeathBlooms.ToArray())EndMushroomDeathBloom(bloom,preserveVisualTail);
        mushroomDeathBloomUsed.Clear();
    }
}
