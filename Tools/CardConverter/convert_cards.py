"""원작 출시 카드 CSV를 게임용 카드 데이터로 변환한다.

입력
  Docs/원작_초기카드.csv              원작 위키에서 정리한 카드 215행 (진화형 포함)
  Tools/CardConverter/card_names_ko.csv  카드 번호별 한국어 이름

출력
  Assets/Scripts/Core/Cards/CardCatalog.Generated.cs  게임에서 쓰는 카드 데이터 (C#)
  Docs/카드목록.md                                     사람이 확인하는 카드 목록

실행 (저장소 루트에서)
  python Tools/CardConverter/convert_cards.py
"""

import csv
import re
import statistics
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE_CSV = ROOT / "Docs" / "원작_초기카드.csv"
NAMES_CSV = ROOT / "Tools" / "CardConverter" / "card_names_ko.csv"
OUT_CS = ROOT / "Assets" / "Scripts" / "Core" / "Cards" / "CardCatalog.Generated.cs"
OUT_MD = ROOT / "Docs" / "카드목록.md"

# 진화 전 기본형만 쓴다. 기본형은 이름이 이 접두사로 시작한다.
BASE_PREFIXES = ("【騎士】", "【妖精】", "【訓練城の番兵】")

# 151번(第二型トリストラム HN)은 152번(같은 이름의 R)과 이름이 같아 덱에 함께 넣을 수 없으므로 뺀다.
EXCLUDED_IDS = {151}

# 레이드(1등급 아이스 드래곤) 드랍 카드. 나머지 ★3 이상은 뽑기로 얻는다.
RAID_DROP_IDS = {69, 75}

JOBS = {"傭兵": "Warrior", "富豪": "Guardian", "盗賊": "Mage", "歌姫": "Priest", "その他": "None"}
ELEMENTS = {"火": "Fire", "氷": "Ice", "風": "Wind", "光": "Light", "闇": "Dark"}
RARITIES = {"N": 1, "HN": 2, "R": 3, "SR": 4, "UR": 5}
CATEGORIES = {"物": "Physical", "魔": "Magic", "支": "Support", "防": "Defense", "治": "Heal", "妨": "Hinder"}
TARGETS = {
    "自分": "Self",
    "味方1人": "AllySingle",
    "味方一人": "AllySingle",
    "味方全員": "AllAllies",
    "敵単体": "EnemySingle",
    "敵全体": "AllEnemies",
}
STATS = {
    "物理ダメージ": ("Damage", "Physical"),
    "魔法ダメージ": ("Damage", "Magic"),
    "全ダメージ": ("Damage", "All"),
    "物理防御": ("Defense", "Physical"),
    "魔法防御": ("Defense", "Magic"),
}

# 원작 자료에 수치가 없는 부가 효과의 추정값. 주 효과 수치에 대한 비율이다.
ESTIMATE_RATIO = {"stat": 0.2, "poison": 0.1, "hot": 0.1, "self_guard": 0.3}
CRIT_CHANCE = 0.75      # 高確率でクリティカル
STUN_CHANCE = 0.2       # 低確率で敵の行動を止める
LIFESTEAL_RATIO = 0.3   # 与えたダメージを回復
HEAL_BOOST_RATIO = 0.3  # 回復量アップ

TARGET_PATTERN = "(" + "|".join(TARGETS) + ")"
NUM = r"(\d+)(?:\((\d*)\))?"
RE_DAMAGE = re.compile(rf"^(敵単体|敵全体)/(物理|魔法)/?{NUM}の(.)ダメージ$")
RE_STAT = re.compile(rf"^{TARGET_PATTERN}/(\d+)ターン/(" + "|".join(STATS) + rf")を{NUM}(アップ|ダウン)$")
RE_HEAL = re.compile(rf"^{TARGET_PATTERN}/HPを{NUM}回復$")
RE_HOT = re.compile(rf"^{TARGET_PATTERN}/(\d+)ターン/ターン毎にHPを{NUM}回復$")
RE_POISON = re.compile(rf"^(敵単体|敵全体)/(\d+)ターン/毎ターン{NUM}の毒ダメージ$")
RE_DRAW = re.compile(r"^(味方全員)?ドロー\+(\d+)$")
RE_X_STAT = re.compile(r"^(\d+)ターン/(物理ダメージ|魔法ダメージ|物理防御|魔法防御)を(アップ|ダウン)$")
RE_X_POISON = re.compile(r"^(\d+)ターン/毎ターン毒ダメージ$")
RE_X_HEAL_BOOST = re.compile(r"^自分/(\d+)ターン/回復量アップ$")
RE_X_HOT = re.compile(r"^自分/(\d+)ターン/ターン毎にHPを回復$")


