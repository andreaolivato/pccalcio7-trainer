"""Player records in PC Calcio 7 Plus memory.

Players are located by a name-pointer quad: four consecutive u32 pointers
[short, full, previous-club, short] where ptr1 == ptr4 and ptr1 < ptr2, with
short and full adjacent in a plain-ASCII string pool
("Algerino\\0Jimmy ALGERINO\\0Chateauroux (96)\\0"). The previous-club string
is elsewhere entirely for career-generated players (empty and ~2 MB away for
G. Melosi), so ptr3 is only required to be a plausible pointer.

Record stride is 0xF8. Layout relative to the quad start Q:

    Q-0x1C   5 bytes   base/database ratings, NOT the card values
    Q-0x16   u16       the PREVIOUS record's birth year (0xF8 - 0x16 = 0xE2)
    Q-0x08   u32       player id, matches the FDI database record id
    Q-0x02   u16       club in the shipped 1999 database
    Q+0x00   4 x u32   the name pointers
    Q+0x10   u16       CURRENT club in the career (203 = Sampdoria)
    Q+0x68   u32       club registration - all 25 settled squad members hold
                       203 here; leaving it on the old club makes the player
                       show as "Ceduto" and keeps him out of the formation
    Q+0xD4   ptr       per-player history list every squad member has and a
                       transferred-in player does not (null for Dabo)

DO NOT try to fabricate squad membership in memory. The Rosa is built from a
per-team array of pointers to player records (record start = quad - 8),
terminated by a null and allocated as a heap block; the team record holds the
count at +0x14. Adding a player needs all of: club id +0x10, registration +0x68,
position code, a non-null +0xD4 history pointer, an extra roster slot and the
incremented count. Doing exactly that CRASHED the game - most likely because the
game then walked the new squad member and dereferenced structures that a real
signing would have built. Two crashes came out of this area.

The supported route is the game's own transfer screen: writing +0x10 and +0x68
is enough to make a player appear under "direttore sportivo -> ingaggiare", and
signing him there lets the game create the bookkeeping correctly. set_team()
therefore writes only those two fields.
    Q+0x99   13 bytes  the live card attributes (see ATTRS below)
    Q+0xA6   13 bytes  a second, similar run - base or potential values
    Q+0xE2   u16       birth year
    Q+0xE4   u8        birth day
    Q+0xE5   u8        birth month

The 13 attribute bytes at Q+0x99, all 0-99, every one confirmed on screen by
probing Algerino with distinct values and reading his card:

    0 Velocita     1 Resistenza   2 Aggressivita  3 Qualita
    4 Gioco Mani   5 Entrate      6 Passaggio     7 Dribbling
    8 Rifinitura   9 Tiro        10 Stato di forma

    Slot 11 is NOT Morale. It reads 99 for every player, which made it look
    like Morale, but writing 22 there left the card showing 99. Morale is not
    a plain byte in this record at all: Owen is the only player below 99 (95)
    and no byte in his record holds 95.

    MORALE: the five bytes at Q+0xB0..Q+0xB4 do control it, deterministically -
    restoring them to 99 reliably brings the card back to 99. Zeroing one byte
    at a time gives their individual weights, and they are recency-ordered:

        byte 0 -> no effect      byte 1 -> -10      byte 2 -> -10
        byte 3 -> -21            byte 4 -> -35

    so byte 4 is the most recent entry and byte 0 has aged out. A weighted mean
    with those weights plus a hidden 23/99 term fits all six single-byte probes
    exactly, but predicted 70 where the game showed 66 for a mixed write, and
    the widest linear search still leaves an error of 4 across the readings. The
    exact combining rule is therefore NOT solved.

    What works in practice: all five at 99 gives Morale 99, and lowering them
    lowers it monotonically. Use set_morale() for that, and treat any value
    other than the 99 maximum as approximate.

    ORIGINAL NOTE - morale is not directly editable. Writing the five
    bytes at Q+0xB0..Q+0xB4 does appear to move it, but no arithmetic fits all
    four observations:

        tail [99,99,99,99,99] -> 99      tail [50,50,50,50,50] -> 62
        tail [ 0,20,40,60,80] -> 69      tail [99,99,67,67,67] -> 80

    "mean of all five" fits observations 1 and 4; "mean of b2,b3,b4 and 99"
    fits 1, 2 and 3 but predicted 75 for the fourth, which read 80. The most
    likely explanation is that morale is computed from match events and drifts
    on its own, so these readings mix real drift with the effect of the writes.
    Do not add a morale command until that is separated out.
    12 POSITION code, not a rating: both keepers hold 3, the three centre
       backs hold 7, the forwards 8, the midfielders 4. It is 0 for a player
       who is not registered in the squad, and the card does not display it.

MEDIA is not stored: it is the average of slots 0-3, floored. Verified twice -
[78,89,88,83] averages 84.5 and the card read 84; [11,22,33,44] averages 27.5
and the card read 27. The six visible skills do not affect it.

AGE is derived from the birth date at Q+0xE2. All 25 squad members' birth dates
match their FDI records exactly. Setting Algerino's birth year 1971 -> 1979
changed his card from Eta 30 to Eta 22, confirmed in game.

Usage:
    python tools/players.py 203                    # list a club's squad
    python tools/players.py show Algerino          # one player's full card
    python tools/players.py set Algerino tiro 90   # set any attribute
    python tools/players.py media Algerino 95      # set Media (writes slots 0-3)
    python tools/players.py age Algerino 22        # set age via the birth year
    python tools/players.py move Algerino 204      # change club
    python tools/players.py restore Algerino       # undo this tool's writes
"""

