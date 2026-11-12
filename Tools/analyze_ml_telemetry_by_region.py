#!/usr/bin/env python3
"""Build five map-specific balance reports from schema-10 battle telemetry."""

import argparse
import collections
import concurrent.futures
import datetime as dt
import itertools
import json
from pathlib import Path


REGIONS = (
    ("prototype_region", "태초의 초원", "velos|terakan|ager|lilas|woongi|nukgu|aranea|chundongi|tino|chiwoo|kogason|kingmore|gaiason|jangpung|forestdeer|entpone"),
    ("poison_fog_swamp", "독안개 습지", "raputo|geomchi|golgaebi|kimbul|ragon|versus|sundongi|bonrex|toto|nectis|doksirum|flotis|eclipsedeer|holophonas|vivre|mukryeong"),
    ("crimson_jungle", "붉은 밀림", "velok|nox|rokrok|tarvil|magril|patjwi|berber|honga|hongryeon|magno|maka|mantius|silanti|griphonas|igvire|yeomhae"),
    ("frozen_highlands", "얼어붙은 고원", "wilo|seola|krogal|bingareu|seoljwi|ironkuma|seollang|icearachne|seolmyo|cheolno|seolgason|irontusk|atlagon|lunadeer|frovire|haeta"),
    ("sky_sanctuary", "천공 성역", "kingpa|frosk|moska|kongjwi|byeoljwi|dami|wolyeong|uno|santius|lumirum|solarbeetle|noepung|holydeer|pone|temvire|geumgangsu"),
)
REGION_META = {region_id: (name, ids.split("|")) for region_id, name, ids in REGIONS}
EVENT_MARKERS = tuple(f'"event":"{name}"' for name in
                      ("episode_start", "placement", "episode_end", "unit_result"))


def fresh_stat():
    return {"games": 0, "wins": 0, "residual": 0.0,
            "damage": 0.0, "survived": 0.0}


def add(bucket, key, won, residual, damage=0.0, survived=0.0):
    row = bucket[key]
    row["games"] += 1
    row["wins"] += int(won)
    row["residual"] += residual
    row["damage"] += damage
    row["survived"] += survived


def finish(key, row):
    games = row["games"]
    return {"key": key, "games": games, "win_rate": row["wins"] / games,
            "matched_lift": row["residual"] / games,
            "avg_damage": row["damage"] / games,
            "survival_rate": row["survived"] / games}


def read_episode(path):
    context, end = None, None
    placements, results = {}, {}
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            for line in stream:
                if not any(marker in line for marker in EVENT_MARKERS):
                    continue
                event = json.loads(line)
                if event.get("schema") != 10:
                    continue
                kind = event.get("event")
                if kind == "episode_start" and event.get("mode") == "ml_training":
                    context = event
                elif kind == "placement":
                    placements[event["unit"]] = event
                elif kind == "episode_end":
                    end = event
                elif kind == "unit_result":
                    results[event["unit"]] = event
    except (OSError, ValueError, KeyError):
        return None
    if not context or not end or end.get("winner") not in ("Player", "Enemy"):
        return None
    region = context.get("region_id")
    if region not in REGION_META:
        return None
    players, enemies = [], []
    for unit, placed in placements.items():
        if placed.get("in_storage"):
            continue
        result = results.get(unit, {})
        entry = (
            placed.get("monster_id", ""), placed.get("family_id", ""),
            placed.get("expedition_rank", ""),
            tuple(filter(None, placed.get("items", "").split("|"))),
            int(placed.get("anchor_x", 0)), int(placed.get("anchor_y", 0)),
            float(result.get("damage_dealt", 0.0)),
            float(bool(result.get("survived"))),
        )
        (players if placed.get("team") == "Player" else enemies).append(entry)
    if not players or not enemies:
        return None
    return {
        "region": region, "difficulty": context.get("difficulty", ""),
        "wave": int(context.get("wave", 0)),
        "profile": context.get("scenario_profile", ""),
        "player_won": end["winner"] == "Player",
        "time": float(end.get("time", 0.0)),
        "player_survivors": int(end.get("player_survivors", 0)),
        "enemy_survivors": int(end.get("enemy_survivors", 0)),
        "players": tuple(players), "enemies": tuple(enemies),
    }


