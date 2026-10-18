"""Создаёт src/app.ico: графитовая клавиша K с малиново-фиолетовым контуром."""
from pathlib import Path
from PIL import Image, ImageDraw

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
OUT = Path(__file__).resolve().parent.parent / "src" / "app.ico"


def render(size: int) -> Image.Image:
    scale = 8
    unit = size * scale / 32
    img = Image.new("RGBA", (size * scale, size * scale))
    draw = ImageDraw.Draw(img)
    accent = (255, 255, 255, 255)
    graphite = (32, 29, 36, 255)
    def coords(values):
        return tuple(round(v * unit) for v in values)
    draw.rounded_rectangle(coords((3, 2, 29, 30)), radius=round(5 * unit), fill=accent)
    draw.rounded_rectangle(coords((5, 4, 27, 28)), radius=round(3 * unit), fill=graphite)
    width = max(1, round(2.5 * unit))
    draw.line(coords((11, 9, 11, 23)), fill=accent, width=width)
    draw.line(coords((11, 16, 15, 16)), fill=accent, width=width)
    draw.line(coords((22, 9, 15, 16, 22, 23)), fill=accent, width=width, joint="curve")
    # Маска контура и буквы отделяется от графитовой внутренней поверхности.
    pixels = img.load()
    for x in range(img.width):
        t = min(1, max(0, (x / unit - 3) / 26))
        color = tuple(round(a + (b - a) * t) for a, b in zip((250, 45, 76), (191, 47, 223)))
        for y in range(img.height):
            if pixels[x, y][:3] == (255, 255, 255):
                pixels[x, y] = color + (255,)
    return img.resize((size, size), Image.Resampling.LANCZOS)


if __name__ == "__main__":
    frames = [render(n) for n in SIZES]
    frames[-1].save(OUT, format="ICO", sizes=[(n, n) for n in SIZES], append_images=frames[:-1])
    print("Создан", OUT)
