"""Regenerates the CSO fixtures in this folder (Python standard library only).

    python make_cso_fixtures.py

They reproduce maxcso's layout for blocks larger than 2048 bytes, which it uses
for images of 2 GB and more: a final partial block is zero-filled and compressed
as a full block, padding after each block is zeros, and the final index entry
points after that padding. A block whose deflate output is not smaller than the
block is stored.

- cso1-16k-tail.cso: CSO1 (raw deflate), 16384-byte blocks, partial last block.
- cso2-16k-tail.cso: CSO2 with deflate and stored blocks only, same layout.
- dax-tail.dax: DAX (zlib, 8192-byte blocks) whose final partial block is
  compressed as a full block.
"""
import hashlib
import struct
import zlib
from pathlib import Path

HERE = Path(__file__).resolve().parent


def logical_bytes(size: int) -> bytes:
    out = bytearray()
    state = 0x2468ACE1
    block = 0
    while len(out) < size:
        kind = block % 3
        if kind == 0:
            chunk = bytes(2048)
        elif kind == 1:
            chunk = (b"CsoKit CSO fixture %05d " % block) * 90
        else:
            chunk = bytearray()
            for _ in range(2048):
                state = (state * 1103515245 + 12345) & 0x7FFFFFFF
                chunk.append((state >> 16) & 0xFF)
        out += chunk[:2048]
        block += 1
    return bytes(out[:size])


def raw_deflate(data: bytes) -> bytes:
    c = zlib.compressobj(9, zlib.DEFLATED, -15)
    return c.compress(data) + c.flush()


def write_cso(data: bytes, block_size: int, align: int, version: int) -> bytes:
    blocks = (len(data) + block_size - 1) // block_size
    unit = 1 << align
    out = bytearray(24 + 4 * (blocks + 1))
    out += bytes((unit - len(out) % unit) % unit)
    index = []

    for i in range(blocks):
        chunk = data[i * block_size:(i + 1) * block_size]
        chunk += bytes(block_size - len(chunk))
        comp = raw_deflate(chunk)
        stored = len(comp) >= block_size
        # CSO1 marks stored blocks with the high bit; CSO2 uses the high bit for
        # LZ4 and recognizes stored blocks by size >= block_size.
        flag = 0x80000000 if (stored and version == 1) else 0
        index.append((len(out) >> align) | flag)
        out += chunk if stored else comp
        out += bytes((unit - len(out) % unit) % unit)

    index.append(len(out) >> align)
    struct.pack_into("<4sIQIBB2x", out, 0, b"CISO", 24, len(data), block_size, version, align)
    struct.pack_into("<%dI" % len(index), out, 24, *index)
    return bytes(out)


def write_dax(data: bytes) -> bytes:
    block_size = 8192
    blocks = (len(data) + block_size - 1) // block_size
    payloads = []
    for i in range(blocks):
        chunk = data[i * block_size:(i + 1) * block_size]
        chunk += bytes(block_size - len(chunk))
        payloads.append(zlib.compress(chunk, 9))
    table = 32 + 4 * blocks + 2 * blocks
    offsets, pos = [], table
    for payload in payloads:
        offsets.append(pos)
        pos += len(payload)
    out = bytearray(struct.pack("<4sIII16x", b"DAX\x00", len(data), 1, 0))
    out += struct.pack("<%dI" % blocks, *offsets)
    out += struct.pack("<%dH" % blocks, *[len(x) for x in payloads])
    for payload in payloads:
        out += payload
    return bytes(out)


SIZE = 3 * 16384 + 5 * 2048
data = logical_bytes(SIZE)
(HERE / "cso1-16k-tail.cso").write_bytes(write_cso(data, 16384, 2, 1))
(HERE / "cso2-16k-tail.cso").write_bytes(write_cso(data, 16384, 2, 2))
print(f"{SIZE} bytes, sha256 {hashlib.sha256(data).hexdigest()}")

DAX_SIZE = 3 * 8192 + 3 * 2048
dax_data = logical_bytes(DAX_SIZE)
(HERE / "dax-tail.dax").write_bytes(write_dax(dax_data))
print(f"DAX {DAX_SIZE} bytes, sha256 {hashlib.sha256(dax_data).hexdigest()}")
