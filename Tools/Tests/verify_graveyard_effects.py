"""Run graveyard/banishment regressions against the shipped Windows ocgcore.

Uses cards.bin and the same script search order as ScriptRepository, not a
mock of Card.IsType. Does not modify the catalog, player saves or Unity scene.
Run with Python 3 (64-bit); no third-party packages required.
"""

import ctypes as c
import struct
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
YGO = ROOT / "Assets/StreamingAssets/Ygo"


class Card(c.Structure):
    _fields_ = [("code", c.c_uint32), ("alias", c.c_uint32),
                ("sets", c.POINTER(c.c_uint16)), ("type", c.c_uint32),
                ("level", c.c_uint32), ("attribute", c.c_uint32),
                ("race", c.c_uint64), ("attack", c.c_int32),
                ("defense", c.c_int32), ("lscale", c.c_uint32),
                ("rscale", c.c_uint32), ("link", c.c_uint32)]


class Player(c.Structure):
    _fields_ = [("lp", c.c_uint32), ("hand", c.c_uint32), ("draw", c.c_uint32)]


Reader = c.CFUNCTYPE(None, c.c_void_p, c.c_uint32, c.POINTER(Card))
Done = c.CFUNCTYPE(None, c.c_void_p, c.POINTER(Card))
Script = c.CFUNCTYPE(c.c_int, c.c_void_p, c.c_void_p, c.c_char_p)
Logger = c.CFUNCTYPE(None, c.c_void_p, c.c_char_p, c.c_int)


class Options(c.Structure):
    _fields_ = [("seed", c.c_uint64 * 4), ("flags", c.c_uint64),
                ("p0", Player), ("p1", Player),
                ("reader", Reader), ("payload1", c.c_void_p),
                ("script", Script), ("payload2", c.c_void_p),
                ("logger", Logger), ("payload3", c.c_void_p),
                ("done", Done), ("payload4", c.c_void_p),
                ("unsafe", c.c_uint8)]


def read_catalog():
    records, allocations = {}, []
    with (YGO / "Data/cards.bin").open("rb") as stream:
        magic, version, count = struct.unpack("<4sII", stream.read(12))
        assert (magic, version) == (b"ADCB", 1)
        fmt = struct.Struct("<IIIIIQiiIII")
        for _ in range(count):
            code, alias, kind, level, attribute, race, atk, defense, ls, rs, link = fmt.unpack(stream.read(fmt.size))
            size = stream.read(1)[0]
            sets = (c.c_uint16 * (size + 1))(*struct.unpack(f"<{size}H", stream.read(size * 2)), 0)
            allocations.append(sets)
            records[code] = Card(code, alias, sets, kind, level, attribute, race, atk, defense, ls, rs, link)
        assert not stream.read(1)
    return records, allocations


def main():
    assert c.sizeof(c.c_void_p) == 8, "Use 64-bit Python."
    core = c.CDLL(str(ROOT / "Assets/Plugins/Windows/x86_64/ocgcore.dll"))
    core.OCG_CreateDuel.argtypes = [c.POINTER(c.c_void_p), c.POINTER(Options)]
    core.OCG_CreateDuel.restype = c.c_int
    core.OCG_DestroyDuel.argtypes = [c.c_void_p]
    core.OCG_DestroyDuel.restype = None
    core.OCG_LoadScript.argtypes = [c.c_void_p, c.c_char_p, c.c_uint32, c.c_char_p]
    core.OCG_LoadScript.restype = c.c_int
    records, allocations = read_catalog()
    errors, reports = [], []

    @Reader
    def reader(_, code, destination):
        if code in records:
            destination[0] = records[code]
        else:
            errors.append(f"Missing card {code}")

    @Done
    def done(_, __):
        pass  # The catalog/setcode buffers live until after DestroyDuel.

    @Script
    def script(_, duel, raw_name):
        name = raw_name.decode("utf-8")
        if Path(name).name != name:
            return 0
        for directory in ("CustomScripts", "Scripts", "Scripts/official"):
            path = YGO / directory / name
            if path.is_file():
                data = path.read_bytes()
                return core.OCG_LoadScript(duel, data, len(data), raw_name)
        return 0

    @Logger
    def logger(_, message, kind):
        text = message.decode("utf-8", errors="replace")
        (errors if kind == 0 else reports).append(text)
        print(text)

    options = Options((c.c_uint64 * 4)(1, 2, 3, 4), 0,
                      Player(8000, 0, 0), Player(8000, 0, 0),
                      reader, None, script, None, logger, None, done, None, 1)
    duel = c.c_void_p()
    assert core.OCG_CreateDuel(c.byref(duel), c.byref(options)) == 0
    try:
        for name in (b"constant.lua", b"utility.lua"):
            assert script(None, duel, name), name
        path = Path(__file__).with_name("graveyard_effects.lua")
        data = path.read_bytes()
        loaded = core.OCG_LoadScript(duel, data, len(data), b"graveyard_effects.lua")
        assert loaded and not errors, "Native regression failed: " + " | ".join(errors)
        assert "PASS: graveyard effect regressions" in reports, "Tests did not reach completion."
    finally:
        core.OCG_DestroyDuel(duel)


if __name__ == "__main__":
    main()