class ConversionError(Exception):
    pass


class Effect:
    def __init__(self, kind, target, stat="None", base=0, max_value=None, turns=0, chance=0.0, estimated=False):
        self.kind = kind
        self.target = target
        self.stat = stat
        self.base = base
        self.max = max_value
        self.turns = turns
        self.chance = chance
        self.estimated = estimated


def stat_effect(group, stat, direction, target, base, max_value, turns, estimated=False):
    kind = group + ("Up" if direction == "アップ" else "Down")
    return Effect(kind, target, stat, base, max_value, turns, estimated=estimated)


def to_max(text):
    return int(text) if text else None


def parse_skill(raw, card_element, where):
    """'스킬 이름 / 효과' 문자열을 효과 목록으로 바꾼다."""
    text = raw.replace("[覚醒]", "").strip()
    if not text:
        return None
    _, sep, body = text.partition(" / ")
    if not sep:
        raise ConversionError(f"{where}: 스킬 이름과 효과를 나눌 수 없음: {raw}")
    body = unicodedata.normalize("NFKC", body)

    clauses = [c for part in body.split("|") for c in part.split(" ") if c]
    effects = []
    for clause in clauses:
        effects.append(parse_clause(clause, effects, card_element, where))
    return effects


def parse_clause(clause, previous, card_element, where):
    main = previous[0] if previous else None
    enemy_target = next((e.target for e in previous if e.target.startswith("Enemy") or e.target == "AllEnemies"), "EnemySingle")

    m = RE_DAMAGE.match(clause)
    if m:
        target, damage_type, base, max_value, element = m.groups()
        if ELEMENTS[element] != card_element:
            raise ConversionError(f"{where}: 카드 속성과 피해 속성이 다름: {clause}")
        stat = "Physical" if damage_type == "物理" else "Magic"
        return Effect("Damage", TARGETS[target], stat, int(base), to_max(max_value))

    m = RE_STAT.match(clause)
    if m:
        target, turns, stat_name, base, max_value, direction = m.groups()
        group, stat = STATS[stat_name]
        return stat_effect(group, stat, direction, TARGETS[target], int(base), to_max(max_value), int(turns))

    m = RE_HEAL.match(clause)
    if m:
        target, base, max_value = m.groups()
        return Effect("Heal", TARGETS[target], "None", int(base), to_max(max_value))

    m = RE_HOT.match(clause)
    if m:
        target, turns, base, max_value = m.groups()
        return Effect("HealOverTime", TARGETS[target], "None", int(base), to_max(max_value), int(turns))

    m = RE_POISON.match(clause)
    if m:
        target, turns, base, max_value = m.groups()
        return Effect("Poison", TARGETS[target], "None", int(base), to_max(max_value), int(turns))

    m = RE_DRAW.match(clause)
    if m:
        all_allies, count = m.groups()
        return Effect("Draw", "AllAllies" if all_allies else "Self", "None", int(count), int(count))

    # 아래는 수치 없이 적힌 부가 효과다. 주 효과 수치에 비율을 곱해 추정한다.
    if main is None:
        raise ConversionError(f"{where}: 주 효과 없이 부가 효과만 있음: {clause}")

    m = RE_X_STAT.match(clause)
    if m:
        turns, stat_name, direction = m.groups()
        group, stat = STATS[stat_name]
        target = enemy_target if direction == "ダウン" else "Self"
        return stat_effect(group, stat, direction, target, *estimate(main, ESTIMATE_RATIO["stat"]), int(turns), estimated=True)

    m = RE_X_POISON.match(clause)
    if m:
        return Effect("Poison", enemy_target, "None", *estimate(main, ESTIMATE_RATIO["poison"]), int(m.group(1)), estimated=True)

    m = RE_X_HEAL_BOOST.match(clause)
    if m:
        return Effect("HealBoost", "Self", turns=int(m.group(1)), chance=HEAL_BOOST_RATIO, estimated=True)

    m = RE_X_HOT.match(clause)
    if m:
        return Effect("HealOverTime", "Self", "None", *estimate(main, ESTIMATE_RATIO["hot"]), int(m.group(1)), estimated=True)

    if clause == "高確率でクリティカルが発生":
        return Effect("CritRateUp", "Self", chance=CRIT_CHANCE, estimated=True)
    if clause == "低確率で敵の行動を止める":
        return Effect("Stun", enemy_target, turns=1, chance=STUN_CHANCE, estimated=True)
    if clause == "与えたダメージを回復":
        return Effect("Lifesteal", "Self", chance=LIFESTEAL_RATIO, estimated=True)
    if clause == "自分の魔法防御をアップ":
        return Effect("DefenseUp", "Self", "Magic", *estimate(main, ESTIMATE_RATIO["self_guard"]), main.turns, estimated=True)

    raise ConversionError(f"{where}: 해석할 수 없는 효과: {clause}")


