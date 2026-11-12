# SR-03 중복 실행·저장 충돌 방지

2026-09-25. [Steam 출시 준비 과제표](https://docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/edit#gid=1950270647)

## 적용

- `ProjectSettings/ProjectSettings.asset`: `forceSingleInstance: 1`. Unity 기본 Windows 단일 실행 기능을 사용한다. 게임 코드에 별도의 잠금 파일이나 Mutex를 추가하지 않았다.
- 이미 게임이 실행 중이면 두 번째 실행은 게임 초기화 전에 Unity 안내창에서 차단된다. 실제 Windows 안내창 제목은 `Fatal error`, 본문은 `Another instance is already running`이다. 기존 게임의 충돌 오류가 아니라 중복 실행을 차단하는 Unity 기본 문구다.
- 근거: [Unity 2022.3 PlayerSettings.forceSingleInstance](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PlayerSettings-forceSingleInstance.html).

## Windows 빌드에서 확인

- 빌드: `Builds/BecomeZ_BombBrick_Windows_20260925_SR03/BecomeZ_BombBrick.exe`.
- Windows x64 / Mono / Development=false / StrictMode / LZ4. 활성 빌드 씬 4개 그대로 사용. 빌드 오류 0, 경고 0, 약 278.69 MB.
- 실제 첫 게임을 Boot에 둔 채 중복 실행을 3회 시도했다. 매번 같은 중복 실행 안내를 확인했고, 두 번째 게임은 Unity player 로그조차 생성하지 않았다. 첫 게임은 계속 유지됐다.
- 안내창을 닫으면 중복 프로세스가 정상 종료됐다. 첫 게임 종료 후에는 새 게임이 정상 실행됐다.
- 초기 확인에서는 첫 게임의 창 닫기 정상 종료를 확인했다. 후속 숨김 실행 검증에서는 창 닫기가 8초 내 끝나지 않아, 테스트가 띄운 Boot 프로세스만 강제 종료하고 재실행을 확인했다. 전투 중 정상 종료·저장 실패 거부 동작을 검증한 것은 아니다.
- 실제 플레이 저장·보관 파일 8개 모두 전후 SHA-256 동일. 게임 진입·튜토리얼 초기화는 하지 않았다.
- 증빙: `Logs/SteamRelease/SR-03/player-verification.json`, `player-verification-initial-process-check.json`, `asset-save-verification.json`, `build-report.json`. 실행 검증 절차는 같은 폴더 `verify-player.ps1`에 보관했다.

## 빌드를 막던 기존 VFX 호환 문제 수정

- 프로젝트는 URP 14.0.12인데 수입한 Zap VFX 셰이더는 URP 17의 MotionVectors 헤더를 참조해 첫 빌드가 실패했다.
- 실제 천둥 효과에서 사용하는 셰이더 2개의 해당 include만 `MotionVectorsCompat.hlsl`로 연결했다. Unity 2022.3에서는 설치된 URP 14의 ObjectMotionVectors 계산과 같은 화면 UV 속도 계산을 사용한다. Unity 6 이상에서는 원래 URP 헤더를 사용한다. 색상·입자·Forward 패스는 변경하지 않았다.
- `Assets/Vefects/Zap VFX URP/Demo/Resources`를 `Demo/ShowcaseAssets`로 이동해 게임에서 사용하지 않는 URP 17 데모 캐릭터 셰이더의 자동 빌드 포함을 제거했다. GUID를 유지했고 이동한 14개 파일·메타의 바이트가 모두 동일하다. 실제 천둥 프리팹 4종의 참조는 유지된다.
- 기존 해태 천둥 테스트 **9/9 통과**. 실제 프리팹·타격 음원·입자 생성 타이밍과 피해 판정을 포함한다. 셰이더 2개 컴파일 메시지 없음. 증빙: `thunder-tests.json`.

## 범위 및 다음 과제

- 이 설정을 적용해 만든 Windows 플레이어에 대한 검증이다. Unity Editor·이전 빌드·다른 OS의 동시 실행까지 같은 보장으로 간주하지 않는다.
- Unity는 Boot 씬, PlayMode 정지 상태로 유지했다. 테스트로 띄운 플레이어는 모두 종료했다.
- SR-03 완료. 다음은 **SR-04 튜토리얼 다시 보기·도움말**.
