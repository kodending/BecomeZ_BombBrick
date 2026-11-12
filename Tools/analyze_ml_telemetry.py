#!/usr/bin/env python3
"""Summarize schema-10 ML combat telemetry without external dependencies."""

import argparse
import collections
import json
from pathlib import Path


def add(bucket, key, won, damage=0.0, survived=0.0):
    row = bucket[key]
    row["games"] += 1
    row["wins"] += int(won)
    row["damage"] += damage
    row["survived"] += survived


def ranked(bucket, minimum=1, limit=10):
    rows = []
    for key, value in bucket.items():
        games = value["games"]
        if games < minimum:
            continue
        rows.append((key, games, value["wins"] / games,
                     value["damage"] / games, value["survived"] / games))
    return sorted(rows, key=lambda row: (-row[2], -row[1], row[0]))[:limit]


def read_episode(path):
    context = None
    placements, results, behavior = {}, {}, collections.defaultdict(
        lambda: {"steps": 0, "target": 0.0, "ally": 0.0,
                 "enemy": 0.0, "actions": collections.Counter()})
    winner = ""
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            for line in stream:
                event = json.loads(line)
                if event.get("schema") != 10:
                    continue
                kind = event.get("event")
                if kind == "episode_start" and event.get("mode") == "ml_training":
                    context = event
                elif kind == "placement":
                    placements[event["unit"]] = event
                elif kind == "unit_result":
                    results[event["unit"]] = event
                elif kind == "episode_end":
                    winner = event.get("winner", "")
                elif kind == "step":
                    row = behavior[event["unit"]]
                    row["steps"] += 1
                    row["target"] += event.get("target_distance", 0.0)
                    row["ally"] += event.get("nearest_ally", 0.0)
                    row["enemy"] += event.get("nearest_enemy", 0.0)
                    row["actions"][event.get("action", "")] += 1
    except (OSError, ValueError, KeyError):
        return None
    return (context, placements, results, behavior, winner) if context else None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("telemetry", type=Path)
    parser.add_argument("--output", type=Path, default=Path("Logs/MLBalance/latest.md"))
    parser.add_argument("--minimum", type=int, default=5)
    args = parser.parse_args()

    buckets = {name: collections.defaultdict(lambda: collections.defaultdict(float))
               for name in ("combo", "placement", "monster", "family",
                            "personality", "trait", "item", "context")}
    personality_behavior = collections.defaultdict(
        lambda: {"steps": 0, "target": 0.0, "ally": 0.0, "enemy": 0.0,
                 "target_changes": 0, "games": 0, "actions": collections.Counter()})
    episodes = 0
    for path in sorted(args.telemetry.glob("battle_*.jsonl")):
        episode = read_episode(path)
        if not episode:
            continue
        context, placements, results, behavior, winner = episode
        won = winner == "Player"
        deployed = [row for row in placements.values()
                    if row.get("team") == "Player" and not row.get("in_storage")]
        if not deployed:
            continue
        episodes += 1
        combo = "|".join(sorted(row["monster_id"] for row in deployed))
        formation = "|".join(sorted(
            f'{row["monster_id"]}@{row["anchor_x"]},{row["anchor_y"]}'
            for row in deployed))
        context_key = (f'{context.get("region_id")}|{context.get("difficulty")}|'
                       f'W{context.get("wave")}|{context.get("scenario_profile")}')
        add(buckets["combo"], combo, won)
        add(buckets["placement"], formation, won)
        add(buckets["context"], context_key, won)
        for row in deployed:
            result = results.get(row["unit"], {})
            damage = float(result.get("damage_dealt", 0.0))
            survived = float(bool(result.get("survived")))
            add(buckets["monster"], row["monster_id"], won, damage, survived)
            add(buckets["family"], row.get("family_id", ""), won, damage, survived)
            add(buckets["personality"], row.get("personality", ""), won,
                damage, survived)
            for trait in filter(None, row.get("active_traits", "").split("|")):
                add(buckets["trait"], trait, won, damage, survived)
            for item in filter(None, row.get("items", "").split("|")):
                add(buckets["item"], item, won, damage, survived)
            observed = behavior.get(row["unit"])
            if observed and observed["steps"]:
                personality = row.get("personality", "")
                summary = personality_behavior[personality]
                summary["steps"] += observed["steps"]
                summary["target"] += observed["target"]
                summary["ally"] += observed["ally"]
                summary["enemy"] += observed["enemy"]
                summary["target_changes"] += int(result.get("target_changes", 0))
                summary["games"] += 1
                summary["actions"].update(observed["actions"])

    lines = ["# ML 전투 데이터 요약", "", f"- 유효 에피소드: {episodes}",
             "- 기준: 배치된 아군, schema 10 ML 학습 로그", ""]
    labels = (("combo", "조합 TOP 10"), ("placement", "조합+배치 TOP 10"),
              ("monster", "몬스터 TOP 10"), ("family", "계열 TOP 10"),
              ("personality", "성격 TOP 10"), ("trait", "특성 TOP 10"),
              ("item", "아이템 TOP 10"), ("context", "조건 TOP 10"))
    for name, label in labels:
        lines += [f"## {label}", "", "| 항목 | 표본 | 승률 | 평균 피해 | 생존율 |",
                  "|---|---:|---:|---:|---:|"]
        for key, games, win_rate, damage, survival in ranked(
                buckets[name], args.minimum, 10):
            lines.append(f"| {key} | {games} | {win_rate:.1%} | {damage:.1f} | {survival:.1%} |")
        lines.append("")

    lines += ["## 성격별 행동 차이", "",
              "| 성격 | 전투 | 관측 | 표적 거리 | 아군 거리 | 적 거리 | 표적 변경 | 주 행동 |",
              "|---|---:|---:|---:|---:|---:|---:|---|"]
    for personality, row in sorted(personality_behavior.items()):
        steps = max(1, row["steps"])
        games = max(1, row["games"])
        dominant = row["actions"].most_common(1)
        lines.append(
            f'| {personality} | {row["games"]} | {row["steps"]} | '
            f'{row["target"] / steps:.3f} | {row["ally"] / steps:.3f} | '
            f'{row["enemy"] / steps:.3f} | {row["target_changes"] / games:.2f} | '
            f'{dominant[0][0] if dominant else "-"} |')
    lines.append("")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text("\n".join(lines), encoding="utf-8")
    print(f"episodes={episodes} report={args.output}")


if __name__ == "__main__":
    main()
