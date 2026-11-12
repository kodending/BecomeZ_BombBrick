using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

[RequireComponent(typeof(BehaviorParameters),typeof(DecisionRequester))]
public sealed class BombBrickBalanceAgent : Agent
{
    public const string BehaviorName="BombBrickFormationV2";
    public const int MaximumRosterSize=8;
    public const int MaximumTileCount=15;
    public const int MaximumEnemyCount=12;
    public const int FamilyObservationCount=20;
    public const int PersonalityObservationCount=14;
    public const int TraitObservationCount=16;
    public const int ItemObservationCount=8;
    public const int UnitObservationSize=70;
    public const int ActionCount=MaximumTileCount+1;
    public const int ObservationSize=1505;

    private static readonly string[] PersonalityIds={
        "persistent","calm","challenger","vengeful","fickle","cooperative","independent",
        "protective","opportunist","impulsive","cautious","bold","timid","eccentric"
    };

    [SerializeField,Range(0,12)] private int fixedWave;
    [SerializeField,Range(1,12)] private int minimumTrainingWave=1;
    [SerializeField,Range(1,12)] private int maximumTrainingWave=12;
    [SerializeField] private int baseSeed=260723;
    [SerializeField] private bool collectMissingDifficultyWaveConditions=true;
    [SerializeField] private bool highSpeedSimulation=true;
    [SerializeField,Range(1f,30f)] private float simulationTimeScale=12f;
    [SerializeField] private bool hideRenderingDuringSimulation=true;
    [Header("A/B Evaluation")]
    [SerializeField] private bool runAbEvaluation;
    [SerializeField,Range(1,500)] private int abEvaluationScenarios=50;

    private MonsterAutoBattlePrototype environment;
    private AutoBattleMlPlacementPhase phase;
    private int selectedRosterIndex=-1;
    private int episodeSequence;
    private int episodeSeed;
    private bool sessionSettingsApplied;
    private float originalTimeScale=1f;
    private float originalCaptureDeltaTime;
    private int originalCullingMask=-1;
    private bool originalRunInBackground;
    private bool abInferencePhase;
    private bool abEvaluationComplete;
    private int abScenarioIndex;
    private AutoBattleMlAbReport abReport;
    private string abReportPaths="";
    private List<string> familyIds=new List<string>();

    public bool AbEvaluationComplete=>abEvaluationComplete;
    public string AbReportPaths=>abReportPaths;

    protected override void Awake()
    {
        BehaviorParameters behavior=GetComponent<BehaviorParameters>();
        DecisionRequester requester=GetComponent<DecisionRequester>();
        requester.DecisionPeriod=20;
        requester.TakeActionsBetweenDecisions=false;
        behavior.BehaviorName=BehaviorName;
        behavior.BrainParameters.VectorObservationSize=ObservationSize;
        behavior.BrainParameters.NumStackedVectorObservations=1;
        behavior.BrainParameters.ActionSpec=ActionSpec.MakeDiscrete(
            Enumerable.Repeat(ActionCount,MaximumRosterSize).ToArray());
        MaxStep=0;
        base.Awake();
    }

    public override void Initialize()
    {
        environment=FindObjectOfType<MonsterAutoBattlePrototype>();
        if(environment==null)
        {
            Debug.LogError("[ML-Agents] MonsterAutoBattlePrototype environment was not found.");
            enabled=false;
            return;
        }
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        familyIds=database==null?new List<string>():database.monsters
            .Where(definition=>definition!=null&&definition.enabled&&
                !string.IsNullOrWhiteSpace(definition.familyId))
            .Select(definition=>definition.familyId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id=>id,StringComparer.Ordinal)
            .Take(FamilyObservationCount).ToList();
        environment.SetMlEpisodeCompletedHandler(OnBattleCompleted);
        if(runAbEvaluation)
        {
            BehaviorParameters behavior=GetComponent<BehaviorParameters>();
            if(behavior.Model==null)
            {
                Debug.LogError("[ML A/B] Evaluation model is missing.");
                enabled=false;
                return;
            }
            behavior.BehaviorType=BehaviorType.HeuristicOnly;
            abReport=new AutoBattleMlAbReport
            {
                modelName=behavior.Model.name,baseSeed=baseSeed,
                requestedScenarios=abEvaluationScenarios
            };
        }
    }

