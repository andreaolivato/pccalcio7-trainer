# PC Calcio 7 — memory map

Everything known about where PC Calcio 7 keeps its career data in memory, and how confident we
are about each piece. If you are porting this to another PC Fútbol / PC Calcio version, read
[METHODOLOGY.md](METHODOLOGY.md) alongside it — the offsets will differ, the structures and the
technique should not.

**Target:** `MANAGCAL.EXE`, PE32 i386 GUI, PC Calcio 7 and PC Calcio 7 Plus (Dinamic
Multimedia, 1998; the Italian edition of PC Fútbol 7). Runs natively on Windows 11, which is
what makes live editing practical.

## Confidence levels used below

| | Meaning |
|---|---|
| **confirmed** | Written to, and the change was observed on the game's own screen |
| **verified** | Read and cross-checked against an independent source (the game's database, or the whole squad), but not written to |
| **measured** | Behaviour quantified by experiment, but the model is incomplete |
| **unknown** | Present, purpose not established |

---

## 1. Club record

A flat structure, one per club, several hundred of them in memory. **624 exist with a career
loaded, 2 at the main menu** — which is the cleanest available test for "is a career loaded".

Located by searching for the club id as a u32 and validating the record by its own contents.
**Do not locate it by record spacing:** matching consecutive ids at a fixed stride produced a
false positive whose "capacity" read 253 and silently aimed reads and writes at the wrong
address.

| Offset | Type | Field | Status |
|---|---|---|---|
| `+0x00` | u32 | Club id (matches the database record id; Sampdoria = 203) | verified |
| `+0x04` | u32 | **Stadium capacity** | **confirmed** |
| `+0x08` | u32 | unknown (observed 0) | unknown |
| `+0x0C` | u32 | Attendance / season-ticket figure (matches the database) | verified |
| `+0x10` | ptr | unknown | unknown |
| `+0x14` | u32 | Squad size (28 for a 28-man roster) | verified |
| `+0x18` | ptr | unknown | unknown |
| `+0x1C` | ptr | unknown | unknown |
| `+0x22` | u16 | Founding year (Sampdoria 1946, Lazio 1900, Inter 1908) | verified |
| `+0x24` | u16 | constant 68 on Italian clubs, varies elsewhere | unknown |
| `+0x26` | u16 | City id (matches the database) | verified |
| `+0x7C` | f32 | A budget-like figure, round for every club | unknown |
| `+0x80` | f64 | **Income accumulator** — see the warning below | measured |

Validation rule that works in practice: capacity in 1,000–300,000, founding year 1880–2000,
city id 1–399.

Capacities read straight out of this table match the real 1998 grounds, which is what first
confirmed the field: Inter 85,443 (San Siro), Lazio and Roma 82,922 (Olimpico), Napoli 78,210
(San Paolo), Juventus 69,041 (Delle Alpi), Sampdoria 40,122 (Marassi).

### Capacity exists in several places

Writing the club record alone changes **nothing on screen**. Live copies exist elsewhere and
the display reads those. Everything tied to the club must be written.

**The per-club stadium struct (found 2026-07-28, debugging a Pisa career).** Every club — 2,054
of them, all countries — has a 184-byte (0xB8) stadium object:

```
+0x00  ptr   into the club's binary sub-record in the FDI pool (the strings just
             before that target name the owning club)
+0x04  u32   small tag, 1–7
+0x08  u32   capacity
+0x0C  u32   75          }
+0x10  u32   1000        }  literal run, the struct's signature
+0x14  u32   50          }
+0x18  u32   50          }
+0x1C  u32   ground value = capacity × 750, rounded to thousands
+0x20  u32   75, then 50, 50, 43000, 1000 ...
```

Copies of a club's struct also appear inside career arrays (fixtures, the current match)
where the **club id sits 0xD8 bytes before the capacity**.

**The ambiguity trap.** Sampdoria's Marassi (40,122 → upgraded 76,122) is a unique value, so
"write every copy of the current value" worked and the original notes wrongly concluded there
were exactly two copies. Pisa's default 20,000-seat ground is shared by *dozens* of clubs —
50 addresses hold 20000, and the stadium structs of Bochum, Alverca, River Plate and others are
byte-identical to Pisa's. Nothing in the struct itself says whose it is. The trainer therefore
only writes a candidate when something ties it to the club: the id right before it (club
record), the id 0xD8 before it (career copies), or a signature/shape match that is **unique in
the whole process** (with a unique capacity value, the one matching stadium struct can only be
ours). With a shared capacity and the stadium screen closed, only the club record is safely
identifiable — the trainer says so and asks for the stadium screen to be opened in game and
Apply pressed again, which brings the on-screen copy into existence where the shape rule can
catch it.

The on-screen (widget) copy is recognised by shape: the capacity value, then a u32 zero, then
an f32 in a money-like range (1e6–1e12), with a heap pointer immediately before it. That shape
is **not rare** — it matches thousands of unrelated places on its own — so it is only trusted
when filtering by the current capacity value leaves exactly one candidate. A round capacity
such as 200,000 matched four addresses, two of them noise.

### The +0x80 trap

This f64 looks exactly like club money: it holds a plausible balance, and **the game really does
add match income to it** — it grew by 217,705,322 after one match. It is not what the screen
reads. Writing 99,000,000,000 here changed nothing visible. Hours were lost here; treat it as a
decoy.

---

## 2. Club cash (the number on screen)

**Not** in the club record. An f64 held **bit-identically in two or three places** at once,
depending on which screen the game has drawn.

**Units.** The value is in the engine's internal currency and the screen shows ten times it:

```
miliardi_on_screen = internal_value / 1e8
```

Confirmed end to end: an internal `33,763,112,606.36` displayed as **337,6 miliardi**, and
writing `50,000,000,000` to every copy made the game show **500**. The likely reason is that PC
Calcio is the Italian edition of the Spanish PC Fútbol — the stored figure is effectively
pesetas, displayed as lire.

**How to find it.** Two ways, both used by the trainer:

1. Read the candidate from the club record at `+0x80`, then search for that exact double. The
   real balance appears more than once; the accumulator copy is one of them.
2. Search for the figure the user reports on screen, as a double in
   `[miliardi × 1e8, (miliardi + 0.1) × 1e8)`, then **group candidates by exact value and take
   the largest group**. Unrelated floats land in the same displayed bucket — a search for 337,6
   returned 8 addresses of which only 3 shared the true value.

**Write every copy.** Writing one leaves the screen unchanged, which is what made this field
look wrong for hours.

---

## 3. Which club the user manages

No field says so. It is inferred, in stages (reworked 2026-07-28 after the original logic
failed on a Pisa career and silently fell back to the club remembered in the settings file):

1. Candidates: clubs whose balance is **unique among all clubs** *and* appears elsewhere in
   memory. The game duplicates the balance into the finance ledger only for the club being
   managed. On a live Pisa career this stage produced eight to ten candidates depending on the
   screen: some AI figures happen to sit in a second struct too.
2. The decider: **fractional lire**. Every human balance observed ends in odd centesimi from
   interest arithmetic (33,763,112,606.36 / 49,494,659,227.07 / 15,692,778,476.98) while every
   impostor was a whole number. Exactly one fractional candidate → done. This is what picks
   Pisa. ("Whole millions" was tried first and lost to Beasain's 87,500,000, a database figure
   with half-million granularity.) No fractional candidate — a career where nothing has been
   earned yet — and detection stands down rather than guess; the club dropdown and the
   remembered choice cover that case.
3. Only on a fractional tie (never observed), the original structural signal: the candidate
   whose **capacity also has a live copy**, in strict form — the unique-stadium-struct rule is
   excluded here because every club with a unique capacity passes it (that rule once crowned
   Beasain), leaving the id-keyed and on-screen shapes described above.

Single tests alone fail badly. "Balance appears more than once" picked **Bournemouth**, because
94 clubs share a round 20 miliardi and common values win a popularity contest. The structural
shape matches 4,789 places. Requiring cash *and* structure unconditionally — the pre-2026-07-28
logic — failed the other way: a club with a shared default capacity has no recognisable live
copy, so detection returned nothing.

---

## 4. Player record

Located by a **name-pointer quad**: four consecutive u32 pointers `[short, full, previous-club,
short]` where the first equals the fourth, aimed into a plain-ASCII string pool
(`"Algerino\0Jimmy ALGERINO\0Chateauroux (96)\0"`). Short and full name are adjacent
(`p1 < p2`, `p2 - p1 <= 120`), but the previous-club pointer `p3` is **unreliable** and has
cost a missing player twice. For a player the career generated itself (youth intake) it is an
empty string in a different heap block — G. Melosi's `p3` sat ~2 MB past `p1`, which is why an
earlier `p3 - p1 <= 120` filter silently dropped him. For other players it is plain **NULL** —
E. Cambiasso and Felipe at Sampdoria, invisible until the `0x00100000 <= p3` filter learned to
accept zero. Practical filters: `p1` inside `0x00100000–0x7FFF0000`, `p3` **null or** inside
that range, `p4 == p1`, `p1 < p2`, `p2 - p1 <= 120`, first string starting with a letter.

In the pool, each name pair is preceded by a small binary header (`… 00 DE 00 8C` before
`"G. Melosi\0Genny Melosi\0"`); the `p1` pointer skips it, so the header never reaches the
string filter.

**Record stride is 0xF8.** All offsets below are relative to the quad, called `Q`.

| Offset | Type | Field | Status |
|---|---|---|---|
| `Q-0x1C` | 5 bytes | Base/database ratings — **not** the card values | verified |
| `Q-0x16` | u16 | The **previous record's** birth year, not this one's — see the trap below | verified |
| `Q-0x08` | u32 | Player id (matches the database record id) | verified |
| `Q-0x02` | u16 | Club in the shipped 1999 database | verified |
| `Q+0x00` | 4×u32 | The name pointers | verified |
| `Q+0x10` | u16 | **Current club in the career** | confirmed |
| `Q+0x1D` | u8 | **Nationality** — country code, drives the extracomunitario rule (see below) | **confirmed** |
| `Q+0x68` | u32 | Club registration — must match `+0x10` | confirmed |
| `Q+0x99` | 13 bytes | **Card attributes** (see below) | **confirmed** |
| `Q+0xA6` | 15 bytes | A second, similar run — base or potential values, ending in the morale history | measured |
| `Q+0xB0` | 5 bytes | **Morale form-history** (see below) | measured |
| `Q+0xD4` | ptr | Per-player history list; null for a player not registered to the club | verified |
| `Q+0xE2` | u16 | **Birth year** | **confirmed** |
| `Q+0xE4` | u8 | Birth day | verified |
| `Q+0xE5` | u8 | Birth month | verified |

### The neighbouring-record trap

`Q-0x16` looks like this player's birth year and is not: the stride is `0xF8`, the birth year
sits at `+0xE2`, and `0xF8 - 0x16 = 0xE2`. Reading "0x16 before the current player" lands on
**the man before him in memory**. This produced a table of plausible-but-wrong ages, and writing
it changed a different player's age while leaving the intended one untouched.

### Card attributes at Q+0x99

Thirteen bytes, each 0–99. Every one confirmed by writing distinct probe values and reading the
card back.

| Slot | Field |
|---|---|
| 0 | Velocità |
| 1 | Resistenza |
| 2 | Aggressività |
| 3 | Qualità |
| 4 | Gioco Mani |
| 5 | Entrate |
| 6 | Passaggio |
| 7 | Dribbling |
| 8 | Rifinitura |
| 9 | Tiro |
| 10 | Stato di forma |
| 11 | **Not Morale.** Reads 99 for every player, which is exactly why it looked like Morale — writing 22 there left the card showing 99. Purpose unknown. |
| 12 | Position code, not a rating. Both keepers share 3, the centre-backs 7, the forwards 8, the midfielders 4. It is 0 for a player not registered to the club, and the card never displays it. |

Independent cross-checks that the mapping is right, taken from a real squad: Buffon has Gioco
Mani 99 with Tiro 30; Christanval Entrate 99; Davids Passaggio 99; Beckham Dribbling 99.

### Media is not stored

```
Media = floor((slot0 + slot1 + slot2 + slot3) / 4)
```

Verified twice on screen: `78, 89, 88, 83` → Media 84, and `11, 22, 33, 44` → Media 27. The six
visible skills do not affect it, which is why probing them left Media unchanged and made the
field look unrelated.

### Age is derived from the birth date

There is no age field. The card computes it from `Q+0xE2`. All 25 players in a test squad had
birth dates matching their database records exactly, and changing a birth year from 1971 to 1979
moved the card from **Età 30** to **Età 22**.

**A birth-date edit does not survive a reload.** Verified live after a season rollover reverted
one: the career record at `Q+0xE2` is the **only** copy in memory (a full scan for the date in
both byte orders finds nothing else for that player), and `main.dat` does not store database
players' birth dates at all — only **career-generated** players' (their dates sit in the save
in FDI order `day u8, month u8, year u16`; database players' dates appear zero times). Static
player data is re-imported from `DBDAT\jug*.fdi` whenever the career is rebuilt, which restores
the original date. In the FDI player record the birth date is at `+0x2E`: `day u8, month u8,
year u16` right before the birthplace string. Career-evolving data (the card attributes) lives
in the save and keeps edits; the birth date is static data and does not.

### Nationality is one byte, and it is the extracomunitario rule

`Q+0x1D` holds a country code. It was found by diffing the full records of a 24-man squad
grouped by known nationality: exactly one byte in the whole record was constant within every
group and different across groups. Writing 36 over Rivaldo's 10 flipped him from Brazilian to
Italian on screen **and let a fourth non-EU player be fielded** — the game has no separate
comunitario flag, this byte is the whole story.

The codes are indexes into the game's country table (the flag archive `DBDAT\BANDERAS.PKF` is
keyed by them). The numbering is alphabetical in **Spanish** — Alemania=2, Argentina=3, …,
"País de Gales"=45 between Noruega=44 and Polonia=46 — with later additions appended at the
end (USA=61, Japan=65). Confirmed against named players in a live career, plus five league
blocks of 900–2,100 players each whose modal code matches the league's country:

| Code | Country | Code | Country | Code | Country |
|---|---|---|---|---|---|
| 2 | Germany | 24 | France | 46 | Poland |
| 3 | Argentina | 27 | Netherlands | 47 | Portugal |
| 4 | Australia | 30 | England | 48 | Czech Republic |
| 9 | Bosnia | 31 | Ireland | 49 | Romania |
| 10 | Brazil | 33 | Iceland | 53 | Sweden |
| 13 | Cameroon | 36 | Italy | 54 | Switzerland |
| 14 | Chile | 43 | Nigeria | 56 | Ukraine |
| 17 | Croatia | 44 | Norway | 57 | Uruguay |
| 18 | Denmark | 45 | Wales | 58 | Yugoslavia |
| 19 | Scotland | | | 61 | USA |
| 22 | Spain | | | 65 | Japan |

Codes not in the table are real countries without a confirmed witness yet; by the alphabetical
pattern, 5–6 should be Austria and Belgium, 50 Russia and 55 Turkey, but none of those has been
checked against a named player.

Two details worth knowing. First, the database stores the **passport** nationality: Simeone
holds 22 (Spain) and Almeyda 36 (Italy) despite both being Argentine internationals, and
Mboma holds 24 (France) — that is how Dinamic modelled EU-passport South Americans, and more
evidence that this byte exists to feed the foreigner rule. Second, like the birth date this is
static database data, re-imported from `DBDAT\jug*.fdi` when the career is rebuilt (the byte
sits in the fixed block that follows the name strings in the FDI record). An edit therefore
lasts until a reload or season rollover and must be re-applied; attribute edits are career
state and persist.

### Morale — measured, not solved

Five bytes at `Q+0xB0`, a recency-weighted form history. Zeroing one byte at a time gives each
byte's own contribution:

| Byte | Morale when zeroed | Weight |
|---|---|---|
| 0 | 99 (no change) | aged out |
| 1 | 89 | ~10% |
| 2 | 89 | ~10% |
| 3 | 78 | ~21% |
| 4 | 64 | ~35% |

The weights sum to 76/99, leaving a hidden ~23/99 term that always reads 99 and acts as a floor —
**nothing below 23 is reachable**. The model

```
morale = (10·b1 + 10·b2 + 21·b3 + 35·b4 + 23·99) / 99
```

reproduces **all six single-byte measurements exactly**, then predicted 70 where the game showed
66 for a mixed write. An exhaustive search over every combination of five weights, a divisor and
a constant leaves a residual error of 4. So the combining rule is real but incomplete.

What works in practice: all five bytes at 99 gives Morale 99 reliably; intermediate targets land
within a point or two. The game rewrites these bytes as the season progresses — bytes written as
99 were later observed reading 97 — which is itself evidence for the form-history reading.

---

## 5. Squad membership — do not fake it

A club's roster is **not** derived from the player's club fields. It is a per-club array of
pointers to player records (pointing at `Q - 8`), null-terminated, heap-allocated, with the count
in the club record at `+0x14`.

Writing a player's club fields makes him appear as belonging to the club on individual screens
but flagged *Ceduto*, and absent from the squad list. Adding him to the roster array and
incrementing the count **crashed the game** — a real signing evidently builds structures that
were never identified.

The supported route is the game's own transfer screen: writing `Q+0x10` and `Q+0x68` is enough to
offer the player under *direttore sportivo → ingaggiare*, and the game then does its own
bookkeeping correctly.

---

## 6. Game files

Read-only in this project; nothing is ever written to disk.

### DBDAT\\*.fdi — the database

`eq*.fdi` clubs, `jug*.fdi` players, `ent*.fdi` coaches. Format:

```
0x00  char[8]  "DMFIv1.0"
0x08  8 bytes  unknown
0x10  u32      number of index entries
0x14  entries, 13 bytes each:  { u32 id, u8 pad, u32 offset, u32 size }
```

Entries cover the file exactly, back to back. Inside a record, **text is XOR 0x61** with plain
u16 length prefixes — the lengths are *not* encoded. A club record's strings run: short name,
**stadium name**, full club name, president, sponsors. A player record holds short name, full
name, birthplace, previous club with season, and scouting prose.

The trainer uses this at runtime purely to show club and stadium names.

### Club ids encode country and division

```
country = id / 100          division = ((id - 1) % 100) / 24 + 1
```

For Italy this lands exactly on the real pyramid: 201–224 Serie A, 225–248 Serie B, 249–272
Serie C1, 273–296 Serie C2. Spain is block 0, England 300, Germany 400, France 500, and so on to
Argentina at 9000 and free agents at 9900.

**Caveat:** the blocks are frozen at the shipped 1999 season. A club promoted or relegated during
a career still sits in its original block, so this is "where the club started", not a live table.

### SAVE\\MANAGER\\E036-N\\main.dat — the save

Largely **not decoded**. What is known:

```
0x00  u32 0x24, u32 0x22        version/magic
0x08  u16 len + bytes           save label   (obfuscated, unsolved, cosmetic)
....  u16 len + bytes           manager name (same encoding)
....  u8 day, u8 month, u16 year, u8 hour, u8 minute   real-world save time
....  16 bytes flags, u16 unknown
....  in-game date, then interleaved club and player blocks
```

Club records appear in the save with a different layout from memory: the club id, then 14 bytes,
then the capacity. Two saves of the same career seconds apart differ in only two bytes besides
the label, so **no strict checksum is expected** — hex-editing saves reportedly works.

Notably, the club cash could **not** be found in the save in any plain encoding, so it is stored
packed or derived. That is the main obstacle to a save editor.

---

## Dead ends worth not repeating

* **`ProMan8/10/12/14/18` are font names** shipped in the executable, not career tags. A money
  value happened to sit 0x40 bytes after one, which produced a very convincing false anchor.
* **The club record `+0x80` accumulator** receives match income but is not the displayed balance.
* **Attribute slot 11** reads 99 for everyone and is not Morale.
* **`Q-0x16`** is the previous player's birth year.
* **Locating records by fixed stride** produced a record whose "capacity" was 253.
* **Faking squad membership** crashes the game.
