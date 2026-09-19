"""Подготовка картинок интерфейса: уменьшение до размера на экране.

Берёт мастер-копии из SourceArt/UI/Game (уже обрезанные, в полном разрешении)
и кладёт в Assets/Art/UI уменьшенные версии.

Зачем: картинка, которая на экране 54 px, а в файле 790 px, занимает в сборке
в ~200 раз больше памяти, чем нужно. Больше ~2x размера на экране (при 1080p)
глаз никогда не увидит — даже с mip-картами.

Обе стороны результата кратны 4 (холст дополняется прозрачными пикселями по центру):
GPU-форматы сжатия (DXT, ASTC) работают блоками 4x4, иначе Unity оставит картинку несжатой.

Запуск:  python Tools/prepare_ui.py
Потом Unity сама переимпортирует изменённые файлы (ссылки не ломаются — .meta не трогаем).
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "SourceArt" / "UI" / "Game"
DST = ROOT / "Assets" / "Art" / "UI"

# Файл (путь внутри UI) -> максимальный размер по длинной стороне.
# None — не уменьшать (только дополнить до кратности 4).
TARGETS = {
    "paw_empty.png": 128,        # на экране 54-66 px
    "paw_filled.png": 128,
    "Portraits/belchik_portrait_hit.png": 256,      # ~150 px
    "Portraits/belchik_portrait_neutral.png": 256,
    "Portraits/murzik_portrait_hit.png": 256,
    "Portraits/murzik_portrait_neutral.png": 256,
    "portrait_frame.png": 512,   # 214-460 px (экран выбора). Рамка и маска — строго одинаково!
    "portrait_mask.png": 512,
    "timer_splash.png": 512,     # 300 px
    "name_murzik.png": 768,      # 330-760 px
    "name_belchik.png": 768,
    "End/wins_murzik.png": 768,
    "End/wins_belchik.png": 768,
    # Крупное (~1000 px на экране) — размер оставляем
    "healthbar_frame.png": None,
    "healthbar_fill.png": None,
    "fight_logo.png": None,
    "ko_logo.png": None,
    "round_1.png": None,
    "round_2.png": None,
    "round_3.png": None,
    "End/title_win.png": None,
    "End/title_lose.png": None,
    "End/button_retry.png": None,
}


def round_up4(n: int) -> int:
    return (n + 3) // 4 * 4


def prepare(img: Image.Image, max_side) -> Image.Image:
    img = img.convert("RGBA")
    if max_side is not None and max(img.size) > max_side:
        k = max_side / max(img.size)
        size = (max(1, round(img.width * k)), max(1, round(img.height * k)))
        # LANCZOS — самый качественный фильтр уменьшения в Pillow (резко, без "лесенок")
        img = img.resize(size, Image.LANCZOS)
    w, h = round_up4(img.width), round_up4(img.height)
    if (w, h) != img.size:
        canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        canvas.paste(img, ((w - img.width) // 2, (h - img.height) // 2))
        img = canvas
    return img


def prepare_arena():
    # Фон на весь экран — ширину не трогаем, а высоту делаем кратной 4,
    # срезая лишние строки сверху (там потолок; пол внизу остаётся на месте).
    src = Image.open(ROOT / "SourceArt" / "UI" / "Elements" / "arena_background.png").convert("RGB")
    h = src.height // 4 * 4
    out = src.crop((0, src.height - h, src.width, src.height))
    dst = DST / "Arena" / "arena_background.jpg"
    out.save(dst, quality=95)
    print(f"  Arena/arena_background.jpg  {src.size} -> {out.size}")


def main():
    for rel, max_side in TARGETS.items():
        src = SRC / rel
        img = Image.open(src)
        out = prepare(img, max_side)
        dst = DST / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        out.save(dst, optimize=True)
        print(f"  {rel:40s} {img.size} -> {out.size}")
    prepare_arena()


if __name__ == "__main__":
    main()
