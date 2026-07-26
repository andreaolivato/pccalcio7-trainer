"""PC Calcio 7 Plus live trainer (prototype).

Usage (game must be running with a career loaded):

    python tools/trainer.py findcash 339.8         # locate the cash shown as 339,8 miliardi
    python tools/trainer.py setcash 339.8 500      # change that 339,8 into 500 miliardi
    python tools/trainer.py restore-cash 500 <old> # put the saved internal value back
    python tools/trainer.py show 203            # read a club record (capacity etc.)
    python tools/trainer.py capacity 203 90000  # set stadium capacity (all copies)

Cash is given in *miliardi as displayed in game*. Internally the engine stores
a tenth of that (the Italian build shows lire for a Spanish-peseta value), so
the tool multiplies by 1e8 for you.

Nothing is written unless a write subcommand is used.
"""

import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from pcc7mem import Game, find_process, OFF_MONEY_F64, OFF_CAPACITY  # noqa: E402

BACKUP = Path(__file__).parent.parent / "backups" / "field_backup.json"


def _backup(key, value):
    BACKUP.parent.mkdir(exist_ok=True)
    data = json.loads(BACKUP.read_text()) if BACKUP.exists() else {}
    data.setdefault(key, value)   # keep the earliest original
    BACKUP.write_text(json.dumps(data, indent=2))


def connect_process():
    pid = find_process()
    if pid is None:
        sys.exit("MANAGCAL.EXE is not running - start the game first.")
    return Game(pid)


def connect():
    return connect_process(), None


DISPLAY_SCALE = 1e8      # internal units per miliardo shown on screen


def cash_cmds(cmd, g, current_miliardi):
    """Cash commands are keyed on the figure currently shown in game."""
    found = g.find_money_by_display(current_miliardi)
    if not found:
        sys.exit(f"no value matching {current_miliardi} miliardi found - "
                 f"check the figure on screen (one decimal, e.g. 339.8)")
    print(f"{len(found)} location(s) hold {current_miliardi} miliardi:")
    for a, v in found:
        print(f"   0x{a:08X} = {v:,.2f} internal")

    if cmd == "findcash":
        return

    # Several unrelated floats can land in the same displayed bucket. The real
    # balance is stored bit-identically in more than one place, so group by
    # exact value and take the largest group; a tie means we cannot tell.
    groups = {}
    for a, v in found:
        groups.setdefault(v, []).append(a)
    ranked = sorted(groups.items(), key=lambda kv: len(kv[1]), reverse=True)
    if len(ranked) > 1 and len(ranked[0][1]) == len(ranked[1][1]):
        sys.exit("candidate groups are tied; cannot tell which is the balance")
    value, addrs = ranked[0]
    print(f"\nusing the {len(addrs)}-way group at {value:,.2f} internal; "
          f"ignoring {len(found) - len(addrs)} unrelated match(es)")

    if cmd == "setcash":
        new = float(sys.argv[3]) * DISPLAY_SCALE
        _backup(f"cash:{current_miliardi}", value)
        n = g.set_money_everywhere(addrs, new, expect=value)
        print(f"\ncash -> {float(sys.argv[3]):,.1f} miliardi "
              f"({n} location(s) written, original {value:,.2f} backed up)")
    elif cmd == "restore-cash":
        old = float(sys.argv[3])
        g.set_money_everywhere(addrs, old, expect=value)
        print(f"\ncash restored to {old/DISPLAY_SCALE:,.1f} miliardi")


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return
    cmd = sys.argv[1]

    if cmd in ("findcash", "setcash", "restore-cash"):
        g = connect_process()
        cash_cmds(cmd, g, float(sys.argv[2]))
        return

    team_id = int(sys.argv[2])
    g, _ = connect()
    addr = g.find_team(team_id)
    if addr is None:
        sys.exit(f"team {team_id} not found (is a career loaded?)")

    if cmd == "show":
        t = g.read_team(addr)
        print(f"team {t['id']} @ 0x{t['addr']:08X}")
        print(f"  stadium capacity : {t['capacity']:,}")
        print(f"  attendance       : {t['attendance']:,}")
        print(f"  founded          : {t['founded']}")
        print(f"  income accumulator (+0x80): {t['money_f64']:,.0f}  "
              f"(NOT the cash on screen - use findcash)")
        print(f"  budget (f32)              : {t['budget_f32']:,.0f}")

    elif cmd == "accumulator":
        # Team record +0x80. The game adds match income here, but this is NOT
        # the cash shown on screen - use findcash/setcash for that.
        new = float(sys.argv[3])
        old = struct.unpack("<d", g.read(addr + OFF_MONEY_F64, 8))[0]
        _backup(f"accumulator:{team_id}", old)
        g.set_team_accumulator(addr, new)
        print(f"accumulator {old:,.0f} -> {new:,.0f}  (original backed up)")

    elif cmd == "capacity":
        new = int(sys.argv[3])
        old = struct.unpack("<I", g.read(addr + OFF_CAPACITY, 4))[0]
        _backup(f"capacity:{team_id}", old)
        hit = g.set_capacity_everywhere(old, new)
        print(f"capacity {old:,} -> {new:,} in {len(hit)} location(s): "
              + ", ".join(f"0x{a:08X}" for a in hit))
        print("(both copies are required - the stadium screen reads the second one)")

    elif cmd == "restore-accumulator":
        old = json.loads(BACKUP.read_text())[f"accumulator:{team_id}"]
        g.set_team_accumulator(addr, old)
        print(f"accumulator restored to {old:,.0f}")

    else:
        print(__doc__)


if __name__ == "__main__":
    main()
