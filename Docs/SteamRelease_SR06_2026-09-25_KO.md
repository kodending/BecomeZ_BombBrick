# SR-06 카메라 흔들림 강도 설정

2026-09-25. [Steam 출시 준비 과제표](https://docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/edit#gid=1950270647)

## 적용 결과

- 메인 화면/ESC 메뉴의 **설정 → 화면 → 카메라 흔들림**에서 `끄기`, `약하게 (35%)`, `기본 (100%)`을 선택한다. 선택 즉시 적용·저장하며 화면 설정 적용 버튼을 따로 누르지 않아도 된다.
- 기본값은 기존 강도 100%. 새 항목이 없는 이전 설정도 기본값으로 읽는다. 잘못된 선택 값은 기본값으로 정규화한다. 기존 UserSettings 저장 키를 사용하며 SR-05 새 게임 시에도 유지된다.
- `CombatCameraDirector` 공통 진동 계산의 진폭에만 배율을 적용한다. 타격/스킬 수치, 범위, 이동, 애니메이션 타이밍, 게임 배속을 변경하지 않는다. 기존 자동 카메라 구도 설정도 그대로 유지한다.
- 끄기를 선택하면 이미 적용된 화면 이동을 즉시 되돌리고 남은 진동을 취소한다. 꺼진 동안의 충격을 쌓아 두지 않으므로 다시 켤 때 지난 진동이 재생되지 않는다.
- 실행 중 카메라는 설정 적용 시 갱신하고, 새로 초기화한 전투/테스트 카메라는 저장된 설정을 읽는다. 매 프레임 저장 파일을 읽지 않는다.
- 전체 Assets C# 검색에서 별도 카메라 흔들림/DOShake/Impulse/투영 이동 경로는 없고 전투 흔들림은 공통 Director로 모이는 것을 확인했다. 지면 강타, 발걸음, 압축 브레스, 열매, 그리폰 착지, 해태 번개, 턱, 실 창, 곰 포효, 강한 치명타·사망 진동에 적용된다.
- 한·영 5문구를 로컬 DB와 `Localization_Master!A1624:I1628`에 반영했다. 기존 로컬 1,621항목은 그대로이며 Google 재조회에서 45셀 값 일치 및 Approved/체크박스/줄바꿈 형식 보존 확인.

## 검증

- EditMode **52/52 통과**: CombatCameraDirectorTests, GameFrontendTests, GameLocalizationTests.DatabaseHasCompleteEnglishAndStableKeys, PlayerProgressResetTests.
- 14가지 진동 보고 경로를 호출해 실제 `WorldToScreenPoint` 이동량을 검증했다. 약하게는 픽셀 반올림 오차 내 35%, 끄기는 0으로 복귀하며 다시 켜도 과거 충격이 재생되지 않는다. FOV·카메라 위치·배속은 강도 설정으로 바뀌지 않는다.
- PlayMode 저장→다시 로드→카메라 초기화 검증: 동일한 강한 내려찍기의 표본에서 기본 10.198px, 약하게 3.162px, 끄기 0px. 기존 픽셀 스냅 때문에 화면상 값은 정수 픽셀로 반올림된다. 전투 도중 설정 Apply로 끈 경우에도 투영 복귀 및 남은 진동 0 확인.
- 한국어 1920×1080, 영어 1366×768 설정 화면을 캡처해 새 행과 주변 옵션 배치를 확인했다. 기존 화면 설정 확인/되돌리기 흐름은 유지했다.
- 설정 저장 검증은 try/finally에서 이전 PlayerPrefs 원문으로 복원했고 원문 일치를 다시 확인했다. 이번 작업 시작 시점에 존재한 실제 사용자 저장 파일 2개는 모두 전후 SHA-256 동일하고 개수도 동일하다.
- 콘솔 오류 0. Boot 정지 상태와 원래 1920×1080 Game View로 복귀했다. 별도 Windows 실행 파일은 이번에 재빌드하지 않았다.
- 증빙: `Logs/SteamRelease/SR-06/editmode-tests.json`, `runtime-settings-proof.json`, `localization-sync-proof.json`, `player-save-proof.json`, 설정 화면 PNG.

SR-06 완료. 다음은 **SR-07 강한 섬광 감소 설정**.
