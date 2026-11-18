"""Generates the SPEC-0002 importer test fixtures (TASKS-0002 T0.3).

Run from anywhere:  python make_fixtures.py
Writes data/ next to this script. Standard library only; the output is
byte-identical on every run, so regenerating shows no diff in git.
README.md lists every file and the exact pixel values the tests assert.

Pixel tables below are written top row first, as (R, G, B) or (R, G, B, A).
"""

import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")

MAGENTA = (255, 0, 255)
NEAR_KEY = (250, 5, 250)      # within the color-key tolerance of 10
RED, GREEN, BLUE = (255, 0, 0), (0, 255, 0), (0, 0, 255)
WHITE, BLACK, YELLOW = (255, 255, 255), (0, 0, 0), (255, 255, 0)


def write(rel, data):
    path = os.path.join(DATA, *rel.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)
    print(f"{len(data):5d} bytes  data/{rel}")


def bmp_headers(width, height, bpp, pixel_bytes, palette=b""):
    offset = 14 + 40 + len(palette)
    size = offset + len(pixel_bytes)
    file_header = struct.pack("<2sIHHI", b"BM", size, 0, 0, offset)
    colors = len(palette) // 4
    info = struct.pack("<IiiHHIIiiII", 40, width, height, 1, bpp, 0,
                       len(pixel_bytes), 2835, 2835, colors, 0)
    return file_header + info + palette


def bmp24(rows):
    """rows: top row first. Stored bottom-up, BGR, rows padded to 4 bytes."""
    width, height = len(rows[0]), len(rows)
    pad = (4 - (width * 3) % 4) % 4
    body = b""
    for row in reversed(rows):
        body += b"".join(bytes((b, g, r)) for (r, g, b) in row) + b"\0" * pad
    return bmp_headers(width, height, 24, body) + body


def bmp8(palette, rows):
    """palette: list of (R, G, B); rows: palette indices, top row first."""
    width, height = len(rows[0]), len(rows)
    pad = (4 - width % 4) % 4
    pal = b"".join(bytes((b, g, r, 0)) for (r, g, b) in palette)
    body = b"".join(bytes(row) + b"\0" * pad for row in reversed(rows))
    return bmp_headers(width, height, 8, body, pal) + body


def tga32(rows):
    """Uncompressed true-color TGA, 8 alpha bits, bottom-left origin (the default)."""
    width, height = len(rows[0]), len(rows)
    header = struct.pack("<BBBHHBHHHHBB", 0, 0, 2, 0, 0, 0, 0, 0,
                         width, height, 32, 8)
    body = b"".join(bytes((b, g, r, a)) for row in reversed(rows) for (r, g, b, a) in row)
    return header + body


def png_rgba(rows):
    width, height = len(rows[0]), len(rows)

    def chunk(kind, data):
        crc = zlib.crc32(kind + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", crc)

    raw = b"".join(b"\0" + b"".join(bytes(p) for p in row) for row in rows)
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def dds_1x1():
    """A real (uncompressed A8R8G8B8) DDS. Valid, but not a supported texture format."""
    flags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x8      # CAPS HEIGHT WIDTH PIXELFORMAT PITCH
    pixel_format = struct.pack("<IIIIIIII", 32, 0x41, 0, 32,
                               0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000)
    header = struct.pack("<IIIIIII", 124, flags, 1, 1, 4, 0, 0) + b"\0" * 44
    header += pixel_format + struct.pack("<IIIII", 0x1000, 0, 0, 0, 0)
    return b"DDS " + header + bytes((0, 255, 0, 255))


def main():
    sys.stdout.reconfigure(encoding="utf-8")   # Korean names on a Windows console

    # 24-bit BMP, 3 x 2: odd width exercises the 4-byte row padding.
    write("texture/a.bmp", bmp24([
        [MAGENTA, RED, MAGENTA],
        [GREEN, NEAR_KEY, BLUE],
    ]))

    # 8-bit paletted BMP, 4 x 4, upper-case name as in the real data.
    # Palette: 0 magenta, 1 white, 2 black, 3 yellow.
    write("texture/KEY8.BMP", bmp8([MAGENTA, WHITE, BLACK, YELLOW], [
        [0, 0, 0, 0],
        [0, 1, 2, 0],
        [0, 3, 1, 0],
        [0, 0, 0, 0],
    ]))

    # 32-bit TGA with real alpha, 2 x 2. The opaque magenta must NOT be keyed.
    write("texture/alpha32.tga", tga32([
        [(255, 0, 0, 255), (0, 255, 0, 128)],
        [(0, 0, 255, 0), (255, 0, 255, 255)],
    ]))

    # RGBA PNG, 2 x 2, with partial alpha and an opaque magenta pixel.
    write("texture/rgba.png", png_rgba([
        [(255, 255, 255, 255), (0, 0, 0, 64)],
        [(255, 0, 255, 255), (10, 20, 30, 200)],
    ]))

    # Unsupported format: a valid DDS file.
    write("texture/unsupported.dds", dds_1x1())

    # Korean-named folder (stored NFC), 24-bit BMP, 2 x 2 green on magenta.
    write("texture/나무잡초꽃/leaf.bmp", bmp24([
        [GREEN, MAGENTA],
        [MAGENTA, GREEN],
    ]))

    # UI folder: usage is Ui by path (plan section 4.4). 24-bit BMP, 2 x 2.
    write("texture/유저인터페이스/button.bmp", bmp24([
        [WHITE, MAGENTA],
        [MAGENTA, BLACK],
    ]))

    # Corrupt: valid headers for 16 x 16 x 24-bit, but only 10 of 768 pixel bytes.
    good = bmp24([[RED] * 16] * 16)
    write("texture/corrupt.bmp", good[:54 + 10])


if __name__ == "__main__":
    main()