import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from pcc7mem import Game, find_process  # noqa: E402

OFF_PID = -0x08
OFF_DB_TEAM = -0x02
OFF_TEAM = 0x10
OFF_TEAM_REG = 0x68      # second club id; if this still points at the old club
                         # the player shows up as "Ceduto" and stays out of the
                         # formation, so a transfer must write both
OFF_ATTRS = 0x99
OFF_BIRTH_YEAR = 0xE2
OFF_BIRTH_DAY = 0xE4
OFF_BIRTH_MONTH = 0xE5

RECORD_STRIDE = 0xF8

ATTRS = {
    "velocita": 0, "resistenza": 1, "aggressivita": 2, "qualita": 3,
    "giocomani": 4, "entrate": 5, "passaggio": 6, "dribbling": 7,
    "rifinitura": 8, "tiro": 9, "forma": 10,
    # slot 11 deliberately absent: writing it does not change Morale
    "position": 12,
}
MEDIA_SLOTS = (0, 1, 2, 3)

BACKUP = Path(__file__).parent.parent / "backups" / "players.json"
# In-game date of the reference career; override on the age command line.
DEFAULT_SEASON = (17, 9, 2002)


def cstring(g, addr, maxlen=48):
    d = g.read(addr, maxlen)
    if not d:
        return None
    z = d.find(b"\x00")
    try:
        return d[:z if z >= 0 else maxlen].decode("latin-1")
    except Exception:
        return None


def media(attrs):
    """The card's Media: floor of the average of slots 0-3."""
    return sum(attrs[i] for i in MEDIA_SLOTS) // len(MEDIA_SLOTS)


def scan(g, team_id=None, name=None):
    out = []
    for base, size in g.regions():
        d = g.read(base, size)
        n = len(d)
        for o in range(0x20, n - 0x100, 4):
            p1, p2, p3, p4 = struct.unpack_from("<IIII", d, o)
            # short and full are adjacent in the pool, but the previous-club
            # string is NOT for career-generated players (G. Melosi's sat ~2 MB
            # away), so p3 only has to look like a pointer.
            if p1 != p4 or p1 == 0 or not (p1 < p2) or p2 - p1 > 120:
                continue
            if not (0x00100000 <= p3 <= 0x7FFF0000):
                continue
            team = struct.unpack_from("<H", d, o + OFF_TEAM)[0]
            if team_id is not None and team != team_id:
                continue
            short = cstring(g, p1)
            if not short or not short[:1].isalpha():
                continue
            if name is not None and name.lower() not in short.lower():
                continue
            attrs = list(d[o + OFF_ATTRS:o + OFF_ATTRS + 13])
            out.append({
                "addr": base + o,
                "short": short,
                "full": cstring(g, p2),
                "prev_club": cstring(g, p3),
                "pid": struct.unpack_from("<I", d, o + OFF_PID)[0],
                "team": team,
                "db_team": struct.unpack_from("<H", d, o + OFF_DB_TEAM)[0],
                "team_reg": struct.unpack_from("<I", d, o + OFF_TEAM_REG)[0],
                "attrs": attrs,
                "media": media(attrs),
                "birth": (d[o + OFF_BIRTH_DAY], d[o + OFF_BIRTH_MONTH],
                          struct.unpack_from("<H", d, o + OFF_BIRTH_YEAR)[0]),
            })
    return out


