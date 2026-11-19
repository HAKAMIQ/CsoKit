"""Regenerates the ZSO fixtures in this folder with the reference LZ4 library.

    pip install lz4==4.4.5
    python make_zso_fixtures.py

The fixtures reproduce the block layouts of the two ZSO writers in use:

- ziso.py (ARK-4 / PRO CFW): pads BEFORE each block up to 1 << align with b"X",
  the final index entry points right after the last block.
- maxcso: pads AFTER each block with zeros; a final partial block is zero-filled
  and compressed as a full block; the final index entry points after the padding.

Both store a block uncompressed with index bit 0x80000000 set.
"""
import hashlib
import struct
from pathlib import Path

import lz4.block

HERE = Path(__file__).resolve().parent


def logical_bytes(size: int) -> bytes:
    """Deterministic content mixing compressible runs and incompressible noise."""
    out = bytearray()
    state = 0x12345678
    block = 0
    while len(out) < size:
        kind = block % 4
        if kind == 0:
            chunk = bytes(2048)
        elif kind == 1:
            chunk = (b"CsoKit ZSO fixture %05d " % block) * 90
        else:
            chunk = bytearray()
            for _ in range(2048):
                state = (state * 1103515245 + 12345) & 0x7FFFFFFF
                chunk.append((state >> 16) & 0xFF)
            if kind == 3:
                chunk[: 2048 - 333] = bytes([block & 0xFF]) * (2048 - 333)
        out += chunk[:2048]
        block += 1
    return bytes(out[:size])


def write_zso(data: bytes, block_size: int, align: int, style: str) -> bytes:
    blocks = (len(data) + block_size - 1) // block_size
    unit = 1 << align
    pad_byte = b"X" if style == "ziso" else b"\0"
    out = bytearray(24 + 4 * (blocks + 1))
    index = []

    if style == "maxcso":
        out += b"\0" * ((unit - len(out) % unit) % unit)

    for i in range(blocks):
        chunk = data[i * block_size:(i + 1) * block_size]
        if style == "maxcso" and len(chunk) < block_size:
            chunk += bytes(block_size - len(chunk))
        if style == "ziso":
            out += pad_byte * ((unit - len(out) % unit) % unit)
        comp = lz4.block.compress(chunk, mode="high_compression", compression=12, store_size=False)
        threshold = len(chunk) * 95 // 100 if style == "ziso" else len(chunk)
        stored = len(comp) >= threshold
        index.append((len(out) >> align) | (0x80000000 if stored else 0))
        out += chunk if stored else comp
        if style == "maxcso":
            out += b"\0" * ((unit - len(out) % unit) % unit)

    index.append(len(out) >> align)
    struct.pack_into("<4sIQIBB2x", out, 0, b"ZISO", 24, len(data), block_size, 1, align)
    struct.pack_into("<%dI" % len(index), out, 24, *index)
    return bytes(out)


FIXTURES = [
    ("ziso-xpad-align2.zso", 24 * 2048, 2048, 2, "ziso"),
    ("maxcso-align2.zso", 24 * 2048, 2048, 2, "maxcso"),
    ("maxcso-16k-tail.zso", 2 * 16384 + 3 * 2048, 16384, 0, "maxcso"),
]

for name, size, block_size, align, style in FIXTURES:
    data = logical_bytes(size)
    (HERE / name).write_bytes(write_zso(data, block_size, align, style))
    print(f"{name}: {size} bytes, sha256 {hashlib.sha256(data).hexdigest()}")