    public override void OnEpisodeBegin()
    {
        if(environment==null||abEvaluationComplete)return;
        ApplySessionSettings();
        selectedRosterIndex=-1;
        phase=AutoBattleMlPlacementPhase.SelectMonster;
        int scenario=runAbEvaluation?abScenarioIndex:episodeSequence;
        int minimumWave=Mathf.Clamp(Mathf.Min(minimumTrainingWave,maximumTrainingWave),1,12);
        int maximumWave=Mathf.Clamp(Mathf.Max(minimumTrainingWave,maximumTrainingWave),minimumWave,12);
        if(fixedWave>0)minimumWave=maximumWave=Mathf.Clamp(fixedWave,1,12);
        IReadOnlyList<HunterRegionDefinition> regions=HunterRegionCatalog.All;
        bool missingOnly=collectMissingDifficultyWaveConditions&&!runAbEvaluation;
        Vector3Int trainingScenario=ResolveTrainingScenario(
            scenario,baseSeed,minimumWave,maximumWave,regions.Count,missingOnly);
        int wave=trainingScenario.x;
        int regionIndex=trainingScenario.y;
        episodeSeed=unchecked(baseSeed+scenario*7919);
        string regionId=regions.Count==0?"prototype_region":regions[regionIndex].id;
        ExpeditionDifficulty difficulty=(ExpeditionDifficulty)trainingScenario.z;
        if(!runAbEvaluation)episodeSequence++;
        environment.BeginMlEpisode(
            wave,episodeSeed,regionId,difficulty,Academy.Instance.StepCount,
            missingOnly?"ml_missing36_v1":"ml_balanced_v3");
        if(runAbEvaluation&&abReport!=null&&string.IsNullOrEmpty(abReport.rosterSignature))
            abReport.rosterSignature=string.Join("|",environment.GetMlSnapshot().roster
                .Select(unit=>unit.stableId));
        ContinuePlacementOrStartBattle();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        AutoBattleMlSnapshot snapshot=environment==null?null:environment.GetMlSnapshot();
        sensor.AddObservation(phase==AutoBattleMlPlacementPhase.SelectMonster?1f:0f);
        sensor.AddObservation(phase==AutoBattleMlPlacementPhase.SelectTile?1f:0f);
        sensor.AddObservation(snapshot==null?0f:Mathf.Clamp01(snapshot.wave/12f));
        sensor.AddObservation(snapshot==null?0f:SafeRatio(snapshot.deploymentCount,snapshot.maximumDeploymentCount));
        sensor.AddObservation(snapshot==null?0f:Mathf.Clamp01(snapshot.maximumDeploymentCount/8f));
        sensor.AddObservation(selectedRosterIndex<0?0f:(selectedRosterIndex+1f)/MaximumRosterSize);
        AddStringOneHot(sensor,snapshot==null?"":snapshot.regionId,
            HunterRegionCatalog.All.Select(region=>region.id).Take(5).ToList(),5);
        for(int difficulty=0;difficulty<4;difficulty++)
            sensor.AddObservation(snapshot!=null&&snapshot.difficulty==difficulty?1f:0f);

        for(int i=0;i<MaximumRosterSize;i++)
            AddUnitObservation(sensor,snapshot!=null&&i<snapshot.roster.Count?snapshot.roster[i]:null,
                i==selectedRosterIndex);
        for(int i=0;i<MaximumTileCount;i++)
        {
            AutoBattleMlTileObservation tile=snapshot!=null&&i<snapshot.deploymentTiles.Count
                ?snapshot.deploymentTiles[i]:null;
            bool valid=tile!=null&&phase==AutoBattleMlPlacementPhase.SelectMonster&&
                       Enumerable.Range(0,MaximumRosterSize)
                           .Any(rosterIndex=>environment.CanMlPlaceMonster(rosterIndex,i));
            sensor.AddObservation(tile==null?0f:1f);
            sensor.AddObservation(tile!=null&&tile.occupied?1f:0f);
            sensor.AddObservation(valid?1f:0f);
            sensor.AddObservation(tile==null?0f:Mathf.Clamp01(tile.coordinate.x/16f));
            sensor.AddObservation(tile==null?0f:Mathf.Clamp01(tile.coordinate.y/12f));
            sensor.AddObservation(tile==null?0f:
                Mathf.Clamp01(1f-Mathf.Abs(tile.coordinate.x-8f)/8f));
        }
        for(int i=0;i<MaximumEnemyCount;i++)
            AddUnitObservation(sensor,snapshot!=null&&i<snapshot.enemies.Count?snapshot.enemies[i]:null,false);
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if(environment==null)return;
        for(int rosterIndex=0;rosterIndex<MaximumRosterSize;rosterIndex++)
        for(int action=0;action<ActionCount;action++)
        {
            bool skip=action==MaximumTileCount;
            actionMask.SetActionEnabled(rosterIndex,action,
                skip||(phase!=AutoBattleMlPlacementPhase.Battle&&
                       environment.CanMlPlaceMonster(rosterIndex,action)));
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if(environment==null)return;
        if(phase==AutoBattleMlPlacementPhase.Battle)
            return;
        int placed=0;
        for(int rosterIndex=0;rosterIndex<MaximumRosterSize;rosterIndex++)
        {
            int tileIndex=actions.DiscreteActions[rosterIndex];
            if(tileIndex==MaximumTileCount)continue;
            if(environment.PlaceMlMonster(rosterIndex,tileIndex))
            {
                AddReward(AutoBattleMlReward.ValidPlacementReward);
                placed++;
            }
            else AddReward(AutoBattleMlReward.InvalidActionPenalty);
        }
        if(placed==0)placed=PlaceFirstAvailableMonster();
        if(placed==0)
        {
            AddReward(AutoBattleMlReward.InvalidActionPenalty);
            EndEpisode();
            return;
        }
        phase=AutoBattleMlPlacementPhase.Battle;
        bool started=environment.StartMlBattle();
        if(!started)
        {
            AddReward(AutoBattleMlReward.InvalidActionPenalty);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> actions=actionsOut.DiscreteActions;
        for(int i=0;i<actions.Length;i++)actions[i]=MaximumTileCount;
        if(environment==null)return;
        AutoBattleMlSnapshot snapshot=environment.GetMlSnapshot();
        List<AutoBattleFormationCandidate> candidates=new List<AutoBattleFormationCandidate>();
        for(int i=0;i<snapshot.roster.Count;i++)
        {
            if(!environment.CanMlSelectMonster(i))continue;
            AutoBattleMlUnitObservation unit=snapshot.roster[i];
            candidates.Add(new AutoBattleFormationCandidate
            {
                stableId=unit.stableId,maxHp=unit.maxHp,damage=unit.damage,
                attackInterval=unit.attackInterval,attackRange=unit.attackRange,
                rank=unit.rank,traitCount=unit.traitCount
            });
        }
        List<string> selected=AutoBattleHeuristicPolicy.SelectDeployment(
            candidates,snapshot.maximumDeploymentCount,episodeSeed);
        HashSet<int> usedTiles=new HashSet<int>();
        foreach(string stableId in selected)
        {
            int rosterIndex=snapshot.roster.FindIndex(unit=>unit.stableId==stableId);
            if(rosterIndex<0||rosterIndex>=MaximumRosterSize)continue;
            AutoBattleMlUnitObservation chosen=snapshot.roster[rosterIndex];
            int preferredColumn=AutoBattleHeuristicPolicy.PreferredDeploymentRow(
                chosen.attackRange,0,MonsterAutoBattlePrototype.AllyDeploymentColumns-1);
            int tileIndex=Enumerable.Range(0,Mathf.Min(MaximumTileCount,snapshot.deploymentTiles.Count))
                .Where(index=>!usedTiles.Contains(index)&&
                       environment.CanMlPlaceMonster(rosterIndex,index))
                .OrderBy(index=>Mathf.Abs(
                    snapshot.deploymentTiles[index].coordinate.x-preferredColumn))
                .ThenBy(index=>Mathf.Abs(
                    snapshot.deploymentTiles[index].coordinate.y-
                    (MonsterAutoBattlePrototype.HexDeploymentRows-1)*0.5f))
                .ThenBy(index=>index).DefaultIfEmpty(MaximumTileCount).First();
            actions[rosterIndex]=tileIndex;
            if(tileIndex<MaximumTileCount)usedTiles.Add(tileIndex);
        }
    }

    private void ContinuePlacementOrStartBattle()
    {
        AutoBattleMlSnapshot snapshot=environment.GetMlSnapshot();
        if(snapshot.deploymentCount>=snapshot.maximumDeploymentCount||!environment.HasMlPlacementChoice())
        {
            phase=AutoBattleMlPlacementPhase.Battle;
            environment.StartMlBattle();
            return;
        }
        RequestDecision();
    }

    private void OnBattleCompleted(AutoBattleMlOutcome outcome)
    {
        AddReward(AutoBattleMlReward.Terminal(outcome));
        if(runAbEvaluation)
        {
            RecordAbOutcome(outcome);
            AdvanceAbEvaluation();
            return;
        }
        if(episodeSequence%10==0)
            Debug.Log("[ML-Agents] Episode "+episodeSequence+" wave result="+
                      (outcome.won?"win":"loss")+" reward="+GetCumulativeReward().ToString("0.000"));
        EndEpisode();
    }

    private void RecordAbOutcome(AutoBattleMlOutcome outcome)
    {
        AutoBattleMlSnapshot snapshot=environment.GetMlSnapshot();
        abReport.trials.Add(new AutoBattleMlAbTrialRecord
        {
            policy=abInferencePhase?"ML":"Heuristic",scenario=abScenarioIndex,
            wave=snapshot.wave,seed=episodeSeed,won=outcome.won,
            battleSeconds=outcome.battleSeconds,playerDeaths=outcome.playerDeaths,
            playerSurvivors=outcome.playerSurvivors,enemySurvivors=outcome.enemySurvivors,
            survivingPlayerHpRatio=outcome.survivingPlayerHpRatio,
            enemyRemainingHpRatio=outcome.enemyRemainingHpRatio
        });
    }

    private void AdvanceAbEvaluation()
    {
        abScenarioIndex++;
        if(abScenarioIndex<abEvaluationScenarios)
        {
            EndEpisode();
            return;
        }

        if(!abInferencePhase)
        {
            abInferencePhase=true;
            abScenarioIndex=0;
            GetComponent<BehaviorParameters>().BehaviorType=BehaviorType.InferenceOnly;
            Debug.Log("[ML A/B] Heuristic arm complete. Starting identical ML scenarios.");
            EndEpisode();
            return;
        }

        abEvaluationComplete=true;
        abReportPaths=AutoBattleMlAbReportWriter.Write(abReport);
        abReport.Rebuild();
        Debug.Log("[ML A/B] Complete. Heuristic win="+
                  (abReport.heuristic.winRate*100f).ToString("0.0")+"% ML win="+
                  (abReport.ml.winRate*100f).ToString("0.0")+"% HP delta="+
                  abReport.mlMinusHeuristicHpRatio.ToString("+0.000;-0.000;0.000")+
                  " time delta="+abReport.mlMinusHeuristicBattleSeconds.ToString("+0.00;-0.00;0.00")+
                  "s report="+abReportPaths);
        RestoreSessionSettings();
        EndEpisode();
    }

    private int PlaceFirstAvailableMonster()
    {
        for(int rosterIndex=0;rosterIndex<MaximumRosterSize;rosterIndex++)
        for(int tileIndex=0;tileIndex<MaximumTileCount;tileIndex++)
            if(environment.PlaceMlMonster(rosterIndex,tileIndex))
            {
                AddReward(AutoBattleMlReward.ValidPlacementReward);
                return 1;
            }
        return 0;
    }

    private void AddUnitObservation(VectorSensor sensor,AutoBattleMlUnitObservation unit,bool selected)
    {
        sensor.AddObservation(unit==null?0f:1f);
        sensor.AddObservation(unit!=null&&unit.deployed?1f:0f);
        sensor.AddObservation(unit!=null&&selected?1f:0f);
        sensor.AddObservation(unit==null?0f:Mathf.Clamp01(unit.maxHp/500f));
        sensor.AddObservation(unit==null?0f:Mathf.Clamp01(unit.damage/100f));
        sensor.AddObservation(unit==null?0f:Mathf.Clamp01(unit.attackInterval/5f));
        sensor.AddObservation(unit==null?0f:Mathf.Clamp01(unit.moveSpeed/10f));
        sensor.AddObservation(unit==null?0f:Mathf.Clamp01(unit.attackRange/8f));
        sensor.AddObservation(unit==null?0f:Mathf.Clamp01(unit.rank/3f));
        sensor.AddObservation(unit==null?0f:Mathf.Clamp01(unit.traitCount/3f));
        sensor.AddObservation(unit==null?0f:Mathf.Clamp01(unit.deploymentSlots/3f));
        sensor.AddObservation(unit==null?0f:Mathf.Clamp01(unit.monsterSerial/80f));
        AddStringOneHot(sensor,unit==null?"":unit.familyId,
            familyIds,FamilyObservationCount);
        AddStringOneHot(sensor,unit==null?"":unit.personalityId,
            PersonalityIds,PersonalityObservationCount);
        AddHashedMultiHot(sensor,unit==null?null:unit.activeTraitIds,
            TraitObservationCount);
        AddHashedMultiHot(sensor,unit==null?null:unit.itemIds,
            ItemObservationCount);
    }

    private static void AddStringOneHot(
        VectorSensor sensor,string value,IReadOnlyList<string> values,int size)
    {
        int selected=-1;
        if(!string.IsNullOrWhiteSpace(value)&&values!=null)
            for(int i=0;i<Mathf.Min(size,values.Count);i++)
                if(string.Equals(value,values[i],StringComparison.Ordinal))
                {selected=i;break;}
        for(int i=0;i<size;i++)sensor.AddObservation(i==selected?1f:0f);
    }

    private static void AddHashedMultiHot(
        VectorSensor sensor,IReadOnlyList<string> values,int size)
    {
        bool[] active=new bool[size];
        if(values!=null)
            for(int i=0;i<values.Count;i++)
                if(!string.IsNullOrWhiteSpace(values[i]))
                    active[PositiveModulo(
                        MonsterInstanceGenerator.StableSeed(values[i]),size)]=true;
        for(int i=0;i<size;i++)sensor.AddObservation(active[i]?1f:0f);
    }

    private void ApplySessionSettings()
    {
        if(sessionSettingsApplied||!highSpeedSimulation)return;
        sessionSettingsApplied=true;
        originalTimeScale=Time.timeScale;
        originalCaptureDeltaTime=Time.captureDeltaTime;
        originalRunInBackground=Application.runInBackground;
        Application.runInBackground=true;
        Time.captureDeltaTime=1f/60f;
        Time.timeScale=simulationTimeScale;
        Camera camera=Camera.main;
        if(hideRenderingDuringSimulation&&camera!=null)
        {
            originalCullingMask=camera.cullingMask;
            camera.cullingMask=0;
        }
    }

    protected override void OnDisable()
    {
        if(environment!=null)environment.SetMlEpisodeCompletedHandler(null);
        RestoreSessionSettings();
        base.OnDisable();
    }

    private void RestoreSessionSettings()
    {
        if(!sessionSettingsApplied)return;
        Time.timeScale=originalTimeScale;
        Time.captureDeltaTime=originalCaptureDeltaTime;
        Application.runInBackground=originalRunInBackground;
        Camera camera=Camera.main;
        if(camera!=null&&originalCullingMask>=0)camera.cullingMask=originalCullingMask;
        sessionSettingsApplied=false;
    }

    private static float SafeRatio(int numerator,int denominator)
    {
        return denominator<=0?0f:Mathf.Clamp01((float)numerator/denominator);
    }

    private static int PositiveModulo(int value,int divisor)
    {
        int result=value%divisor;
        return result<0?result+divisor:result;
    }

    public static Vector3Int ResolveTrainingScenario(
        int scenario,int baseSeed,int minimumWave,int maximumWave,
        int regionCount,bool missingOnly)
    {
        minimumWave=Mathf.Clamp(minimumWave,1,12);
        maximumWave=Mathf.Clamp(maximumWave,minimumWave,12);
        regionCount=Mathf.Max(1,regionCount);
        int waveCount=maximumWave-minimumWave+1;
        int difficultyCount=missingOnly?3:4;
        int slot=PositiveModulo(
            scenario,waveCount*regionCount*difficultyCount);
        int waveOffset=slot%waveCount;
        int regionIndex=slot/waveCount%regionCount;
        int difficulty=slot/(waveCount*regionCount);
        if(missingOnly)
        {
            int legacy=LegacyDifficulty(
                baseSeed,waveOffset,regionIndex,waveCount,regionCount);
            if(legacy>=0&&difficulty>=legacy)difficulty++;
        }
        return new Vector3Int(minimumWave+waveOffset,regionIndex,difficulty);
    }

    private static int LegacyDifficulty(
        int baseSeed,int waveOffset,int regionIndex,int waveCount,int regionCount)
    {
        for(int scenario=waveOffset;scenario<waveCount*regionCount;scenario+=waveCount)
        {
            int seed=unchecked(baseSeed+scenario*7919);
            if(PositiveModulo(seed,regionCount)==regionIndex)
                return PositiveModulo(seed/regionCount,4);
        }
        return -1;
    }
}
