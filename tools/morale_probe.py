"""Isolate the effect of each tail byte on the displayed Morale.

Morale is deterministic - restoring a player's original bytes reliably brings it
back to 99 - but it is NOT a plain average of the five bytes at Q+0xB0..Q+0xB4.
Every subset of those bytes plus slot 11 was brute-forced against four bulk
observations, with flooring and with rounding, and nothing fit:

    tail [99,99,99,99,99] -> 99     tail [50,50,50,50,50] -> 62
    tail [ 0,20,40,60,80] -> 69     tail [99,99,67,67,67] -> 80

The flaw was changing five bytes at once and trying to solve for five unknowns
from a single number. This probe changes exactly ONE byte, so each reading
measures that byte's own contribution:

    python tools/morale_probe.py <player> <index 0-4>   # that byte 0, rest 99
    python tools/morale_probe.py <player> reset         # all five back to 99

If the function is a weighted average, a reading of M for index i means that
byte carries weight (99 - M) / 99 of the total.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from pcc7mem import Game, find_process  # noqa: E402
from players import scan  # noqa: E402

TAIL = 0xB0
TAIL_LEN = 5


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return
    pid = find_process()
    if pid is None:
        sys.exit("MANAGCAL.EXE is not running")
    g = Game(pid)
    rows = scan(g, name=sys.argv[1])
    if len(rows) != 1:
        sys.exit(f"{len(rows)} players match {sys.argv[1]!r}")
    p = rows[0]
    base = p["addr"] + TAIL

    g.write(base, bytes([99] * TAIL_LEN))
    if sys.argv[2] == "reset":
        print(f"{p['short']}: tail reset to {list(g.read(base, TAIL_LEN))} "
              f"- Morale should read 99")
        return

    idx = int(sys.argv[2])
    if not 0 <= idx < TAIL_LEN:
        sys.exit("index must be 0-4")
    g.write(base + idx, bytes([0]))
    print(f"{p['short']}: tail = {list(g.read(base, TAIL_LEN))}  (only byte {idx} zeroed)")
    print("\nwhat the reading will mean for THIS byte:")
    for n in (4, 5, 6, 8):
        print(f"   Morale {(99 * (n - 1)) // n:>3}  ->  one of {n} equal inputs")
    print("   Morale  99  ->  not an input at all")
    print("   anything else -> it contributes, but the weights are not uniform")


if __name__ == "__main__":
    main()
