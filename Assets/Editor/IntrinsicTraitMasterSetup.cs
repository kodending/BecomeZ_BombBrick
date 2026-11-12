using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class IntrinsicTraitMasterSetup
{
    private const string GameMasterPath="Assets/Resources/CombatData/GameMasterDatabase.asset";
    private const string MonsterDatabasePath="Assets/Resources/MonsterData/MonsterDatabase.asset";

    private sealed class Spec
    {
        public readonly string id,name,tags,trigger,summary;
        public Spec(string id,string name,string tags,string trigger,string summary)
        {
            this.id=id;this.name=name;this.tags=tags;this.trigger=trigger;this.summary=summary;
        }
    }

    private static readonly Spec[] Specs=
    {
        new Spec(RaptorCombatRules.VelosTraitId,"연속 사냥","Skill|MovementSkill|DamageSkill","사냥 도약으로 적 처치","남은 재사용 대기시간 50% 감소"),
        new Spec(RaptorCombatRules.VelokTraitId,"악마의 추격","Skill|MovementSkill|DamageSkill","체력 35% 이하 대상에게 사냥 도약","사냥 도약 피해 40% 증가"),
        new Spec(RaptorCombatRules.RaputoTraitId,"전갈 맹독","Skill|MovementSkill|DamageSkill","사냥 도약 적중","4초간 매초 공격력 12% 독 피해"),
        new Spec(RaptorCombatRules.WiloTraitId,"얼음 갑각","Skill|MovementSkill|StatusOrMarkSkill","사냥 도약 착지","최대 체력 15% 보호막 4초; 착지 범위 적 2초간 25% 둔화"),

        new Spec(SabertoothCombatRules.GeomchiTraitId,"포식 회복","Skill|SingleTargetSkill|DamageSkill","엄니 고정 종료","엄니 고정 중 받은 실제 피해의 35%를 종료 시 회복"),
        new Spec(SabertoothCombatRules.SeolaTraitId,"빙결 송곳니","Skill|SingleTargetSkill|DamageSkill|StatusOrMarkSkill","엄니 고정 피해 적중","타격마다 냉기 1중첩; 3중첩 시 1초 빙결 후 중첩 소모"),
        new Spec(SabertoothCombatRules.NoxTraitId,"야간 처형","Skill|SingleTargetSkill|DamageSkill","체력 35% 이하 대상에게 엄니 고정 마지막 타격","마지막 타격이 대상 방어력 60% 무시"),
        new Spec(SabertoothCombatRules.TerakanTraitId,"암석 엄니","Skill|SingleTargetSkill|StatusOrMarkSkill","엄니 고정 사용","붙잡는 동안 방어력 35% 증가; 종료 시 현재 방어력 60% 보호막 4초"),

        new Spec(FrogCombatRules.GolgaebiTraitId,"고대 점액","Ranged|Skill|SingleTargetSkill|DamageSkill|StatusOrMarkSkill","혀 작살 적중","대상 방어력 20% 감소, 4초"),
        new Spec(FrogCombatRules.KingpaTraitId,"왕실 칙령","Ranged|Skill|SingleTargetSkill|StatusOrMarkSkill","혀 작살 사용","2.5타일 내 아군 공격속도와 상태이상 저항력 15% 증가, 3초"),
        new Spec(FrogCombatRules.RokrokTraitId,"폭발 타액","Ranged|Skill|SingleTargetSkill|DamageSkill","혀 작살 회수 타격","대상 주변 1.2타일에 공격력 60% 범위 피해"),
        new Spec(FrogCombatRules.FroskTraitId,"영혼 회수","Ranged|Skill|SingleTargetSkill|DamageSkill","혀 작살 피해","실제 피해의 30%만큼 체력 비율이 가장 낮은 아군 회복"),

        new Spec(KingKongCombatRules.LilasTraitId,"왕의 위압","Skill|AreaSkill|DamageSkill|StatusOrMarkSkill|KingKongFamily","대지 고동 적중","적 공격력 15% 감소 4초; 적중 1명당 최대 체력 3% 보호막 5초"),
        new Spec(KingKongCombatRules.MagrilTraitId,"용암 균열","Skill|AreaSkill|DamageSkill|StatusOrMarkSkill|KingKongFamily","대지 고동 착지","4초간 용암 지대 생성, 매초 공격력 25% 피해. 다른 아군이 부여한 중독 또는 노출(방어력 감소)이 남아 있는 대상은 첫 용암 틱만 피해 +60%. 자신의 스킬·아이템 효과는 제외하며 이후 틱은 기본 피해. 화상 연계 생성."),
        new Spec(KingKongCombatRules.BingareuTraitId,"빙하 고동","Skill|AreaSkill|DamageSkill|StatusOrMarkSkill|KingKongFamily","대지 고동 적중","이동·공격속도 25% 감소 3초, 냉기 1중첩(3중첩 시 1초 빙결). 다른 아군의 냉기가 남아 있으면 해당 타격 피해 +20%, 냉기를 소모해 기본 1.25초 빙결. 대상별 시전당 1회, 후속 파동은 재발동하지 않음. 저항·면역 적용."),
        new Spec(KingKongCombatRules.MoskaTraitId,"생명의 진동","Skill|AreaSkill|DamageSkill|HealingSkill|KingKongFamily","대지 고동 피해 확정","가한 실제 피해의 25%를 주변 아군에게 나눠 회복. 수혜자 자신이 아닌 아군이 준 보호막이 남아 있으면 공격속도 +15%, 4초. 만피·실제 회복 0이어도 발동. 자신의 보호막·소진된 보호막은 제외, 재적용은 시간만 갱신."),

        new Spec(WizardMouseCombatRules.KongjwiTraitId,"보은의 별빛","Skill|MultiHitSkill|ProjectileSkill|HealingSkill|WizardMouseFamily","마력별 난사 종료","체력 비율이 가장 낮은 아군을 ATK×60% + 서로 다른 적중 대상당 ATK×15%만큼 회복, 최대 ATK×120%"),
        new Spec(WizardMouseCombatRules.PatjwiTraitId,"심술 집중","Skill|MultiHitSkill|ProjectileSkill|DamageSkill|WizardMouseFamily","마력별 난사 목표 선정","모든 마력별이 체력이 가장 낮은 적에게 집중; 반복 적중 피해 감소 제거; 마지막 마력별 피해 40% 증가"),
        new Spec(WizardMouseCombatRules.SeoljwiTraitId,"서리별","Skill|MultiHitSkill|ProjectileSkill|StatusOrMarkSkill|WizardMouseFamily","마력별 적중","3초간 냉기 1중첩; 3중첩 시 0.8초 빙결 후 중첩 소모"),
        new Spec(WizardMouseCombatRules.ByeoljwiTraitId,"별의 굴절","Skill|MultiHitSkill|ProjectileSkill|DamageSkill|WizardMouseFamily","마력별 적중","아직 맞지 않은 가장 가까운 적에게 ATK×25% 피해로 한 번 굴절; 유효 대상이 없으면 종료"),

        new Spec(JaguarCombatRules.BumiTraitId,"사냥의 반점","Skill|MovementSkill|DamageSkill","그림자 가르기 주 대상 지정","5초 표식; 이후 자신 피해 20% 증가, 처치 시 남은 스킬 재사용 대기시간 35% 감소"),
        new Spec(JaguarCombatRules.StonetoraTraitId,"암석 외피","Skill|MovementSkill|DamageSkill","그림자 가르기 적중","적중 1명당 최대 체력 8% 보호막, 최대 24%, 4초"),
        new Spec(JaguarCombatRules.MuntashaTraitId,"월식 잔영","Skill|MovementSkill|DamageSkill","그림자 가르기 사용","0.6초 후 같은 경로에 공격력 65.25% 피해, 방어력 35% 무시"),
        new Spec(JaguarCombatRules.SuntigerTraitId,"태양 각인","Skill|MovementSkill|DamageSkill","그림자 가르기 적중","3초간 총 공격력 60% 화상 피해; 중첩 없이 갱신"),

        new Spec(CrocodileCombatRules.AgerTraitId,"질긴 비늘","Skill|SingleTargetSkill|DamageSkill|StatusOrMarkSkill","데스 롤 도중","데스 롤 도중 받는 피해 35% 감소"),
        new Spec(CrocodileCombatRules.KimbulTraitId,"황금 포식","Skill|SingleTargetSkill|DamageSkill","데스 롤 피해","데스 롤로 가한 실제 피해의 35% 회복"),
        new Spec(CrocodileCombatRules.KrogalTraitId,"수호의 턱","Skill|SingleTargetSkill|StatusOrMarkSkill","데스 롤 시작","자신에게 최대 체력의 12% 보호막, 5초"),
        new Spec(CrocodileCombatRules.TarvilTraitId,"저승 낙인","Skill|SingleTargetSkill|DamageSkill|StatusOrMarkSkill","데스 롤 대상 지정","5초간 치유량 40% 감소; 대상 사망 시 남은 스킬 재사용 대기시간 50% 감소"),

        new Spec(GiraffeCombatRules.GiriniTraitId,"높은 시야","Skill|DamageSkill|StatusOrMarkSkill","높은 머리 내려치기 적중","적중 1명당 남은 스킬 재사용 대기시간 0.7초 감소"),
        new Spec(GiraffeCombatRules.GeriJacksonTraitId,"문워크","Skill|DamageSkill|StatusOrMarkSkill","높은 머리 내려치기 사용","공격 지점 반대 방향으로 1타일 후퇴; 공격속도 15% 증가, 3초"),
        new Spec(GiraffeCombatRules.SerapiTraitId,"영혼의 잔상","Skill|DamageSkill|StatusOrMarkSkill","높은 머리 내려치기 사용","1초 후 같은 범위에 공격력 44% 추가 피해"),
        new Spec(GiraffeCombatRules.ToyrafTraitId,"블록 조립","Skill|DamageSkill|StatusOrMarkSkill","높은 머리 내려치기 사용","세 번째 시전의 피해와 범위 35% 증가 후 초기화"),

        new Spec(BearCombatRules.WoongiTraitId,"끈질긴 투지","Skill|BuffSkill|BasicAttackEnhance|BearFamily","곰의 투지 중 기본 공격","기본 공격마다 지속시간 0.25초 연장(최대 1.5초) 및 최대 체력 1% 회복"),
        new Spec(BearCombatRules.DamiTraitId,"포근한 품","Skill|BuffSkill|ShieldSkill|BearFamily","곰의 투지 활성화","자신에게 자신의 최대 체력 10% 보호막, 4초"),
        new Spec(BearCombatRules.IronKumaTraitId,"철갑 본능","Skill|BuffSkill|BasicAttackEnhance|BearFamily","곰의 투지 지속 중","방어력 35% 증가; 기본 공격에 자신의 방어력×35% 물리 피해 추가"),
        new Spec(BearCombatRules.BerberTraitId,"야성 폭주","Skill|BuffSkill|BasicAttackEnhance|BearFamily","곰의 투지 중 기본 공격","기본 공격마다 공격속도 6% 증가, 최대 5중첩; 투지 종료 시 초기화"),

        new Spec(WolfCombatRules.NukguTraitId,"끈질긴 추적","Skill|MultiHitSkill|MovementSkill|DamageSkill|WolfFamily","삼면 사냥 종료","스킬 대상에게 4초간 이동속도 30% 증가, 기본 공격 피해 15% 증가"),
        new Spec(WolfCombatRules.SeollangTraitId,"서리 발톱","Skill|MultiHitSkill|StatusOrMarkSkill|WolfFamily","삼면 사냥 각 타격","3초간 냉기 1중첩; 3중첩 시 0.8초 빙결"),
        new Spec(WolfCombatRules.HongaTraitId,"붉은 송곳니","Skill|MultiHitSkill|DamageSkill|WolfFamily","스킬 마지막 타격 및 처치","스킬 마지막 본체 타격에 대상 잃은 체력 8% 추가 피해; 처치 시 남은 스킬 쿨타임 50% 감소. 천랑백귀는 분신 8타와 본체 마무리를 한 시전으로 판정하며, 어느 타격이든 주 대상을 처치하면 환급은 시전당 1회만 적용."),
        new Spec(WolfCombatRules.WolyeongTraitId,"월영 잔상","Skill|MultiHitSkill|ShieldSkill|WolfFamily","삼면 사냥 잔상 적중","잔상 1회당 자신의 최대 체력 4% 보호막 획득, 최대 8%, 4초"),

        new Spec(SpiderCombatRules.AraneaTraitId,"정교한 매듭","Skill|AreaSkill|DurationSkill|StatusOrMarkSkill|ArachneFamily","대형 포획망 구속","구속시간 1.6초로 증가; 구속 대상을 구역 중심으로 0.75타일 끌어당김"),
        new Spec(SpiderCombatRules.SilantiTraitId,"사막의 실","Skill|AreaSkill|DurationSkill|DamageSkill|ArachneFamily","포획망 내부 적","1초마다 공격력 25% 피해"),
        new Spec(SpiderCombatRules.RagonTraitId,"독기 장난","Skill|AreaSkill|DurationSkill|DamageSkill|ArachneFamily","포획망 내부 적 매초","독기 1중첩; 3중첩 시 공격력 70% 피해 후 중첩 소모"),
        new Spec(SpiderCombatRules.IcearachneTraitId,"빙결실","Skill|AreaSkill|DurationSkill|StatusOrMarkSkill|ArachneFamily","대형 포획망 적용","이동속도 감소 50%로 증가; 구속 대신 1.25초 빙결"),

        new Spec(NinjaCatCombatRules.ChundongiTraitId,"빈틈 포착","Skill|MovementSkill|DamageSkill|BasicAttackEnhance|NinjaCatFamily","그림자 사냥·3등급 스킬 종료","기본·3등급 스킬 종료 후 평타 대기시간 초기화, 다음 평타가 공격력 60%씩 2연타로 변경. 사거리 밖이면 접근 후 공격하며 순간이동하지 않음. 다음 평타 2회 +20%를 각 타격에 소모(각 72%). 재시전 시 준비 상태만 갱신."),
        new Spec(NinjaCatCombatRules.SeolmyoTraitId,"빙설 쿠나이","Skill|MovementSkill|StatusOrMarkSkill|NinjaCatFamily","그림자 사냥 적중","냉기 2중첩; 이미 이동 방해 상태라면 1초 빙결"),
        new Spec(NinjaCatCombatRules.SundongiTraitId,"독안개 암살","Skill|MovementSkill|DamageOverTime|HealingReduction|NinjaCatFamily","그림자 사냥 적중","3초간 매초 ATK×25% 독 피해 및 회복량 35% 감소"),
        new Spec(NinjaCatCombatRules.HongryeonTraitId,"화염 잔상","Skill|MovementSkill|AreaSkill|DamageSkill|NinjaCatFamily","그림자 사냥 순간이동","출발지와 도착지 주변 적에게 각각 ATK×55% 피해"),

        new Spec(TyrannoCombatRules.TinoTraitId,"포식 본능","Skill|AreaSkill|ControlSkill|BasicAttackEnhance|TyrannoFamily","폭군의 진동으로 2명 이상 적중","다음 기본 공격 ATK×60% 추가 피해 및 최대 체력 5% 회복"),
        new Spec(TyrannoCombatRules.MagnoTraitId,"용암 발자국","Skill|AreaSkill|DurationSkill|DamageOverTime|TyrannoFamily","폭군의 진동 적중","4초간 용암 지대 생성, 매초 ATK×25% 피해"),
        new Spec(TyrannoCombatRules.IronRexTraitId,"강철 피부","Skill|AreaSkill|ShieldSkill|TyrannoFamily","폭군의 진동 적중","적중한 적 하나당 현재 방어력 20% 보호막 획득, 최대 60%"),
        new Spec(TyrannoCombatRules.BoneRexTraitId,"고대의 균열","Skill|AreaSkill|DebuffSkill|TyrannoFamily","폭군의 진동 적중","적의 방어력과 저항력을 5초간 20% 감소"),

        new Spec(MonkeyCombatRules.ChiwooTraitId,"정확한 손놀림","Skill|MultiHitSkill|DamageSkill|MonkeyFamily","곡예 탄막이 동일 대상에게 연속 적중","다음 투사체 피해 10% 증가"),
        new Spec(MonkeyCombatRules.TotoTraitId,"맹독 열매","Skill|MultiHitSkill|StatusOrMarkSkill|MonkeyFamily","곡예 탄막 적중","독 1중첩; 3중첩 시 ATK×70% 피해 후 소모"),
        new Spec(MonkeyCombatRules.UnoTraitId,"구름탄","Skill|MultiHitSkill|MultiTargetSkill|MonkeyFamily","곡예 탄막 마지막 투사체 적중","주변 적 2명에게 한 번씩 튕김"),
        new Spec(MonkeyCombatRules.MakaTraitId,"화약 주머니","Skill|MultiHitSkill|AreaSkill|DamageSkill|MonkeyFamily","곡예 탄막 마지막 투사체","피해 +60%, 폭발 범위 2타일로 증가"),

        new Spec(MantisCombatRules.MantiusTraitId,"정밀 절단","Skill|MultiHitSkill|DamageSkill|MantisFamily","십자 낫바람 이중 적중","두 번째 타격 피해 +40%"),
        new Spec(MantisCombatRules.FlotisTraitId,"환각 꽃가루","Skill|MultiHitSkill|DebuffSkill|MantisFamily","십자 낫바람 이중 적중","공격속도 20%, 이동속도 30% 감소, 4초"),
        new Spec(MantisCombatRules.SantiusTraitId,"모래 낫바람","Skill|LineSkill|AreaSkill|MantisFamily","십자 낫바람 사용","사거리 +1.5타일, 폭 +50%"),
        new Spec(MantisCombatRules.NectisTraitId,"사신의 재단","Skill|MultiHitSkill|ExecutionSkill|MantisFamily","십자 낫바람으로 적 처치","가장 가까운 적에게 60% 위력으로 한 번 재발동"),

        new Spec(MushroomCombatRules.VersusTraitId,"연쇄 발아","Skill|DelayedSkill|DamageSkill|MushroomFamily","동일 적에게 기본 포자 가스 반복 적중","기본 포자 가스가 같은 적에게 반복 적중하면 다음 틱 피해 +20%, 최대 +40%."),
        new Spec(MushroomCombatRules.DoksirumTraitId,"맹독 균사","Skill|DelayedSkill|DurationSkill|DamageOverTime|MushroomFamily","기본 포자 가스 생성 후 1·2·3초","기본 포자 가스 생성 후 1·2·3초에 범위 안의 적에게 각각 ATK×20% 추가 독 피해. 범위를 벗어나면 피해 중단."),
        new Spec(MushroomCombatRules.LumirumTraitId,"치유 포자","Skill|AreaSkill|HealSkill|MushroomFamily","기본 포자 가스 범위의 아군","기본 포자 가스 안의 아군을 0.5초마다 회복한다. 4초간 8회 모두 받으면 시전자 ATK×105% 회복. 같은 시전자의 가스 회복은 중첩되지 않는다."),
        new Spec(MushroomCombatRules.KingmoreTraitId,"왕성한 번식","Skill|DelayedSkill|SummonSkill|StatusOrMarkSkill|MushroomFamily","포자 군락으로 피해","포자 군락 피해로 4초간 번식 포자 표식을 심고, 표식 중 적 사망 시 소형 버섯을 생성한다. 기본 가스의 소형 버섯은 1.2초 후 반경 1타일에 ATK×27.5% 피해. 소형 버섯은 추가 증식하지 않는다."),

        new Spec(MammothCombatRules.KogasonTraitId,"압도적 중량","Skill|MovementSkill|ShieldSkill|MammothFamily","대지 돌진 적중","적 하나당 최대 체력 3% 보호막 획득"),
        new Spec(MammothCombatRules.SeolgasonTraitId,"빙하의 자취","Skill|MovementSkill|DurationSkill|StatusOrMarkSkill|MammothFamily","대지 돌진 경로","4초간 이동속도 35% 감소 빙판 생성"),
        new Spec(MammothCombatRules.IrontuskTraitId,"강철 상아","Skill|MovementSkill|DefenseScaling|MammothFamily","대지 돌진 피해","자신의 방어력 100% 추가 피해"),
        new Spec(MammothCombatRules.GaiasonTraitId,"선조의 길","Skill|MovementSkill|ShieldSkill|BuffSkill|MammothFamily","대지 돌진 경로의 아군","최대 체력 8% 보호막과 4초간 이동속도 20% 증가"),

        new Spec(BeetleCombatRules.JangpungTraitId,"충각의 달인","Skill|ControlSkill|AreaSkill|BeetleFamily","천공 뒤집기 낙하","주변 적에게 ATK×50% 추가 피해"),
        new Spec(BeetleCombatRules.NoepungTraitId,"낙뢰 유도","Skill|ControlSkill|MultiTargetSkill|BeetleFamily","천공 뒤집기로 공중에 띄움","대상에게 ATK×60% 낙뢰 후 주변 적 2명에게 전달"),
        new Spec(BeetleCombatRules.AtlagonTraitId,"요새 갑각","Skill|ControlSkill|DamageReduction|BeetleFamily","천공 뒤집기 사용","사용 중과 종료 후 4초간 받는 피해 25% 감소"),
        new Spec(BeetleCombatRules.SolarbeetleTraitId,"태양 충각","Skill|ControlSkill|DebuffSkill|BeetleFamily","천공 뒤집기 낙하 범위","방어력과 저항력 5초간 15% 감소"),

        new Spec(SpiritDeerCombatRules.HolydeerTraitId,"성스러운 뿔","Skill|AreaSkill|CleanseSkill|ShieldSkill|SpiritDeerFamily","정령의 성역 최초 진입 아군","시전자 최대 체력 8% 회복, 최대 체력 6% 보호막과 해로운 효과 면역 2초"),
        new Spec(SpiritDeerCombatRules.LunadeerTraitId,"월광 순환","Skill|AreaSkill|HealSkill|SpiritDeerFamily","정령의 성역 최초 진입 아군","최대 체력 3% + 잃은 체력 18% 회복"),
        new Spec(SpiritDeerCombatRules.ForestdeerTraitId,"숲의 맥동","Skill|AreaSkill|BuffSkill|SpiritDeerFamily","정령의 성역 최초 진입 아군","최대 체력 7% 회복, 공격속도와 이동속도 12% 증가(4초)"),
        new Spec(SpiritDeerCombatRules.EclipsedeerTraitId,"식의 그림자","Skill|AreaSkill|DebuffSkill|HealingReduction|SpiritDeerFamily","정령의 성역 최초 진입","아군은 공격력 45% 회복, 적은 회복량 25%·저항력 10% 감소(4초)"),

        new Spec(GriffinCombatRules.PoneTraitId,"왕조의 발톱","Skill|AreaSkill|DamageSkill|GriffinFamily","천공 강하 착지 중심","0.75타일 적에게 ATK×50% 추가 피해"),
        new Spec(GriffinCombatRules.GriponasTraitId,"지옥문 착지","Skill|AreaSkill|DurationSkill|DamageOverTime|GriffinFamily","천공 강하 착지","4초간 매초 ATK×25% 화염 지대 생성"),
        new Spec(GriffinCombatRules.HoloponasTraitId,"홀로 잔상","Skill|AreaSkill|DelayedSkill|GriffinFamily","천공 강하 착지 후 0.6초","같은 위치에 50% 위력 충격파 재발동"),
        new Spec(GriffinCombatRules.EntPoneTraitId,"고목의 품","Skill|AreaSkill|ShieldSkill|GriffinFamily","천공 강하 착지 범위의 아군","자신의 최대 체력 8% 보호막, 5초"),

        new Spec(WyvernCombatRules.VivreTraitId,"맹독 숨결","Skill|LineSkill|DamageOverTime|WyvernFamily","와이번 브레스 적중","4초간 매초 ATK×25% 독 피해"),
        new Spec(WyvernCombatRules.IgvireTraitId,"화염 숨결","Skill|LineSkill|DurationSkill|DamageOverTime|WyvernFamily","와이번 브레스 경로","4초간 매초 ATK×20% 화염 지대 생성"),
        new Spec(WyvernCombatRules.FrovireTraitId,"빙하 숨결","Skill|LineSkill|StatusOrMarkSkill|WyvernFamily","와이번 브레스 적중","냉기 2중첩; 이미 이동 방해 상태라면 1초 빙결"),
        new Spec(WyvernCombatRules.TemvireTraitId,"폭풍 숨결","Skill|LineSkill|MultiTargetSkill|WyvernFamily","와이번 브레스 적중 대상","주변 적 1명에게 ATK×50% 번개 전달"),

        new Spec(HaetaeCombatRules.HaetaTraitId,"맑은 판결","Skill|AreaSkill|CleanseSkill|HealSkill|HaetaeFamily","정의의 포효로 아군 정화","해타의 ATK×50%만큼 회복"),
        new Spec(HaetaeCombatRules.YeomhaeTraitId,"화염 심판","Skill|AreaSkill|DamageOverTime|HaetaeFamily","정의의 포효 적중","4초간 매초 ATK×25% 화염 피해"),
        new Spec(HaetaeCombatRules.GeumgangsuTraitId,"금강 수호","Skill|AreaSkill|ShieldSkill|HaetaeFamily","정의의 포효 범위의 아군","해타 최대 체력 8% 보호막, 5초"),
        new Spec(HaetaeCombatRules.MukryeongTraitId,"죄업 각인","Skill|AreaSkill|DebuffSkill|HealingReduction|HaetaeFamily","정의의 포효 적중","회복량 40%, 받는 회복·보호막 효과 25% 감소")
    };

    [MenuItem("Tools/Game Database/Apply Active Intrinsic Trait Master")]
    public static void Apply()
    {
        GameMasterDatabase gameMaster=AssetDatabase.LoadAssetAtPath<GameMasterDatabase>(GameMasterPath);
        MonsterDatabase monsters=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(MonsterDatabasePath);
        if(gameMaster==null)throw new InvalidOperationException("GameMasterDatabase is missing.");
        if(monsters==null)throw new InvalidOperationException("MonsterDatabase is missing.");
        HashSet<string> activeTraitIds=new HashSet<string>(
            monsters.monsters
                .Where(monster=>monster!=null&&monster.enabled)
                .Select(monster=>monster.intrinsicTraitId)
                .Where(id=>!string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        foreach(Spec spec in Specs.Where(spec=>activeTraitIds.Contains(spec.id)))
            Upsert(gameMaster,spec);
        gameMaster.traits.RemoveAll(row=>
            row!=null&&
            string.Equals(
                row.category,"Intrinsic",StringComparison.OrdinalIgnoreCase)&&
            !activeTraitIds.Contains(row.id));

        gameMaster.traits=gameMaster.traits.Where(row=>row!=null)
            .OrderBy(row=>row.id,StringComparer.Ordinal).ToList();
        EditorUtility.SetDirty(gameMaster);
        AssetDatabase.SaveAssets();
        GameMasterData.ClearCache();
        Validate();
        Debug.Log("[Intrinsic Trait Master] Active roster traits applied.");
    }

    [MenuItem("Tools/Game Database/Validate Active Intrinsic Trait Master")]
    public static void Validate()
    {
        GameMasterDatabase gameMaster=AssetDatabase.LoadAssetAtPath<GameMasterDatabase>(GameMasterPath);
        MonsterDatabase monsters=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(MonsterDatabasePath);
        if(gameMaster==null||monsters==null)throw new InvalidOperationException("Master database is missing.");
        HashSet<string> implemented=new HashSet<string>(
            Specs.Select(spec=>spec.id),StringComparer.Ordinal);
        HashSet<string> expected=new HashSet<string>(
            monsters.monsters
                .Where(monster=>monster!=null&&monster.enabled)
                .Select(monster=>monster.intrinsicTraitId)
                .Where(id=>!string.IsNullOrWhiteSpace(id)&&implemented.Contains(id)),
            StringComparer.Ordinal);
        int actual=gameMaster.traits.Count(row=>row!=null&&row.enabled&&
            string.Equals(row.category,"Intrinsic",StringComparison.OrdinalIgnoreCase)&&
            expected.Contains(row.id));
        if(actual!=expected.Count)throw new InvalidOperationException("Intrinsic trait master rows are incomplete.");
        foreach(MonsterDefinition monster in monsters.monsters)
        {
            if(monster==null||!monster.enabled||
               string.IsNullOrWhiteSpace(monster.serialNumber))continue;
            if(implemented.Contains(monster.intrinsicTraitId)&&
               !expected.Contains(monster.intrinsicTraitId))
                throw new InvalidOperationException(monster.serialNumber+" intrinsic trait is not mastered.");
        }
    }

    private static void Upsert(GameMasterDatabase database,Spec spec)
    {
        TraitMasterRow row=database.traits.FirstOrDefault(item=>item!=null&&item.id==spec.id);
        if(row==null){row=new TraitMasterRow();database.traits.Add(row);}
        row.id=spec.id;row.displayName=spec.name;row.requiredTags=spec.tags;
        row.category="Intrinsic";row.triggerRule=spec.trigger;row.effectSummary=spec.summary;
        row.enabled=true;row.dataVersion=Mathf.Max(2,row.dataVersion);
    }
}