def one(g, name):
    rows = scan(g, name=name)
    if not rows:
        sys.exit(f"no player matching {name!r}")
    if len(rows) > 1:
        sys.exit(f"{len(rows)} players match {name!r}: "
                 + ", ".join(r["short"] for r in rows[:8]))
    return rows[0]


def save_original(p):
    """Remember a player's untouched state the first time we write to him."""
    BACKUP.parent.mkdir(exist_ok=True)
    data = json.loads(BACKUP.read_text()) if BACKUP.exists() else {}
    key = str(p["pid"])
    if key not in data:
        data[key] = {"short": p["short"], "attrs": p["attrs"],
                     "birth": list(p["birth"]), "team": p["team"],
                     "team_reg": p["team_reg"]}
        BACKUP.write_text(json.dumps(data, indent=2))


def set_attr(g, p, attr, value):
    key = attr.lower()
    if key not in ATTRS:
        sys.exit(f"unknown attribute {attr!r}; pick from {', '.join(sorted(ATTRS))}")
    if not 0 <= int(value) <= 99:
        sys.exit("value must be 0-99")
    save_original(p)
    g.write(p["addr"] + OFF_ATTRS + ATTRS[key], bytes([int(value)]))


def set_media(g, p, value):
    if not 0 <= int(value) <= 99:
        sys.exit("value must be 0-99")
    save_original(p)
    for i in MEDIA_SLOTS:
        g.write(p["addr"] + OFF_ATTRS + i, bytes([int(value)]))


def birth_year_for_age(day, month, age, season):
    d_now, m_now, y_now = season
    had_birthday = (m_now, d_now) >= (month, day)
    return y_now - age - (0 if had_birthday else 1)


def set_age(g, p, age, season):
    day, month, _ = p["birth"]
    year = birth_year_for_age(day, month, int(age), season)
    if not 1930 < year < 2020:
        sys.exit(f"that age implies birth year {year}; refusing")
    save_original(p)
    g.write(p["addr"] + OFF_BIRTH_YEAR, struct.pack("<H", year))
    return year


# Weight of each tail byte, measured by zeroing one at a time (byte 0 unused).
MORALE_TAIL = 0xB0
MORALE_WEIGHTS = (0, 10, 10, 21, 35)
MORALE_HIDDEN = 23          # remaining share, always observed at 99


def predict_morale(tail):
    return (sum(w * v for w, v in zip(MORALE_WEIGHTS, tail))
            + MORALE_HIDDEN * 99) // 99


def set_morale(g, p, target):
    """Aim the five history bytes at a target morale.

    Exact at 99 (all bytes 99). Everything below that is approximate: the
    combining rule is not fully solved, so expect to land within a few points.
    """
    target = int(target)
    lo, hi = predict_morale((99, 0, 0, 0, 0)), 99
    if not lo <= target <= hi:
        sys.exit(f"morale must be between {lo} and {hi} (a hidden term sets the floor)")
    save_original(p)
    x = max(0, min(99, round((99 * target - MORALE_HIDDEN * 99) / sum(MORALE_WEIGHTS))))
    tail = [99] + [x] * 4
    g.write(p["addr"] + MORALE_TAIL, bytes(tail))
    return tail, predict_morale(tail)


def set_team(g, p, team_id):
    """Move a player. Both club fields must change: OFF_TEAM alone leaves him
    listed at the new club but flagged 'Ceduto' and out of the squad."""
    save_original(p)
    g.write(p["addr"] + OFF_TEAM, struct.pack("<H", int(team_id)))
    g.write(p["addr"] + OFF_TEAM_REG, struct.pack("<I", int(team_id)))


