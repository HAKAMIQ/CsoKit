# Third-Party Notices

CsoKit includes third-party compression libraries in its native backend.

These components are maintained by their respective upstream projects and remain subject to their original licenses.

## zlib

- Project: zlib
- Version: 1.3.2
- License: zlib License
- Upstream: https://github.com/madler/zlib

Used for raw Deflate compression candidate trials, including default, filtered, Huffman-only, and RLE strategies.

## libdeflate

- Project: libdeflate
- Version: 1.25
- License: MIT License
- Upstream: https://github.com/ebiggers/libdeflate

Used for raw Deflate compression candidate trials at multiple compression levels.

## Zopfli

- Project: Zopfli Compression Algorithm
- License: Apache License 2.0
- Upstream: https://github.com/google/zopfli
- Source: `native/third_party/zopfli`
- License file: `native/third_party/zopfli/COPYING`

Zopfli is used only when `--zopfli` is explicitly requested. Normal compression profiles do not enable it automatically.

## Additional notes

The managed Deflate path remains available without the native backend.

See `LICENSE.txt` for CsoKit license terms.
