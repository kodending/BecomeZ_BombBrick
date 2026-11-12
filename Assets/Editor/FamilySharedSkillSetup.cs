using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FamilySharedSkillSetup
{
    public const string RaptorSkillId="raptor_hunting_leap";
    public const string SabertoothSkillId=SabertoothCombatRules.SkillId;
    public const string FrogSkillId=FrogCombatRules.SkillId;
    public const string CrocodileSkillId=CrocodileCombatRules.SkillId;
    public const string KingKongSkillId=KingKongCombatRules.SkillId;
    public const string WizardMouseSkillId=WizardMouseCombatRules.SkillId;
    public const string BearSkillId=BearCombatRules.SkillId;
    public const string WolfSkillId=WolfCombatRules.SkillId;
    public const string ArachneSkillId=SpiderCombatRules.SkillId;
    public const string NinjaCatSkillId=NinjaCatCombatRules.SkillId;
    public const string TyrannoSkillId=TyrannoCombatRules.SkillId;
    public const string MonkeySkillId=MonkeyCombatRules.SkillId;
    public const string MantisSkillId=MantisCombatRules.SkillId;
    public const string MushroomSkillId=MushroomCombatRules.SkillId;
    public const string MammothSkillId=MammothCombatRules.SkillId;
    public const string BeetleSkillId=BeetleCombatRules.SkillId;
    public const string SpiritDeerSkillId=SpiritDeerCombatRules.SkillId;
    public const string GriffinSkillId=GriffinCombatRules.SkillId;
    public const string WyvernSkillId=WyvernCombatRules.SkillId;
    public const string HaetaeSkillId=HaetaeCombatRules.SkillId;
    private static readonly string[] IntrinsicTraitIds=
    {
        RaptorCombatRules.VelosTraitId,RaptorCombatRules.VelokTraitId,
        RaptorCombatRules.RaputoTraitId,RaptorCombatRules.WiloTraitId,
        SabertoothCombatRules.GeomchiTraitId,SabertoothCombatRules.SeolaTraitId,
        SabertoothCombatRules.NoxTraitId,SabertoothCombatRules.TerakanTraitId,
        FrogCombatRules.GolgaebiTraitId,FrogCombatRules.KingpaTraitId,
        FrogCombatRules.RokrokTraitId,FrogCombatRules.FroskTraitId,
        CrocodileCombatRules.AgerTraitId,CrocodileCombatRules.KimbulTraitId,
        CrocodileCombatRules.KrogalTraitId,CrocodileCombatRules.TarvilTraitId,
        KingKongCombatRules.LilasTraitId,KingKongCombatRules.MagrilTraitId,
        KingKongCombatRules.BingareuTraitId,KingKongCombatRules.MoskaTraitId,
        WizardMouseCombatRules.KongjwiTraitId,WizardMouseCombatRules.PatjwiTraitId,
        WizardMouseCombatRules.SeoljwiTraitId,WizardMouseCombatRules.ByeoljwiTraitId,
        BearCombatRules.WoongiTraitId,BearCombatRules.DamiTraitId,
        BearCombatRules.IronKumaTraitId,BearCombatRules.BerberTraitId,
        WolfCombatRules.NukguTraitId,WolfCombatRules.SeollangTraitId,
        WolfCombatRules.HongaTraitId,WolfCombatRules.WolyeongTraitId,
        SpiderCombatRules.AraneaTraitId,SpiderCombatRules.SilantiTraitId,
        SpiderCombatRules.RagonTraitId,SpiderCombatRules.IcearachneTraitId,
        NinjaCatCombatRules.ChundongiTraitId,NinjaCatCombatRules.SeolmyoTraitId,
        NinjaCatCombatRules.SundongiTraitId,NinjaCatCombatRules.HongryeonTraitId,
        TyrannoCombatRules.TinoTraitId,TyrannoCombatRules.MagnoTraitId,
        TyrannoCombatRules.IronRexTraitId,TyrannoCombatRules.BoneRexTraitId,
        MonkeyCombatRules.ChiwooTraitId,MonkeyCombatRules.TotoTraitId,
        MonkeyCombatRules.UnoTraitId,MonkeyCombatRules.MakaTraitId,
        MantisCombatRules.MantiusTraitId,MantisCombatRules.FlotisTraitId,
        MantisCombatRules.SantiusTraitId,MantisCombatRules.NectisTraitId,
        MushroomCombatRules.VersusTraitId,MushroomCombatRules.DoksirumTraitId,
        MushroomCombatRules.LumirumTraitId,MushroomCombatRules.KingmoreTraitId,
        MammothCombatRules.KogasonTraitId,MammothCombatRules.SeolgasonTraitId,
        MammothCombatRules.IrontuskTraitId,MammothCombatRules.GaiasonTraitId,
        BeetleCombatRules.JangpungTraitId,BeetleCombatRules.NoepungTraitId,
        BeetleCombatRules.AtlagonTraitId,BeetleCombatRules.SolarbeetleTraitId,
        SpiritDeerCombatRules.HolydeerTraitId,SpiritDeerCombatRules.LunadeerTraitId,
        SpiritDeerCombatRules.ForestdeerTraitId,SpiritDeerCombatRules.EclipsedeerTraitId,
        GriffinCombatRules.PoneTraitId,GriffinCombatRules.GriponasTraitId,
        GriffinCombatRules.HoloponasTraitId,GriffinCombatRules.EntPoneTraitId,
        WyvernCombatRules.VivreTraitId,WyvernCombatRules.IgvireTraitId,
        WyvernCombatRules.FrovireTraitId,WyvernCombatRules.TemvireTraitId,
        HaetaeCombatRules.HaetaTraitId,HaetaeCombatRules.YeomhaeTraitId,
        HaetaeCombatRules.GeumgangsuTraitId,HaetaeCombatRules.MukryeongTraitId
    };
    private const string GameMasterPath="Assets/Resources/CombatData/GameMasterDatabase.asset";
    private const string MonsterDatabasePath="Assets/Resources/MonsterData/MonsterDatabase.asset";

    [MenuItem("Tools/Game Database/Apply Family Shared Skills 001-080")]
    public static void Apply()
    {
        GameMasterDatabase gameMaster=AssetDatabase.LoadAssetAtPath<GameMasterDatabase>(GameMasterPath);
        MonsterDatabase monsters=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(MonsterDatabasePath);
        if(gameMaster==null||monsters==null)
            throw new InvalidOperationException("Required master database asset is missing.");

        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=RaptorSkillId,displayName="사냥 도약",behavior="AssassinLeap",
            baseCooldown=6.5f,damageMultiplier=1.75f,radiusTiles=1.5f,
            durationSeconds=0.24f,castRangeTiles=5f,
            damageFormulaNote="남은 물리 유효 체력이 가장 낮은 적에게 도약해 ATK×175%; 적중 후 해당 적을 기본 공격 대상으로 유지",
            implementationStatus="구현·이펙트 적용",enabled=true,dataVersion=4
        });
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=SabertoothSkillId,displayName="엄니 고정",behavior="FangLock",
            baseCooldown=7.5f,damageMultiplier=1.8f,durationSeconds=1.2f,
            hpScalingTrueDamage=true,
            statusEffect="Root|AttackSpeedDown",statusValue=0.3f,
            statusDurationSeconds=1.2f,castRangeTiles=1f,
            damageFormulaNote="3회 분할 총 ATK×180% 물리 피해 + 대상 MaxHP×20% 고정 피해; 1.2초 이동 고정 및 공격속도 30% 감소",
            implementationStatus="구현·이펙트 적용",enabled=true,dataVersion=5
        });
        UpsertTrait(gameMaster,SabertoothCombatRules.GeomchiTraitId,"포식 회복",
            "Skill|SingleTargetSkill|DamageSkill","엄니 고정 종료",
            "엄니 고정 중 받은 실제 피해의 35%를 종료 시 회복");
        UpsertTrait(gameMaster,SabertoothCombatRules.SeolaTraitId,"빙결 송곳니",
            "Skill|SingleTargetSkill|DamageSkill|StatusOrMarkSkill","엄니 고정 피해 적중",
            "타격마다 냉기 1중첩; 3중첩 시 1초 빙결 후 중첩 소모");
        UpsertTrait(gameMaster,SabertoothCombatRules.NoxTraitId,"야간 처형",
            "Skill|SingleTargetSkill|DamageSkill","체력 35% 이하 대상에게 엄니 고정 마지막 타격",
            "마지막 타격이 대상 방어력 60% 무시");
        UpsertTrait(gameMaster,SabertoothCombatRules.TerakanTraitId,"암석 엄니",
            "Skill|SingleTargetSkill|StatusOrMarkSkill","엄니 고정 사용",
            "붙잡는 동안 방어력 35% 증가; 종료 시 현재 방어력 60% 보호막 4초");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=FrogSkillId,displayName="혀 작살",behavior="TongueHarpoon",
            baseCooldown=7f,damageMultiplier=1.3f,secondaryDamageMultiplier=0.5f,
            durationSeconds=0.7f,statusEffect="Slow",statusValue=0.5f,
            statusDurationSeconds=4f,castRangeTiles=5f,
            damageFormulaNote="첫 적중 ATK×130% → 혀 긴장 0.12초 → 0.45초간 대상 최대 0.75타일 당김·시전자 최대 1.5타일 후방 점프 → 착지 시 ATK×50% 및 이동속도 50% 감소 4초; 경계·개체 충돌로 이동 제한",
            implementationStatus="구현·이펙트 적용",enabled=true,dataVersion=4
        });
        UpsertTrait(gameMaster,FrogCombatRules.GolgaebiTraitId,"고대 점액",
            "Ranged|Skill|SingleTargetSkill|DamageSkill|StatusOrMarkSkill","혀 작살 적중",
            "대상 방어력 20% 감소, 4초");
        UpsertTrait(gameMaster,FrogCombatRules.KingpaTraitId,"왕실 칙령",
            "Ranged|Skill|SingleTargetSkill|StatusOrMarkSkill","혀 작살 사용",
            "2.5타일 내 아군 공격속도와 상태이상 저항력 15% 증가, 3초");
        UpsertTrait(gameMaster,FrogCombatRules.RokrokTraitId,"폭발 타액",
            "Ranged|Skill|SingleTargetSkill|DamageSkill","혀 작살 회수 타격",
            "대상 주변 1.2타일에 공격력 60% 범위 피해");
        UpsertTrait(gameMaster,FrogCombatRules.FroskTraitId,"영혼 회수",
            "Ranged|Skill|SingleTargetSkill|DamageSkill","혀 작살 피해",
            "실제 피해의 30%만큼 체력 비율이 가장 낮은 아군 회복");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=CrocodileSkillId,displayName="데스 롤",behavior="DeathRoll",
            baseCooldown=8f,damageMultiplier=0.7f,secondaryDamageMultiplier=0.4f,
            durationSeconds=1f,targetMissingHpRatio=0.08f,
            statusEffect="ActionLock",statusDurationSeconds=1f,castRangeTiles=1f,
            damageFormulaNote="3회 ATK×70%/40%/40% + 마지막 타격 대상 LostHP×8%; 1초 행동 봉쇄",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,CrocodileCombatRules.AgerTraitId,"질긴 비늘",
            "Skill|SingleTargetSkill|DamageSkill|StatusOrMarkSkill","데스 롤 도중",
            "데스 롤 도중 받는 피해 35% 감소");
        UpsertTrait(gameMaster,CrocodileCombatRules.KimbulTraitId,"황금 포식",
            "Skill|SingleTargetSkill|DamageSkill","데스 롤 피해",
            "데스 롤로 가한 실제 피해의 35% 회복");
        UpsertTrait(gameMaster,CrocodileCombatRules.KrogalTraitId,"수호의 턱",
            "Skill|SingleTargetSkill|StatusOrMarkSkill","데스 롤 시작",
            "자신에게 최대 체력의 12% 보호막, 5초");
        UpsertTrait(gameMaster,CrocodileCombatRules.TarvilTraitId,"저승 낙인",
            "Skill|SingleTargetSkill|DamageSkill|StatusOrMarkSkill","데스 롤 대상 지정",
            "5초간 치유량 40% 감소; 대상 사망 시 남은 스킬 재사용 대기시간 50% 감소");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=KingKongSkillId,displayName="대지 고동",behavior="EarthPulse",
            baseCooldown=8f,damageMultiplier=1.1f,casterMaxHpRatio=0.05f,
            radiusTiles=2.2f,durationSeconds=0.6f,statusEffect="Airborne",
            statusDurationSeconds=0.6f,
            damageFormulaNote="반경 2.2타일: ATK×110% + 시전자 MaxHP×5%; 0.6초 공중 부양. 다른 아군이 부여한 구속/변위 대상은 총 피해×125%, 공중 부양 +0.4초",
            implementationStatus="구현·내려찍기 연출: 힘 모으기 0.2초→내려찍기 0.08초→접촉 유지 0.06초→복원 0.16초(기본 재생 속도). 타격 순간 개별 Hit·흰 섬광·파편 18개·진동 10px/0.22초, 줌 없음. 굴곡진 충격파가 중심→실제 반경 끝까지 0.315초에 확산, 0.42초에 소멸. 빠른 띄우기·착지; 기존 피해·반경·쿨·제어 시간 유지.",enabled=true,dataVersion=4
        });
        UpsertTrait(gameMaster,KingKongCombatRules.LilasTraitId,"왕의 위압",
            "Skill|AreaSkill|DamageSkill|StatusOrMarkSkill|KingKongFamily","대지 고동 적중",
            "적 공격력 15% 감소 4초; 적중 1명당 최대 체력 3% 보호막 5초");
        UpsertTrait(gameMaster,KingKongCombatRules.MagrilTraitId,"용암 균열",
            "Skill|AreaSkill|DamageSkill|StatusOrMarkSkill|KingKongFamily","대지 고동 착지",
            "4초간 용암 지대 생성, 매초 공격력 25% 피해. 다른 아군이 부여한 중독 또는 노출(방어력 감소)이 남아 있는 대상은 첫 용암 틱만 피해 +60%. 자신의 스킬·아이템 효과는 제외하며 이후 틱은 기본 피해. 화상 연계 생성.");
        UpsertTrait(gameMaster,KingKongCombatRules.BingareuTraitId,"빙하 고동",
            "Skill|AreaSkill|DamageSkill|StatusOrMarkSkill|KingKongFamily","대지 고동 적중",
            "이동·공격속도 25% 감소 3초, 냉기 1중첩(3중첩 시 1초 빙결). 다른 아군의 냉기가 남아 있으면 해당 타격 피해 +20%, 냉기를 소모해 기본 1.25초 빙결. 대상별 시전당 1회, 후속 파동은 재발동하지 않음. 저항·면역 적용.");
        UpsertTrait(gameMaster,KingKongCombatRules.MoskaTraitId,"생명의 진동",
            "Skill|AreaSkill|DamageSkill|HealingSkill|KingKongFamily","대지 고동 피해 확정",
            "가한 실제 피해의 25%를 주변 아군에게 나눠 회복. 수혜자 자신이 아닌 아군이 준 보호막이 남아 있으면 공격속도 +15%, 4초. 만피·실제 회복 0이어도 발동. 자신의 보호막·소진된 보호막은 제외, 재적용은 시간만 갱신.");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=WizardMouseSkillId,displayName="마력별 난사",behavior="MagicStarBarrage",
            baseCooldown=8.2f,damageMultiplier=0.42f,
            secondaryDamageMultiplier=0f,durationSeconds=0.48f,
            castRangeTiles=5.5f,
            damageFormulaNote="마력별 5개를 0.12초 간격으로 발사. 각 ATK×42%; 서로 다른 적 우선. 대상이 부족하면 같은 적에게 정상 피해로 재적중",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,WizardMouseCombatRules.KongjwiTraitId,"보은의 별빛",
            "Skill|MultiHitSkill|ProjectileSkill|HealingSkill|WizardMouseFamily","마력별 난사 종료",
            "체력 비율이 가장 낮은 아군을 ATK×60% + 서로 다른 적중 대상당 ATK×15%만큼 회복, 최대 ATK×120%");
        UpsertTrait(gameMaster,WizardMouseCombatRules.PatjwiTraitId,"심술 집중",
            "Skill|MultiHitSkill|ProjectileSkill|DamageSkill|WizardMouseFamily","마력별 난사 목표 선정",
            "모든 마력별이 체력이 가장 낮은 적에게 집중; 반복 피해 감소 제거; 마지막 마력별 피해 40% 증가");
        UpsertTrait(gameMaster,WizardMouseCombatRules.SeoljwiTraitId,"서리별",
            "Skill|MultiHitSkill|ProjectileSkill|StatusOrMarkSkill|WizardMouseFamily","마력별 적중",
            "3초간 냉기 1중첩; 3중첩 시 0.8초 빙결 후 중첩 소모");
        UpsertTrait(gameMaster,WizardMouseCombatRules.ByeoljwiTraitId,"별의 굴절",
            "Skill|MultiHitSkill|ProjectileSkill|DamageSkill|WizardMouseFamily","마력별 적중",
            "아직 맞지 않은 가장 가까운 적에게 ATK×25% 피해로 한 번 굴절");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=BearSkillId,displayName="곰의 투지",behavior="FightingSpirit",
            baseCooldown=10f,durationSeconds=BearCombatRules.ResolveDuration,
            secondaryDamageMultiplier=BearCombatRules.BasicCleaveDamageRatio,
            statusEffect="AttackUp|AttackSpeedUp|DamageReduction|BasicAttackCleave",
            statusValue=BearCombatRules.ResolveAttackDamageBonus,
            statusDurationSeconds=BearCombatRules.ResolveDuration,
            damageFormulaNote="4초간 공격력·공격속도 +20%, 받는 피해 15% 감소; 기본 공격 시 주변 적에게 ATK×30%",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,BearCombatRules.WoongiTraitId,"끈질긴 투지",
            "Skill|BuffSkill|BasicAttackEnhance|BearFamily","곰의 투지 중 기본 공격",
            "기본 공격마다 지속시간 0.25초 연장(최대 1.5초) 및 최대 체력 1% 회복");
        UpsertTrait(gameMaster,BearCombatRules.DamiTraitId,"포근한 품",
            "Skill|BuffSkill|ShieldSkill|BearFamily","곰의 투지 활성화",
            "자신에게 자신의 최대 체력 10% 보호막, 4초");
        UpsertTrait(gameMaster,BearCombatRules.IronKumaTraitId,"철갑 본능",
            "Skill|BuffSkill|BasicAttackEnhance|BearFamily","곰의 투지 지속 중",
            "방어력 35% 증가; 기본 공격에 자신의 방어력×35% 물리 피해 추가");
        UpsertTrait(gameMaster,BearCombatRules.BerberTraitId,"야성 폭주",
            "Skill|BuffSkill|BasicAttackEnhance|BearFamily","곰의 투지 중 기본 공격",
            "기본 공격마다 공격속도 6% 증가, 최대 5중첩; 투지 종료 시 초기화");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=WolfSkillId,displayName="삼면 사냥",behavior="ThreeSidedHunt",
            baseCooldown=8.5f,damageMultiplier=0.6f,
            durationSeconds=0.36f,castRangeTiles=4.5f,
            damageFormulaNote="본체와 잔상 2개가 ATK×60%로 0.18초 간격 3회 공격; 종료 후 대상 주변 빈 위치에 착지",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,WolfCombatRules.NukguTraitId,"끈질긴 추적",
            "Skill|MultiHitSkill|MovementSkill|DamageSkill|WolfFamily","삼면 사냥 종료",
            "스킬 대상에게 4초간 이동속도 30% 증가, 기본 공격 피해 15% 증가");
        UpsertTrait(gameMaster,WolfCombatRules.SeollangTraitId,"서리 발톱",
            "Skill|MultiHitSkill|StatusOrMarkSkill|WolfFamily","삼면 사냥 각 타격",
            "3초간 냉기 1중첩; 3중첩 시 0.8초 빙결");
        UpsertTrait(gameMaster,WolfCombatRules.HongaTraitId,"붉은 송곳니",
            "Skill|MultiHitSkill|DamageSkill|WolfFamily","스킬 마지막 타격 및 처치",
            "스킬 마지막 본체 타격에 대상 잃은 체력 8% 추가 피해; 처치 시 남은 스킬 쿨타임 50% 감소. 천랑백귀는 분신 8타와 본체 마무리를 한 시전으로 판정하며, 어느 타격이든 주 대상을 처치하면 환급은 시전당 1회만 적용.");
        UpsertTrait(gameMaster,WolfCombatRules.WolyeongTraitId,"월영 잔상",
            "Skill|MultiHitSkill|ShieldSkill|WolfFamily","삼면 사냥 잔상 적중",
            "잔상 1회당 자신의 최대 체력 4% 보호막 획득, 최대 8%, 4초");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=ArachneSkillId,displayName="대형 포획망",behavior="GiantCaptureWeb",
            baseCooldown=9f,damageMultiplier=1.1f,radiusTiles=1.3f,
            durationSeconds=4f,statusEffect="Slow|Root",statusValue=0.35f,
            statusDurationSeconds=1f,castRangeTiles=5f,
            damageFormulaNote="목표 지점 반경 1.3타일: 최초 ATK×110%; 4초간 이동속도 35% 감소; 0.8초 이상 머문 적 1초 속박(이동·공격·스킬 사용 불가), 대상별 1회",
            implementationStatus="구현",enabled=true,dataVersion=3
        });
        UpsertTrait(gameMaster,SpiderCombatRules.AraneaTraitId,"정교한 매듭",
            "Skill|AreaSkill|DurationSkill|StatusOrMarkSkill|ArachneFamily",
            "대형 포획망 구속","구속시간 1.6초로 증가; 구속 대상을 구역 중심으로 0.75타일 끌어당김");
        UpsertTrait(gameMaster,SpiderCombatRules.SilantiTraitId,"사막의 실",
            "Skill|AreaSkill|DurationSkill|DamageSkill|ArachneFamily",
            "포획망 내부 적","1초마다 ATK×25% 피해");
        UpsertTrait(gameMaster,SpiderCombatRules.RagonTraitId,"독기 장난",
            "Skill|AreaSkill|DurationSkill|DamageSkill|ArachneFamily",
            "포획망 내부 적 매초","독기 1중첩; 3중첩 시 ATK×70% 피해 후 중첩 소모");
        UpsertTrait(gameMaster,SpiderCombatRules.IcearachneTraitId,"빙결실",
            "Skill|AreaSkill|DurationSkill|StatusOrMarkSkill|ArachneFamily",
            "대형 포획망 적용","이동속도 감소 50%로 증가; 구속 대신 1.25초 빙결");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=NinjaCatSkillId,displayName="그림자 사냥",behavior="ShadowHunt",
            baseCooldown=8.5f,damageMultiplier=1.5f,durationSeconds=4f,
            maxStacks=2,stackBonus=0.2f,secondaryDamageMultiplier=0.2f,
            targetMissingHpRatio=0.06f,defenseIgnoreRatio=0.25f,
            statusEffect="AssassinationMark",statusValue=0.2f,
            statusDurationSeconds=4f,castRangeTiles=5f,
            damageFormulaNote="사거리 내 체력 비율이 가장 낮은 적 뒤로 순간이동; ATK×150% + 대상 잃은 체력×6%, 방어력 25% 무시; 다음 기본 공격 2회 피해 20% 증가",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,NinjaCatCombatRules.ChundongiTraitId,"빈틈 포착",
            "Skill|MovementSkill|DamageSkill|BasicAttackEnhance|NinjaCatFamily",
            "그림자 사냥·3등급 스킬 종료","기본·3등급 스킬 종료 후 평타 대기시간 초기화, 다음 평타가 공격력 60%씩 2연타로 변경. 사거리 밖이면 접근 후 공격하며 순간이동하지 않음. 다음 평타 2회 +20%를 각 타격에 소모(각 72%). 재시전 시 준비 상태만 갱신.");
        UpsertTrait(gameMaster,NinjaCatCombatRules.SeolmyoTraitId,"빙설 쿠나이",
            "Skill|MovementSkill|StatusOrMarkSkill|NinjaCatFamily",
            "그림자 사냥 적중","냉기 2중첩; 이미 이동 방해 상태라면 1초 빙결");
        UpsertTrait(gameMaster,NinjaCatCombatRules.SundongiTraitId,"독안개 암살",
            "Skill|MovementSkill|DamageOverTime|HealingReduction|NinjaCatFamily",
            "그림자 사냥 적중","3초간 매초 ATK×25% 독 피해 및 회복량 35% 감소");
        UpsertTrait(gameMaster,NinjaCatCombatRules.HongryeonTraitId,"화염 잔상",
            "Skill|MovementSkill|AreaSkill|DamageSkill|NinjaCatFamily",
            "그림자 사냥 순간이동","출발지와 도착지 주변 적에게 각각 ATK×55% 피해");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=TyrannoSkillId,displayName="폭군의 진동",behavior="TyrantTremor",
            baseCooldown=9f,damageMultiplier=1.6f,radiusTiles=2f,
            durationSeconds=0.75f,statusEffect="Knockdown",statusValue=1f,
            statusDurationSeconds=0.75f,
            damageFormulaNote="자신 주변 2타일 적에게 ATK×160% 피해 및 0.75초 넘어뜨림",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,TyrannoCombatRules.TinoTraitId,"포식 본능",
            "Skill|AreaSkill|ControlSkill|BasicAttackEnhance|TyrannoFamily",
            "폭군의 진동으로 2명 이상 적중",
            "다음 기본 공격 ATK×60% 추가 피해 및 최대 체력 5% 회복");
        UpsertTrait(gameMaster,TyrannoCombatRules.MagnoTraitId,"용암 발자국",
            "Skill|AreaSkill|DurationSkill|DamageOverTime|TyrannoFamily",
            "폭군의 진동 적중","4초간 용암 지대 생성, 매초 ATK×25% 피해");
        UpsertTrait(gameMaster,TyrannoCombatRules.IronRexTraitId,"강철 피부",
            "Skill|AreaSkill|ShieldSkill|TyrannoFamily","폭군의 진동 적중",
            "적중한 적 하나당 현재 방어력 20% 보호막 획득, 최대 60%");
        UpsertTrait(gameMaster,TyrannoCombatRules.BoneRexTraitId,"고대의 균열",
            "Skill|AreaSkill|DebuffSkill|TyrannoFamily","폭군의 진동 적중",
            "적의 방어력과 저항력을 5초간 20% 감소");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=MonkeySkillId,displayName="곡예 탄막",behavior="AcrobaticBarrage",
            baseCooldown=8f,damageMultiplier=0.45f,
            secondaryDamageMultiplier=0.45f,radiusTiles=1.5f,
            durationSeconds=MonkeyCombatRules.BarrageReleaseDuration,maxStacks=5,castRangeTiles=4.5f,
            damageFormulaNote="작은 열매 4개를 0.12초 간격으로 포물선 투척한 뒤, 0.28초 후 큰 열매를 투척; 각 ATK×45%, 마지막 착탄은 주변 1.5타일에도 동일 피해",
            implementationStatus="구현",enabled=true,dataVersion=3
        });
        UpsertTrait(gameMaster,MonkeyCombatRules.ChiwooTraitId,"정확한 손놀림",
            "Skill|MultiHitSkill|DamageSkill|MonkeyFamily",
            "곡예 탄막이 동일 대상에게 연속 적중","다음 투사체 피해 10% 증가");
        UpsertTrait(gameMaster,MonkeyCombatRules.TotoTraitId,"맹독 열매",
            "Skill|MultiHitSkill|StatusOrMarkSkill|MonkeyFamily","곡예 탄막 적중",
            "독 1중첩; 3중첩 시 ATK×70% 피해 후 소모");
        UpsertTrait(gameMaster,MonkeyCombatRules.UnoTraitId,"구름탄",
            "Skill|MultiHitSkill|MultiTargetSkill|MonkeyFamily",
            "곡예 탄막 마지막 투사체 적중","주변 적 2명에게 한 번씩 튕김");
        UpsertTrait(gameMaster,MonkeyCombatRules.MakaTraitId,"화약 주머니",
            "Skill|MultiHitSkill|AreaSkill|DamageSkill|MonkeyFamily",
            "곡예 탄막 마지막 투사체","피해 +60%, 폭발 범위 2타일로 증가");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=MantisSkillId,displayName="십자 낫바람",behavior="CrossScytheWind",
            baseCooldown=8f,damageMultiplier=0.8f,
            secondaryDamageMultiplier=0.8f,radiusTiles=1.2f,
            durationSeconds=3f,maxStacks=2,castRangeTiles=3.5f,
            statusEffect="Bleed",statusValue=0.2f,statusDurationSeconds=3f,
            damageFormulaNote="0.12초 준비 후 0.1초 간격으로 관통 낫바람 2회 발사; 각 칼날이 적을 통과할 때 ATK×80% 피해; 두 칼날 모두 적중 시 3초간 매초 ATK×20% 출혈",
            implementationStatus="구현",enabled=true,dataVersion=3
        });
        UpsertTrait(gameMaster,MantisCombatRules.MantiusTraitId,"정밀 절단",
            "Skill|MultiHitSkill|DamageSkill|MantisFamily",
            "십자 낫바람 이중 적중","두 번째 타격 피해 +40%");
        UpsertTrait(gameMaster,MantisCombatRules.FlotisTraitId,"환각 꽃가루",
            "Skill|MultiHitSkill|DebuffSkill|MantisFamily",
            "십자 낫바람 이중 적중","공격속도 20%, 이동속도 30% 감소, 4초");
        UpsertTrait(gameMaster,MantisCombatRules.SantiusTraitId,"모래 낫바람",
            "Skill|LineSkill|AreaSkill|MantisFamily","십자 낫바람 사용",
            "사거리 +1.5타일, 폭 +50%");
        UpsertTrait(gameMaster,MantisCombatRules.NectisTraitId,"사신의 재단",
            "Skill|MultiHitSkill|ExecutionSkill|MantisFamily",
            "십자 낫바람으로 적 처치","가장 가까운 적에게 60% 위력으로 한 번 재발동");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=MushroomSkillId,displayName="포자 군락",behavior="SporeColony",
            baseCooldown=9f,damageMultiplier=.2f,radiusTiles=2f,
            durationSeconds=4f,maxStacks=8,castRangeTiles=4.5f,
            damageFormulaNote="목표 도착 지점에 반경 2타일의 포자 가스를 4초간 생성한다. 0.35초 동안 퍼지고, 0.5초마다 범위 안의 적에게 ATK×20% 피해를 준다(총 8회, 최대 160%). 같은 시전자의 겹친 가스는 0.5초마다 1회만 피해를 준다. 범위 밖에서는 피해를 받지 않는다. 사거리 4.5타일, 쿨타임 9초.",
            implementationStatus="구현",enabled=true,dataVersion=3
        });
        UpsertTrait(gameMaster,MushroomCombatRules.VersusTraitId,"연쇄 발아",
            "Skill|DelayedSkill|DamageSkill|MushroomFamily",
            "동일 적에게 기본 포자 가스 반복 적중","기본 포자 가스가 같은 적에게 반복 적중하면 다음 틱 피해 +20%, 최대 +40%.");
        UpsertTrait(gameMaster,MushroomCombatRules.DoksirumTraitId,"맹독 균사",
            "Skill|DelayedSkill|DurationSkill|DamageOverTime|MushroomFamily",
            "기본 포자 가스 생성 후 1·2·3초","기본 포자 가스 생성 후 1·2·3초에 범위 안의 적에게 각각 ATK×20% 추가 독 피해. 범위를 벗어나면 피해 중단.");
        UpsertTrait(gameMaster,MushroomCombatRules.LumirumTraitId,"치유 포자",
            "Skill|AreaSkill|HealSkill|MushroomFamily",
            "기본 포자 가스 범위의 아군","기본 포자 가스 안의 아군을 0.5초마다 회복한다. 4초간 8회 모두 받으면 시전자 ATK×105% 회복. 같은 시전자의 가스 회복은 중첩되지 않는다.");
        UpsertTrait(gameMaster,MushroomCombatRules.KingmoreTraitId,"왕성한 번식",
            "Skill|DelayedSkill|SummonSkill|StatusOrMarkSkill|MushroomFamily",
            "포자 군락으로 피해","포자 군락 피해로 4초간 번식 포자 표식을 심고, 표식 중 적 사망 시 소형 버섯을 생성한다. 기본 가스의 소형 버섯은 1.2초 후 반경 1타일에 ATK×27.5% 피해. 소형 버섯은 추가 증식하지 않는다.");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=MammothSkillId,displayName="대지 돌진",behavior="EarthCharge",
            baseCooldown=10f,damageMultiplier=1.2f,
            secondaryDamageMultiplier=0.7f,radiusTiles=1.5f,
            durationSeconds=0.6f,statusEffect="DebuffImmune|Knockback|Knockdown",
            statusValue=0.75f,statusDurationSeconds=0.6f,castRangeTiles=3.5f,
            damageFormulaNote="최대 3.5타일 돌진; 경로 ATK×120%, 0.75타일 밀치기 및 0.6초 넘어뜨림; 도착 반경 1.5타일 ATK×70% 및 0.6초 넘어뜨림",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,MammothCombatRules.KogasonTraitId,"압도적 중량",
            "Skill|MovementSkill|ShieldSkill|MammothFamily",
            "대지 돌진 적중","적 하나당 최대 체력 3% 보호막 획득");
        UpsertTrait(gameMaster,MammothCombatRules.SeolgasonTraitId,"빙하의 자취",
            "Skill|MovementSkill|DurationSkill|StatusOrMarkSkill|MammothFamily",
            "대지 돌진 경로","4초간 이동속도 35% 감소 빙판 생성");
        UpsertTrait(gameMaster,MammothCombatRules.IrontuskTraitId,"강철 상아",
            "Skill|MovementSkill|DefenseScaling|MammothFamily",
            "대지 돌진 피해","자신의 방어력 100% 추가 피해");
        UpsertTrait(gameMaster,MammothCombatRules.GaiasonTraitId,"선조의 길",
            "Skill|MovementSkill|ShieldSkill|BuffSkill|MammothFamily",
            "4초간 유지되는 대지 돌진 경로에 진입한 모든 아군(본인 포함)",
            "진입 시 최대 체력 8% 보호막과 4초간 이동속도 20% 증가");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=BeetleSkillId,displayName="천공 뒤집기",behavior="SkyFlip",
            baseCooldown=8.5f,damageMultiplier=1f,secondaryDamageMultiplier=.8f,
            radiusTiles=1f,durationSeconds=1f,statusEffect="Airborne",
            statusValue=1f,statusDurationSeconds=1f,castRangeTiles=1f,
            damageFormulaNote="대상 ATK×100% 및 1초 공중 띄우기; 낙하 반경 1타일 ATK×80%",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,BeetleCombatRules.JangpungTraitId,"충각의 달인",
            "Skill|ControlSkill|AreaSkill|BeetleFamily","천공 뒤집기 낙하",
            "주변 적에게 ATK×50% 추가 피해");
        UpsertTrait(gameMaster,BeetleCombatRules.NoepungTraitId,"낙뢰 유도",
            "Skill|ControlSkill|MultiTargetSkill|BeetleFamily","천공 뒤집기로 공중에 띄움",
            "대상에게 ATK×60% 낙뢰 후 주변 적 2명에게 전달");
        UpsertTrait(gameMaster,BeetleCombatRules.AtlagonTraitId,"요새 갑각",
            "Skill|ControlSkill|DamageReduction|BeetleFamily","천공 뒤집기 사용",
            "사용 중과 종료 후 4초간 받는 피해 25% 감소");
        UpsertTrait(gameMaster,BeetleCombatRules.SolarbeetleTraitId,"태양 충각",
            "Skill|ControlSkill|DebuffSkill|BeetleFamily","천공 뒤집기 낙하 범위",
            "방어력과 저항력 5초간 15% 감소");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=SpiritDeerSkillId,displayName="정령의 성역",behavior="SpiritSanctuary",
            baseCooldown=10f,damageMultiplier=0f,secondaryDamageMultiplier=0f,
            casterMaxHpRatio=0f,targetMissingHpRatio=0f,
            radiusTiles=2f,durationSeconds=5f,statusEffect="OneShotHealCleanseZone",
            statusValue=0f,statusDurationSeconds=5f,castRangeTiles=4.5f,
            damageFormulaNote="체력 비율이 가장 낮은 아군 위치에 5초 성역; 아군은 최초 진입 시 개체별 회복과 모든 해로운 효과 제거를 1회 적용",
            implementationStatus="구현",enabled=true,dataVersion=4
        });
        UpsertTrait(gameMaster,SpiritDeerCombatRules.HolydeerTraitId,"성스러운 뿔",
            "Skill|AreaSkill|CleanseSkill|ShieldSkill|SpiritDeerFamily",
            "정령의 성역 최초 진입 아군",
            "시전자 최대 체력 8% 회복, 최대 체력 6% 보호막과 해로운 효과 면역 2초");
        UpsertTrait(gameMaster,SpiritDeerCombatRules.LunadeerTraitId,"월광 순환",
            "Skill|AreaSkill|HealSkill|SpiritDeerFamily",
            "정령의 성역 최초 진입 아군","최대 체력 3% + 잃은 체력 18% 회복");
        UpsertTrait(gameMaster,SpiritDeerCombatRules.ForestdeerTraitId,"숲의 맥동",
            "Skill|AreaSkill|BuffSkill|SpiritDeerFamily",
            "정령의 성역 최초 진입 아군",
            "최대 체력 7% 회복, 공격속도와 이동속도 12% 증가(4초)");
        UpsertTrait(gameMaster,SpiritDeerCombatRules.EclipsedeerTraitId,"식의 그림자",
            "Skill|AreaSkill|DebuffSkill|HealingReduction|SpiritDeerFamily",
            "정령의 성역 최초 진입",
            "아군은 공격력 45% 회복, 적은 회복량 25%·저항력 10% 감소(4초)");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=GriffinSkillId,displayName="천공 강하",behavior="SkyDive",
            baseCooldown=9f,damageMultiplier=1.7f,radiusTiles=1.8f,
            durationSeconds=.7f,statusEffect="Knockback|Knockdown",
            statusValue=.75f,statusDurationSeconds=.6f,
            damageFormulaNote="0.7초 대상 지정 불가; 착지 반경 1.8타일 ATK×170%",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,GriffinCombatRules.PoneTraitId,"왕조의 발톱",
            "Skill|AreaSkill|DamageSkill|GriffinFamily","천공 강하 착지 중심",
            "0.75타일 적에게 ATK×50% 추가 피해");
        UpsertTrait(gameMaster,GriffinCombatRules.GriponasTraitId,"지옥문 착지",
            "Skill|AreaSkill|DurationSkill|DamageOverTime|GriffinFamily",
            "천공 강하 착지","4초간 매초 ATK×25% 화염 지대 생성");
        UpsertTrait(gameMaster,GriffinCombatRules.HoloponasTraitId,"홀로 잔상",
            "Skill|AreaSkill|DelayedSkill|GriffinFamily",
            "천공 강하 착지 후 0.6초","같은 위치에 50% 위력 충격파 재발동");
        UpsertTrait(gameMaster,GriffinCombatRules.EntPoneTraitId,"고목의 품",
            "Skill|AreaSkill|ShieldSkill|GriffinFamily",
            "천공 강하 착지 범위의 아군","자신의 최대 체력 8% 보호막, 5초");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=WyvernSkillId,displayName="와이번 브레스",behavior="WyvernBreath",
            baseCooldown=8.5f,damageMultiplier=1.5f,radiusTiles=.6f,
            durationSeconds=.4f,castRangeTiles=5f,
            damageFormulaNote="목표 방향 5타일 직선 범위 모든 적에게 ATK×150%",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,WyvernCombatRules.VivreTraitId,"맹독 숨결",
            "Skill|LineSkill|DamageOverTime|WyvernFamily","와이번 브레스 적중",
            "4초간 매초 ATK×25% 독 피해");
        UpsertTrait(gameMaster,WyvernCombatRules.IgvireTraitId,"화염 숨결",
            "Skill|LineSkill|DurationSkill|DamageOverTime|WyvernFamily",
            "와이번 브레스 경로","4초간 매초 ATK×20% 화염 지대 생성");
        UpsertTrait(gameMaster,WyvernCombatRules.FrovireTraitId,"빙하 숨결",
            "Skill|LineSkill|StatusOrMarkSkill|WyvernFamily","와이번 브레스 적중",
            "냉기 2중첩; 이미 이동 방해 상태라면 1초 빙결");
        UpsertTrait(gameMaster,WyvernCombatRules.TemvireTraitId,"폭풍 숨결",
            "Skill|LineSkill|MultiTargetSkill|WyvernFamily","와이번 브레스 적중 대상",
            "주변 적 1명에게 ATK×50% 번개 전달");
        UpsertSkill(gameMaster,new SkillMasterRow
        {
            id=HaetaeSkillId,displayName="정의의 포효",behavior="JusticeRoar",
            baseCooldown=10f,damageMultiplier=1.2f,radiusTiles=2.5f,
            durationSeconds=4f,statusEffect="AttackDown|CleanseAll",
            statusValue=.15f,statusDurationSeconds=4f,
            damageFormulaNote="주변 적 ATK×120% 및 공격력 15% 감소; 아군 해로운 효과 모두 제거",
            implementationStatus="구현",enabled=true,dataVersion=2
        });
        UpsertTrait(gameMaster,HaetaeCombatRules.HaetaTraitId,"맑은 판결",
            "Skill|AreaSkill|CleanseSkill|HealSkill|HaetaeFamily",
            "정의의 포효로 아군 정화","해타의 ATK×50%만큼 회복");
        UpsertTrait(gameMaster,HaetaeCombatRules.YeomhaeTraitId,"화염 심판",
            "Skill|AreaSkill|DamageOverTime|HaetaeFamily","정의의 포효 적중",
            "4초간 매초 ATK×25% 화염 피해");
        UpsertTrait(gameMaster,HaetaeCombatRules.GeumgangsuTraitId,"금강 수호",
            "Skill|AreaSkill|ShieldSkill|HaetaeFamily","정의의 포효 범위의 아군",
            "해타 최대 체력 8% 보호막, 5초");
        UpsertTrait(gameMaster,HaetaeCombatRules.MukryeongTraitId,"죄업 각인",
            "Skill|AreaSkill|DebuffSkill|HealingReduction|HaetaeFamily",
            "정의의 포효 적중","회복량 40%, 받는 회복·보호막 효과 25% 감소");

        gameMaster.skills.RemoveAll(row=>row!=null&&
            (row.id=="frog_tongue_sweep"||row.id=="kingkong_ground_slam"||
             row.id=="crocodile_death_grip"||row.id=="spider_capture_web"||
             row.id=="griffin_skyfall"));

        foreach(MonsterDefinition definition in monsters.monsters.Where(item=>item!=null))
        {
            if(!int.TryParse(definition.serialNumber,out int serial)||serial<1||serial>80)continue;
            string skill=SkillForFamily(definition.familyId);
            if(string.IsNullOrEmpty(skill))continue;
            definition.skillId=skill;
            definition.intrinsicTraitId=IntrinsicTraitIds[serial-1];
            EditorUtility.SetDirty(definition);
        }
        gameMaster.skills=gameMaster.skills.OrderBy(row=>row.id,StringComparer.Ordinal).ToList();
        EditorUtility.SetDirty(gameMaster);
        AssetDatabase.SaveAssets();
        GameMasterData.ClearCache();
        Validate();
        Debug.Log("[Family Shared Skill] 001-080 applied and validated.");
    }

    [MenuItem("Tools/Game Database/Validate Family Shared Skills 001-080")]
    public static void Validate()
    {
        GameMasterDatabase gameMaster=AssetDatabase.LoadAssetAtPath<GameMasterDatabase>(GameMasterPath);
        MonsterDatabase monsters=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(MonsterDatabasePath);
        if(gameMaster==null||monsters==null)
            throw new InvalidOperationException("Required master database asset is missing.");
        ValidateSkill(gameMaster,RaptorSkillId,"AssassinLeap",6.5f,1.75f,1.5f);
        ValidateSkill(gameMaster,SabertoothSkillId,"FangLock",7.5f,1.8f,0f);
        ValidateSkill(gameMaster,FrogSkillId,"TongueHarpoon",7f,1.3f,0f);
        ValidateSkill(gameMaster,CrocodileSkillId,"DeathRoll",8f,0.7f,0f);
        ValidateSkill(gameMaster,KingKongSkillId,"EarthPulse",8f,1.1f,2.2f);
        ValidateSkill(gameMaster,WizardMouseSkillId,"MagicStarBarrage",8.2f,0.42f,0f);
        ValidateSkill(gameMaster,BearSkillId,"FightingSpirit",10f,0f,0f);
        ValidateSkill(gameMaster,WolfSkillId,"ThreeSidedHunt",8.5f,0.6f,0f);
        ValidateSkill(gameMaster,ArachneSkillId,"GiantCaptureWeb",9f,1.1f,1.8f);
        ValidateSkill(gameMaster,NinjaCatSkillId,"ShadowHunt",8.5f,1.5f,0f);
        ValidateSkill(gameMaster,TyrannoSkillId,"TyrantTremor",9f,1.6f,2f);
        ValidateSkill(gameMaster,MonkeySkillId,"AcrobaticBarrage",8f,0.45f,1.5f);
        ValidateSkill(gameMaster,MantisSkillId,"CrossScytheWind",8f,0.8f,1.2f);
        ValidateSkill(gameMaster,MushroomSkillId,"SporeColony",9f,.2f,2f);
        ValidateSkill(gameMaster,MammothSkillId,"EarthCharge",10f,1.2f,1.5f);
        ValidateSkill(gameMaster,BeetleSkillId,"SkyFlip",8.5f,1f,1f);
        ValidateSkill(gameMaster,SpiritDeerSkillId,"SpiritSanctuary",10f,0f,2f);
        ValidateSkill(gameMaster,GriffinSkillId,"SkyDive",9f,1.7f,1.8f);
        ValidateSkill(gameMaster,WyvernSkillId,"WyvernBreath",8.5f,1.5f,.6f);
        ValidateSkill(gameMaster,HaetaeSkillId,"JusticeRoar",10f,1.2f,2.5f);
        for(int serial=1;serial<=80;serial++)
        {
            string key=serial.ToString("000");
            MonsterDefinition definition=monsters.monsters.FirstOrDefault(item=>
                item!=null&&item.serialNumber==key);
            if(definition==null)throw new InvalidOperationException("Monster "+key+" is missing.");
            string expected=SkillForFamily(definition.familyId);
            if(string.IsNullOrEmpty(expected)||definition.skillId!=expected)
                throw new InvalidOperationException(key+" has an invalid family shared skill.");
            if(definition.intrinsicTraitId!=IntrinsicTraitIds[serial-1])
                throw new InvalidOperationException(key+" has an invalid intrinsic trait.");
            if(serial>=5&&!gameMaster.traits.Any(row=>row!=null&&row.enabled&&
               row.id==definition.intrinsicTraitId))
                throw new InvalidOperationException(key+" intrinsic trait master is missing.");
        }
    }

    private static void UpsertSkill(GameMasterDatabase database,SkillMasterRow value)
    {
        SkillMasterRow current=database.skills.FirstOrDefault(row=>row!=null&&row.id==value.id);
        if(current==null)database.skills.Add(value);
        else
        {
            current.displayName=value.displayName;
            current.behavior=value.behavior;
            current.baseCooldown=value.baseCooldown;
            current.damageMultiplier=value.damageMultiplier;
            current.radiusTiles=value.radiusTiles;
            current.durationSeconds=value.durationSeconds;
            current.maxStacks=value.maxStacks;
            current.stackBonus=value.stackBonus;
            current.secondaryDamageMultiplier=value.secondaryDamageMultiplier;
            current.casterMaxHpRatio=value.casterMaxHpRatio;
            current.targetCurrentHpRatio=value.targetCurrentHpRatio;
            current.targetMissingHpRatio=value.targetMissingHpRatio;
            current.hpScalingCapAttackMultiplier=value.hpScalingCapAttackMultiplier;
            current.defenseIgnoreRatio=value.defenseIgnoreRatio;
            current.hpScalingTrueDamage=value.hpScalingTrueDamage;
            current.damagePerStackMultiplier=value.damagePerStackMultiplier;
            current.statusEffect=value.statusEffect;
            current.statusValue=value.statusValue;
            current.statusDurationSeconds=value.statusDurationSeconds;
            current.castRangeTiles=value.castRangeTiles;
            current.damageFormulaNote=value.damageFormulaNote;
            current.implementationStatus=value.implementationStatus;
            current.enabled=value.enabled;
            current.dataVersion=value.dataVersion;
        }
    }

    private static void UpsertTrait(GameMasterDatabase database,string id,string name,
        string tags,string trigger,string summary)
    {
        TraitMasterRow row=database.traits.FirstOrDefault(item=>item!=null&&item.id==id);
        if(row==null){row=new TraitMasterRow();database.traits.Add(row);}
        row.id=id;row.displayName=name;row.requiredTags=tags;row.category="Intrinsic";
        row.triggerRule=trigger;row.effectSummary=summary;row.enabled=true;row.dataVersion=Mathf.Max(2,row.dataVersion);
    }

    private static void ValidateSkill(GameMasterDatabase database,string id,string behavior,
        float cooldown,float damage,float radius)
    {
        SkillMasterRow row=database.skills.FirstOrDefault(item=>item!=null&&item.id==id);
        if(row==null||!row.enabled||row.behavior!=behavior||
           !Mathf.Approximately(row.baseCooldown,cooldown)||
           !Mathf.Approximately(row.damageMultiplier,damage)||
           !Mathf.Approximately(row.radiusTiles,radius))
            throw new InvalidOperationException(id+" master row is missing or invalid.");
    }

    private static string SkillForFamily(string familyId)
    {
        if(string.Equals(familyId,"raptor",StringComparison.OrdinalIgnoreCase))return RaptorSkillId;
        if(string.Equals(familyId,"sabertooth",StringComparison.OrdinalIgnoreCase))
            return SabertoothSkillId;
        if(string.Equals(familyId,"frog",StringComparison.OrdinalIgnoreCase))
            return FrogSkillId;
        if(string.Equals(familyId,"crocodile",StringComparison.OrdinalIgnoreCase))
            return CrocodileSkillId;
        if(string.Equals(familyId,"kingkong",StringComparison.OrdinalIgnoreCase))
            return KingKongSkillId;
        if(string.Equals(familyId,"wizard_mouse",StringComparison.OrdinalIgnoreCase))
            return WizardMouseSkillId;
        if(string.Equals(familyId,"bear",StringComparison.OrdinalIgnoreCase))
            return BearSkillId;
        if(string.Equals(familyId,"wolf",StringComparison.OrdinalIgnoreCase))
            return WolfSkillId;
        if(string.Equals(familyId,"arachne",StringComparison.OrdinalIgnoreCase))
            return ArachneSkillId;
        if(string.Equals(familyId,"ninja_cat",StringComparison.OrdinalIgnoreCase))
            return NinjaCatSkillId;
        if(string.Equals(familyId,"tyranno",StringComparison.OrdinalIgnoreCase))
            return TyrannoSkillId;
        if(string.Equals(familyId,"monkey",StringComparison.OrdinalIgnoreCase))
            return MonkeySkillId;
        if(string.Equals(familyId,"mantis",StringComparison.OrdinalIgnoreCase))
            return MantisSkillId;
        if(string.Equals(familyId,"mushroom",StringComparison.OrdinalIgnoreCase))
            return MushroomSkillId;
        if(string.Equals(familyId,"mammoth",StringComparison.OrdinalIgnoreCase))
            return MammothSkillId;
        if(string.Equals(familyId,"beetle",StringComparison.OrdinalIgnoreCase))
            return BeetleSkillId;
        if(string.Equals(familyId,"spirit_deer",StringComparison.OrdinalIgnoreCase))
            return SpiritDeerSkillId;
        if(string.Equals(familyId,"griffin",StringComparison.OrdinalIgnoreCase))
            return GriffinSkillId;
        if(string.Equals(familyId,"wyvern",StringComparison.OrdinalIgnoreCase))
            return WyvernSkillId;
        if(string.Equals(familyId,"haetae",StringComparison.OrdinalIgnoreCase))
            return HaetaeSkillId;
        return string.Empty;
    }
}
