# SR-12 크레딧·사용 자산 정리

이 문서는 최초 구현 및 중간 점검 기록이다. **최신 판정은 [최종 검수 기록](SteamRelease_SR12_2026-09-29_KO.md)을 기준으로 한다. SR-12는 2026-09-30 완료했다.** Asset Store 상품별 조건·Tribal Drum 출처 확인 및 미사용 자산의 배포 제외는 후속 작업에서 해결했다. 기존 활성 음원 28개와 사망음도 Unity Asset Store 취득 음원이라는 사용자 확인을 반영해 출처를 정정하고 확인 대기를 해제했다. 아래 과거 ElevenLabs 분류 및 대기 기록은 현재 판정이 아니다.

## 2026-09-29 재개 당시 중간 기록 (아래 확인 대기는 최신 기록에서 갱신됨)

- 사용자 확인: 이번 교체 음원은 **ElevenLabs 유료 플랜에서 새로 생성**했다. 계열별 기존 원본 해시와 비교해 실제 변경된 177개에 이 확인을 연결했다. 계정 결제 내역을 직접 열람한 결과와 구별한다.
- 현재 활성 음원 205개 중 177개 교체. 이전 파일과 완전히 같은 28개 및 공통 사망음은 **사용자의 유지 지시**에 따라 보존했다. 미사용 웅이 종료/해제 2개도 별도 보관 중이며 Resources 포함 문제는 남아 있다.
- 정령사슴 차단음의 미정리 원본 파일명도 기존 block 연결에 반영했다. 원본은 수정하지 않았다.
- 새 적용본은 기존 기본 Hit 기준 믹서를 통과한다. 205개 실제 계열 재생 함수, 동시 재생 상한, 일시정지·설정 반영 검증 완료. 관련 오디오/크레딧/도움말/메뉴 검사 69/69 통과.
- PangStudio / Pangsoo / 춘동이 제작진 표기 및 기존 고지 6개를 유지한다. 새 음원 제공자도 ElevenLabs이므로 기존 `OtherAssets.txt`의 제공자 표기를 그대로 사용한다. 현재 크레딧 UI를 다시 만드는 작업은 필요 없다.
- [ElevenLabs 공식 상업 이용 안내](https://help.elevenlabs.io/hc/en-us/articles/13313564601361-Can-I-publish-the-content-I-generate-on-the-platform)를 2026-09-29 다시 확인했다. 유료 구독 중 생성물에 대한 상업 이용 안내가 있으나 약관·개별 서비스 조건·필요 권리 충족이 전제이고 Beta Services는 제외된다. 새 생성 확인을 이전 동일 파일에 소급 적용하지 않는다.
- 원본/적용본 해시, 변경 전 백업, 보존한 28개+사망음 목록: `Logs/AudioReplacement_2026-09-29/`. 상세 작업: `Docs/CombatSoundReplacement_2026-09-29.md`.
- 2026-09-29 사용자 답변 **“응 다 Asset Store에서 받은거야”**에 따라 AS-03~10 효과음팩·음악·VFX 및 `Tribal Drum.wav`의 취득 경로를 Unity Asset Store로 기록했다. 계정 영수증을 직접 열람한 결과는 아니며, 같은 취득 질문을 다시 요청할 필요는 없다. `Tribal Drum.wav`의 개별 상품명은 아직 특정되지 않았다.
- [RPG Essentials Free](https://assetstore.unity.com/packages/audio/sound-fx/rpg-essentials-sound-effects-free-227708)와 [Basic RPG Sounds](https://assetstore.unity.com/packages/audio/sound-fx/basic-rpg-sounds-325670)의 공식 상품 페이지에서 Standard Unity Asset Store EULA를 확인했다. [표준 EULA 2.2.1](https://unity.com/legal/as-terms)은 비제한 자산을 독창적인 게임에 포함해 배포·수익화하는 범위를 허용한다. 다른 팩의 개별 조건이나 오래된 EmojiOne 샘플 라이선스까지 확인한 것으로 확대하지 않는다.
- 남은 확인은 유지한 이전 ElevenLabs 음원, 일부 상품별 조건·정확한 상품 연결, AS-11 EmojiOne 샘플이다. 이번 재개 작업에서는 새 Windows 배포 빌드를 만들지 않았다.

## 적용 내용

- 메인 화면과 일시정지 메뉴의 `게임 도움말` 옆에 `크레딧` 추가.
- 제작진: PangStudio / Pangsoo, 마스코트: 춘동이(Chundongi).
- 상태 아이콘, 기존 CC0 효과음, 추가 사용 자산, Galmuri, Liberation Sans, 소프트웨어 고지 열람.
- 긴 고지는 고정 크기의 본문 글자와 스크롤로 표시. 본문은 가독성을 위해 일반 글꼴을 사용하고, 메뉴는 기존 픽셀 스타일을 유지한다.
- X·ESC로 크레딧을 닫아도 기존 일시정지 메뉴는 유지. 메뉴를 닫을 때 이전 게임 속도로 복귀.
- `Assets/StreamingAssets/Notices/`의 텍스트 6개는 Windows 배포 시 `<게임명>_Data/StreamingAssets/Notices/`에 동봉되며, 게임에서도 같은 파일을 읽는다.
- 새 문구 12개만 `Localization_Master!A1635:I1646`에 `Review`로 추가. 기존 로컬 번역 1,632개 및 사용자 한글·영어 검수 입력은 변경하지 않았다.

## 조사 범위와 판정 기준

Unity 2022.3.62f2에서 활성 빌드 씬 4개와 모든 Resources 자산을 뿌리로 `AssetDatabase.GetDependencies(..., true)`를 조회했다. 5,439개 참조 후보 중 이미지 4,676개·음원 278개가 포함됐다. 이것은 **배포 포함 후보** 목록이며 모든 파일이 현재 게임 플레이 중 호출된다는 뜻은 아니다. 기존 SR-09 빌드는 상세 자산 목록을 보관하지 않아 기존 빌드의 실제 포함 자산 목록으로 취급하지 않았다.

이번 SR-12 Windows 빌드의 상세 보고서에는 코드·패키지·내장 자산을 포함한 고유 원본 경로 7,508개가 기록됐다. 음원은 278개이며 EmojiOne 샘플과 URP 디버그 폰트도 실제 포함됐다. 상세 목록은 `Logs/SteamRelease/SR-12/packed-assets.txt`에 보관했다.

- 원본 연결: 이름만으로 단정하지 않고, 가능한 음원은 SHA-256 일치를 확인했다.
- 공개 자산: 로컬 고지와 제공자 공식 페이지를 함께 확인했다.
- 구매 자산: 폴더 존재나 `Free`라는 이름만으로 상업 이용 권한을 확정하지 않았다.
- 생성/직접 제작 자산: 가공 기록과 원본 경로는 제작 흔적이며, 사용 서비스·요금제·외부 참조물의 권리를 자동으로 증명하지 않는다.

## 확인된 고지

| 자산 | 사용/포함 근거 | 확인 자료 및 조치 |
|---|---|---|
| Galmuri11-Bold | 기본 픽셀 UI의 Resources 폰트 | Lee Minseo, SIL OFL 1.1. 프로젝트 저작권·라이선스 원문을 바이트 그대로 동봉. |
| Liberation Sans | TMP Resources 폰트와 원본 TTF 참조 | Google/Red Hat 저작권·SIL OFL 1.1 원문을 바이트 그대로 동봉. |
| Perfect DOS VGA 437 | SR-12 상세 빌드 보고서의 URP Core/Runtime/Debugging 폰트 | TTF 메타데이터의 zeh와 제공 페이지의 Zeh Fernando 연결 확인. [저자의 2014-09-16 사용 허용 문장](https://www.dafont.com/font-comment.php?file=perfect_dos_vga_437)을 추가 자산 고지에 함께 동봉. |
| Game-icons.net 상태 아이콘 | Resources/UI/CombatStatusIcons | Lorc, Delapouite, Carl Olsen, sbed, Skoll, Willdabeast, Zeromancer. 개별 링크와 CC BY 3.0, 래스터화·합성 변경 고지를 게임에서 열람 가능하게 추가. |
| 기존 전투 샘플 55개 | Resources/Audio/Combat/Samples | Kenney의 Impact/RPG/Sci-fi, artisticdude의 Swishes, Someoneman의 Cure Magic, JaggedStone의 Magic Spell SFX. 기존 출처 문서와 제공자 페이지의 CC0 확인. 이 판정은 Samples 폴더에만 적용. |
| 런타임 패키지 | SR-09 Managed DLL 목록 및 SR-12 상세 빌드 보고서 | ML-Agents, Barracuda, Burst, Mathematics, URP, ShaderGraph, TMP, Timeline, uGUI, Visual Scripting, Newtonsoft, Test Framework의 설치본 라이선스·서드파티 고지를 동봉. Protobuf/Grpc 등의 패키지 내 고지도 포함. |
| MCP for Unity 런타임 | SR-09 Managed/MCPForUnity.Runtime.dll | packages-lock의 정확한 커밋 `11836003a5e2ffcb7715ecec7e1fbb9d9cdb5bb8`에서 MIT 원문 확보 및 동봉. |

패키지 고지는 설치본에 포함된 원문 전체를 보존하므로 실제 플레이에서 사용하지 않는 해당 패키지 내부 구성요소의 설명도 들어 있을 수 있다.

## 자산별 확인 현황

| ID | 자산/범위 | 확인된 연결 | 남은 확인 |
|---|---|---|---|
| AS-01 | 20개 계열 선택 음원 207개 | 2026-09-29 유료 플랜 새 생성 사용자 확인. 실제 변경된 활성 177개 교체·검수 완료. 기존 동일 활성 28개 및 미사용 2개는 유지 | 새 177개는 생성 확인·해시 기록. 기존 30개에는 새 생성 확인을 적용하지 않음. 남은 기존 파일의 허가/교체 또는 미사용 자산의 배포 제외가 필요. |
| AS-02 | 사망음 death_burst.mp3 | 선택 폴더 사망음과 적용본이 동일하며 이전 파일 해시 유지. 사용자 지시로 교체하지 않음 | 이전 무료 플랜 생성물의 상업 이용 문제는 남음. 별도 허가 또는 교체 필요. |
| AS-03 | Deadly Kombat Free version / Epic Game Hits SFX | prepare_combat_feedback.py가 기본 Hit·크리티컬을 원본에서 합성. 예전 death_pop도 Resources에 남아 포함 후보. 본인 Asset Store 취득 사용자 확인 | 정확한 상품 연결·개별 조건 확인. |
| AS-04 | CombatMagicSpells | Bear Fighting Spirit Growl과 Ray_Spell 원본 SHA-256 일치. 본인 Asset Store 취득 사용자 확인 | 재개 당시 상품별 조건 확인 대기. 정정: Organice Ice 전환음 2개는 CombatMagicSpells가 아닌 AS-03 Epic Game Hits SFX이며, 후속 PCM 대조에서 앞부분만 자른 동일 음원으로 확인. |
| AS-05 | Leohpaz RPG Essentials Free | 배치음 45_Landing_01 SHA-256 일치. 본인 Asset Store 취득 사용자 확인. 공식 상품 227708의 표준 EULA 확인 | 취득 경로·적용 약관 확인 완료. 기존 제공자 표기 유지. |
| AS-06 | Multiple Solutions / Basic RPG Sounds | 메뉴음 Menu Pop Up Open 1 SHA-256 일치. 본인 Asset Store 취득 사용자 확인. 공식 상품 325670의 표준 EULA 확인 | 취득 경로·적용 약관 확인 완료. 기존 제공자 표기 유지. |
| AS-07 | INDIE FRIENDLY - Casual Game Sounds | 로고음 Special 4-6 SHA-256 일치. 본인 Asset Store 취득 사용자 확인 | 상품별 조건 확인. |
| AS-08 | RedsenGameMusic_Afternoon | TownAfternoonBGM과 원본 Loopable WAV SHA-256 일치. 본인 Asset Store 취득 사용자 확인 | 상품별 조건 확인. |
| AS-09 | Tribal Drum.wav | Resources 사본과 Assets/Sounds 원본 SHA-256 일치. Asset Store 취득 사용자 확인 | 개별 상품명·원작자 연결 및 상품별 조건 확인. |
| AS-10 | Vefects Zap VFX URP | 해태 HeavenlyThunder가 프리팹 4종, 텍스처·머티리얼·셰이더, 낙뢰 WAV 참조. 본인 Asset Store 취득 사용자 재확인 | 상품별 조건 확인. 취득 사실은 사용자 확인으로 기록하며 영수증 직접 검증과 구별. |
| AS-11 | TMP EmojiOne 샘플 | TMP 기본 Resources 스프라이트가 참조하며 SR-12 빌드에도 EmojiOne.png 포함 확인 | 제공된 원문 고지는 동봉. 포함된 오래된 버전의 라이선스 확인 또는 미사용 입증 후 빌드 제외가 필요. 현재 JoyPixels 조건을 과거 EmojiOne 버전에 소급 적용하지 않음. |
| AS-12 | 몬스터·6개 맵·배경·춘동이 등 직접/생성 이미지 | 2026-09-27 사용자 확인: 직접 제작·생성 외 구매·외주·외부 다운로드 이미지 없음 | 사용자 제작 이미지의 외부 취득 여부 확인 완료. 프로젝트에 별도로 포함된 폰트·Game-icons·Vefects·TMP 샘플의 고지는 각 항목으로 관리. 생성 콘텐츠 기록 및 Steam AI 설문은 SR-24에서 이어감. |

`EchoFragments/README.txt`에는 Armazi의 상업/비상업 사용 허용 문장이 있지만, 현재 조사한 런타임 참조 및 Resources 복사본 연결에서는 해당 음악 사용을 확인하지 못했다. 따라서 게임 크레딧에 사용 음악으로 새로 등록하지 않았다.

위 확인 대기 항목을 CC0/OFL 자산과 같은 허가 범주로 묶지 않는다. 확인 후 필요한 저작자 표기나 상품별 고지를 추가하고 SR-12를 완료 처리한다. 9월 27일에는 자산 파일 삭제·음원 교체·미사용 패키지 제거를 수행하지 않았으며, 9월 29일에는 상단 기록대로 새 음원 177개를 교체했다.

## 재생성 및 근거

### ElevenLabs 음원 처리

2026-09-27 공식 페이지 확인: [약관 1(c)](https://elevenlabs.io/terms-of-use)는 무료 이용을 비상업 목적으로 제한하고, [Sound Effects 약관](https://elevenlabs.io/sound-effects-terms)은 그 제한을 면제하지 않는다. [SFX 제품 안내](https://elevenlabs.io/sound-effects)도 무료 계정의 비상업 사용을 명시한다. [도움말](https://help.elevenlabs.io/hc/en-us/articles/13313564601361-Can-I-publish-the-content-I-generate-on-the-platform)은 유료 구독 기간 밖의 생성물에 상업 라이선스가 없다고 설명한다. 따라서 지금 결제하거나 기존 파일을 다시 다운로드하는 것만으로 해결된다고 판단하지 않는다.

일부 하위 홍보 페이지에는 상업 이용 가능이라는 포괄적 문구가 있으나 플랜 구분을 생략한다. 실제 약관·요금제·도움말을 기준으로 현재 파일을 출시 허가 처리하지 않았다. 무료 홍보 크레딧에 별도 조건이 있었다면 그 조건도 제공처에 확인한다.

9월 27일 권장 순서: 기존 효과음에 대한 별도 상업 사용 허가 가능 여부를 먼저 문의 → 불가하면 유료 플랜에서 새로 생성하거나 다른 상업 이용 가능 음원으로 교체. 다시 생성한 음원은 기존 것과 소리가 달라질 수 있어 타이밍·음량 재검수가 필요하다. 당시 오디오 연결은 수정하지 않았다. 9월 29일 교체 및 검수 결과는 상단에 별도로 기록했다. 출처 고지 추가는 허가 취득을 대신하지 않는다.

2026-09-27 공식 도움말의 AI 지원 챗봇에 사용자 승인 후 실제 질의했다. 계정 정보·음원 파일은 전송하지 않고 Free 10,000크레딧 → Starter 전환 → 기존 History 재다운로드 상황과 별도 허가 가능 여부만 질문했다.

- 재다운로드: 챗봇도 생성 당시 플랜이 기준이며, 재다운로드로 소급 전환되지 않는다고 답했다. 공개 라이선스 안내와 일치한다.
- 별도 허가: 처음에는 소급 라이선스·개별 서면 허가 절차가 없다고 답했다. 그 주장의 공식 근거를 재질문하자, 명시된 표준 절차를 찾지 못했다는 의미였으며 **예외적 협의 가능 여부는 AI가 불가능하다고 단정할 수 없고 실제 담당 부서에 확인해야 한다**고 정정했다.
- 따라서 챗봇 답변을 사람 담당자의 허가·거절로 취급하지 않는다. 기존 음원에 대한 별도 허가 가능성은 여전히 미확정이다.
- 현재 [공식 지원 페이지](https://help.elevenlabs.io/hc/en-us/articles/14139492797073-How-can-I-contact-support)의 실제 화면은 `support@elevenlabs.io`로 해당 ElevenLabs 계정 이메일에서 문의하도록 안내한다. 챗봇의 다른 주소 안내보다 이 페이지의 현재 표기를 우선한다.

사람 담당자에게 보낼 문의 초안(이메일 미발송):

받는 주소: `support@elevenlabs.io` · 제목: `Commercial license for existing Sound Effects generated on Free plan`

> Hello ElevenLabs Support, I created sound effects using free credits for my game, BecomeZ: Wild Crew. The selected set contains 207 files plus one death sound. Before distributing the paid game on Steam, I would like to confirm whether you can provide written commercial permission or offer a license for these existing outputs. I understand that subscribing later does not automatically cover outputs generated on the free plan. If licensing the existing files is possible, please specify the covered outputs, requirements, attribution, and any fee. If not, please confirm whether newly generating replacements during an eligible paid subscription would be the appropriate route. Thank you.

### 생성 도구와 기록

- `Tools/build_release_notices.py`: 설치된 패키지/기존 고지에서 배포용 텍스트 생성. 패키지 변경 시 버전 목록을 갱신하고 재실행.
- `Tools/Notices/MCP-LICENSE.txt`: 공식 고정 커밋의 원문.
- `Logs/SteamRelease/SR-12/notice-sources.json`: 고지 원본 경로·해시.
- `build-candidate-dependencies.txt`: 전체 참조 후보.
- `audio-original-links.json`: 공용 음원과 원본의 해시 대조.
- `selected-audio-inventory.json`: 계열별 선택 음원 수. 빈 이전 폴더는 0개로 별도 기록.
- `GameFrontend.before.cs.txt`, `LocalizationDatabase.before.json`: 변경 전 백업.

공식 기준: [SIL OFL](https://openfontlicense.org/open-font-license-official-text/), [Game-icons.net 출처 표기](https://game-icons.net/about.html), [Unity Asset Store 약관](https://unity.com/legal/as-terms), [Kenney Impact](https://kenney.nl/assets/impact-sounds), [RPG](https://kenney.nl/assets/rpg-audio), [Sci-fi](https://kenney.nl/assets/sci-fi-sounds), [Swishes](https://opengameart.org/content/swishes-sound-pack), [Cure Magic](https://opengameart.org/content/cure-magic), [Magic Spell SFX](https://opengameart.org/content/magic-spell-sfx).

## 2026-09-27 구현·빌드 검증 기록

- GameCreditsTests / GameHelpTests / GameFrontendTests: 27/27 통과.
- 실행 중 크레딧이 입력을 차단하고, 크레딧 닫기 후 메뉴 정지를 유지하며, 메뉴 종료 후 이전 시간 배율 0.75로 복귀하는 것 확인.
- 한글·영어 제작진 화면과 고지의 위/아래 스크롤 화면을 캡처해 검수.
- 긴 소프트웨어 고지는 보이는 줄만 그려 전체 스크롤에서 메뉴·닫기 버튼이 유지됨을 확인. 최종 화면: `Logs/SteamRelease/SR-12/software-scroll-verified.png`.
- 설정 원문과 프로필 파일 해시가 작업 전후 동일. 기존 번역 1,632개도 값 기준으로 동일.
- Windows 64비트 비개발 빌드(StrictMode, LZ4) 성공, 오류·경고 0개. `Builds/BecomeZ_WildCrew_Windows_20260927_SR12/BecomeZ Wild Crew.exe`.
- 최종 빌드 수치와 배포 고지 6개의 원본 대비 해시는 `build-summary.json`, `bundled-notice-verification.json`에 기록.
- Unity는 한국어·Boot 씬·재생 정지 상태로 복귀했다.
