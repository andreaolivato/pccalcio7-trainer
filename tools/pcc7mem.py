"""Live memory access layer for PC Calcio 7 Plus (MANAGCAL.EXE).

Reverse-engineered team record (found in the process heap, verified against
DBDAT/eq99036.fdi and against the user's in-game values):

    struct Team {                      // stride 0x14C = 332 bytes, contiguous table
      +0x00  u32  team_id;             // matches FDI record id (Sampdoria = 203)
      +0x04  u32  stadium_capacity;    // VERIFIED: user's upgraded Marassi = 76122
      +0x08  u32  ?
      +0x0C  u32  attendance;          // matches FDI "spettatori" field
      +0x10  ptr
      +0x14  u32  ?
      +0x18  ptr
      +0x1C  ptr
      +0x22  u16  founded_year;        // matches FDI (Sampdoria 1946, Lazio 1900)
      +0x24  u16  const 68
      +0x26  u16  city_id;             // matches FDI
      +0x7C  f32  budget?;             // round values for every club (5.0 mld for Samp)
      +0x80  f64  money;               // CANDIDATE: only the human club has a
                                       // non-round value here (33,763,112,606 lire)
    };

Cross-check: capacities read out of this table match the real 1998 stadiums
exactly - Inter 85443 (San Siro), Lazio/Roma 82922 (Olimpico), Napoli 78210
(San Paolo), Juventus 69041 (Delle Alpi).
"""

import ctypes
import ctypes.wintypes as wt
import struct

PROCESS_VM_READ = 0x0010
PROCESS_VM_WRITE = 0x0020
PROCESS_VM_OPERATION = 0x0008
PROCESS_QUERY_INFORMATION = 0x0400
MEM_COMMIT = 0x1000
PAGE_GUARD = 0x100
WRITABLE = (0x04, 0x08, 0x40, 0x80)

TEAM_STRIDE = 0x14C
OFF_ID = 0x00
OFF_CAPACITY = 0x04
OFF_ATTENDANCE = 0x0C
OFF_FOUNDED = 0x22
OFF_CITY = 0x26
OFF_BUDGET_F32 = 0x7C
OFF_MONEY_F64 = 0x80

k32 = ctypes.windll.kernel32


class MEMORY_BASIC_INFORMATION(ctypes.Structure):
    _fields_ = [("BaseAddress", ctypes.c_void_p),
                ("AllocationBase", ctypes.c_void_p),
                ("AllocationProtect", wt.DWORD),
                ("RegionSize", ctypes.c_size_t),
                ("State", wt.DWORD),
                ("Protect", wt.DWORD),
                ("Type", wt.DWORD)]


def find_process(name="MANAGCAL.EXE"):
    """Return the pid of the running game, or None."""
    import subprocess
    out = subprocess.run(
        ["tasklist", "/FI", f"IMAGENAME eq {name}", "/FO", "CSV", "/NH"],
        capture_output=True, text=True).stdout
    for line in out.splitlines():
        parts = [p.strip('"') for p in line.split('","')]
        if len(parts) >= 2 and parts[0].upper() == name.upper():
            return int(parts[1])
    return None


