# 와이번 4등급 — 천룡의 숨결

기존 용혈 각성을 교체한다. 저장 ID `trait_transcend_wyvern_dragon_blood_awakening`과 `Rank4Transcendence` 분류는 유지한다.

## 동작

- 2026-10-28 규칙: 4등급에서는 1~3등급 스킬을 대체해 천룡의 숨결만 기존 스킬 쿨타임마다 사용한다. 기존 스킬 종료 뒤에 덧붙이는 후속기가 아니다. 기본 공격은 유지한다.
- 무대 컷씬은 웨이브마다 몬스터당 최초 1회만 재생한다. 이후 쿨타임 사용은 인게임 연출만 재생한다. 아군·적군에 동일하게 적용한다.
- 0.00~0.90초: 양 날개와 긴 꼬리가 있는 거대한 용 그림자가 화면 아래→위로 통과한다. 128픽셀 논리 해상도, 반투명 실루엣이다. 배경을 포함한 화면 전체를 가로지르며 화면 밖 아래에서 들어와 위로 빠져나간다. 그림자에는 전투 영역 마스크를 적용하지 않는다.
- 0.90~1.10초: 짧은 정적.
- 1.10~1.65초: 중앙의 큰 브레스 덩어리 하나가 가속 낙하한다. 작은 착지 그림자가 점점 커져 전투 영역 모서리까지 덮는다. 서로 겹치는 입자로 덩어리와 짧은 꼬리를 구성하고, 본체의 밑부분이 착지점에 닿도록 배치한다.
- 1.65초: 덩어리가 사방으로 터지며 영역 안 모든 공격 가능한 적에게 공격력 300% 피해 한 번. 아군·보관 중인 개체·죽은 개체·영역 밖의 적은 제외한다. 추가 상태 이상은 없다.
- 착지 시 카메라 12픽셀 진동 0.26초. 줌 변화 없이 기존 무거운 지면 타격 진동을 재사용한다.
- 착지 후 0.15초 즈음 중심부가 걷히고, 낮게 퍼지는 숨결과 가장자리 파편이 0.55초 안에 사라진다.
- 기존 4등급의 추가 관통 피해와 평타 3회마다 범위 폭발은 사용하지 않는다.
- 일시정지 때 전투 시계가 멈춘다. 확정된 시전은 시전자 사망·기절·빙결에도 끝까지 유지한다. 전투 재시작과 다음 웨이브 전에는 남은 효과를 정리한다.

## 구현과 검증

아래는 각 작성 시점의 과거 검증 기록이다. 현재 시전 규칙은 위의 2026-10-28 동작을 따른다. Google 마스터는 이번 변경에서 수정하지 않았다.

### 덩어리 낙하 개정 (최신)

- 용 그림자 통과 0.6→0.9초, 덩어리 낙하 0.15→0.55초. 착지 그림자는 기존 박격포 그림자를 재사용하며 전투 영역으로 잘라낸다. 착지 순간 바닥빛과 중심 폭발, 0.1초 동안 전역으로 퍼지는 입자를 표시한다.
- 3등급 낙하 브레스의 `SetRollPose` 기울기를 제거했다. 실제 전체 시전에서 최대 기울기 0도 확인. 폭압 브레스의 별도 자세는 그대로다.
- 최종 관련 EditMode 테스트 20/20 통과 (`05393c8c0d4f497a9a45735e1d30beb7`). 실제 기본/폭압/낙하 종료 후 효과의 99번째 프레임(60fps, 1.65초)에 피해가 1회 들어가는 것을 확인했다. 기본 경로에서 진동 강도 0→12가 같은 프레임에 발생했다.
- 최종 캡처: `Logs/WyvernHeaven/v4_s1_078.png`, `v4_s1_098.png`, `v4_s1_102.png`. 기록: `capture-v4-results.txt`. 플레이 종료 후 원래 설정 복원, 씬 dirty=false, 콘솔 경고/오류 0.
- Google `Trait_Master!F201,H201`, `Localization_Master!D1181:E1181,I1181` 총 5셀 수정. Trait 버전 4, 설명 번역 버전 3. 재조회 값 일치, 기존 형식과 유효성 검사 보존, 두 시트의 실제 표시 확인 완료.