def estimate(main, ratio):
    """주 효과 수치에 비율을 곱한 (레벨 1 값, 최대 레벨 값). 주 효과의 최대값은 나중에 채워지므로 None일 수 있다."""
    base = round(main.base * ratio)
    max_value = round(main.max * ratio) if main.max is not None else None
    return base, max_value


def fill_missing_max(cards):
    """최대값이 비어 있으면 같은 성, 같은 스킬 종류(통상/각성)의 평균 성장 비율로 채운다."""
    ratios = {}
    for card in cards:
        for awakened, effects in ((False, card["normal"]), (True, card["awakened"])):
            for e in effects or []:
                if e.max is not None and e.base > 0 and e.kind != "Draw":
                    ratios.setdefault((card["rarity"], awakened), []).append(e.max / e.base)
    median = {key: statistics.median(values) for key, values in ratios.items()}

    for card in cards:
        for awakened, effects in ((False, card["normal"]), (True, card["awakened"])):
            effects = effects or []
            for e in effects:
                if e.max is None:
                    ratio = median.get((card["rarity"], awakened)) or median[(card["rarity"], not awakened)]
                    e.max = round(e.base * ratio)
                    e.estimated = True
            # 주 효과를 바탕으로 추정한 부가 효과는 주 효과의 최대값이 채워진 뒤에 다시 계산한다.
            main = effects[0] if effects else None
            for e in effects[1:]:
                if e.estimated and e.base and main and main.base:
                    e.max = round(e.base * main.max / main.base)


