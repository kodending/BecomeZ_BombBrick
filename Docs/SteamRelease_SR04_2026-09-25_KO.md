# SR-04 튜토리얼 다시 보기·도움말

2026-09-25. [Steam 출시 준비 과제표](https://docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/edit#gid=1950270647)

## 적용 결과

- 메인 화면과 ESC 일시정지 메뉴에 **게임 도움말** 추가.
- 8개 주제: 원정덱 편성, 지역과 난이도, 배치와 전투, 성장과 특성, 포획하기, 아이템 활용, 귀환과 성장, 조작과 저장. 주제별 3단계 설명과 목차·이전·다음 제공.
- X/ESC로 닫는다. 전투 메뉴에서 열었다면 도움말만 닫고 일시정지 메뉴로 돌아간다. 계속하기를 누르면 메뉴 진입 전 배속으로 복귀한다. 이미 멈춰 있던 상태도 유지된다.
- 기존 최초 튜토리얼은 그물 지급·진행 단계 저장과 결합되어 있어, 저장을 건드리지 않는 별도 읽기용 도움말로 구현했다. 튜토리얼 단계 초기화·건너뛰기·보상 재지급·해금 변경은 수행하지 않는다.
- 저장 실패/복구 안내, 장면 전환, 시작 로딩, 설정/종료 확인창 위로 도움말이 열리지 않도록 했다. 도움말의 입력은 하위 게임 화면에 전달되지 않는다.
- 한·영 35개 문구를 로컬 `LocalizationDatabase.json`과 `Localization_Master!A1572:I1606`에 동일하게 추가했다. 기존 로컬 1,569개 항목은 내용이 그대로다. Google 재조회에서 315셀 값 일치, 상태 Approved·체크박스 및 줄바꿈 형식 보존 확인.

## 검증

- EditMode: `GameHelpTests`, `GameFrontendTests`, `GameLocalizationTests`, `ProductionExpeditionFlowTests`, **47개 중 43개 통과 / 기존 문제 4개 실패**. 도움말의 모든 문구 영어 검사, 메뉴 배치 크기, 기존 튜토리얼·원정 편성 검사는 통과했다.
- PlayMode: Boot 메인 도움말 진입과 입력 차단 확인. 실제 저장을 로드하는 마을/전투 컨트롤러 대신 비어 있는 임시 Town/Battle/Result 씬으로 공통 메뉴 경로를 확인했다. 모두 자동 일시정지 → 도움말 닫아도 일시정지 유지 → 메뉴 닫으면 1.3배 복귀. 원래 timeScale=0인 경우도 0으로 복귀했다.
- 1920×1080 한국어 및 영어, 1366×768 영어 화면 캡처 확인. 8개 주제의 한·영 본문 48개를 실제 GUIStyle로 측정했고 모두 65 높이 안에 들어갔다(최대 41). 메인/일시정지 메뉴의 새 진입 버튼도 확인.
- 실제 프로필·원정·보관 파일 **8개 모두 전후 SHA-256 동일**. 신규 저장 파일 생성 없음. 플레이 데이터 초기화 없음.
- PlayMode 콘솔 오류 0. Boot 정지 상태, 원래 Game View 1920×1080으로 복귀. 이번 UI 변경으로 별도 Windows 플레이어를 다시 빌드하지는 않았다.
- 증빙: `Logs/SteamRelease/SR-04/tests.json`, `menu-pause-proof.json`, `context-pause-proof.json`, `text-layout-proof.json`, `player-save-proof.json`, 같은 폴더의 화면 PNG.

## SR-10에서 처리할 기존 영어 검사 실패

- `EveryMonsterAbilityTooltipDescriptionIsEnglish`: 특성 14개의 발동 조건에 한국어 잔존(중복을 제외하면 13개 원문).
- `ExpeditionHudGrowthAndShopStringsAreEnglish`: 개체별 역할명 9개 미번역.
- `FangLockTooltipDistinguishesPhysicalAndTrueDamage`: 한·영 2건. 실제 데이터는 개체별 주계수를 사용하는 스킬 위력으로 변경됐지만 테스트는 예전 공격력 계수 문구를 기대한다.
- 변경 전 번역 DB에서도 위 조건/역할명 22개 원문 번역이 없고 검치 설명은 이미 스킬 위력 문구였음을 확인했다. 이번 추가 35개 문구와 관계없는 기존 문제이며 SR-10 비고에 기록했다. 테스트를 통과시키기 위해 게임 수치를 되돌리거나 검사를 약화하지 않았다.

SR-04 완료. 다음은 **SR-05 안전한 새 게임·데이터 초기화**.
