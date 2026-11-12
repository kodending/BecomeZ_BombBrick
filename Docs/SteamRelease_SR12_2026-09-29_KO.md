# SR-12 크레딧·자산 이용 근거 최종 검수

2026-09-29 구현·검증, 2026-09-30 출처 정정. **SR-12 완료.** 사용자는 기존 활성 계열 음원 28개와 공통 사망음 1개 모두 Unity Asset Store에서 받은 이용 가능한 음원이라고 확인했다. 이를 이전 ElevenLabs 무료 생성물로 일괄 분류했던 판단을 정정하고 이용 권한 확인 대기를 해제했다. 이 출처 확인의 근거는 사용자의 직접 확인이며, 파일별 상품 대조를 새로 수행했다는 뜻은 아니다. 기존 음원은 그대로 유지한다. 이 빌드는 SR-12 기능 검증본이며 전체 출시 검증은 후속 과제에서 진행한다.

## 반영 사항

- 기존 메인/일시정지 크레딧 메뉴, PangStudio / Pangsoo / 춘동이 표기 유지.
- `OtherAssets.txt`에 아래 9개 Asset Store 상품의 정확한 제작자·상품 링크를 반영. Afternoon 음악 표기와 Tribal Drum의 Tim Beek 표기 보완.
- 게임 내 한글 크레딧 메뉴에서 추가 자산 본문의 위·아래 구간, 긴 URL 줄바꿈과 스크롤 확인. 개별 라이선스 원문은 임의 번역하지 않았다.
- 배포 고지 6개 유지: `GameIcons.txt`, `AudioSources.txt`, `OtherAssets.txt`, `Galmuri-OFL.txt`, `LiberationSans-OFL.txt`, `Software.txt`.
- 미사용 웅이 `roarend.wav`, `departure.wav`는 GUID·내용을 유지하여 `Assets/Sounds/UnusedCombat/`로 이동. 선택된 원본 음원은 그대로 보존.
- 미사용 TMP `EmojiOne.asset`은 `Resources/Sprite Assets/`에서 `Assets/TextMesh Pro/Sprites/`로 이동하고 TMP Settings의 기본 스프라이트 참조를 해제. 샘플 이미지·원본 고지는 프로젝트에 보존하되 배포에서는 제외. 첫 빌드에서 저장된 참조가 남아 있는 것을 발견하여 설정 파일을 수정한 뒤 재검증했다.
- 전투 동작, 현재 사용 중인 음원 연결, 사용자 번역 검수 내용은 이번 크레딧 작업에서 변경하지 않았다.

## Asset Store 확인 결과

