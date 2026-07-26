# Contributing

Thanks for looking. This is a small project with a small surface, so contributing is easy.
The project's official website is **[calcio.dev/en](https://calcio.dev/en)** (also in
[Italian](https://calcio.dev/) and [Spanish](https://calcio.dev/es)).

## Adding or fixing a translation

Translations are **compiled into the exe** — there is no language file to ship or copy. That
keeps the release a single self-contained file, and it means a new language arrives as a pull
request rather than as a download.

Each language is one file, so two people translating into different languages never touch the
same lines.

**To add a language, say French:**

1. Copy `src/lang/En.cs` to `src/lang/Fr.cs`.
2. Rename the class from `En` to `Fr`, set `DisplayName` to `"Français"`, and translate the
   values. **Leave the keys alone** — only the right-hand side of each pair changes.
3. Keep the `{0}` placeholders. They are filled in with numbers and names at runtime, and
   dropping one will throw. You may reorder them if your language needs a different word order.
4. In `src/Lang.cs`, add `Fr` to the four arrays in the marked block: `Names`, `Maps`,
   `AttrSets` and `Codes` (`Codes` is the two-letter code used to follow the Windows setting,
   so `"fr"`). Add `Fr = 3` to the `Lg` enum.
5. Run `build.cmd` and check it.

`build.cmd` picks up `src/lang/*.cs` with a wildcard, so there's nothing to add there.

**A translated README is just as welcome**, and independent of the interface. `README.it.md` and
`README.es.md` are deliberately shorter than the English one — install, use, warnings, and links
to the full documentation — because a short accurate translation ages better than a full one
that drifts. Copy either as a starting point, name it `README.<code>.md`, and add it to the
language bar at the top of all the others.

**Partial translations are welcome.** Any key you leave out falls back to Italian rather than
showing a raw key, so submitting something incomplete is genuinely useful — it doesn't have to
be finished to be merged.

**A note on accented characters.** Source files are compiled as UTF-8 (`/codepage:65001`).
Write accents directly if your editor saves UTF-8; if you're unsure, `à`-style escapes
always work.

**Attribute names should match the game.** These label a player's card, so use whatever wording
the game itself shows in your language if a version exists — don't translate from the English.
The Spanish file does this: PC Calcio is the Italian edition of PC Fútbol, so its attribute
names are the Spanish originals.

## Reporting a bug

Please include:

* Which edition — PC Calcio 7, or 7 Plus
* Your Windows version
* What the trainer's log panel said (the box at the bottom of the window)
* Whether the game was running as administrator

If the trainer can't find something, `SelfTest.exe` from a release or your own build prints what
it sees — club count, detected club, timings. That output is the single most useful thing to
paste into an issue.

## Code changes

A few rules that exist because breaking them cost real time during development:

* **Never write to an address you found only by matching a value.** Round numbers collide with
  unrelated data — a capacity of 200000 matched four addresses, two of them noise. Validate a
  candidate structurally, and refuse to write when the count is implausible. Writing unvalidated
  addresses crashed the game during development.
* **Assume every value the game displays exists in more than one place.** Money lives in
  several, capacity in two. A write that changes nothing on screen usually means the copy the UI
  reads still holds the old value.
* **Don't locate a record by fixed spacing alone.** That produced a false positive whose
  "capacity" read 253 and quietly aimed reads and writes at the wrong place.
* **Verify a field by writing to it,** not by noticing that its value looks right. A byte that
  read 99 for every player looked exactly like Morale, and writing to it did nothing at all.
* Keep it dependency-free and buildable with `build.cmd` on a stock Windows machine. No NuGet,
  no SDK, no IDE project files.

[`docs/MEMORY-MAP.md`](docs/MEMORY-MAP.md) has the full field map with a confidence level per
field, and [`docs/METHODOLOGY.md`](docs/METHODOLOGY.md) explains how it was worked out. Read both
before changing anything that touches memory offsets.
