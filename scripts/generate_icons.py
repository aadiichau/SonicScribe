"""Generate WinUI asset files from a source app icon PNG."""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from PIL import Image


ICO_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def resize_square(img: Image.Image, size: int) -> Image.Image:
    return img.resize((size, size), Image.Resampling.LANCZOS)


def save_png(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG", optimize=True)


def save_ico(img: Image.Image, path: Path) -> None:
    """Write a multi-size ICO with PNG-compressed entries for crisp Windows icons."""
    path.parent.mkdir(parents=True, exist_ok=True)
    entries: list[tuple[int, bytes]] = []

    for size in ICO_SIZES:
        frame = resize_square(img, size)
        buffer = _encode_png(frame)
        entries.append((size, buffer))

    with path.open("wb") as handle:
        handle.write(struct.pack("<HHH", 0, 1, len(entries)))
        offset = 6 + (16 * len(entries))

        for size, data in entries:
            width_byte = 0 if size >= 256 else size
            height_byte = width_byte
            handle.write(
                struct.pack(
                    "<BBBBHHII",
                    width_byte,
                    height_byte,
                    0,
                    0,
                    1,
                    32,
                    len(data),
                    offset,
                )
            )
            offset += len(data)

        for _, data in entries:
            handle.write(data)


def _encode_png(img: Image.Image) -> bytes:
    from io import BytesIO

    buffer = BytesIO()
    img.save(buffer, format="PNG", optimize=True)
    return buffer.getvalue()


def main() -> int:
    if len(sys.argv) < 3:
        print("Usage: generate_icons.py <source.png> <assets_dir>")
        return 1

    source = Path(sys.argv[1]).resolve()
    assets_dir = Path(sys.argv[2]).resolve()

    if not source.is_file():
        print(f"Source not found: {source}")
        return 1

    with Image.open(source) as raw:
        img = raw.convert("RGBA")

    save_png(img, assets_dir / "AppIcon.png")
    save_ico(img, assets_dir / "AppIcon.ico")

    # In-app UI assets at high resolution for HiDPI displays.
    save_png(resize_square(img, 256), assets_dir / "StoreLogo.png")
    save_png(resize_square(img, 128), assets_dir / "TitleBarIcon.png")

    # Packaging / tile assets.
    save_png(resize_square(img, 88), assets_dir / "Square44x44Logo.scale-200.png")
    save_png(resize_square(img, 24), assets_dir / "Square44x44Logo.targetsize-24_altform-unplated.png")
    save_png(resize_square(img, 48), assets_dir / "Square44x44Logo.targetsize-48_altform-lightunplated.png")
    save_png(resize_square(img, 300), assets_dir / "Square150x150Logo.scale-200.png")
    save_png(resize_square(img, 620), assets_dir / "Wide310x150Logo.scale-200.png")
    save_png(resize_square(img, 620), assets_dir / "SplashScreen.scale-200.png")
    save_png(resize_square(img, 48), assets_dir / "LockScreenLogo.scale-200.png")

    ico_size = (assets_dir / "AppIcon.ico").stat().st_size
    print(f"Generated assets in {assets_dir} (AppIcon.ico: {ico_size:,} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())