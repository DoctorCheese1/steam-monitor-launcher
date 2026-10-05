"""Export classic Windows ICO frames with complete XOR pixels and AND masks.
Requires Pillow. Run from any directory. Artwork is unchanged.
"""
from pathlib import Path
import struct
from PIL import Image
root = Path(__file__).resolve().parent
sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
for name in ('Launcher', 'LauncherPaused'):
    image = Image.open(root / (name + '.png')).convert('RGBA')
    frames = []
    for size in sizes:
        frame = image.resize((size, size), Image.Resampling.LANCZOS)
        pixels = frame.load()
        xor = bytearray()
        mask = bytearray()
        stride = ((size + 31) // 32) * 4
        for y in range(size - 1, -1, -1):
            row = bytearray(stride)
            for x in range(size):
                r, g, b, a = pixels[x, y]
                xor.extend((b, g, r, a))
                if a == 0:
                    row[x // 8] |= 0x80 >> (x % 8)
            mask.extend(row)
        header = struct.pack('<IiiHHIIiiII', 40, size, size * 2, 1, 32, 0, len(xor), 0, 0, 0, 0)
        frames.append(header + xor + mask)
    result = bytearray(struct.pack('<HHH', 0, 1, len(frames)))
    offset = 6 + len(frames) * 16
    for size, data in zip(sizes, frames):
        result.extend(struct.pack('<BBBBHHII', size % 256, size % 256, 0, 0, 1, 32, len(data), offset))
        offset += len(data)
    result.extend(b''.join(frames))
    (root.parent / (name + '.ico')).write_bytes(result)
