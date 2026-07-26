# Changelog

## v1.1.0

* **New: player nationality.** A dropdown on the player panel with 31 confirmed
  countries. Nationality is the byte the game's comunitario/extracomunitario rule
  reads — making a Brazilian Italian frees up a foreigner slot, confirmed in game by
  fielding a fourth non-EU player. Like the age, the edit lasts until the career is
  reloaded (the value is re-imported from `DBDAT\jug*.fdi`); re-apply it after a
  reload. Restore covers it, and `.originals` files from v1.0.0 still load.
* The country table (code, name in the three languages, how each code was confirmed)
  is documented in `docs/MEMORY-MAP.md`; unmapped codes show as "Code N" and remain
  editable.
* `SelfTest.exe` now prints each player's nationality, and accepts a club id argument
  (`SelfTest 203`) to run the squad checks when club auto-detection stands down.
* Fix: two classes of player were missing from the squad list, both from the same
  unreliable field. A career-generated player's previous-club string is an empty
  string in a distant heap block (found with G. Melosi), and for some players the
  pointer to it is plain NULL (found with E. Cambiasso and Felipe) — the scanner
  required all three name strings to sit within 120 bytes, then required the
  pointer to at least look like a pointer, and both requirements dropped real
  players. Only the short/full name pair is checked now; the previous-club pointer
  may be null or anything plausible.
* Known limit, now documented: an age edit lasts only until the game reloads the
  career (new season or full reload). Birth dates of database players are never
  stored in the save — the game re-imports them from `DBDAT\jug*.fdi` — so the
  edit has to be re-applied. Attribute edits are career state and do persist.

## v1.0.0

First release.

* Club money, stadium capacity, player attributes, age and morale, all edited in the
  running game
* Detects which club you manage with no input
* Searches all 925 clubs by name; club and stadium names read from the game's own database
* Per-player Restore, which survives closing the trainer
* Italian, English and Spanish; follows the Windows language, falls back to Italian
* Attaches on startup; explains inline when the game isn't running or no career is loaded
* Single exe under 50 KB, no dependencies, builds with the compiler already in Windows

Known limits: no player transfers (squad membership needs structures a real signing
creates), no save-file editing, and morale is exact at 99 but can land a point or two
out at intermediate values.