def load_cards():
    names = {}
    with NAMES_CSV.open(encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            names[int(row["no"])] = row["name_ko"].strip()

    cards = []
    with SOURCE_CSV.open(encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            card_id = int(row["no"])
            if not row["name_jp"].startswith(BASE_PREFIXES) or card_id in EXCLUDED_IDS:
                continue
            if card_id not in names:
                raise ConversionError(f"{card_id}번 카드의 한국어 이름이 card_names_ko.csv에 없음")

            job = JOBS[row["type"]]
            element = ELEMENTS[row["element"]]
            rarity = RARITIES[row["rarity"]]
            where = f"{card_id}번 {row['name_jp']}"
            normal = parse_skill(row["skill"], element, where + " 통상")
            awakened = parse_skill(row["awakening_skill"], element, where + " 각성") if job != "None" else None

            if job == "None" or rarity <= 2:
                source = "Starter"
            elif card_id in RAID_DROP_IDS:
                source = "RaidDrop"
            else:
                source = "Gacha"

            cards.append({
                "id": card_id,
                "name_ko": names.pop(card_id),
                "name_jp": row["name_jp"],
                "job": job,
                "element": element,
                "rarity": rarity,
                "cost": int(row["cost"]),
                "category": CATEGORIES[row["category"]],
                "normal": normal,
                "awakened": awakened,
                "source": source,
            })

    if names:
        raise ConversionError(f"card_names_ko.csv에 있지만 카드가 없는 번호: {sorted(names)}")
    fill_missing_max(cards)
    return cards


def cs_effect(e):
    chance = f"{e.chance:g}f"
    estimated = "true" if e.estimated else "false"
    return (f"new SkillEffect(EffectKind.{e.kind}, EffectTarget.{e.target}, StatType.{e.stat}, "
            f"{e.base}, {e.max}, {e.turns}, {chance}, {estimated})")


def cs_skill(effects, indent):
    if effects is None:
        return "null"
    pad = " " * indent
    inner = (",\n" + pad + "    ").join(cs_effect(e) for e in effects)
    return f"new CardSkill(\n{pad}    {inner})"


def write_cs(cards):
    lines = [
        "// <auto-generated>",
        "// Tools/CardConverter/convert_cards.py가 만든 파일. 직접 고치지 말고 변환기를 다시 실행할 것.",
        "// </auto-generated>",
        "",
        "namespace ChainKnights.Core.Cards",
        "{",
        "    public static partial class CardCatalog",
        "    {",
        "        private static CardDefinition[] CreateCards()",
        "        {",
        "            return new[]",
        "            {",
    ]
    for c in cards:
        lines.append(
            f"                new CardDefinition({c['id']}, \"{c['name_ko']}\", \"{c['name_jp']}\", "
            f"Job.{c['job']}, Element.{c['element']}, {c['rarity']}, {c['cost']}, SkillCategory.{c['category']},")
        lines.append("                    " + cs_skill(c["normal"], 20) + ",")
        lines.append("                    " + cs_skill(c["awakened"], 20) + ",")
        lines.append(f"                    CardSource.{c['source']}),")
    lines += [
        "            };",
        "        }",
        "    }",
        "}",
        "",
    ]
    OUT_CS.write_text("\n".join(lines), encoding="utf-8")


TARGET_KO = {"Self": "자신", "AllySingle": "아군 1명", "AllAllies": "아군 전원", "EnemySingle": "적 단일", "AllEnemies": "적 전체"}
STAT_KO = {"Physical": "물리", "Magic": "마법", "All": "전체", "None": ""}
JOB_KO = {"Warrior": "전사", "Guardian": "수호자", "Mage": "마법사", "Priest": "사제", "None": "공용"}
ELEMENT_KO = {"Fire": "화", "Ice": "빙", "Wind": "풍", "Light": "광", "Dark": "암"}
CATEGORY_KO = {"Physical": "물리", "Magic": "마법", "Support": "지원", "Defense": "방어", "Heal": "치유", "Hinder": "방해"}
SOURCE_KO = {"Starter": "스타터", "Gacha": "뽑기", "RaidDrop": "레이드"}


def describe(e):
    target = TARGET_KO[e.target]
    stat = STAT_KO[e.stat]
    values = f"{e.base}→{e.max}"
    turns = f"{e.turns}턴 " if e.turns else ""
    text = {
        "Damage": lambda: f"{target} {stat} 피해 {values}",
        "Heal": lambda: f"{target} HP {values} 회복",
        "HealOverTime": lambda: f"{target} {turns}매 턴 HP {values} 회복",
        "DamageUp": lambda: f"{target} {turns}{stat} 피해 +{values}",
        "DefenseUp": lambda: f"{target} {turns}{stat} 방어 +{values}",
        "DamageDown": lambda: f"{target} {turns}{stat} 피해 −{values}",
        "DefenseDown": lambda: f"{target} {turns}{stat} 방어 −{values}",
        "Poison": lambda: f"{target} {turns}매 턴 독 {values}",
        "Draw": lambda: ("아군 전원 " if e.target == "AllAllies" else "") + f"드로우 +{e.base}",
        "CritRateUp": lambda: f"크리티컬 확률 {e.chance:.0%}",
        "Stun": lambda: f"{e.chance:.0%} 확률로 {turns}행동 정지",
        "Lifesteal": lambda: f"준 피해의 {e.chance:.0%} 흡혈",
        "HealBoost": lambda: f"{turns}회복량 +{e.chance:.0%}",
    }[e.kind]()
    return text + ("\\*" if e.estimated else "")


def write_md(cards):
    lines = [
        "# 카드 목록",
        "",
        "> `Tools/CardConverter/convert_cards.py`가 만든 파일입니다. 직접 고치지 말고 변환기를 다시 실행하세요.",
        "",
        f"- 카드 {len(cards)}장 (원작 출시 카드 기본형, 151번은 152번과 이름이 같아 제외)",
        "- 수치는 `레벨 1 → 최대 레벨`입니다. 최대 레벨은 ★1 = 10, ★2 = 20, ★3 = 30, ★4 = 40, ★5 = 50입니다.",
        "- `*`는 원작 자료에 수치가 없어 변환기가 추정한 값입니다.",
        "",
        "| 번호 | 이름 | 직업 | 속성 | ★ | 코스트 | 분류 | 획득 | 통상 스킬 | 각성 스킬 |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    for c in cards:
        normal = "<br>".join(describe(e) for e in c["normal"])
        awakened = "<br>".join(describe(e) for e in c["awakened"]) if c["awakened"] else "-"
        lines.append(
            f"| {c['id']} | {c['name_ko']} | {JOB_KO[c['job']]} | {ELEMENT_KO[c['element']]} | {c['rarity']} | {c['cost']} | "
            f"{CATEGORY_KO[c['category']]} | {SOURCE_KO[c['source']]} | {normal} | {awakened} |")
    lines.append("")
    OUT_MD.write_text("\n".join(lines), encoding="utf-8")


def main():
    # Windows 콘솔 기본 인코딩으로는 한국어가 출력되지 않는다.
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    cards = load_cards()
    write_cs(cards)
    write_md(cards)
    estimated = sum(e.estimated for c in cards for e in (c["normal"] or []) + (c["awakened"] or []))
    print(f"카드 {len(cards)}장 변환 완료 (추정 수치 {estimated}개)")
    print(f"  {OUT_CS.relative_to(ROOT)}")
    print(f"  {OUT_MD.relative_to(ROOT)}")


if __name__ == "__main__":
    try:
        main()
    except ConversionError as error:
        print(f"변환 실패: {error}", file=sys.stderr)
        sys.exit(1)
