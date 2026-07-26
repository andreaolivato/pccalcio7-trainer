# Changelog

## Unreleased

* Fix: players the career generated itself (youth intake) were missing from the squad
  list. Their previous-club string is an empty string in a distant heap block, so the
  scanner's requirement that all three name strings sit within 120 bytes rejected the
  record. Found with G. Melosi at Sampdoria; the squad scan now requires only the
  short/full pair to be adjacent.
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
