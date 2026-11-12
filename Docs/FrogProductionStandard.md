# BombBrick UltraLowSD 개구리 제작 규격

## 기준 개체

- 번호: `005`
- 이름: `골개비`
- Monster ID: `golgaebi`
- Family ID: `frog`
- 기준 FBX: `Frog_UltraLowSD_V6_ReducedEyesToes_Rigged.fbx`
- 전투 Prefab: `Assets/Resources/Enemies/GolgaebiEnemy.prefab`

## 프리팹 구조

```text
GolgaebiEnemy (전투 이동·회전 루트)
└─ Visual (FBX 축 보정 -90°, Animator)
   └─ Root
      └─ Body
```

전투 코드는 최상위 `GolgaebiEnemy`만 이동·회전한다. `Visual`의 FBX 축 보정 회전은 덮어쓰지 않는다.

## 공용 애니메이션

경로:

`Assets/Resources/Enemies/UltraLowSDFrogAnimation`

- `UltraLowSD_Frog_Idle.anim`
- `UltraLowSD_Frog_Move.anim`
- `UltraLowSD_Frog.controller`
- Controller 파라미터: `Moving` (Bool)

같은 리그를 사용하는 개구리 변형은 위 Controller와 클립을 공유한다.

## 필수 물리 본

- `Body`, `Head`
- `Arm.L`, `Hand.L`, `Arm.R`, `Hand.R`
- `UpperLeg.L`, `LowerLeg.L`, `Foot.L`
- `UpperLeg.R`, `LowerLeg.R`, `Foot.R`

사망 프록시는 12개 Rigidbody와 11개 ConfigurableJoint를 사용한다. Renderer 장식 오브젝트는 물리 본으로 취급하지 않는다.

## 자동화 메뉴

- 생성: `Tools > Monster Production > Build UltraLowSD Frog 005`
- 검증: `Tools > Monster Production > Validate UltraLowSD Frog 005`

검증 항목은 FBX 축 보정, Animator 위치, 공용 Controller, Idle·Move 바인딩, Renderer 24개, Camera·Light 미포함 여부다.
