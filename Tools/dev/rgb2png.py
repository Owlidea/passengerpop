"""Converts the raw frames written by ZzShotTemp into PNG: rgb2png.py SRC.rgb DST.png [x y w h]"""
import struct, sys, zlib

W, H = 1080, 1920
src, dst = sys.argv[1], sys.argv[2]
x0, y0, cw, ch = [int(a) for a in sys.argv[3:7]] if len(sys.argv) >= 7 else (0, 0, W, H)
data = open(src, "rb").read()
rows = [b"\0" + data[((H - 1 - y) * W + x0) * 3:((H - 1 - y) * W + x0 + cw) * 3] for y in range(y0, y0 + ch)]


def chunk(tag, body):
    return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag + body) & 0xffffffff)


open(dst, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", cw, ch, 8, 2, 0, 0, 0))
                      + chunk(b"IDAT", zlib.compress(b"".join(rows), 6)) + chunk(b"IEND", b""))
