# SR-13 문의·버전·오류 로그 안내

2026-09-30 완료. 공식 문의 주소는 사용자 확인에 따라 **codending@gmail.com**으로 적용했다.

## 적용

- 메인 화면과 일시정지 메뉴에 `문의` 버튼 추가. 기존 게임 도움말의 마지막 `문의 · 오류 신고` 페이지로 열린다.
- 제품명과 현재 `Application.version` 표시. 영어 전환 시 번역 패턴이 제품명 접두어를 없애는 것을 피하도록 제품명은 원문 그대로 표시한다.
- `이메일로 문의`: 기본 메일 앱에 수신 주소와 게임명·버전이 포함된 제목을 전달한다. `문의 주소 복사`도 제공한다.
- `진단 정보 복사`: 게임명, 버전, 빌드 GUID(Editor에서는 Editor), Unity 버전, OS, GPU, 화면 크기·모드, 언어, 현재 씬을 복사한다. 사용자·기기 고유 ID, 세이브 내용, 로그 본문, 사용자 경로는 복사하지 않는다.
- `로그 폴더 열기`: `Application.consoleLogPath`를 기준으로 실제 로그 디렉터리를 연다. 로그 위치도 화면에 표시한다. 기본 Windows 플레이어 로그는 Player.log이며 Editor 검수에서는 Editor.log를 가리킨다. 사용자 지정 로그 경로도 같은 방식으로 처리한다.
- 경로가 없거나 외부 앱·클립보드 호출에서 예외가 나면 안내 문구를 표시한다. 메일 앱의 설치·연결 상태는 운영체제에 따른다. 자동 발송·파일 첨부·원격 수집은 추가하지 않았다.
- 문의 창 X/ESC는 도움말만 닫는다. 전투 메뉴는 계속 일시정지 상태이며, 계속하기에서 이전 배속으로 돌아간다.

변경 파일:

- `Assets/Game/Core/GameSupport.cs`: 확정 문의 주소, 메일 링크, 진단 정보, 로그 경로.
- `Assets/Game/Core/GameFrontend.cs`: 문의 진입 버튼과 기존 도움말 내 페이지.
- `Assets/Resources/Localization/LocalizationDatabase.json`: 신규 한·영 문구 13개.
- `Assets/Tests/Editor/GameSupportTests.cs`: 문의 주소·메일 링크, 진단 정보 포함 범위, 비정상·사용자 지정 로그 경로 검사.

## 구글 DB

- `Localization_Master!A1647:I1659`: 신규 13개 문구를 `Review`, enabled=true, version=1로 등록. 기존 로컬 1,644개 문구와 기존 구글 검수 내용은 변경하지 않았다.
- `Steam_출시준비!C4,F19,H19,K19`: 결과와 완료 상태 기록. G열은 기존 점검 분류이므로 유지한다.

## 검증

- GameSupport / GameFrontend / GameHelp / GameCredits / GameBackground EditMode 검사 **45/45 통과**. 작업 `092dbd1cf8b34fb3b0ba53d7dba9915f`.
- Unity 실제 재생에서 문의 페이지 진입, 주소·진단 정보 복사와 원래 클립보드 복구, 도움말 닫기 후 메뉴 일시정지 유지, 메뉴 종료 시 2배속 복귀 확인.
- 신규 문구 13개 영어 매칭, 최종 한국어·영어 화면, 메인 메뉴 문의 버튼 확인.
- 세이브 관련 파일 **84개 해시 동일**, 사용자 설정 동일. 검수 후 한국어·Boot 씬·재생 정지 복귀.
- Windows x64 비개발 clean/LZ4/strict/detailed 빌드 성공. **오류 0, 경고 0**, 278.29 MiB. 작업 `build-4aab0fea0d`.
- 메일 전송이나 OS 메일 앱 실행은 검수에서 수행하지 않았다. 실제 외부 앱 동작과 설치 환경별 확인은 후속 배포 검수에서 진행한다. SR-13 빌드 성공은 전체 출시 후보 검수 완료를 뜻하지 않는다.

빌드: `Builds/BecomeZ_WildCrew_Windows_20260930_SR13/BecomeZ Wild Crew.exe`.

근거: `Logs/SteamRelease/SR-13-20260930/`의 `runtime-verification.txt`, `diagnostic-sample.txt`, `support-final-ko.png`, `support-final-en.png`, `main-support.png`, `saves-verified.json`, `Before/`.
