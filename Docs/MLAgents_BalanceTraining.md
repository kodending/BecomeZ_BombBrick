# BombBrick 배치 학습 V1

## 학습 범위

- 원정덱 8개체 중 출전 몬스터를 고른다.
- 고정 배치 구역 15칸에 한 개체씩 배치한다.
- 전투, 성장 카드, 적 구성은 현재 게임 규칙과 시드로 자동 실행한다.
- 상점·아이템 구매·승급 판단은 V1 행동 공간에 포함하지 않는다.

행동은 `몬스터 선택 → 타일 선택`의 단일 이산 분기(15개)로 번갈아 처리한다.
유효하지 않은 몬스터와 타일은 action mask로 차단한다.

## 보상

- 정상 배치: `+0.01`
- 비정상 행동: `-0.05` (마스크 방어용)
- 승리/패배: `+1 / -1`
- 아군 잔여 체력, 적에게 준 피해, 승리 속도를 terminal shaping으로 더한다.

## 호환 버전

- Unity package: `com.unity.ml-agents 2.3.0-exp.3`
- Python trainer: `mlagents 0.30.0`
- Python: `3.8.13 ~ 3.10.8`

Unity package와 Python trainer는 ML-Agents Release 20 조합을 사용한다.

## 실행

1. 프로젝트에 준비된 `.venv-mlagents` 가상환경을 사용한다.
2. Unity에서 `Assets/Scenes/MonsterAutoBattleMLTraining.unity`를 연다.
3. 프로젝트 루트에서 아래 명령을 실행한다.

```powershell
.\.venv-mlagents\Scripts\mlagents-learn.exe Config/mlagents/bombbrick_formation_ppo.yaml --run-id=formation_v1
```

4. 터미널에 `Start training by pressing the Play button`이 표시되면 Unity Play를 누른다.
5. 결과 모델은 `results/formation_v1/BombBrickFormationV1.onnx`에 생성된다.

현재 학습 씬은 Wave 1~12를 순환한다. 에피소드마다 시드가 바뀌므로 적 조합,
성장 선택과 배치 판단도 함께 달라지며, 각 전투 결과가 PPO 학습 데이터로 누적된다.
같은 Wave만 검사하려면 `ML Balance Agent`의 `Fixed Wave`를 1~12로 지정하고,
전체 곡선을 수집하려면 `Fixed Wave`를 0으로 둔다.

게임 안의 DEV `Run 1-12` 버튼은 빠른 휴리스틱 밸런스 검사이며 ML 학습은 아니다.
ML 학습 데이터는 위 명령을 먼저 실행한 뒤 에디터 Play 버튼을 눌러 수집한다.

재학습 시 기존 결과를 이어 쓰려면 `--resume`, 같은 run-id를 덮어쓰려면
`--force`를 사용한다.

빠른 파이프라인 검증은 다음 파일을 사용한다.

```powershell
.\.venv-mlagents\Scripts\mlagents-learn.exe Config/mlagents/bombbrick_formation_pilot.yaml --run-id=formation_pilot_v1
```

이 PC의 Python 3.10용 환경에는 Release 20의 오래된 NumPy 1.21.2 고정 문제를
피하기 위해 NumPy 1.23.5와 PyTorch 1.11.0 CPU 조합을 적용했다.
