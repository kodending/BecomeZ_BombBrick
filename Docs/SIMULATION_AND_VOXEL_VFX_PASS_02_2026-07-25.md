# 시뮬레이션·복셀 전투 VFX 2차 작업 결과

점검일: 2026-07-25  
환경: Unity 2022.3.62f2

## 공용 복셀 파티클 시스템

`VoxelCombatParticleSystem`을 추가해 전투 피드백을 Cube Mesh 기반 파티클로 통일했다.

공용 표현:

- 일반 공격 피격: 밝은 아이보리 파편
- 스킬 피격: 보라색 파편
- 치명타: 금색 대형 파편과 중심 flash
- 피해 차단: 청록색 반사 파편
- 사망: 진영색 원형 burst
- 스킬 범위 발동: 방사형 voxel burst
- 이동·베기 궤적: 수명이 짧은 voxel trail
- 투사체 시작점: 소형 voxel muzzle

전환한 전투 효과:

- 공통 일반 공격·스킬·치명타·BLOCK 피격
- 공통 사망 및 스킬 pulse
- 재규어 베기 궤적과 사냥 표식
- 거미 기본 공격 실 투사체
- 거미 포획 장판
- 속박·빙결 상태 표식
- 개구리 혀 채찍의 16개 Cube 체인을 3D Cube Particle 체인으로 전환

Wave 준비 시 공용 파티클 채널을 즉시 비워 이전 Wave 이펙트가 남지 않도록 했다.

유닛 선택 링, 체력 UI, 전리품 오브젝트는 전투 이펙트가 아니므로 기존 표현을 유지한다.

## VFX와 게임 난수 분리

초기 구현 점검 중 파티클의 모양 난수가 `UnityEngine.Random`을 소비하면 전투의 치명타·스킬·AI 난수 순서에 영향을 줄 수 있음을 확인했다.

조치:

- 공용 VFX에 독립된 xorshift PRNG 적용
- 지속형 파티클 패턴의 회전은 index 기반으로 결정
- VFX 호출 전후에 게임 RNG 값이 동일한지 확인하는 회귀 테스트 추가

결과:

- 시각 효과의 발생 횟수와 종류가 seeded 전투 결과에 영향을 주지 않는다.

## 시뮬레이션 watchdog

- Balance/ML 자동 전투 제한시간: 60초
- 제한시간 도달 시 남은 체력 비율로 결과를 확정
- Balance JSON/CSV에 `timedOut`, `timedOutRuns` 기록
- Balance 패널에 100회 실행 버튼과 timeout 수 표시

## 발견한 교착과 수정

첫 100-seed sweep에서 다음 교착을 발견했다.

- Wave: 6
- runIndex: 67
- seed: 781298
- 상태: 문타샤와 벨로스가 각각 1개체 남은 뒤 60초간 피해 교환 없음

수정:

- 6초 동안 피해가 없으면 양측의 타깃, 공격 슬롯, 이동 예약, A* 경로, 후퇴 상태를 초기화
- 가장 가까운 적을 다시 지정하고 공격 경로를 재생성
- 두 번째 복구부터 짧은 전진 보정 적용
- 복구 시점에 작은 주황색 voxel 신호 표시

## 최종 100-seed 결과

VFX 난수 분리 및 교착 복구 적용 후 동일 조건을 다시 실행했다.

| 항목 | 결과 |
|---|---:|
| Wave | 6 |
| Base seed | 250725 |
| 완료 | 100 / 100 |
| 승리 | 51 |
| 승률 | 51% |
| 평균 전투 시간 | 8.42초 |
| 평균 아군 사망 | 2.17 |
| timeout | 0 |

최종 보고서:

- `balance_wave_6_seed_250725_20260724_161145_514.json`
- `balance_wave_6_seed_250725_20260724_161145_514.csv`

저장 위치:

`%USERPROFILE%\AppData\LocalLow\DefaultCompany\BecomeZ_BombBrick\BalanceReports`

## 검증

- Unity 컴파일 오류: 0
- Unity Console Error/Warning: 0
- EditMode 회귀 테스트: 186 / 186 통과
- 복셀 VFX Game View 캡처:
  - `Assets/Screenshots/voxel_combat_vfx_validation.png`

## 다음 VFX 단계

1. 계열별 색상·파편 모양·밀도를 Master DB 데이터로 분리한다.
2. 보호막 파괴, 회복, 독·화상 tick에 전용 voxel 문법을 추가한다.
3. 저사양 품질에서는 방출량을 줄이되 타이밍과 색상은 유지하는 품질 단계를 만든다.
4. 실제 플레이 캡처를 기준으로 크기와 밝기를 조정한다.