class Game:
    def __init__(self, pid):
        self.pid = pid
        self.h = k32.OpenProcess(
            PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION,
            False, pid)
        if not self.h:
            raise OSError(f"OpenProcess({pid}) failed: {ctypes.GetLastError()}")

    def regions(self):
        addr = 0
        mbi = MEMORY_BASIC_INFORMATION()
        while addr < 0x7FFF0000:
            if not k32.VirtualQueryEx(self.h, ctypes.c_void_p(addr), ctypes.byref(mbi),
                                      ctypes.sizeof(mbi)):
                break
            if (mbi.State == MEM_COMMIT and (mbi.Protect & 0xFF) in WRITABLE
                    and not (mbi.Protect & PAGE_GUARD)):
                yield (mbi.BaseAddress or 0), mbi.RegionSize
            addr = (mbi.BaseAddress or 0) + mbi.RegionSize

    def read(self, addr, size):
        buf = ctypes.create_string_buffer(size)
        got = ctypes.c_size_t()
        if not k32.ReadProcessMemory(self.h, ctypes.c_void_p(addr), buf, size, ctypes.byref(got)):
            return b""
        return buf.raw[:got.value]

    def write(self, addr, data):
        wrote = ctypes.c_size_t()
        ok = k32.WriteProcessMemory(self.h, ctypes.c_void_p(addr), data, len(data),
                                    ctypes.byref(wrote))
        if not ok:
            raise OSError(f"WriteProcessMemory @0x{addr:X} failed: {ctypes.GetLastError()}")
        return wrote.value

    # ---- team table ----

    def find_team(self, team_id):
        """Address of one team record, verified by its own contents.

        Matching consecutive ids at TEAM_STRIDE is NOT reliable - it hit a
        false positive whose "capacity" read 253. A record is accepted only if
        the capacity, founding year and city id are all plausible.
        """
        pat = struct.pack("<I", int(team_id))
        for base, size in self.regions():
            d = self.read(base, size)
            s = 0
            while True:
                i = d.find(pat, s)
                if i < 0:
                    break
                s = i + 1
                if i + 0x30 > len(d):
                    continue
                cap = struct.unpack_from("<I", d, i + OFF_CAPACITY)[0]
                founded = struct.unpack_from("<H", d, i + OFF_FOUNDED)[0]
                city = struct.unpack_from("<H", d, i + OFF_CITY)[0]
                if 1000 <= cap <= 300000 and 1880 <= founded <= 2000 and 0 < city < 400:
                    return base + i
        return None

    def find_team_table(self, sample_ids=(203, 204, 205, 206)):
        """Deprecated: use find_team(). Kept so older call sites keep working."""
        return self.find_team(sample_ids[0])

    def team_addr(self, table_addr, team_id, first_id=203, span=1200):
        """Address of a team record, located by scanning the table for its id."""
        for n in range(-span, span):
            a = table_addr + n * TEAM_STRIDE
            d = self.read(a, 4)
            if len(d) == 4 and struct.unpack_from("<I", d, 0)[0] == team_id:
                return a
        return None

    def read_team(self, addr):
        d = self.read(addr, TEAM_STRIDE)
        if len(d) < TEAM_STRIDE:
            return None
        return {
            "addr": addr,
            "id": struct.unpack_from("<I", d, OFF_ID)[0],
            "capacity": struct.unpack_from("<I", d, OFF_CAPACITY)[0],
            "attendance": struct.unpack_from("<I", d, OFF_ATTENDANCE)[0],
            "founded": struct.unpack_from("<H", d, OFF_FOUNDED)[0],
            "city": struct.unpack_from("<H", d, OFF_CITY)[0],
            "budget_f32": struct.unpack_from("<f", d, OFF_BUDGET_F32)[0],
            "money_f64": struct.unpack_from("<d", d, OFF_MONEY_F64)[0],
        }

    def find_capacity_copies(self, current):
        """Every u32 holding the current capacity.

        Capacity lives in TWO places, like the club cash: the team record and a
        second live-career structure. Writing only the team record leaves the
        stadium screen unchanged - confirmed in game.
        """
        pat = struct.pack("<I", int(current))
        out = []
        for base, size in self.regions():
            d = self.read(base, size)
            s = 0
            while True:
                i = d.find(pat, s)
                if i < 0:
                    break
                out.append(base + i)
                s = i + 1
        return out

    def set_capacity_everywhere(self, current, value):
        """Set the stadium capacity in every copy. Returns the addresses hit."""
        if not 100 <= int(value) <= 1_000_000:
            raise ValueError("implausible capacity")
        addrs = self.find_capacity_copies(current)
        if not addrs:
            raise ValueError(f"no location holds {current}")
        if len(addrs) > 8:
            raise ValueError(f"{len(addrs)} matches is too many to be the capacity")
        for a in addrs:
            self.write(a, struct.pack("<I", int(value)))
        return addrs

    def set_capacity(self, addr, value):
        self.write(addr + OFF_CAPACITY, struct.pack("<I", int(value)))

    def set_team_accumulator(self, addr, value):
        """Team-record +0x80. The game DOES add match income here, but the club
        cash shown on screen is NOT read from it - see find_money()."""
        self.write(addr + OFF_MONEY_F64, struct.pack("<d", float(value)))

    # ---- club cash (the number shown on screen) ----
    #
    # Lives in the career/manager structure, which begins with the ASCII tag
    # "ProMan<slot>" (slot matches the SAVE\MANAGER\E036-<slot> folder).
    # The balance is an f64 at +0x40 from that tag.
    #
    # UNITS: the value is in the engine's internal currency (the game is an
    # Italian localisation of a Spanish original, so this is effectively
    # pesetas). The screen shows LIRE = internal * 10, i.e.
    #     miliardi_on_screen = internal / 1e8
    #
    # CONFIRMED end-to-end 2026-07-26: balance 33,763,112,606.36 shown as
    # "337,6 miliardi" was stored bit-identically at 3 addresses; writing
    # 50,000,000,000 to all three made the game display "500". Writing only the
    # team-record accumulator (+0x80) does NOT change the display.

    MONEY_TAG_OFFSET = 0x40
    DISPLAY_SCALE = 1e8          # internal units per "miliardo" on screen

    # A balance below this is certainly not the club cash - it is an
    # uninitialised double or an unrelated field that merely follows a
    # "ProMan..." byte sequence. Without this floor, denormals such as 1e-323
    # pass a naive "v > 0" test and every zero-filled page looks like a match.
    MONEY_MIN = 1e6
    MONEY_MAX = 1e15
    MAX_COPIES = 16          # refuse to write if more "copies" than this

    def find_money_by_display(self, miliardi, step=0.1):
        """Find the club cash from the figure shown in game.

        `miliardi` is what the screen shows (e.g. 339.8). The engine value is
        miliardi * DISPLAY_SCALE; the screen shows one decimal, so accept the
        whole bucket [miliardi, miliardi + step).

        Returns [(address, value)] sorted by address.

        NOTE: do not anchor on the "ProMan<n>" strings near a match - those are
        font names (ProMan8/10/12/14/18 ship in the executable), so a hit only
        means the value sits in a widget that draws it.
        """
        lo = miliardi * self.DISPLAY_SCALE
        hi = (miliardi + step) * self.DISPLAY_SCALE
        out = []
        for base, size in self.regions():
            d = self.read(base, size)
            for k in range(8):
                buf = d[k:len(d) - ((len(d) - k) % 8)]
                for idx, (v,) in enumerate(struct.iter_unpack("<d", buf)):
                    if lo <= v < hi:
                        out.append((base + k + idx * 8, v))
        return sorted(set(out))

    def find_money_copies(self, value):
        """Addresses holding EXACTLY `value` as f64 (bit-identical).

        Exact matching only. An earlier tolerance-based version matched every
        zero-filled page in the heap and a write loop over those addresses
        crashed the game.
        """
        if not (self.MONEY_MIN <= value <= self.MONEY_MAX):
            raise ValueError(f"implausible balance {value!r}; refusing to scan")
        pat = struct.pack("<d", float(value))
        out = []
        for base, size in self.regions():
            d = self.read(base, size)
            s = 0
            while True:
                i = d.find(pat, s)
                if i < 0:
                    break
                out.append(base + i)
                s = i + 1
        return out

    def set_money(self, addr, internal_value, expect=None):
        """Write the balance, optionally asserting the current value first."""
        v = float(internal_value)
        if not (self.MONEY_MIN <= v <= self.MONEY_MAX):
            raise ValueError(f"refusing to write implausible balance {v!r}")
        if expect is not None:
            (cur,) = struct.unpack("<d", self.read(addr, 8))
            if cur != expect:
                raise ValueError(
                    f"0x{addr:08X} holds {cur!r}, expected {expect!r} - not writing")
        self.write(addr, struct.pack("<d", v))

    def set_money_everywhere(self, copies, internal_value, expect):
        """Guarded multi-write: bails out if the address list looks wrong."""
        if not copies:
            raise ValueError("no addresses to write")
        if len(copies) > self.MAX_COPIES:
            raise ValueError(
                f"{len(copies)} candidate addresses is implausible for a balance; "
                "refusing to write (this guard exists because an unguarded "
                "write loop once crashed the game)")
        for a in copies:
            self.set_money(a, internal_value, expect=expect)
        return len(copies)
