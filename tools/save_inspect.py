"""Inspection utilities for PC Calcio 7 Plus data files.

Findings so far (2026-07-26):

DBDAT/*.fdi  (players jug*, teams eq*, coaches ent*)
  - magic "DMFIv1.0" (8 bytes), 8 bytes unknown
  - offset 0x10: u32 = number of index entries
  - offset 0x14: index entries, 13 bytes each: <u32 id, u8 pad, u32 offset, u32 size>
    entries cover the file exactly, back to back
  - record payload text is XOR 0x61 encoded (space -> 0x41, 'a' -> 0x00),
    mixed with binary fields. Player record contains: short name, full name,
    birthplace, team name + season tag, scouting texts, and stat bytes (0-99).

SAVE/MANAGER/E036-N/  (one directory per save slot)
  - main.dat    core save (~6.5 MB)
  - esttemp.dat, estpart.dat, estoff.dat  statistics/history (variable size)

main.dat header:
  0x00  u32 = 0x24, u32 = 0x22                 (version/magic)
  0x08  u16 len + bytes   save label (small substitution/obfuscated encoding, unsolved - cosmetic)
  ....  u16 len + bytes   manager name (same encoding; identical across slots of same manager)
  ....  u8 day, u8 month, u16 year             REAL-WORLD save date (verified vs file mtimes)
  ....  u8 hour, u8 minute
  ....  16 bytes flags: 00*5 01 00 01 00 01 [02|03] 01 01 00 00 01
  ....  u16 unknown (a2 0f / ef 03 / 8a 13 / 40 1f seen)
  ....  u8 day, u8 month, u8 ?, u16 year       IN-GAME current date (year 2001-2009 seen)
  ....  u32 unknown (43959, 51737 seen - points/fans?)
  ....  u16 team_id, u16 team_id (repeated)    user's team id
  ....  5 bytes team attributes
  ....  ... then interleaved team/player blocks

MONEY: stored as IEEE754 float32 in LIRE (e.g. 0x4EF3A9EE = 2.04e9 = 2.04 miliardi).
  Money-like float pairs appear near the header (club funds + second amount),
  often duplicated. In-memory representation is expected to be the same float32.

STATS: plain bytes 0-99 (0x63 = 99 cap, hence runs of 'c').

Diffing two saves of the same career made seconds apart: only 2 bytes differ
(besides the save label) -> no strict checksum expected; community hex-edits
of saves reportedly load fine.
"""

import os
import re
import struct
import sys
from pathlib import Path

# Point CALCIO7_DIR at your own installation, or edit the fallback below.
GAME = Path(os.environ.get("CALCIO7_DIR", r"C:\CALCIO7"))
DB = GAME / "DBDAT"
SAVE = GAME / "SAVE" / "MANAGER"


def parse_fdi_index(data: bytes):
    """Return list of (id, offset, size) for a DMFIv1.0 .fdi file."""
    assert data[:8] == b"DMFIv1.0", "not an FDI file"
    (count,) = struct.unpack_from("<I", data, 0x10)
    entries = []
    pos = 0x14
    for _ in range(count):
        id_, _pad, off, size = struct.unpack_from("<IBII", data, pos)
        entries.append((id_, off, size))
        pos += 13
    return entries


def dec61(bs: bytes) -> bytes:
    """Decode FDI text encoding."""
    return bytes(b ^ 0x61 for b in bs)


def fdi_strings(rec: bytes, minlen: int = 4):
    """Readable text runs from a decoded FDI record."""
    dec = dec61(rec)
    return [m.group().decode("latin-1")
            for m in re.finditer(rb"[A-Za-z\xc0-\xff][a-zA-Z\xc0-\xff\'\. ]{%d,}" % (minlen - 1), dec)]


def parse_save_header(data: bytes):
    p = 8
    (l1,) = struct.unpack_from("<H", data, p); p += 2
    label = data[p:p + l1]; p += l1
    (l2,) = struct.unpack_from("<H", data, p); p += 2
    manager = data[p:p + l2]; p += l2
    day, month, year = struct.unpack_from("<BBH", data, p); p += 4
    hour, minute = data[p], data[p + 1]; p += 2
    flags = data[p:p + 16]; p += 16
    (unk1,) = struct.unpack_from("<H", data, p); p += 2
    gday, gmonth, gunk, gyear = struct.unpack_from("<BBBH", data, p); p += 5
    (unk2,) = struct.unpack_from("<I", data, p); p += 4
    team_a, team_b = struct.unpack_from("<HH", data, p); p += 4
    return {
        "label_enc": label.hex(" "),
        "manager_enc": manager.hex(" "),
        "saved": f"{day:02d}/{month:02d}/{year} {hour:02d}:{minute:02d}",
        "flags": flags.hex(" "),
        "unk1": unk1,
        "ingame_date": f"{gday:02d}/{gmonth:02d}/{gyear} (x={gunk})",
        "unk2": unk2,
        "team_id": (team_a, team_b),
        "body_offset": p,
    }


def scan_money_floats(data: bytes, start: int = 0, end: int = 0x2000):
    """List plausible money float32s (1e5..1e11 lire) in a range."""
    out = []
    for off in range(start, min(end, len(data) - 3)):
        (v,) = struct.unpack_from("<f", data, off)
        if 1e5 <= v <= 1e11 and v == v:  # not NaN
            out.append((off, v))
    return out


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "slots":
        for slot in sorted(SAVE.iterdir()):
            f = slot / "main.dat"
            if f.exists():
                h = parse_save_header(f.read_bytes())
                print(slot.name, h)
    elif len(sys.argv) > 2 and sys.argv[1] == "money":
        data = (SAVE / sys.argv[2] / "main.dat").read_bytes()
        h = parse_save_header(data)
        print(h)
        for off, v in scan_money_floats(data, h["body_offset"], h["body_offset"] + 0x400):
            print(f"  0x{off:X}: {v:,.0f} lire ({v / 1e9:.3f} mld)")
    else:
        print("usage: save_inspect.py slots | money <E036-N>")


if __name__ == "__main__":
    main()
