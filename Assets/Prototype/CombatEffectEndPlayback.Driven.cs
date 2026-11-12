using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed partial class CombatEffectEndPlayback
{
    private bool TryCreateDrivenStep(MonoBehaviour effect,out Action<float> step,out float remainingLifetime)
    {
        step=null;remainingLifetime=float.PositiveInfinity;
        if(effect is ReadabilityAttackOriginEffect attackOrigin)
        {
            attackOrigin.PrepareEndPlayback();step=attackOrigin.AdvanceEnd;return true;
        }
        if(effect is PixelMonkeyStorm storm)
        {
            storm.StopEmission();
            return Drive(storm.Advance,storm.Elapsed,storm.Elapsed+storm.RemainingLifetime,out step,out remainingLifetime);
        }
        if(effect is PixelNinjaSpacetime spacetime)
        {
            spacetime.PrepareEndPlayback();step=spacetime.AdvanceEnd;
            remainingLifetime=spacetime.RemainingEndTime;return true;
        }
        if(effect is PixelCrocodileAbyssDive abyssDive)
        {
            abyssDive.PrepareEndPlayback();step=abyssDive.AdvanceEnd;
            remainingLifetime=abyssDive.RemainingEndTime;return true;
        }
        if(effect is PixelFrogGrandSlam frogSlam)
        {
            frogSlam.PrepareEndPlayback();step=frogSlam.AdvanceEnd;
            remainingLifetime=frogSlam.RemainingEndTime;return true;
        }
        if(effect is PixelSabertoothDevour devour)
        {
            Vector3 mouth=devour.Mouth;
            return Drive(t=>devour.Advance(t,mouth),devour.Age,SabertoothDevourRules.Duration,out step,out remainingLifetime);
        }
        if(effect is PixelCombatEffect spriteEffect)
        {
            Set(spriteEffect,"fangLockStatuses",null);
            Action<float> tick=spriteEffect.Advance;
            Action late=Bind(spriteEffect,"LateUpdate");step=dt=>{tick(dt);if(spriteEffect!=null&&spriteEffect.gameObject.activeInHierarchy)late?.Invoke();};
            return true;
        }
        if(effect is Rank3FxLifetime lifetime)
        {step=lifetime.Advance;return true;}
        if(effect is PixelTyrannoLavaFieldEffect lava)
        {step=lava.Advance;return true;}
        if(effect is PixelSpiritSanctuaryEffect sanctuary)
        {step=sanctuary.Advance;return true;}
        if(effect is PixelTranscendenceFlameOutline flame)
        {
            FrozenSprite(flame,"source");float remaining=Get<float>(flame,"remaining");
            return remaining>=0f?Native(effect,remaining,out step,out remainingLifetime):RetirePersistent(effect,.35f,out step,out remainingLifetime);
        }
        if(effect is PixelBearKingRoarSpirit roarSpirit)
        {
            var frames=Get<Sprite[]>(roarSpirit,"frames");var output=Get<SpriteRenderer>(roarSpirit,"spirit");
            int frame=output==null?-1:Array.IndexOf(frames,output.sprite);
            float age=frame>=0?frame/16f:Get<float>(roarSpirit,"elapsed");
            FrozenSprite(roarSpirit,"source");Set(roarSpirit,"animationDriver",null);
            var syncMethod=roarSpirit.GetType().GetMethod("SyncFrame",BindingFlags.Instance|BindingFlags.NonPublic);
            var sync=(Action<int>)Delegate.CreateDelegate(typeof(Action<int>),roarSpirit,syncMethod);
            return Drive(t=>{if(frames.Length>0)sync(Mathf.Min(frames.Length-1,Mathf.FloorToInt(t*16f)));},age,frames.Length/16f,out step,out remainingLifetime);
        }
        if(effect is PixelBearUpperBodyHologram hologram)
        {
            float wait=Mathf.Max(0f,Optional<float>(DrivenState(effect),"bearMountainShieldTimer"));
            FrozenSprite(hologram,"source");Set(hologram,"animationDriver",null);
            var output=Get<SpriteRenderer>(hologram,"hologram");Action late=Bind(hologram,"LateUpdate");
            float passed=0f;bool departed=false;remainingLifetime=wait+1.5f;
            step=dt=>
            {
                passed+=dt;
                if(!departed&&passed>=wait)
                {
                    var departure=PixelBearAvatarDeparture.Create(output,()=>true,null,null,false,.72f);
                    if(departure!=null)Begin(departure.gameObject,effectTypes);
                    if(output!=null)output.enabled=false;
                    foreach(var system in hologram.GetComponentsInChildren<ParticleSystem>())system.Stop(false,ParticleSystemStopBehavior.StopEmitting);
                    departed=true;
                }
                else if(!departed)late?.Invoke();
            };
            return true;
        }
        if(effect is PixelKingKongLeylineAura aura)
        {
            var snapshot=FrozenSprite(aura,"source");if(snapshot!=null)Set(aura,"anchor",snapshot.transform);
            Set(aura,"animationDriver",null);
            float ending=Get<float>(aura,"ending");
            return Native(effect,ending>=0f?.45f-ending:Mathf.Max(0f,FamilyTranscendenceRules.KingKongZoneDuration-Get<float>(aura,"elapsed"))+.45f,out step,out remainingLifetime);
        }
        if(effect is PixelWolfHuntAfterimage wolf)
        {
            if(Get<bool>(wolf,"companion"))
            {
                float wait=Mathf.Max(0f,Optional<float>(DrivenState(effect),"wolfEclipseTimer"));
                var home=Get<Func<Vector3>>(wolf,"companionHome");if(home!=null){Vector3 at=home();Set(wolf,"companionHome",(Func<Vector3>)(()=>at));}
                float ending=Get<float>(wolf,"companionEndAge");bool finished=ending>=0f;float passed=0f;
                Action<float> animate=BindFloatStep(wolf,"AnimateCompanion");
                remainingLifetime=wait+WolfCombatRules.EclipseIdleDuration+WolfCombatRules.EclipseBlinkDuration-Mathf.Max(0f,ending);
                step=dt=>{passed+=dt;if(!finished&&passed>=wait){wolf.FinishCompanion();finished=true;}animate(dt);};return true;
            }
            if(Get<bool>(wolf,"rushing"))
            {
                if(!Get<bool>(wolf,"contact"))wolf.FinishRush();
                step=BindFloatStep(wolf,"AnimateAmbush");remainingLifetime=Mathf.Max(.001f,WolfCombatRules.HuntIdleDuration+
                    WolfCombatRules.HuntScatterDuration-Get<float>(wolf,"elapsed"));return true;
            }
        }
        if(effect is PixelStarProjectile fruit&&Get<int>(fruit,"kingFruitStage")>=0)
        {
            float landed=Get<float>(fruit,"fruitLandingAge");Set(fruit,"kingFruitStage",2);
            step=BindFloatStep(fruit,"Advance");remainingLifetime=landed>=0f?Mathf.Max(.001f,.08f-landed):
                Mathf.Max(.001f,Get<float>(fruit,"duration")-Get<float>(fruit,"elapsed"));return true;
        }
        if(effect is PixelFreezeShell ice)
        {
            var statuses=Get<CombatStatusState>(ice,"statuses");
            if(statuses!=null)Set(ice,"remaining",statuses.Get(CombatStatusKind.Freeze)?.remaining??0f);
            Set(ice,"statuses",null);
            return Native(effect,Mathf.Max(0f,Get<float>(ice,"remaining"))+.18f,out step,out remainingLifetime);
        }
        if(effect is PixelMonkeyReturn returning)
        {
            step=BindFloatStep(returning,"Advance");
            remainingLifetime=Mathf.Max(.001f,4f*MonkeyRank3Rules.ReturnThrowInterval+MonkeyRank3Rules.BoomerangOutDuration+
                MonkeyRank3Rules.ReturnFlightDuration-Get<float>(returning,"age"));return true;
        }
        if(effect is PixelMonkeyMortar mortar)
        {step=BindFloatStep(mortar,"Advance");remainingLifetime=Mathf.Max(.001f,PixelMonkeyMortar.ImpactTime(4)-Get<float>(mortar,"age"));return true;}
        if(effect is PixelWyvernBreath breath)
        {
            float age=Get<float>(breath,"age"),end=breath.EndAge;
            Vector3 forward=Get<Vector3>(breath,"forward");
            return Drive(t=>breath.Pose(t,forward),age,end,out step,out remainingLifetime);
        }
        if(effect is PixelWyvernPressure pressure)
        {
            object state=DrivenState(effect);bool fire=Optional<bool>(state,"fireField");
            float age=fire?Optional<float>(state,"fireAge"):Get<float>(pressure,"age");
            Vector3 direction=Get<Vector3>(pressure,"forward");
            return Drive(t=>{if(fire)pressure.PoseFire(t);else pressure.Pose(t,direction);},age,
                fire?WyvernCombatRules.DotDuration:WyvernRank3Rules.PressureWindup+WyvernRank3Rules.PressureDuration+.24f,
                out step,out remainingLifetime);
        }
        if(effect is PixelWyvernRain rain)
        {
            float age=Get<float>(rain,"age"),final=WyvernRank3Rules.RainImpact(Get<Vector3[]>(rain,"points").Length-1);
            bool fire=Get<bool>(rain,"fire");rain.Finish(fire);
            return Drive(rain.Pose,age,final+(fire?WyvernCombatRules.DotDuration:.8f),out step,out remainingLifetime);
        }
        if(effect is PixelWyvernHeavenBreath heaven)
            return Drive(heaven.Advance,Optional<float>(DrivenState(effect),"time"),WyvernHeavenBreathRules.EndTime,out step,out remainingLifetime);
        if(effect is PixelTyrannoReignStomp reign)
            return Drive(reign.Advance,Optional<float>(DrivenState(effect),"tyrannoStompTime"),FamilyTranscendenceRules.TyrannoStompEnd,out step,out remainingLifetime);
        if(effect is PixelTyrannoMaw maw)
            return Drive(maw.Advance,Optional<float>(DrivenState(effect),"time")-TyrannoRank3Rules.MawWindup,
                TyrannoRank3Rules.MawEnd-TyrannoRank3Rules.MawWindup,out step,out remainingLifetime);
        if(effect is PixelHaetaeJusticeRoar roar)
        {
            float age=Optional<float>(DrivenState(effect),"time");Vector3 at=Get<Vector3>(roar,"origin");
            return Drive(t=>roar.Pose(t,at),age,Get<float>(roar,"windup")+HaetaeCombatRules.WaveDuration+.18f,out step,out remainingLifetime);
        }
        if(effect is PixelHaetaeBellow bellow)
            return Drive(bellow.Pose,Optional<float>(DrivenState(effect),"time"),HaetaeRank3Rules.BellowWindup+
                HaetaeRank3Rules.BellowWaveDuration+.48f,out step,out remainingLifetime);
        if(effect is PixelHaetaeOppression oppression)
        {
            object state=DrivenState(effect);float age=Optional<float>(state,"time")-Optional<float>(state,"windup");
            Vector3 at=effect.transform.position;
            return Drive(t=>{oppression.Pose(t,at);oppression.EndProtectionFrame();},age,HaetaeRank3Rules.OppressionDuration,out step,out remainingLifetime);
        }
        if(effect is PixelHaetaeThunder thunder)
        {
            object state=DrivenState(effect);float age=0f;
            if(state!=null)
            {
                var owner=DrivenOwner(effect);var roars=Optional<IEnumerable>(owner,"haetaeRoars");
                if(roars!=null)foreach(var cast in roars)
                {
                    var strikes=Optional<IList>(cast,"strikes");
                    if(strikes!=null&&strikes.Contains(state)){age=Optional<float>(cast,"time")-Optional<float>(state,"time");break;}
                }
            }
            return Drive(thunder.Pose,age,HaetaeRank3Rules.HeavenlyDelay+PixelHaetaeThunder.Afterglow,out step,out remainingLifetime);
        }
        if(effect is PixelMushroomDeathBloom deathBloomSmoke)
        {
            deathBloomSmoke.StopEmission();
            return Drive(deathBloomSmoke.Advance,deathBloomSmoke.Elapsed,deathBloomSmoke.Elapsed+deathBloomSmoke.RemainingLifetime,out step,out remainingLifetime);
        }
        if(effect is PixelMushroomGas gas)
        {
            object state=DrivenState(effect);float age=Optional<float>(DrivenOwner(effect),"mushroomGasClock")-Optional<float>(state,"start");
            float end=Mathf.Max(Get<float>(gas,"duration"),Get<float>(gas,"resonanceStart")+MushroomRank3Rules.ResonanceWindup+.45f);
            return Drive(gas.Pose,age,end,out step,out remainingLifetime);
        }
        if(effect is PixelParasiticCrown crown)
        {
            float age=Get<float>(crown,"age"),bloom=Get<float>(crown,"bloom");Vector3 ground=Get<Vector3>(crown,"ground");
            remainingLifetime=bloom>=0f?Mathf.Max(.001f,.6f-bloom):Mathf.Max(.001f,MushroomRank3Rules.CrownDuration-age+.6f);
            step=dt=>{age+=dt;if(bloom>=0f)bloom+=dt;else if(age>=MushroomRank3Rules.CrownDuration){bloom=age-MushroomRank3Rules.CrownDuration;crown.Bloom(ground);}crown.Pose(ground,age,bloom);};
            return true;
        }
        if(effect is PixelMushroomMigration migration)
        {
            object state=DrivenState(effect),target=Optional<object>(state,"target");var model=Optional<GameObject>(target,"model");
            Vector3 at=model==null?effect.transform.position:model.transform.position;at.y=0f;
            return Drive(t=>migration.Pose(at,t),Mathf.Max(0f,Optional<float>(state,"flight")),MushroomPropagationRules.FlightDuration,out step,out remainingLifetime);
        }
        if(effect is PixelGriffinRoyal royal)
        {
            object state=DrivenState(effect);bool wave=Get<bool>(royal,"wave");float age=Optional<float>(state,"age");
            royal.CancelPreparation();
            float end=wave?GriffinRank3Rules.RoyalWindDuration+.18f:age+Mathf.Max(.28f,
                Get<float>(royal,"strike")+GriffinRank3Rules.RoyalWingRelease-age);
            return Drive(t=>royal.Pose(t,false),age,end,out step,out remainingLifetime);
        }
        if(effect is PixelBeetleGravity gravity)
            return Drive(gravity.Pose,Get<float>(gravity,"time"),BeetleRank3Rules.GravityLanding+.22f,out step,out remainingLifetime);
        if(effect is PixelBeetleOverturn overturn)
            return Drive(overturn.Pose,Get<float>(overturn,"time"),BeetleOverturnRules.Landing+PixelBeetleOverturn.Afterglow,out step,out remainingLifetime);
        if(effect is PixelBeetleSweep sweep)
        {
            var reference=Get<SpriteRenderer>(sweep,"reference");var driver=reference==null?null:reference.GetComponent<SpriteMonsterAnimationDriver>();
            float phase=driver==null?1f:driver.BeetleSweepPhase;
            remainingLifetime=Mathf.Max(0f,1f-phase)*.24f+.31f;
            step=dt=>{phase+=dt/.24f;sweep.Pose(dt,phase,phase<=1f,false);};return true;
        }
        if(effect is PixelMammothCharge charge)
        {
            // The containing cast advances its own debris on the same authored clock.
            if(charge.transform.parent!=null&&(charge.transform.parent.GetComponent<PixelMammothBoulder>()!=null||
                charge.transform.parent.GetComponent<PixelBeetleGravity>()!=null||charge.transform.parent.GetComponent<PixelBeetleOverturn>()!=null))return true;
            float age=Get<float>(charge,"clock"),end=age;var pieces=Get<IEnumerable>(charge,"pieces");
            foreach(var piece in pieces)end=Mathf.Max(end,Optional<float>(piece,"birth")+Optional<float>(piece,"life"));
            Set(charge,"scraped",true);Vector3 at=charge.transform.position;
            return Drive(t=>charge.Pose(at,t),age,end,out step,out remainingLifetime);
        }
        if(effect is PixelMammothBoulder boulder)
        {
            float age=Get<float>(boulder,"age"),broken=Get<float>(boulder,"broken");Vector3 origin=Get<Vector3>(boulder,"origin"),direction=Get<Vector3>(boulder,"direction");
            float flightEnd=(MammothRank3Rules.TossRangeTiles-.55f)/MammothRank3Rules.TossSpeedTiles;
            float end=broken>=0f?broken+.28f:MammothRank3Rules.TossRelease+flightEnd+.28f;
            bool shattered=broken>=0f;Vector3 ground=Get<Vector3>(boulder,"ground");float radius=Get<float>(boulder,"radius");
            return Drive(t=>
            {
                if(t<MammothRank3Rules.TossRelease){boulder.Prepare(t);return;}
                float flight=t-MammothRank3Rules.TossRelease;
                if(!shattered)ground=origin+direction*MonsterAutoBattlePrototype.TileSize*Mathf.Min(MammothRank3Rules.TossRangeTiles,.55f+flight*MammothRank3Rules.TossSpeedTiles);
                boulder.Fly(ground,flight);
                if(!shattered&&flight>=flightEnd){shattered=true;boulder.Shatter(flight,radius);}
            },age,end,out step,out remainingLifetime);
        }
        if(effect is PixelMammothBrace brace)
        {
            float age=Get<float>(brace,"clock"),load=Get<float>(brace,"load");Vector3 direction=Get<Vector3>(brace,"forward");
            bool swept=age>=MammothRank3Rules.BraceStrike;
            return Drive(t=>{brace.Pose(t,direction,load);if(!swept&&t>=MammothRank3Rules.BraceStrike){swept=true;brace.Sweep(t);}},
                age,MammothRank3Rules.BraceEnd,out step,out remainingLifetime);
        }
        if(effect is PixelMammothExpulsion expulsion)
        {
            expulsion.StopEmission();
            return Drive(expulsion.Pose,expulsion.Elapsed,expulsion.Elapsed+expulsion.RemainingLifetime,out step,out remainingLifetime);
        }
        if(effect is PixelMammothChainImpact chainImpact)
        {
            float age=chainImpact.Elapsed;
            return Drive(t=>chainImpact.Pose(t),age,age+chainImpact.RemainingLifetime,out step,out remainingLifetime);
        }
        if(effect is PixelMammothWallRush wall)
        {
            float age=Get<float>(wall,"clock"),end=age;var broken=Get<float[]>(wall,"broken");
            for(int i=0;i<broken.Length;i++){if(broken[i]<0f)wall.Break(i,age);else end=Mathf.Max(end,broken[i]+.42f);}
            return Drive(wall.Pose,age,Mathf.Max(end,age+.42f),out step,out remainingLifetime);
        }
        if(effect is PixelCrocodileCrushingSweep roll)
        {
            float age=roll.CurrentRollTime;
            return Drive(roll.SetProgress,age,CrocodileCombatRules.CrushingRollStart+CrocodileCombatRules.CrushingRollSpinDuration,out step,out remainingLifetime);
        }
        if(effect is PixelMantisGroundCut cut)
            return Drive(cut.Advance,Get<float>(cut,"age"),MantisRank3Rules.GuillotineFlight+MantisRank3Rules.GuillotineScarDuration,out step,out remainingLifetime);
        if(effect is PixelNinjaEightGate gate)
        {
            float age=Optional<float>(DrivenState(effect),"time");bool detonated=Get<bool>(gate,"detonated");
            if(detonated)return Native(effect,.6f-Get<float>(gate,"blastAge"),out step,out remainingLifetime);
            Action update=Bind(gate,"Update");remainingLifetime=Mathf.Max(0f,NinjaCatRank3Rules.GateBlastTime-age)+.6f;
            step=dt=>{age+=dt;if(!detonated){gate.Sample(age);if(age>=NinjaCatRank3Rules.GateBlastTime){gate.Detonate();detonated=true;}}else update?.Invoke();};return true;
        }
        if(effect is PixelWizardSupernova supernova)
        {
            int phase=Get<int>(supernova,"phase");float phaseAge=Get<float>(supernova,"phaseAge"),age=Get<float>(supernova,"age");
            float up=WizardMouseCombatRules.ProjectileInterval*(WizardMouseCombatRules.ProjectileCount-1)+WizardMouseCombatRules.SupernovaAscentDuration;
            float wait=phase==0?Mathf.Max(0f,up-age):0f,fall=phase<2?Mathf.Max(0f,WizardMouseCombatRules.SupernovaDelay-(phase==1?phaseAge:0f)):0f;
            remainingLifetime=wait+fall+(phase==2?Mathf.Max(.001f,.7f-phaseAge):.7f);
            Action update=Bind(supernova,"Update");float passed=0f;
            step=dt=>{passed+=dt;if(phase==0&&passed>=wait){supernova.BeginDescent();phase=1;}if(phase==1&&passed>=wait+fall){supernova.Impact();phase=2;}update?.Invoke();};return true;
        }
        if(effect is PixelSpiderCaptureNet net)
        {
            int phase=Get<int>(net,"phase");float age=Get<float>(net,"elapsed");
            if(phase==0||phase==3){net.Break();return Native(effect,Get<bool>(net,"heavenly")?.32f:.24f,out step,out remainingLifetime);}
            float end=phase==1?Get<float>(net,"travel")+PixelSpiderCaptureNet.SpreadDuration+PixelSpiderCaptureNet.DropDuration:
                phase==2?PixelSpiderCaptureNet.SpreadDuration+PixelSpiderCaptureNet.DropDuration:Get<bool>(net,"heavenly")?.32f:.24f;
            return Native(effect,end-age,out step,out remainingLifetime);
        }
        if(effect is PixelGriffinDive dive)
        {
            float age=Get<float>(dive,"age"),landed=Get<float>(dive,"landedAt");bool heavy=Get<bool>(dive,"heavy");
            float landTime=Get<float>(dive,"ascent")+GriffinCombatRules.SkyDiveDescentDuration;
            float wait=landed>=0f?0f:Mathf.Max(0f,landTime-age);Action late=Bind(dive,"LateUpdate");
            remainingLifetime=wait+(heavy?.64f:.48f);bool didLand=landed>=0f;
            step=dt=>{age+=dt;dive.Pose(age);if(!didLand&&age>=landTime){dive.Land();didLand=true;}late?.Invoke();};return true;
        }
        if(effect is PixelMycoking king)
        {
            float age=Get<float>(king,"age"),depart=Get<float>(king,"depart"),wait=depart>=0f?0f:
                Mathf.Max(0f,Optional<float>(DrivenState(effect),"rank3SummonTimer"));
            Action late=Bind(king,"LateUpdate");bool leaving=depart>=0f;float passed=0f;
            remainingLifetime=wait+Mathf.Max(.001f,.65f-Mathf.Max(0f,depart));
            step=dt=>{age+=dt;passed+=dt;if(!leaving&&passed>=wait){king.Depart();leaving=true;}king.Pose(age);if(leaving)late?.Invoke();};return true;
        }
        if(effect is PixelMantisCocoon cocoon){cocoon.Finish(false);return Native(effect,.65f-Mathf.Max(0f,Get<float>(cocoon,"release")),out step,out remainingLifetime);}
        if(effect is PixelBearFightingSpirit spirit)
        {
            object state=DrivenState(effect);var snapshot=FrozenSprite(spirit,"reference");
            if(snapshot!=null)Set(spirit,"source",snapshot.transform);Set(spirit,"animationDriver",null);
            int phase=Get<int>(spirit,"phase");float wait=phase==1?Mathf.Max(0f,Optional<float>(state,"bearResolveTimer")):0f;
            float phaseAge=Get<float>(spirit,"phaseAge"),passed=0f;bool ending=phase==2;
            Action late=Bind(spirit,"LateUpdate");remainingLifetime=wait+(ending?Mathf.Max(.001f,.3f-phaseAge):.3f);
            step=dt=>{passed+=dt;if(!ending&&passed>=wait){spirit.Finish();ending=true;}late?.Invoke();};return true;
        }
        if(effect is PixelNinjaScrollOrbit scroll){scroll.Stop(true);return Native(effect,.28f-Get<float>(scroll,"fade"),out step,out remainingLifetime);}
        if(effect is PixelNinjaMoonMark moon)
        {
            if(!Get<bool>(moon,"exploding")){Set(moon,"exploding",true);Set(moon,"elapsed",0f);}
            return Native(effect,.4f-Get<float>(moon,"elapsed"),out step,out remainingLifetime);
        }
        if(effect is PixelSpiderSilkBind bind)
        {bind.Stop();return Native(effect,(Get<bool>(bind,"burst")?.32f:.24f)-Get<float>(bind,"release"),out step,out remainingLifetime);}
        if(effect is PixelSilkQueenDomain domain){domain.Break();return Native(effect,.25f-Get<float>(domain,"fadeAge"),out step,out remainingLifetime);}
        if(effect is PixelWizardPentagramField pentagram){if(!Get<bool>(pentagram,"finishing"))pentagram.Finish();return Native(effect,.72f-Get<float>(pentagram,"finishAge"),out step,out remainingLifetime);}
        if(effect is PixelMonkeyNimbus nimbus){nimbus.Stop(true);remainingLifetime=.5f;return true;}
        if(effect is PixelHaetaeDevourer devourer)
        {
            Set(devourer,"valid",(Func<bool>)(()=>false));return Native(effect,.65f,out step,out remainingLifetime);
        }
        if(effect is PixelSilkPuppet puppet)
        {
            bool departed=Get<bool>(puppet,"departed");float age=Get<float>(puppet,"age"),passed=0f;
            float wait=departed?0f:Mathf.Max(Optional<float>(DrivenState(effect),"rank3SummonTimer"),PixelSilkPuppet.RiseDuration-age);
            var body=Get<SpriteRenderer>(puppet,"body");var outline=Get<SpriteRenderer>(puppet,"outline");Action late=Bind(puppet,"LateUpdate");
            remainingLifetime=wait+PixelBearAvatarDeparture.Duration;
            step=dt=>
            {
                passed+=dt;
                if(!departed&&passed>=wait)
                {
                    puppet.Depart();RegisterNewDepartures(effect);departed=true;
                    if(body!=null)body.forceRenderingOff=true;if(outline!=null)outline.enabled=false;
                }
                else if(!departed)late?.Invoke();
            };
            return true;
        }
        if(effect is PixelSpiritConcert concert)
        {
            bool departed=Get<bool>(concert,"departed");var clock=Get<Func<float>>(concert,"clock");
            float age=clock()-Get<float>(concert,"born"),wait=Mathf.Max(0f,PixelSpiritConcert.ReadyToCast-age);
            Action late=Bind(concert,"LateUpdate");remainingLifetime=wait+.72f;
            step=dt=>{age+=dt;if(!departed&&age>=PixelSpiritConcert.ReadyToCast){concert.Depart();RegisterNewDepartures(effect);departed=true;}late?.Invoke();};
            return true;
        }
        if(effect is PixelSpiritGuardian guardian)
        {
            var body=Get<SpriteRenderer>(guardian,"body");var clock=Get<Func<float>>(guardian,"clock");
            float age=clock()-Get<float>(guardian,"born"),wait=Mathf.Max(0f,3.1f-age);
            Action late=Bind(guardian,"LateUpdate");bool departed=false;remainingLifetime=wait+.72f;
            step=dt=>
            {
                age+=dt;
                if(!departed&&age>=3.1f)
                {
                    var departure=PixelBearAvatarDeparture.Create(body,()=>true,body==null?(Color?)null:body.color,null,false,.72f);
                    if(departure!=null)Begin(departure.gameObject,effectTypes);
                    if(body!=null)body.enabled=false;departed=true;
                }
                else if(!departed)late?.Invoke();
            };
            return true;
        }
        if(effect is PixelWizardCygnusConstellation constellation)
        {
            var snapshot=FrozenSprite(constellation,"reference");if(snapshot!=null)Set(constellation,"source",snapshot.transform);
            return RetirePersistent(effect,.35f,out step,out remainingLifetime);
        }
        if(effect is PixelWizardCygnusMeteorShower meteorShower)
        {
            meteorShower.FreezeTargets();remainingLifetime=meteorShower.RemainingLifetime;
            step=dt=>meteorShower.Advance(meteorShower.Elapsed+dt);return true;
        }
        if(effect is PixelWolfStampede stampede)
        {
            remainingLifetime=Mathf.Max(0f,WolfStampedeRules.Duration-stampede.Elapsed);
            step=dt=>stampede.Advance(stampede.Elapsed+dt);return true;
        }
        if(effect is PixelWolfAlphaMoon)return RetirePersistent(effect,.35f,out step,out remainingLifetime);
        if(effect is PixelGroundWebReveal)return RetirePersistent(effect,.35f,out step,out remainingLifetime);
        if(effect is PixelSpiritBlessing blessing)
        {
            FrozenSprite(blessing,"reference");Set(blessing,"driver",null);
            int mode=Get<int>(blessing,"mode");
            if(mode==0)return RetirePersistent(effect,.35f,out step,out remainingLifetime);
            var clock=Get<Func<float>>(blessing,"clock");float age=clock()-Get<float>(blessing,"born");
            return Native(effect,Get<float>(blessing,"duration")+.35f-age,out step,out remainingLifetime);
        }
        if(effect is PixelTyrannoStomp stomp)
        {
            if(!Get<bool>(stomp,"impacted"))
            {FrozenSprite(stomp,"source");Set(stomp,"animationDriver",null);return RetirePersistent(effect,.25f,out step,out remainingLifetime);}
            if(Get<bool>(stomp,"extinction")&&!Get<bool>(stomp,"automaticSpread"))
            {
                float progress=Get<float>(stomp,"spreadProgress");Action<float> animate=BindFloatStep(stomp,"Animate");
                remainingLifetime=Mathf.Max(.001f,.85f-Get<float>(stomp,"age"));
                step=dt=>{progress+=dt/TyrannoRank3Rules.ExtinctionWaveDuration;stomp.Spread(progress);animate(dt);};return true;
            }
            return Native(effect,(Get<bool>(stomp,"marchFootstep")?.45f:.85f)-Get<float>(stomp,"age"),out step,out remainingLifetime);
        }
        if(effect is PixelWyvernBolt bolt&&Get<Func<float>>(bolt,"charge")!=null)
            return RetirePersistent(effect,.25f,out step,out remainingLifetime);
        if(effect is PixelMantisReaper reaper)
        {
            FrozenSprite(reaper,"reference");float molt=Get<float>(reaper,"moltAge"),flight=Get<float>(reaper,"flightAge");Vector3 start=Get<Vector3>(reaper,"start"),end=Get<Vector3>(reaper,"end");float width=Get<float>(reaper,"width");
            Action late=Bind(reaper,"LateUpdate");remainingLifetime=Mathf.Max(.001f,flight>=0f?.68f-flight:.6f-molt);
            step=dt=>{molt+=dt;reaper.Molt(molt);if(flight>=0f){flight+=dt;reaper.Flight(flight,start,end,width);}late?.Invoke();};return true;
        }
        if(effect is PixelUnitShield shield)
        {
            float time=Optional<float>(DrivenOwner(effect),"battleElapsed");Action late=Bind(shield,"LateUpdate");
            remainingLifetime=.4f;step=dt=>{time+=dt;shield.Pose(time,false);late?.Invoke();};return true;
        }
        return false;
    }

    private static bool Drive(Action<float> pose,float age,float end,out Action<float> step,out float remaining)
    {remaining=Mathf.Max(.001f,end-age);step=dt=>{age+=dt;pose(age);};return true;}

    private static bool Native(MonoBehaviour effect,float duration,out Action<float> step,out float remaining)
    {
        Action<float> animateStep=BindFloatStep(effect,"Animate");
        Action update=Bind(effect,"Update"),late=Bind(effect,"LateUpdate");
        remaining=Mathf.Max(.001f,duration);step=animateStep??(dt=>{update?.Invoke();if(effect!=null)late?.Invoke();});return true;
    }

    private static Action<float> BindFloatStep(MonoBehaviour effect,string methodName)
    {
        var method=effect.GetType().GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(float)},null);
        return method==null?null:(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),effect,method);
    }

    private static bool RetirePersistent(MonoBehaviour effect,float duration,out Action<float> step,out float remaining)
    {
        Action update=Bind(effect,"Update"),late=Bind(effect,"LateUpdate");
        var sprites=effect.GetComponentsInChildren<SpriteRenderer>();var colors=new Color[sprites.Length];
        for(int i=0;i<sprites.Length;i++)colors[i]=sprites[i].color;
        var systems=effect.GetComponentsInChildren<ParticleSystem>();var buffers=new ParticleSystem.Particle[systems.Length][];
        for(int i=0;i<systems.Length;i++)
        {
            var system=systems[i];buffers[i]=new ParticleSystem.Particle[system.main.maxParticles];
            var emission=system.emission;emission.enabled=false;system.Stop(false,ParticleSystemStopBehavior.StopEmitting);
        }
        float age=0f;remaining=duration;
        step=dt=>
        {
            age+=dt;update?.Invoke();if(effect==null)return;late?.Invoke();
            float alpha=1f-Mathf.SmoothStep(0f,1f,age/duration);
            for(int i=0;i<sprites.Length;i++)if(sprites[i]!=null){Color color=colors[i];color.a*=alpha;sprites[i].color=color;}
            for(int i=0;i<systems.Length;i++)if(systems[i]!=null)
            {
                int count=systems[i].GetParticles(buffers[i]);
                for(int n=0;n<count;n++){Color color=buffers[i][n].startColor;color.a*=alpha;buffers[i][n].startColor=color;}
                systems[i].SetParticles(buffers[i],count);
            }
        };
        return true;
    }

    private void RegisterNewDepartures(Component source)
    {
        foreach(var root in source.gameObject.scene.GetRootGameObjects())
        foreach(var departure in root.GetComponentsInChildren<PixelBearAvatarDeparture>())
            if(departure.GetComponentInParent<CombatEffectEndPlayback>()==null)Begin(departure.gameObject,effectTypes);
    }

    private SpriteRenderer FrozenSprite(MonoBehaviour effect,string fieldName)
    {
        var source=Get<SpriteRenderer>(effect,fieldName);if(source==null)return null;
        var chain=new List<Transform>();for(var at=source.transform;at!=null;at=at.parent)chain.Add(at);
        Transform parent=null;GameObject root=null;
        for(int i=chain.Count-1;i>=0;i--)
        {
            var copy=new GameObject("Battle Effect Reference").transform;
            if(parent!=null)copy.SetParent(parent,false);else
            {
                root=copy.gameObject;
                if(effect.gameObject.scene.IsValid()&&effect.gameObject.scene.isLoaded)
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,effect.gameObject.scene);
            }
            copy.localPosition=chain[i].localPosition;copy.localRotation=chain[i].localRotation;copy.localScale=chain[i].localScale;parent=copy;
        }
        referenceSnapshots.Add(root);
        var snapshot=parent.gameObject.AddComponent<SpriteRenderer>();snapshot.sprite=source.sprite;
        snapshot.flipX=source.flipX;snapshot.flipY=source.flipY;snapshot.sharedMaterial=source.sharedMaterial;
        snapshot.color=source.color;snapshot.sortingLayerID=source.sortingLayerID;snapshot.sortingOrder=source.sortingOrder;
        snapshot.enabled=source.enabled;snapshot.forceRenderingOff=true;
        Set(effect,fieldName,snapshot);return snapshot;
    }

    private static T Optional<T>(object target,string name)
    {
        if(target==null)return default(T);
        var field=target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        return field==null?default(T):(T)field.GetValue(target);
    }

    private static MonsterAutoBattlePrototype DrivenOwner(Component effect)
    {
        var owner=effect.GetComponentInParent<MonsterAutoBattlePrototype>();if(owner!=null)return owner;
        foreach(var root in effect.gameObject.scene.GetRootGameObjects())
        {owner=root.GetComponentInChildren<MonsterAutoBattlePrototype>(true);if(owner!=null)return owner;}
        return null;
    }

    // Only inspect the battle's state records once; playback delegates never reflect per frame.
    private static object DrivenState(MonoBehaviour effect)
    {
        var owner=DrivenOwner(effect);if(owner==null)return null;
        return FindDrivenState(owner,effect,new HashSet<object>(),0);
    }

    private static object FindDrivenState(object value,MonoBehaviour effect,HashSet<object> visited,int depth)
    {
        if(value==null||depth>5||!visited.Add(value))return null;
        if(value is IDictionary dictionary)
        {foreach(DictionaryEntry item in dictionary){var found=FindDrivenState(item.Value,effect,visited,depth);if(found!=null)return found;}return null;}
        if(value is IEnumerable sequence&&!(value is string))
        {foreach(var item in sequence){var found=FindDrivenState(item,effect,visited,depth);if(found!=null)return found;}return null;}
        Type type=value.GetType();
        if(type!=typeof(MonsterAutoBattlePrototype)&&type.DeclaringType!=typeof(MonsterAutoBattlePrototype))return null;
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        foreach(var field in fields)if(typeof(Component).IsAssignableFrom(field.FieldType))
        {
            var component=field.GetValue(value) as Component;
            if(component!=null&&(ReferenceEquals(component,effect)||component.gameObject==effect.gameObject))return value;
        }
        foreach(var field in fields)
        {
            if(field.FieldType.IsValueType||field.FieldType==typeof(string)||typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)||
               typeof(Delegate).IsAssignableFrom(field.FieldType)||field.Name=="source"||field.Name=="target"||field.Name=="host"||field.Name=="owner"||field.Name=="root")continue;
            object nested=field.GetValue(value);if(nested==null||nested is UnityEngine.Object)continue;
            var found=FindDrivenState(nested,effect,visited,depth+1);if(found!=null)return found;
        }
        return null;
    }
}