def restore(g, p):
    data = json.loads(BACKUP.read_text()) if BACKUP.exists() else {}
    o = data.get(str(p["pid"]))
    if not o:
        sys.exit(f"no backup stored for {p['short']}")
    g.write(p["addr"] + OFF_ATTRS, bytes(o["attrs"]))
    g.write(p["addr"] + OFF_BIRTH_YEAR, struct.pack("<H", o["birth"][2]))
    g.write(p["addr"] + OFF_BIRTH_DAY, bytes([o["birth"][0]]))
    g.write(p["addr"] + OFF_BIRTH_MONTH, bytes([o["birth"][1]]))
    g.write(p["addr"] + OFF_TEAM, struct.pack("<H", o["team"]))
    if "team_reg" in o:
        g.write(p["addr"] + OFF_TEAM_REG, struct.pack("<I", o["team_reg"]))
    return o


def show(g, p):
    d_, m_, y_ = p["birth"]
    print(f"{p['full'] or p['short']}  (id {p['pid']}) @0x{p['addr']:08X}")
    print(f"  club {p['team']}   born {d_:02d}/{m_:02d}/{y_}   Media {p['media']}")
    for name, i in ATTRS.items():
        print(f"    {name:<13} {p['attrs'][i]:>3}")
    print(f"    slot12 (unmapped) {p['attrs'][12]:>3}")


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return
    pid = find_process()
    if pid is None:
        sys.exit("MANAGCAL.EXE is not running")
    g = Game(pid)
    cmd = sys.argv[1]

    if cmd == "show":
        show(g, one(g, sys.argv[2]))
    elif cmd == "set":
        p = one(g, sys.argv[2])
        set_attr(g, p, sys.argv[3], sys.argv[4])
        show(g, one(g, sys.argv[2]))
    elif cmd == "media":
        p = one(g, sys.argv[2])
        set_media(g, p, sys.argv[3])
        show(g, one(g, sys.argv[2]))
    elif cmd == "age":
        p = one(g, sys.argv[2])
        season = tuple(int(x) for x in sys.argv[4:7]) if len(sys.argv) > 6 else DEFAULT_SEASON
        year = set_age(g, p, sys.argv[3], season)
        print(f"{p['short']}: birth year -> {year} (age {sys.argv[3]} on "
              f"{season[0]:02d}/{season[1]:02d}/{season[2]})")
    elif cmd == "morale":
        p = one(g, sys.argv[2])
        tail, pred = set_morale(g, p, sys.argv[3])
        print(f"{p['short']}: history bytes -> {tail}, predicted Morale {pred}")
        if int(sys.argv[3]) != 99:
            print("   (approximate - only the 99 maximum is exact)")
        return

    elif cmd == "move":
        p = one(g, sys.argv[2])
        set_team(g, p, sys.argv[3])
        print(f"{p['short']}: club {p['team']} -> {sys.argv[3]}")
    elif cmd == "restore":
        p = one(g, sys.argv[2])
        o = restore(g, p)
        print(f"{p['short']} restored: attrs {o['attrs']}, born "
              f"{o['birth'][0]:02d}/{o['birth'][1]:02d}/{o['birth'][2]}, club {o['team']}")
    elif cmd == "find":
        for p in scan(g, name=sys.argv[2]):
            show(g, p)
    else:
        rows = scan(g, team_id=int(cmd))
        print(f"{len(rows)} player(s)")
        for p in sorted(rows, key=lambda r: -r["media"]):
            d_, m_, y_ = p["birth"]
            a = p["attrs"]
            print(f"  {p['short']:<16} media={p['media']:<3} b.{d_:02d}/{m_:02d}/{y_} "
                  f"vel={a[0]} res={a[1]} agg={a[2]} qua={a[3]} "
                  f"gm={a[4]} ent={a[5]} pas={a[6]} dri={a[7]} rif={a[8]} tir={a[9]} "
                  f"forma={a[10]} mor={a[11]}")


if __name__ == "__main__":
    main()
