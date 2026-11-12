# 티라노 4등급 — 폭군의 군림

2026-09-18 네온 예고 및 착지 직후 소멸 수정. 로컬 버전 3, Google DB 버전 3 반영은 승인 대기.

- 3등급 동작이 정상 종료된 후 0.5초 예고. 전투 영역 사각 테두리에 황금색 네온이 올라오고 발 그림자가 짙어진다. 발은 예고 마지막 0.14초에 내려온다.
- 착지 프레임 04에서 모든 공격 가능한 적에게 공격력 200% 피해를 한 번 적용한다. 대기석과 아군은 제외한다.
- 기본 1.2초 쓰러짐. 기존 상태이상 저항과 면역을 적용한다. 발은 착지 후 짧게 압착하고 0.18초 안에 사라진다. 네온은 착지 후 0.08초, 그림자는 0.12초 안에 사라진다. 적의 쓰러짐 시간은 시각 효과와 독립적이다.
- 착지 카메라 진동과 사각 전장 가장자리 흙 파티클. 카메라 줌 변경 없음.
- 기존 공포·감속 영역·기본공격 60% 광역 효과를 교체했다.
- 사망·전투 종료·씬 전환 시 정리. 다음 스킬이 먼저 끝나도 현재 압착을 갱신하지 않고 다음 짓밟기를 대기시킨다.

## 아트

내장 image_gen 도구로 16프레임 투명 스프라이트 시트를 생성했다.

- 최종 에셋: `Assets/Resources/CombatEffects/Tyranno/TyrantStomp16.png`
- 최종 프롬프트: `Docs/TyrannoReignImagePrompt.txt`
- Point 필터, 무압축, 밉맵 없음, 최대 2048, 원본 1254×1254 유지.

## 검증

- 네온 수정 이후 Unity EditMode 48/48 통과. Job: `8dd6ef8a36de41d0a16e96c2de0831d4`.
- `NeonWarning` 전투 330프레임 검사: 착지 04번 프레임, 적 4명 각각 200 피해 1회, 착지 후 0.183초에 발·네온·그림자 모두 꺼지고 쓰러짐은 1.0초 남음. 실제 쓰러짐 종료까지 약 1.2초. 카메라 줌 변화 없음.
- 재현: `Logs/TyrannoReignVerification/verify_neon.py`. 네온 화면 `NeonWarning/trait_tyranno_primordial_great_maw/view_1.png`, 착지 `view_4.png`, 소멸 후 `view_7.png`.

아래는 최초 버전 2 연계 검증 기록이며, 발 유지·회수 시간은 위 버전 3 연출로 교체되었다.

- Unity EditMode 48/48 통과. Job: `5deb6f0903ca4743ac08058fbca42945`.
- 세 3등급 특성 각각 330프레임을 재생해 검사했다. 각 착지에서 적 4명에게 200씩 한 번만 피해, 04번 발 프레임 동기화, 1.2초 쓰러짐, 발 회수까지 유지, 카메라 FOV 36 유지.
- 대멸종 진동 2.350초 / 시원의 거악 2.567초 / 폭군 대행진 2.867초 착지. 서로 다른 스킬 종료 시점 이후 정확히 0.5초 예고.
- 별도 테스트에서 아군·대기석 제외, 제어 면역, 저항 100의 쓰러짐 0.6초, 연속 발동 대기, 사망·전투 종료 취소, 중복 취소를 확인했다.
- 전투 캡처 후 Unity 오류·경고 0건. 시간 배율 1, 캡처 프레임 설정 해제, 재생 종료.
- 기록: `Logs/TyrannoReignVerification/capture-results.json`, 각 특성 폴더의 `timeline.csv`와 화면 캡처.

## 데이터

로컬 마스터와 KO/EN 설명 버전 3. 기존 Google DB의 `Trait_Master!F193,H193`, `Localization_Master!D1173,E1173,I1173` 총 5셀은 이전 버전 2까지 반영되어 있다. 자동 승인 검토가 새 버전 3 쓰기를 거절해 해당 5셀에 대한 사용자 승인을 요청했다. 버전 3 요청 및 읽은 값은 `NeonWarning/google-payload.json`, `NeonWarning/google-before.json`에 있다.

- 요청: `Logs/TyrannoReignVerification/google-payload.json`
- 전후 증거: `google-before.json`, `google-after.json`
