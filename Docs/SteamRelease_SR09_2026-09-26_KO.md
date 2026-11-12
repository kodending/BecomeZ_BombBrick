# SR-09 제품명·개발사·아이콘·버전

2026-09-26. [Steam 출시 준비 과제표](https://docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/edit#gid=1950270647)

2026-10-17: 사용자 결정에 따라 첫 출시 형태와 버전 정책을 갱신했다. 아래 구현·검증 기록은 별도 날짜가 없는 한 2026-09-26 당시의 결과다.

## 확정 및 적용

- 제품명: **BecomeZ: Wild Crew** / **BecomeZ: 와일드 크루**.
- 개발사: **PangStudio**, 개발자 크레딧: **Pangsoo**.
- 메인 화면은 위쪽 작은 `BecomeZ`, 아래쪽 큰 `와일드 크루` / `WILD CREW`로 표시한다. 하단에 버전·개발사·개발자를 표시하며 기존 부팅 로고에도 PangStudio를 추가했다.
- 기본 및 Windows 아이콘은 승인된 춘동이 V4다. `Assets/Art/Branding/ChundongiAppIcon.png`는 `ArtSource/Branding/SR09/ChundongiIcon_LemonMintEyes_v4.png`와 바이트가 같다. 큰 검은 동공, 옅은 레몬·민트 눈동자, 코 옆 털 반점을 다시 그리지 않았다.
- Unity 아이콘 슬롯: 기본 128, Windows 1024/512/256/128/64/48/32/16. Point 필터, 압축·밉맵 해제. 각 출력 크기는 Unity의 네이티브 빌드 과정에서 생성한다.
- Windows 식별자: `com.pangstudio.becomez.wildcrew`. 현재 및 첫 Steam 정식 출시 버전은 **0.1.0**이다. 2026-10-17 사용자가 현재 콘텐츠 범위를 완성품으로 판매하는 정식 출시를 선택했다. 이전의 첫 정식 출시 1.0.0 계획은 이 결정으로 갱신하며, 후속 패치 버전은 변경 범위에 따라 별도로 확정한다.
- 버전은 PlayerSettings와 게임 내 표시를 기준으로 한다. Unity 2022가 생성한 Windows EXE의 탐색기 파일 속성은 엔진 버전 `2022.3.62`를 유지한다. EXE 아이콘은 춘동이로 적용됐으며 별도 PE 리소스 편집기는 추가하지 않았다.

## 기존 데이터 보존

Windows 저장 경로는 제품명의 콜론 처리나 이후 표시 이름 변경에 영향을 받지 않도록 별도로 고정했다.

`%USERPROFILE%/AppData/LocalLow/PangStudio/BecomeZWildCrew/UserData/Profiles`

- 이전 경로 `%USERPROFILE%/AppData/LocalLow/DefaultCompany/BecomeZ_BombBrick`의 `Profiles*` 파일·폴더를 새 `UserData`에 복사한다. 프로필, 원정, 이전 백업, 새 게임 보관 폴더와 진행 중인 초기화 복구 마커를 함께 옮긴다. 로그·밸런스 보고서는 저장 데이터 이전 대상이 아니다.
- 임시 폴더에 전부 복사한 다음 같은 볼륨에서 디렉터리 이름을 바꾸어 확정한다. 실패 시 새 저장 폴더를 확정하지 않고 기존 불러오기 실패 UI로 연결한다. 다음 시도에서 다시 복사한다.
- 이전 원본은 수정하거나 삭제하지 않는다. 새 `UserData`가 있으면 새 데이터를 우선하므로 이전 빌드를 다시 실행하거나 새 게임을 시작해도 과거 진행도가 다시 섞이지 않는다.
- 설정은 새 PlayerPrefs 키가 없을 때만 이전 회사·제품의 키에서 읽어 가져온다. Editor와 Windows 플레이어의 레지스트리 위치를 구분한다. 신규 설정이 있으면 덮어쓰지 않는다.
- Windows 설정 읽기는 [RegGetValueW](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-reggetvaluew)를 사용한다. Unity의 .NET Standard 환경에 별도 Registry 패키지를 추가하지 않았다. 이전 Unity 2022 설정 키의 실제 해시 접미사와 UTF-8 REG_BINARY 형식을 확인했다.
- 이후 제품명을 다시 바꿔도 Windows 저장 경로는 유지한다. Steam Cloud를 연결할 때 이 경로를 기준으로 등록해야 한다. SR-09는 Steam Cloud 등록을 수행하지 않는다.

## 검증

- EditMode **91/91 통과**: 새 이전 테스트 7개, 기존 프로필 복구·새 게임·원정 저장/재시도·메인 화면·백그라운드·번역 키 검사.
- 최종 고정 저장 경로 적용 후 이전 테스트 **7/7 재통과**, 실제 42개 파일 재비교도 일치.
- 이전 테스트: 프로필·원정·복구 파일 동시 보존, 새 데이터 우선, 첫 실행, 읽기 잠금 실패 후 재시도, 원본/대상 경로 충돌, 이전 도중 보관된 새 게임 복구.
- 실제 저장·백업 파일 **42개**를 새 폴더와 바이트 비교해 모두 일치. 이전 원본 SHA-256 **42/42 유지**.
- 기존 설정 JSON을 새 PlayerPrefs에 그대로 가져옴: 1920×1080, VSync, 120fps, 음량, 언어, FPS 표시 유지.
- 한글 1920×1080, 영문 1366×768 메인 화면 및 부팅 로고 스크린샷을 확인했다. 게임 진입·튜토리얼 초기화는 하지 않았다.
- Windows x64 / Mono / StrictMode / LZ4, 활성 씬 4개 빌드 성공. 최종 오류 **0**, 경고 **0**, 약 **278.73 MB**. 첫 빌드의 컴파일 상태 경고는 전체 새로고침·컴파일 완료 후 재빌드하여 해소했다.
- 실행 파일: `Builds/BecomeZ_WildCrew_Windows_20260926_SR09/BecomeZ Wild Crew.exe`. EXE에서 추출한 32px 아이콘이 춘동이임을 확인했다. 숨김 Boot 실행 로그에서 예외가 없으며 검증용 프로세스는 종료했다. 전투 플레이 전체를 다시 검증한 것은 아니다.
- 숨김 실행은 종료 확인 UI를 조작하지 않았으므로 창 닫기 요청 후 검증용 PID만 강제 종료했다. 정상 종료 동작의 검증으로 간주하지 않는다. 종료 후 이전 원본과 새 복사본 모두 42개 파일 해시가 유지됐으며, Unity는 Boot 정지·한국어·기존 설정 상태로 남겨 두었다.
- 증빙: `Logs/SteamRelease/SR-09/editmode-tests.json`, `save-migration.json`, `settings-migration.json`, `source-preservation.json`, `main-ko-1920.png`, `main-en-1366.png`, `boot-studio.png`.

## DB

- `Localization_Master!A1634:I1634`: 새 게임명 한영 표기를 로컬 JSON과 동일하게 반영.
- `Steam_출시준비`: SR-09 **완료**, SR-10 **착수 준비**. 다음은 영어 번역·표기 검수다. SR-10 작업은 아직 시작하지 않았다.
