# How this was worked out

A reusable method for finding game state in memory, written from the mistakes as much as the
successes. The specific offsets are in [MEMORY-MAP.md](MEMORY-MAP.md); this is the part that
transfers to another game.

The whole map was built in a single session with no debugger, no disassembler and no prior
documentation. What it took instead was a person who could read a number off the screen, and
probes designed so that a wrong guess was visibly wrong.

---

## The loop

1. **Get one ground-truth number from the screen.** Not "roughly 300 million" — the exact figure
   the game displays. Every field that got solved started here. Every field that stalled stalled
   because this step was skipped.
2. **Search for it in every plausible encoding.** 32- and 64-bit integers and floats, and at
   every unit scale. Do not assume the storage unit matches the display: this game shows lire for
   a value stored in something else, a factor of ten out, and that alone burned an hour.
3. **Narrow with a second reading.** One number rarely identifies a field uniquely. Two readings
   of the same field at different values, or one reading plus an independent cross-check, usually
   does.
4. **Probe with several distinct values in one write.** Not one field at a time, and never the
   same value in several fields.
5. **Cross-check the candidate across every record** before believing it.
6. **Confirm by writing and looking at the screen.** Until that happens, the field is a
   hypothesis.

---

## Probe design: distinct values, one round trip

To map eleven attributes, the tempting approach is one at a time — eleven round trips through a
human. Instead write **distinct values into several slots at once**:

> wrote `11, 22, 33, 44, 55` into slots 4, 5, 7, 8, 9

The reply — *"Gioco Mani 11, Dribbling 33, Rifinitura 44, Entrate 22, Tiro 55"* — maps five
fields in one exchange, **and the permutation is self-correcting.** If the values had come back
attached to different labels, the mapping would still be readable straight off the answer.

A bonus fell out of it: an untouched slot showed up as *Passaggio 93*, revealing a twelfth field
nobody had asked about.

**State the prediction before the reading**, as a table of what each possible answer would mean.
It turns a one-word reply into a full result and makes being wrong cheap.

### When to switch to one variable at a time

Bulk probes are right for *identifying* fields. They are wrong for *deriving a formula*.

Morale resisted for hours because five bytes were being changed at once and then a model fitted
to a single resulting number — five unknowns from one equation, repeatedly. Four such readings
fit no arithmetic at all. Zeroing **one byte at a time** produced each byte's weight immediately:
0, 10, 10, 21, 35.

Rule of thumb: **bulk probe to find fields, single-variable probe to measure behaviour.**

---

## Validate structurally, never by value alone

The single most productive habit, and the source of the worst failures when skipped.

**Locate records by their contents.** Finding a club record by matching consecutive ids at a
fixed stride returned a false positive whose "capacity" read 253, and silently aimed every read
and write at the wrong address for a while. Accepting a record only when its capacity, founding
year *and* city id are all plausible fixed it permanently.

**Cross-check a candidate offset across every record you have.** The birth-date offset was
confirmed by checking all 25 players in a squad against the game's own database — 25 exact
matches, no user involvement. That is a far stronger signal than one lucky hit, and it caught the
neighbouring-record bug: at stride `0xF8`, reading `0x16` before a player lands on the
*previous* player.

**Watch for values that are common.** A search for a round capacity of 200,000 returned four
addresses, two of them unrelated data. A distinctive value like 76,122 hides this problem
completely — which is exactly why it bit later rather than immediately.

---

## Assume every displayed value has more than one copy

This game keeps money in two or three places and capacity in two. Writing one copy changes
**nothing on screen**, which reads exactly like "wrong field" and sends you back to searching.

The diagnostic: **write your candidate, then search for the OLD value again.** Whatever still
holds it is the copy the UI reads. That single trick solved both money and capacity after each
had been written off as a dead end.

Then write **all** copies together, so nothing disagrees.

---

## Verify a field by writing to it, never by recognising it

A byte that read 99 for every player was recorded as Morale. It was not. Writing 22 there left
the card showing 99 — the value had simply always been 99 for unrelated reasons, and the
coincidence was mistaken for identification.

Corollary: **a negative result is a result.** "Still 99" and "still 30" each eliminated a
hypothesis in one word and were worth more than another round of speculation.

---

## Distinguish "attached" from "ready"

Opening the process proves the game is running and nothing else. With no career loaded the club
table holds **2 entries**; with one loaded it holds **624**. Find a cheap, unambiguous test for
"is there anything to edit" and check it before reporting success — otherwise the tool claims to
be working while showing nothing.

---

## Inferring things the game never stores

Some values are computed and simply are not in memory. Media is the average of four attributes;
age is derived from a birth date. Both were hunted for as stored fields first.

If a displayed number cannot be found anywhere, consider that it may be **derived** — then find
the inputs by changing candidates and watching it move. Media was solved by setting four
suspected inputs to 40 and seeing Media become 40.

The same reasoning identifies the human club, which the game marks nowhere: it is the only club
whose balance is unique *and* duplicated in memory, and whose capacity also has a live copy.
**Two weak signals that agree beat one strong-looking signal.** Either test alone picked the
wrong club out of 624.

---

## Safety rules, learned expensively

The game was crashed **twice** during this work. Both times by writing to addresses that were not
what they were assumed to be. Save files were never damaged — writes went to memory only — but
unsaved progress was lost.

1. **Match bit-exactly, never approximately.** A tolerance-based scan seeded with a garbage value
   (a denormal float near zero) matched **every zero-filled page in the heap**, and the write loop
   that followed scribbled across thousands of addresses. This was the first crash.
2. **Range-check before writing.** A balance below 1e6 or above 1e15 is not a balance; refuse it.
3. **Assert the previous value.** Write only if the address still holds what you expect.
4. **Cap multi-writes.** If a "find all copies" returns 4,000 addresses, that is a bug, not a
   discovery. Refuse and say so.
5. **Never hand the game a structure it did not create.** Faking squad membership — appending to a
   roster array, allocating a history block — was the second crash. If a feature needs the game's
   own bookkeeping, drive the game's own UI instead.
6. **Tell the user to save first**, and be honest that memory editing can crash a 1998 program.

---

## Search, don't hardcode

Every address moves when the game reloads. Nothing here is a static offset: clubs are found by
id and validated by content, cash by matching the displayed figure, players by a pointer
signature.

The cost is a scan of ~70 MB, which in C# takes 150–500 ms. The benefit is that the tool keeps
working after a reload, across save slots, and — in principle — on other people's installations
and other editions, without a rebuild. A hardcoded-offset trainer breaks the moment anything
shifts.

---

## Reproduce or extend this

The Python harness in [`tools/`](../tools/README.md) is what produced the map. To port to another
PC Fútbol / PC Calcio version:

1. Confirm the process runs natively and is 32-bit.
2. Find the club record first — it is the easiest anchor, because a stadium capacity is a
   distinctive number you can read off the screen and validate against the database.
3. From the club record, the cash search follows; from the string pool, the player records.
4. Expect the same shapes — the `DMFIv1.0` database format and the XOR 0x61 text encoding are
   shared across the series — and expect the offsets to differ.
