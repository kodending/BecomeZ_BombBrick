# BombBrick UltraLowSD 랩터 제작 규격

## 1. 기본 원칙

- 001~004와 같은 랩터 계열 몬스터는 하나의 리그와 공용 애니메이션을 사용한다.
- 외형 차이는 리그를 변경하지 않고 본에 종속된 MeshRenderer 오브젝트로 제작한다.
- 영구 능력치와 전투 데이터는 FBX가 아니라 `Monster_Master`에서 관리한다.
- 기존 Monster ID와 전투 Prefab 경로를 유지해 저장 데이터와 Wave 데이터를 보호한다.

## 2. 표준 리그

기준 FBX:

`Assets/Art/Models/RaptorPrototypes/UltraLowSD/Raptor_UltraLowSD_V1_Rig_Rebuilt_Eyebrows.fbx`

전투 Prefab은 회전값이 0인 전투 루트 아래에 FBX `Visual`을 둔다. Animator는 `Visual`에 있으며 애니메이션 바인딩은 `Root` 아래에서 시작한다. 전투 루트만 이동·회전시키고 `Visual`의 FBX 축 보정 회전값은 덮어쓰지 않는다.

```text
Combat Prefab Root (이동·회전)
└─ Visual (FBX 축 보정 -90°, Animator)
   └─ Root (공용 애니메이션 바인딩 시작)
```

필수 리그 계층:

```text
Root
└─ Body
   ├─ Arm.L
   │  └─ Hand.L
   ├─ Arm.R
   │  └─ Hand.R
   ├─ Neck
   │  └─ Head
   │     ├─ Eye.L
   │     ├─ Eye.R
   │     └─ Jaw
   ├─ Tail.01
   │  └─ Tail.02
   │     └─ Tail.03
   ├─ Thigh.L
   │  └─ Shin.L
   │     └─ Foot.L
   └─ Thigh.R
      └─ Shin.R
         └─ Foot.R
```

같은 계열의 모든 FBX는 다음 항목이 기준 FBX와 정확히 같아야 한다.

- 리그 이름
- 부모·자식 관계
- Local Position
- Local Rotation
- Local Scale

외형용 Renderer 개수와 이름은 달라도 된다.

## 3. FBX 출력 규격

- ARMATURE와 MESH만 출력
- Apply Unit Scale 사용
- Forward: `-Z`
- Up: `Y`
- Leaf Bones 비활성화
- 애니메이션 Action은 FBX에 포함하지 않음
- Camera와 Light 제외
- 리그 Rest Pose 상태로 출력

## 4. Unity Import 규격

- Animation Type: Generic
- Avatar Definition: No Avatar
- Import Animation: Off
- Optimize Game Objects: Off
- Import Cameras: Off
- Import Lights: Off
- Global Scale: 1

애니메이션은 FBX 내부 클립을 사용하지 않고 공용 외부 클립을 사용한다.

## 5. 공용 애니메이션

경로:

`Assets/Resources/Enemies/UltraLowSDRaptorAnimation`

공유 자산:

- `UltraLowSD_Raptor_Idle.anim`
- `UltraLowSD_Raptor_Move.anim`
- `UltraLowSD_Raptor.controller`

Controller 파라미터:

- `Moving` (Bool)

모든 랩터 전투 Prefab은 위 Controller 하나를 참조하고 Animator Culling Mode를 `Always Animate`로 사용한다.

## 6. 현재 001~004 매핑

| 번호 | Monster ID | FBX | 전투 Prefab |
|---|---|---|---|
| 001 | `raptor_reference_palette_v2` | `Raptor_UltraLowSD_V1_Rig_Rebuilt_Eyebrows.fbx` | `RaptorReferencePaletteV2Enemy.prefab` |
| 002 | `velok` | `DemonRaptor_UltraLowSD_V2_Overlord_Rigged.fbx` | `VelokEnemy.prefab` |
| 003 | `raputo` | `DesertRaptor_UltraLowSD_V1_SunScorpion_Rigged.fbx` | `RaputoEnemy.prefab` |
| 004 | `wilo` | `IceRaptor_UltraLowSD_V4_CrackedShell_Rigged.fbx` | `WiloEnemy.prefab` |

## 7. 자동 생성과 검증

Unity 메뉴:

- `Tools > Monster Production > Build UltraLowSD 001-004`
- `Tools > Monster Production > Validate UltraLowSD 001-004`

Build 도구는 다음 작업을 수행한다.

1. 네 FBX의 리그와 Rest Pose 비교
2. FBX Import 설정 통일
3. 공용 Idle·Move 클립과 Controller 생성
4. 기존 001~004 전투 Prefab의 모델 교체

검증 조건:

- 필수 리그 노드와 Rest Pose 일치
- 기준 외형 Renderer 누락 없음
- Material 누락 없음
- Camera와 Light 없음
- `Visual`에 Animator가 정확히 1개 존재
- Prefab 루트 회전은 0이고 `Visual`의 FBX 축 보정 회전은 보존
- 네 Prefab이 동일한 공용 Controller 사용
- Idle·Move의 모든 바인딩 경로 해석 성공
- Animator Culling Mode가 `Always Animate`

## 8. 수동 확인

- Idle 한 주기 동안 몸통과 꼬리 연결부가 벌어지지 않는다.
- Move 한 주기 동안 양쪽 다리가 반대 위상으로 움직인다.
- 장식 Mesh가 연결된 본을 정확히 따라간다.
- 모델이 바닥에 뜨거나 지나치게 파묻히지 않는다.
- 전투 카메라에서 001~004 크기와 방향이 일관적이다.