사용자는 아래 자산을 모두 본인 Unity Asset Store 계정으로 취득했다고 확인했다. 로컬 캐시의 `.unitypackage` 상품 메타데이터에서 상품 ID·이름·제작자를 확인하고, 공식 상품 페이지의 **Standard Unity Asset Store EULA** 표시를 대조했다. 캐시 기록을 계정 영수증으로 취급하지 않는다. 게임에 포함하는 이용 범위는 [Unity Asset Store EULA](https://unity.com/legal/as-terms)를 기준으로 정리했다.

| 기존 ID | 상품·제작자 | 사용 연결 및 판정 |
|---|---|---|
| AS-03 | [Free Deadly Kombat — Daniel SoundsGood](https://assetstore.unity.com/packages/audio/sound-fx/free-deadly-kombat-228835) | 기본 Hit·크리티컬 합성 원본. 상품·취득 경로·적용 약관 확인. |
| AS-03 | [Epic Game Hits SFX — Epic Sounds and FX](https://assetstore.unity.com/packages/audio/sound-fx/epic-game-hits-sfx-36617) | 기본 Hit·크리티컬 및 Organice Ice 화면 전환음. 상품·취득 경로·약관 확인. 전환음의 기존 CombatMagicSpells 분류를 정정. |
| AS-04 | [Combat Magic Spells — AD Sounds / Alan Dalcastagne](https://assetstore.unity.com/packages/audio/sound-fx/combat-magic-spells-sound-effects-214178) | Bear Fighting Spirit Growl·Ray_Spell 원본 연결 확인. 상품·취득 경로·약관 확인. |
| AS-05 | [RPG Essentials Sound Effects - FREE! — leohpaz](https://assetstore.unity.com/packages/audio/sound-fx/rpg-essentials-sound-effects-free-227708) | 배치음 45_Landing_01 원본 해시 일치. 상품·취득 경로·약관 확인. |
| AS-06 | [Basic RPG Sounds — Multiple Solutions](https://assetstore.unity.com/packages/audio/sound-fx/basic-rpg-sounds-325670) | 메뉴음 Menu Pop Up Open 1 원본 해시 일치. 상품·취득 경로·약관 확인. |
| AS-07 | [Casual Game Sounds - Indie Friendly Packs — PlaceHolder Inc](https://assetstore-fallback.unity.com/packages/audio/sound-fx/casual-game-sounds-indie-friendly-packs-283136) | 로고음 Special 4-6 원본 해시 일치. 상품·취득 경로·약관 확인. |
| AS-08 | [[FreeBGM] Afternoon — REDSEN Game Music](https://assetstore.unity.com/packages/audio/music/freebgm-afternoon-391254) | TownAfternoonBGM 원본 Loopable WAV 해시 일치. 상품·취득 경로·약관 확인 및 음악 출처 표기. |
| AS-09 | [Free Casual SoundFX Pack — Tim Beek](https://assetstore.unity.com/packages/audio/sound-fx/free-casual-soundfx-pack-164843) | 캐시 패키지의 Tribal Drum.wav와 런타임 파일 SHA-256 일치. 상품·제작자·취득 경로·약관 확인 완료. |
| AS-10 | [Zap VFX - URP — Vefects](https://assetstore.unity.com/packages/vfx/particles/spells/zap-vfx-urp-303479) | 해태 낙뢰 프리팹·머티리얼·텍스처·음원 연결. 상품·취득 경로·약관 확인. |

Tribal Drum의 SHA-256: `43b98cd7c7fe32cd94ec4b2f96ba0924a818b9f390b9ffaf8a3277db24eeef3b`.

Organice Ice 1/2는 앞부분 각각 6,174 / 7,497 샘플을 잘라낸 파일이다. 정렬 후 gain 1.0, PCM 오차 0으로 원본 연결을 확인했다. 파일 해시가 다르다는 이유만으로 별도 출처로 분류하지 않았다.

## 기존 음원과 나머지 자산

- AS-01: 현재 활성 계열 음원 205개 중 새 177개는 ElevenLabs 유료 플랜에서 새로 생성했다는 사용자 확인과 변경 해시를 기록했다. 기존 28개는 2026-09-30 사용자 확인에 따라 Unity Asset Store 취득 음원으로 출처를 정정하고 이용 권한 확인 대기를 해제했다. 미사용 웅이 2개는 배포 제외 완료.
- AS-02: 공통 사망음도 2026-09-30 사용자 확인에 따라 Unity Asset Store 취득 음원으로 출처를 정정했다. 기존 파일을 보존하며 이용 권한 확인 대기를 해제했다.
- AS-11: EmojiOne은 최종 상세 빌드 목록에서 asset·PNG 모두 제외 확인. 배포용 추가 자산 고지에서 해당 항목 제거. 프로젝트 원본과 원본 고지는 보존.
- AS-12: 직접 제작·생성 외 구매·외주·외부 다운로드 이미지가 없다는 사용자 확인을 유지한다. Steam AI 설문 관련 사항은 SR-24에서 진행한다.
- Galmuri, Liberation Sans, Game-icons, 기존 CC0 전투 샘플, URP 디버그 폰트 및 런타임 패키지 고지는 기존 SR-12 조사·원문을 유지한다.

기존 파일 유지 목록은 `Logs/AudioReplacement_2026-09-29/unchanged-files.txt`에 있다. 파일 해시가 같다는 사실은 교체 여부의 근거이며 ElevenLabs 출처를 단정하는 근거가 아니다. 최신 출처 확인은 `license-provenance.json`에 기록했다. 다음 과제는 SR-13 문의·버전·오류 로그 안내다.

## 검증

- 오디오·크레딧·도움말·메뉴 EditMode 검사 **70/70 통과**. 작업 `86b80fa4623345ac937e9cb3d0152f85`.
- 저장된 TMP 참조를 최종 수정한 뒤 크레딧 검사 **3/3 재통과**. 작업 `f65e2df5324e4f6197d4ebf88a5005fb`.
- `Tools/Audio/refresh_selected_sounds.py check`: 활성 205개, 교체 177개, 보존 28개 모두 일치. 기본 Hit 대비 기존 음량 보정 유지.
- Unity 2022.3.62f2 Windows x64 비개발 clean/LZ4/strict/detailed 빌드 성공. **오류 0, 경고 0**, 278.28 MiB. 작업 `build-5a35f2a4cf`.
- 실제 상세 빌드의 원본 경로 7,504개에서 EmojiOne 및 미사용 웅이 2개 없음. 고지 6개 모두 원본과 배포본 SHA-256 일치.
- 세이브 관련 파일 42개 해시와 저장된 사용자 설정 유지. Boot 씬·재생 정지 상태로 복귀.

빌드: `Builds/BecomeZ_WildCrew_Windows_20260929_SR12/BecomeZ Wild Crew.exe`.

검증 자료는 `Logs/SteamRelease/SR-12-20260929/`에 보관:

- `cached-package-evidence.json`: 상품 메타데이터·원본 해시 연결.
- `transition-source-comparison.json`: 화면 전환음 PCM 비교.
- `packed-assets.txt`, `build-verification.txt`, `build-warnings.txt`: 최종 빌드 포함 자산·결과.
- `notices-in-build.json`: 고지 6개 배포 해시.
- `credits-assets-top-1.png`, `credits-assets-bottom.png`: 실제 게임 내 크레딧 화면.
- `Before/`, `before.json`: 변경 전 파일 보관.

고지 재생성은 기존 `Tools/build_release_notices.py`를 사용한다. 상세 이전 기록은 `Docs/SteamRelease_SR12_2026-09-27_KO.md`, 음원 교체 기록은 `Docs/CombatSoundReplacement_2026-09-29.md`에 있다.
