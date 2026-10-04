"""Builds src/app.ico: keyboard glyph (Segoe Fluent Icons) on a gradient rounded square."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

GLYPH = "\uE765"  # KeyboardClassic
FONT = r"C:\Windows\Fonts\SegoeIcons.ttf"
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
OUT = Path(__file__).resolve().parent.parent / "src" / "app.ico"


def render(size: int) -> Image.Image:
    scale = 4  # supersample, then downscale for smooth edges
    s = size * scale
    grad = Image.new("RGBA", (s, s))
    top, bottom = (124, 77, 255), (0, 184, 212)
    px = grad.load()
    for y in range(s):
        for x in range(s):
            t = (x + y) / (2 * (s - 1))
            px[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)) + (255,)
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, s - 1, s - 1), radius=round(s * 0.22), fill=255)
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    img.paste(grad, (0, 0), mask)

    font = ImageFont.truetype(FONT, round(s * 0.66))
    draw = ImageDraw.Draw(img)
    l, t, r, b = draw.textbbox((0, 0), GLYPH, font=font)
    draw.text(((s - (r - l)) / 2 - l, (s - (b - t)) / 2 - t), GLYPH, font=font, fill=(255, 255, 255, 255))
    return img.resize((size, size), Image.LANCZOS)


frames = [render(n) for n in SIZES]
frames[-1].save(OUT, format="ICO", sizes=[(n, n) for n in SIZES], append_images=frames[:-1])
print("wrote", OUT)
