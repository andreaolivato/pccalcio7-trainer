<div align="center">

### 🇬🇧 **English** &nbsp;·&nbsp; 🇮🇹 [Italiano](README.it.md) &nbsp;·&nbsp; 🇪🇸 [Español](README.es.md)

🌐 Official website: **[calcio.dev/en](https://calcio.dev/en)**

</div>

---

# PC Calcio 7 Trainer

A small, free trainer for **PC Calcio 7** and **PC Calcio 7 Plus** (Dinamic Multimedia, 1998) —
the Italian edition of PC Fútbol 7. It edits your career while the game is running: club money,
stadium capacity, and any player's attributes, age and morale.

## TL;DR

### ⬇ [**Download PcCalcio7Trainer.exe**](https://github.com/andreaolivato/pccalcio7-trainer/releases/latest/download/PcCalcio7Trainer.exe)

One file. Nothing to install. This project's official website is
**[calcio.dev/en](https://calcio.dev/en)**.

1. Start **PC Calcio 7** and load your career
2. Run the exe — it attaches on its own and works out which club is yours
3. Change money, stadium or players and press **Apply**
4. Leave the game's screen and come back so it redraws, then **save in the game**

Windows will warn about an unknown publisher and your antivirus may object: the trainer writes
to the game's memory, which is what malware does too. The full source is here if you would
rather build it yourself.

> Not affiliated with Dinamic Multimedia or any rights holder. No game files are included —
> you need your own copy of the game installed.

![The trainer attached to a career, showing club money, stadium capacity and the squad](docs/screenshot.png)

---

## What it can change

| | Detail |
|---|---|
| **Club money** | Set your balance to anything up to 900,000 miliardi |
| **Stadium capacity** | Any value from 100 to 1,000,000 seats |
| **Player attributes** | Velocità, Resistenza, Aggressività, Qualità, Gioco Mani, Entrate, Passaggio, Dribbling, Rifinitura, Tiro, Stato di forma |
| **Media** | Not editable directly — the game computes it as the average of Velocità, Resistenza, Aggressività and Qualità, so raising those raises Media |
| **Player age** | By setting the birth year — lasts until the game reloads the career (new season or full reload), which re-imports birth dates from the database; just re-apply it |
| **Morale** | 23 to 99 |
| **Restore** | Put any player back to the values he had before you touched him |

Works on **your** club and on **every other club** in the game — 925 of them, searchable by name.

### What it deliberately does not do

**Transfers.** Moving a player between clubs is not supported. A club's squad is built from a
separate list, and a player forced into it without the structures a real signing creates makes
the game crash. If you want a player, make yourself rich and sign him through the game's own
*direttore sportivo* screen.

**Editing save files.** Everything happens in the running game's memory. The save format keeps
this data packed in a way this project never decoded.

---

## Requirements

* Windows 8, 10 or 11 — or Windows 7 with .NET Framework 4 installed
* PC Calcio 7 or PC Calcio 7 Plus, installed and running
* Nothing else. No .NET download, no Python, no Visual C++ runtime

The trainer is built against .NET Framework 4.0, which has shipped inside Windows since
Windows 8, so on any modern machine it simply runs.

---

## Use it

1. Start **PC Calcio 7** and load your career.
2. Run **`PcCalcio7Trainer.exe`**. It attaches by itself and works out which club is yours.
3. Change what you want and press **Applica**.
4. **Leave the screen in the game and come back** so it redraws — the number won't update on a
   screen that's already on your monitor.
5. **Save inside the game** to keep the changes.

Changes live in the game's memory. They persist once you save, and are lost if you reload
without saving. The game's own files are never written to.

### If it can't attach

The window tells you which of two things is wrong and what to do:

* **The game isn't running** → start it and load a career, then press *Riprova*.
* **The game is running but no career is loaded** → load your career, then press *Riprova*.
* **The game was found but can't be opened** → you started the game as administrator, so the
  trainer needs the same. Close it, right-click the exe, *Run as administrator*.

---

## What to ship, and what not to

Releases contain exactly one file that matters:

```
PcCalcio7Trainer.exe     <- this is the whole program
```

Translations are **compiled into the exe**. There is no configuration file, no JSON, no
language pack to copy.

`SelfTest.exe` is a **diagnostic** build and nobody needs it to use the trainer. It runs the
same searches from a console and prints what it finds — how much memory it scanned, how many
clubs it saw, which club it detected, the squad it read, and how long each step took. It exists
for two reasons: it verifies a change against a real game without clicking through the
interface, and when someone reports "it can't find my club", its output is far more useful in a
bug report than a screenshot. Attach it to releases or don't; the trainer is complete without
it.

The trainer *writes* three small files next to itself the first time you use it. They are
outputs, not inputs — don't ship them, don't commit them, and deleting them loses nothing but
your preferences:

| File | Holds |
|---|---|
| `PcCalcio7Trainer.club` | the club you last selected |
| `PcCalcio7Trainer.lang` | your chosen interface language |
| `PcCalcio7Trainer.originals` | each player's values before you edited them, so **Restore** works after a restart |

---

## Languages

Italian, English and Spanish. The interface follows your Windows language on first run and
falls back to Italian; a dropdown at the top-left changes it.

Spanish is included for a specific reason: PC Calcio is the Italian edition of the Spanish
PC Fútbol, so the attribute names there are the **original** ones — *Juego de manos*,
*Entradas*, *Regate*, *Remate* — rather than translations of the Italian.

**To add a language**, see [CONTRIBUTING.md](CONTRIBUTING.md). It's one new file in
`src/lang/` and one small block in `src/Lang.cs`. Missing keys fall back to Italian, so a
partial translation is still usable and worth submitting.

---

## Build it yourself

No IDE, no SDK, no internet:

```
build.cmd
```

That's it. The script uses `csc.exe` from `C:\Windows\Microsoft.NET`, which is already on every
Windows machine with .NET Framework. Output lands in `dist\`.

Notes for anyone poking at the build:

* `/platform:x86` — the game is 32-bit, and the memory-query struct layout depends on it.
* `/codepage:65001` — so the accented characters in the translations survive compilation.
* `/main:` is given explicitly because the GUI and the diagnostic build share source files.

---

## How it works

Nothing is hardcoded to an address. Every reload of the game moves everything, so the trainer
finds what it needs by searching for it and validating what it finds:

* **Clubs** are found by id and confirmed by their own contents — a plausible capacity,
  founding year and city. Matching on a fixed record spacing was tried first and silently
  latched onto a bogus record whose "capacity" read 253.
* **Your club** is identified by two signals that have to agree: its balance is unique among
  all clubs *and* appears elsewhere in memory (the game keeps a live second copy for the club
  you manage), and its capacity also has exactly one live copy. Either test alone picks the
  wrong club — 94 clubs share a round 20 miliardi, and the structure's shape matches thousands
  of unrelated places.
* **Money and capacity each exist in more than one place.** Writing one copy changes nothing
  on screen. The trainer writes all of them, and refuses to write at all if it finds an
  implausible number of candidates.
* **Players** are found by a distinctive four-pointer signature that precedes their name
  strings.
* **Club and stadium names** are read from the game's own database (`DBDAT\eq*.fdi`), an
  indexed archive whose text is stored XOR 0x61.

### Documentation

The official website — **[calcio.dev/en](https://calcio.dev/en)**, also in
[Italian](https://calcio.dev/) and [Spanish](https://calcio.dev/es) — is the home of the
project outside GitHub.

This project tries to be genuinely reusable rather than just a working binary. Two documents
carry everything learned:

* **[docs/MEMORY-MAP.md](docs/MEMORY-MAP.md)** — every field found, with its offset, type and a
  confidence level saying whether it was merely read, cross-checked, or actually written to and
  confirmed on screen. Includes the file formats, the club-id-to-country scheme, and a list of
  dead ends worth not repeating.
* **[docs/METHODOLOGY.md](docs/METHODOLOGY.md)** — how to do this yourself: probe design,
  structural validation, the "assume more than one copy" rule, and the safety rules that exist
  because the game got crashed twice while learning them.

The Python scripts in **[tools/](tools/README.md)** are the research harness that produced the
map. They are not needed to use or build the trainer, and are kept so that any claim in the map
can be re-run and checked rather than taken on trust.

---

## Risks, honestly

**Antivirus and SmartScreen will complain.** A program that writes to another program's memory
looks exactly like malware to a heuristic scanner, and an unsigned exe from the internet
triggers "unknown publisher". This is expected and unavoidable without a code-signing
certificate. Build it yourself from source if you'd rather not trust a binary.

**It can crash the game.** Memory editing carries that risk inherently; it happened twice
during development, both times from writing to addresses that turned out not to be what was
expected. The current version is much more careful — it validates every candidate and refuses
to write when unsure — but the risk isn't zero. **Save your career before using it.** Your save
files on disk are never touched, so a crash costs you only unsaved progress.

**An edited career is an edited career.** Once you save, the changes are permanent. Restore
undoes player edits, but there's no undo for money or capacity beyond setting them back.

**Absurd values can look strange.** A 200,000-seat stadium works, but ticket income and
attendance figures are computed from capacity, so the finance screens may read oddly. Nothing
breaks; it just looks silly.

---

## Contributing

Translations, bug reports and fixes are all welcome — see [CONTRIBUTING.md](CONTRIBUTING.md).

The most valuable unsolved problems, if you like a puzzle:

1. **The save format.** `SAVE\MANAGER\E036-*\main.dat` holds career state in a packed form.
   Cracking it would allow editing without the game running.
2. **Transfers.** Squad membership is a per-club pointer array; a real signing also builds
   per-player structures we never identified.
3. **The exact morale formula.** The five form-history bytes have measured weights of
   0/10/10/21/35 plus a hidden 23, which reproduces every single-byte test but misses mixed
   writes by a few points — so something is still missing.

## License

MIT — see [LICENSE](LICENSE).
