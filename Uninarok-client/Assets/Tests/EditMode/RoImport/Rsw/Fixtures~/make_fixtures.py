"""Tiny synthetic RSM 1.4 models for the map scene tests (TASKS-0006 T5.1).

Deterministic, standard library only: run `python make_fixtures.py` to regenerate byte-identical files. Change the
script, never the files by hand. Layout of RSM 1.4 as read by GRF: header, textures, main node name, nodes (name, parent,
texture slots, offset matrix, offset, position, rotation angle and axis, scale, vertices, texture coordinates, faces,
rotation keys), then the global position keys (before 1.5) and the volume boxes.
"""
import os
import struct

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data")


def name(s, length):
    b = s.encode("cp949")
    assert len(b) < length
    return b + b"\0" * (length - len(b))


def box_node(node_name, parent, size, rot_keys):
    """A box of edge `size` (Ragnarok units) centred on the node origin, one texture slot, every face two-sided."""
    h = size / 2.0
    vertices = [(x, y, z) for x in (-h, h) for y in (-h, h) for z in (-h, h)]
    tex = [(0.0, 0.0), (1.0, 0.0), (0.0, 1.0), (1.0, 1.0)]
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    faces = []
    for a, b, c, d in quads:
        faces.append(((a, b, c), (0, 1, 3)))
        faces.append(((a, c, d), (0, 3, 2)))

    out = bytearray()
    out += name(node_name, 40) + name(parent, 40)
    out += struct.pack("<i", 1) + struct.pack("<i", 0)                     # texture slots: [0]
    out += struct.pack("<9f", 1, 0, 0, 0, 1, 0, 0, 0, 1)                    # offset matrix: identity
    out += struct.pack("<3f", 0, 0, 0)                                      # offset
    out += struct.pack("<3f", 0, 0, 0)                                      # position
    out += struct.pack("<f3f", 0, 0, 1, 0)                                  # rotation angle and axis
    out += struct.pack("<3f", 1, 1, 1)                                      # scale
    out += struct.pack("<i", len(vertices))
    for v in vertices:
        out += struct.pack("<3f", *v)
    out += struct.pack("<i", len(tex))
    for u, v in tex:
        out += struct.pack("<Iff", 0xFFFFFFFF, u, v)
    out += struct.pack("<i", len(faces))
    for (v0, v1, v2), (t0, t1, t2) in faces:
        out += struct.pack("<3H3HHHii", v0, v1, v2, t0, t1, t2, 0, 0, 1, 0)
    out += struct.pack("<i", len(rot_keys))
    for frame, q in rot_keys:
        out += struct.pack("<i4f", frame, *q)
    return bytes(out)


def rsm(nodes, anim_length):
    out = bytearray(b"GRSM" + bytes([1, 4]))
    out += struct.pack("<ii", anim_length, 2)       # animation length (ms), shade type smooth
    out += bytes([255])                             # alpha
    out += b"\0" * 16                               # reserved
    out += struct.pack("<i", 1) + name("a.bmp", 40)
    out += name("box", 40)                          # main node
    out += struct.pack("<i", len(nodes))
    for n in nodes:
        out += n
    out += struct.pack("<i", 0)                     # global position keys (before 1.5)
    out += struct.pack("<i", 0)                     # volume boxes
    return bytes(out)


def write(rel, data):
    path = os.path.join(ROOT, *rel.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


def main():
    # A static box, 10 Ragnarok units (one Unity unit) on each side.
    write("model/외부소품/box.rsm", rsm([box_node("box", "", 10.0, [])], 0))
    # An animated box: one turn of a quarter about Y over one second.
    q90 = (0.0, 0.70710678, 0.0, 0.70710678)
    write("model/외부소품/spin.rsm", rsm([box_node("box", "", 10.0, [(0, (0.0, 0.0, 0.0, 1.0)), (1000, q90)])], 1000))
    # The texture both models use (24-bit BMP, 1 x 1, grey).
    write("texture/a.bmp", bmp24_1x1((128, 128, 128)))


def bmp24_1x1(rgb):
    pixel = bytes([rgb[2], rgb[1], rgb[0]]) + b"\0"      # BGR, row padded to 4 bytes
    header = b"BM" + struct.pack("<IHHI", 14 + 40 + len(pixel), 0, 0, 54)
    info = struct.pack("<IiiHHIIiiII", 40, 1, 1, 1, 24, 0, len(pixel), 2835, 2835, 0, 0)
    return header + info + pixel


if __name__ == "__main__":
    main()
