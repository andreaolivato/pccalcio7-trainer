# Research scripts

**These are not the trainer.** The trainer is a single Windows executable built from `src/` —
see the main [README](../README.md). Nothing here is needed to use it or to build it.

These Python scripts are the harness that found the memory layout in the first place. They are
kept in the repository because a claim like *"stadium capacity is a u32 at +0x04 of the club
record"* is worth much more when you can re-run the thing that proved it. Every offset in
[docs/MEMORY-MAP.md](../docs/MEMORY-MAP.md) came from these.

Use them if you want to:

* verify a finding yourself instead of trusting the map
* explore a field nobody has identified yet
* port this work to another PC Fútbol / PC Calcio version, where the offsets will differ but
  the structures and the method should carry over

## Requirements

Python 3 on Windows, no packages. The scripts talk to the game through `ctypes` and
`ReadProcessMemory` / `WriteProcessMemory`, exactly as the trainer does.

Point them at your installation:

```bat
set CALCIO7_DIR=C:\CALCIO7
```

## What each one does

| Script | Purpose |
|---|---|
| `pcc7mem.py` | Process access layer: enumerate writable regions, read, write, locate the club record, find and set the club cash. The Python counterpart of `src/Trainer.cs`. |
| `players.py` | Find players by their name-pointer signature; read and write attributes, birth date, nationality and club. Includes a CLI: `players.py 203`, `players.py show Algerino`, `players.py set Algerino tiro 90`, `players.py nat Rivaldo italy`, `players.py nations`. |
| `trainer.py` | CLI for the club-level values: `trainer.py findcash 337.6`, `trainer.py setcash 337.6 500`, `trainer.py capacity 203 90000`. |
| `save_inspect.py` | Parses the game's own files rather than memory: the `DMFIv1.0` database archives in `DBDAT`, and what little is understood of `SAVE\MANAGER\E036-*\main.dat`. |
| `morale_probe.py` | Isolates one byte of the morale form-history at a time, which is how the weights were measured. A worked example of the single-variable probe described in [docs/METHODOLOGY.md](../docs/METHODOLOGY.md). |

## Warning

These scripts write to a running game's memory and have **fewer guard rails than the trainer**.
The trainer validates every candidate address and refuses to write when a match count looks
implausible; these were exploratory tools, and an early version of one of them crashed the game
by writing to thousands of addresses it should never have matched.

If you use them: **save your career first**. Save files on disk are never touched, so the worst
case is losing unsaved progress — but that is a real cost when it happens mid-season.