def formation_key(enemies):
    front = sum(unit[5] <= 4 for unit in enemies)
    back = len(enemies) - front
    depth = "전방" if front > back else "후방" if back > front else "균형"
    xs = [unit[4] for unit in enemies]
    spread = "분산" if max(xs) - min(xs) >= 5 else "밀집"
    return depth + "·" + spread


def load_names(path):
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    return {row["monsterId"]: row["displayName"] for row in data["rows"]}


def build(episodes, minimum):
    context = collections.defaultdict(lambda: [0, 0])
    for episode in episodes:
        key = (episode["region"], episode["difficulty"], episode["wave"],
               episode["profile"], len(episode["players"]), len(episode["enemies"]))
        context[key][0] += 1
        context[key][1] += int(episode["player_won"])

    regions = {}
    for region_id, _, expected_ids in REGIONS:
        region_episodes = [episode for episode in episodes if episode["region"] == region_id]
        buckets = {name: collections.defaultdict(fresh_stat) for name in
                   ("unit", "pair", "triple", "team", "item", "formation",
                    "matchup", "pair_matchup", "rank", "difficulty")}
        waves = collections.defaultdict(lambda: collections.defaultdict(float))
        difficulty_waves = collections.defaultdict(lambda: [0, 0])
        observed = set()
        for episode in region_episodes:
            key = (region_id, episode["difficulty"], episode["wave"], episode["profile"],
                   len(episode["players"]), len(episode["enemies"]))
            baseline_player = context[key][1] / context[key][0]
            player_won = episode["player_won"]
            enemy_won = not player_won
            player_residual = float(player_won) - baseline_player
            enemy_residual = float(enemy_won) - (1.0 - baseline_player)
            enemies = episode["enemies"]
            enemy_ids = tuple(sorted(unit[0] for unit in enemies))
            player_families = set(unit[1] for unit in episode["players"] if unit[1])
            observed.update(enemy_ids)

            wave = waves[episode["wave"]]
            wave["games"] += 1
            wave["player_wins"] += int(player_won)
            wave["time"] += episode["time"]
            wave["player_survivors"] += episode["player_survivors"]
            wave["enemy_survivors"] += episode["enemy_survivors"]
            wave["player_units"] += len(episode["players"])
            wave["enemy_units"] += len(enemies)
            difficulty_wave = difficulty_waves[(episode["difficulty"], episode["wave"])]
            difficulty_wave[0] += 1
            difficulty_wave[1] += int(player_won)

            add(buckets["team"], "|".join(enemy_ids), enemy_won, enemy_residual)
            add(buckets["formation"], formation_key(enemies), enemy_won, enemy_residual)
            add(buckets["difficulty"], episode["difficulty"], enemy_won, enemy_residual)
            for unit in enemies:
                monster_id, _, rank, items, _, _, damage, survived = unit
                add(buckets["unit"], monster_id, enemy_won, enemy_residual, damage, survived)
                add(buckets["rank"], rank, enemy_won, enemy_residual, damage, survived)
                for item in items:
                    add(buckets["item"], item, enemy_won, enemy_residual, damage, survived)
                for family in player_families:
                    add(buckets["matchup"], monster_id + "|" + family,
                        player_won, player_residual)
            for pair in itertools.combinations(enemy_ids, 2):
                pair_key = "|".join(pair)
                add(buckets["pair"], pair_key, enemy_won, enemy_residual)
                for family in player_families:
                    add(buckets["pair_matchup"], pair_key + "::" + family,
                        player_won, player_residual)
            for triple in itertools.combinations(enemy_ids, 3):
                add(buckets["triple"], "|".join(triple), enemy_won, enemy_residual)

        tables = {name: [finish(key, row) for key, row in bucket.items()]
                  for name, bucket in buckets.items()}
        unit_lift = {row["key"]: row["matched_lift"] for row in tables["unit"]}
        pair_lift = {row["key"]: row["matched_lift"] for row in tables["pair"]}
        for row in tables["pair"]:
            ids = row["key"].split("|")
            row["synergy_lift"] = row["matched_lift"] - sum(unit_lift[x] for x in ids) / 2
        for row in tables["triple"]:
            ids = row["key"].split("|")
            pairs = ("|".join(pair) for pair in itertools.combinations(ids, 2))
            known = [pair_lift[pair] for pair in pairs if pair in pair_lift]
            row["synergy_lift"] = row["matched_lift"] - (sum(known) / len(known) if known else 0)
        wave_rows = []
        for wave_number, row in sorted(waves.items()):
            games = row["games"]
            wave_rows.append({"wave": wave_number, "games": int(games),
                              "player_win_rate": row["player_wins"] / games,
                              "avg_time": row["time"] / games,
                              "avg_player_survivors": row["player_survivors"] / games,
                              "avg_enemy_survivors": row["enemy_survivors"] / games,
                              "avg_player_units": row["player_units"] / games,
                              "avg_enemy_units": row["enemy_units"] / games})
        expected = set(expected_ids.split("|"))
        difficulty_wave_rows = [{"difficulty": difficulty, "wave": wave,
                                 "games": row[0], "player_win_rate": row[1] / row[0]}
                                for (difficulty, wave), row in sorted(difficulty_waves.items())]
        regions[region_id] = {
            "episodes": len(region_episodes), "observed_monsters": sorted(observed),
            "missing_current_monsters": sorted(expected - observed),
            "unexpected_monsters": sorted(observed - expected), "waves": wave_rows,
            "difficulty_waves": difficulty_wave_rows,
            "tables": tables, "minimum": minimum,
        }
    return regions