### 최초 구현 검증 기록

- `MonsterAutoBattleWyvernHeavenBreath.cs`: 완료 후 1회 예약, 착지 피해와 카메라 이벤트, 정리.
- `PixelWyvernHeavenBreath.cs`: 전투 시계 기반 그림자 투영, 낙하 입자, 바닥빛과 가장자리 파편.
- `BattlefieldProjection.shader`: 바닥빛에만 전투 영역 마스크를 적용한다. 그림자는 카메라를 향한 별도 투영으로 전체 화면을 통과한다.
- EditMode 와이번·4등급 규칙 테스트 63/63 통과. 작업 ID `8e19fe17d8b44bce9ab12d842f418f68`.
- 실제 네 개체 시전 경로(기본/폭압/낙하/과열)에서 캡처. 효과 시작 후 57번째 프레임(60fps, 0.95초)에 원거리 적 체력 감소와 진동 강도 12가 함께 발생했다. `Logs/WyvernHeaven/capture-results.txt`.
- 이미지: `Logs/WyvernHeaven/s0_012.png` 용 그림자, `s1_052.png` 낙하, `s1_057.png` 착지, `s1_066.png` 잔류 입자.
- 콘솔 오류/경고 없음. 플레이 모드 종료, 원래 테스트 설정과 timeScale 1 복원. 씬 변경 저장 없음.

## 데이터

로컬 Trait 버전 3, 이름/설명 번역 버전 2. Google DB의 다음 10셀 수정 후 재조회 일치 확인. 기존 형식·검증·상태를 보존했다.

- `Trait_Master!B201,E201:F201,H201`
- `Localization_Master!D626:E626,I626,D1181:E1181,I1181`

후속 요청으로 그림자의 전투판 제한을 제거했다. 카메라 화면 폭의 108% 크기로 배경을 포함한 전체 화면을 아래→위로 지나간다. 그림자에만 영역 마스크를 해제하고 원본 알파 텍스처를 명시적으로 유지해 실루엣이 단순 다각형으로 보이지 않도록 했다. 브레스 피해와 착지 범위는 전투 영역이다. `s0_012.png`, `s0_018.png` 및 `capture-results.txt`는 이 후속 검증 결과이며, `s1`~`s3`는 앞선 4개체 확인 시퀀스다. 후속 시각 테스트 1/1 통과(`c8f054f01070470ca1ec2b131c031974`), 실제 화면에서 배경을 가로지르는 실루엣 확인. Google DB `F201,H201` 추가 수정 후 버전 3 일치 확인.

## 그림자 제작 기록

기본 내장 ImageGen으로 생성. 원본 RGBA의 투명도를 보존해 프로젝트에 복사하고, Unity에서 128 크기·Point 필터·무압축·밉맵 없음으로 임포트했다.

최종 파일: `C:/Unity/BecomeZ_BombBrick/Assets/Resources/CombatEffects/Wyvern/HeavenDragonShadow.png`

프롬프트:

> Create a production game sprite: a single enormous flying WYVERN DRAGON SHADOW, top-down orthographic view from directly above, head points to the TOP of the image, long slightly curved tapering tail points DOWN, two monumental bat wings spread wide left and right, pointed horns and clear long neck, short hind legs and claws. Strong iconic recognizable dragon silhouette, elegant menacing wings with three long sharp wing fingers each, scalloped trailing wing membranes. Pixel-art at a logical 128x128 pixel resolution, hard stepped pixel contour, no antialiasing. Solid near-black silhouette ONLY, NO internal details, no highlights, no eyes, no lighting or cast shadow, no smoke, no text, no border. Transparent background with real alpha. Entire silhouette fully inside canvas, wings occupy 92% width, head to tail occupies 90% height. Centered, one sprite only, NOT a spritesheet. This asset will be a translucent dragon shadow rapidly projected across a pixel-art battlefield.