def pct(value):
    return f"{value:.1%}"


def monster_label(key, names):
    return " + ".join(names.get(value, value) for value in key.split("|"))


def table_rows(rows, minimum, sort_key, reverse=True, limit=10):
    return sorted((row for row in rows if row["games"] >= minimum),
                  key=sort_key, reverse=reverse)[:limit]


def write_reports(output, regions, names, minimum, total, skipped):
    output.mkdir(parents=True, exist_ok=True)
    index = ["# 맵별 ML 전투 집계", "", f"- 유효 에피소드: {total:,}",
             f"- 제외된 미완료·구형 로그: {skipped:,}",
             "- 조건 보정: 맵·난이도·Wave·아이템 시나리오·양 팀 개체 수가 같은 전투 기준", ""]
    data = {"generated_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
            "episodes": total, "skipped": skipped, "regions": regions}
    for order, (region_id, region_name, expected_ids) in enumerate(REGIONS, 1):
        region = regions[region_id]
        filename = f"{order:02d}_{region_id}.md"
        index.append(f"- [{region_name}]({filename}): {region['episodes']:,}전투, "
                     f"현행 16개체 관측 {16-len(region['missing_current_monsters'])}/16, "
                     f"난이도×Wave {len(region['difficulty_waves'])}/48")
        lines = [f"# {region_name} ML 집계", "", f"- 유효 전투: {region['episodes']:,}",
                 f"- 현행 몬스터: {', '.join(names.get(x, x) for x in expected_ids.split('|'))}",
                 f"- 미관측: {', '.join(region['missing_current_monsters']) or '없음'}",
                 f"- 현행 외 관측: {', '.join(region['unexpected_monsters']) or '없음'}", "",
                 f"> 현재 로그의 난이도×Wave 커버리지는 {len(region['difficulty_waves'])}/48입니다. "
                 "Wave마다 난이도 한 종류만 관측되어 Wave 승률은 난이도와 분리해 해석해야 합니다.", "",
                 "## Wave 난이도", "",
                 "| Wave | 표본 | 아군 승률 | 전투 시간 | 아군 생존 | 적 생존 | 아군 수 | 적 수 |",
                 "|---:|---:|---:|---:|---:|---:|---:|---:|"]
        for row in region["waves"]:
            lines.append(f"| {row['wave']} | {row['games']:,} | {pct(row['player_win_rate'])} | "
                         f"{row['avg_time']:.1f}초 | {row['avg_player_survivors']:.2f} | "
                         f"{row['avg_enemy_survivors']:.2f} | {row['avg_player_units']:.2f} | "
                         f"{row['avg_enemy_units']:.2f} |")
        difficulty_lookup = {(row["difficulty"], row["wave"]): row
                             for row in region["difficulty_waves"]}
        lines += ["", "## 난이도별 Wave 아군 승률", "",
                  "| Wave | 초보자 | 보통 | 숙련자 | 헬 |",
                  "|---:|---:|---:|---:|---:|"]
        for wave in range(1, 13):
            values = []
            for difficulty in ("Beginner", "Normal", "Veteran", "Hell"):
                row = difficulty_lookup.get((difficulty, wave))
                values.append(f"{pct(row['player_win_rate'])} ({row['games']})" if row else "-")
            lines.append(f"| {wave} | " + " | ".join(values) + " |")
        lines += ["", "## 16개체 성능", "",
                  "| 개체 | 표본 | 적 승률 | 조건 보정 우세 | 평균 피해 | 생존율 |",
                  "|---|---:|---:|---:|---:|---:|"]
        current = set(expected_ids.split("|"))
        units = sorted((row for row in region["tables"]["unit"] if row["key"] in current),
                       key=lambda row: row["matched_lift"], reverse=True)
        for row in units:
            lines.append(f"| {monster_label(row['key'], names)} | {row['games']:,} | "
                         f"{pct(row['win_rate'])} | {pct(row['matched_lift'])} | "
                         f"{row['avg_damage']:.1f} | {pct(row['survival_rate'])} |")

        sections = (
            ("강한 2개체 조합", "pair", lambda row: row["matched_lift"], False),
            ("순수 시너지 2개체 조합", "pair", lambda row: row["synergy_lift"], True),
            ("강한 3개체 조합", "triple", lambda row: row["matched_lift"], False),
        )
        for title, bucket, key_fn, show_synergy in sections:
            rows = table_rows(region["tables"][bucket], minimum, key_fn)
            lines += ["", f"## {title}", "",
                      "| 조합 | 표본 | 적 승률 | 조건 보정 우세" + (" | 시너지 초과" if show_synergy else "") + " |",
                      "|---|---:|---:|---:" + ("|---:" if show_synergy else "") + "|"]
            for row in rows:
                extra = f" | {pct(row['synergy_lift'])}" if show_synergy else ""
                lines.append(f"| {monster_label(row['key'], names)} | {row['games']:,} | "
                             f"{pct(row['win_rate'])} | {pct(row['matched_lift'])}{extra} |")

        item_rows = table_rows(region["tables"]["item"], minimum,
                               lambda row: row["matched_lift"])
        lines += ["", "## 적 아이템 영향", "",
                  "| 아이템 | 장착 표본 | 적 승률 | 조건 보정 우세 | 장착자 피해 | 장착자 생존 |",
                  "|---|---:|---:|---:|---:|---:|"]
        for row in item_rows:
            lines.append(f"| {row['key']} | {row['games']:,} | {pct(row['win_rate'])} | "
                         f"{pct(row['matched_lift'])} | {row['avg_damage']:.1f} | "
                         f"{pct(row['survival_rate'])} |")

        matchup_rows = table_rows(region["tables"]["matchup"], minimum,
                                  lambda row: abs(row["matched_lift"]), limit=16)
        lines += ["", "## 개체 대 플레이어 계열 상성", "",
                  "| 맵 개체 | 플레이어 계열 | 표본 | 플레이어 승률 | 조건 보정 상성 |",
                  "|---|---|---:|---:|---:|"]
        for row in matchup_rows:
            monster_id, family = row["key"].split("|", 1)
            lines.append(f"| {names.get(monster_id, monster_id)} | {family} | {row['games']:,} | "
                         f"{pct(row['win_rate'])} | {pct(row['matched_lift'])} |")

        formation_rows = table_rows(region["tables"]["formation"], 1,
                                    lambda row: row["matched_lift"], limit=10)
        lines += ["", "## 적 배치 유형", "",
                  "| 유형 | 표본 | 적 승률 | 조건 보정 우세 |",
                  "|---|---:|---:|---:|"]
        for row in formation_rows:
            lines.append(f"| {row['key']} | {row['games']:,} | {pct(row['win_rate'])} | "
                         f"{pct(row['matched_lift'])} |")

        pair_rows = region["tables"]["pair"]
        triple_rows = region["tables"]["triple"]
        lines += ["", "## 표본 커버리지", "",
                  f"- 2개체: {sum(row['games'] >= minimum for row in pair_rows)}/120 조합이 {minimum}회 이상",
                  f"- 3개체: {sum(row['games'] >= minimum for row in triple_rows)}/560 조합이 {minimum}회 이상",
                  f"- 난이도×Wave: {len(region['difficulty_waves'])}/48 조건 관측",
                  f"- 고유 전체 편성: {len(region['tables']['team']):,}개",
                  "", "> 조건 보정 우세는 인과관계가 아니라 동일 조건 대비 관측 차이입니다. "
                  "시너지 초과는 조합 우세에서 구성원 평균 우세를 뺀 탐색 지표입니다.", ""]
        (output / filename).write_text("\n".join(lines), encoding="utf-8")
    (output / "README.md").write_text("\n".join(index) + "\n", encoding="utf-8")
    (output / "map_balance_data.json").write_text(
        json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def self_test():
    bucket = collections.defaultdict(fresh_stat)
    add(bucket, "a", True, 0.25, 10, 1)
    add(bucket, "a", False, -0.25, 20, 0)
    row = finish("a", bucket["a"])
    assert row == {"key": "a", "games": 2, "win_rate": 0.5,
                   "matched_lift": 0.0, "avg_damage": 15.0,
                   "survival_rate": 0.5}
    assert formation_key((("a", "f", "R", (), 0, 3, 0, 0),
                          ("b", "f", "R", (), 2, 4, 0, 0))) == "전방·밀집"
    print("self-test: ok")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("telemetry", nargs="?", type=Path)
    parser.add_argument("--output", type=Path, default=Path("Logs/MLBalance/Regions"))
    parser.add_argument("--monster-snapshot", type=Path,
                        default=Path("Assets/MonsterData/Generated/MonsterMasterSnapshot.json"))
    parser.add_argument("--minimum", type=int, default=30)
    parser.add_argument("--workers", type=int, default=8)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return
    if not args.telemetry:
        parser.error("telemetry directory is required")
    episodes, skipped = [], 0
    paths = list(args.telemetry.glob("battle_*.jsonl"))
    with concurrent.futures.ProcessPoolExecutor(max_workers=args.workers) as pool:
        parsed = pool.map(read_episode, paths, chunksize=64)
        for index, episode in enumerate(parsed, 1):
            if episode:
                episodes.append(episode)
            else:
                skipped += 1
            if index % 10000 == 0:
                print(f"read {index:,}/{len(paths):,} files, valid {len(episodes):,}", flush=True)
    names = load_names(args.monster_snapshot)
    regions = build(episodes, args.minimum)
    write_reports(args.output, regions, names, args.minimum, len(episodes), skipped)
    print(f"episodes={len(episodes)} skipped={skipped} output={args.output}")


if __name__ == "__main__":
    main()
