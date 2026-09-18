"""
Нарезка спрайт-листов кота на отдельные кадры одинакового размера.

Что делает:
  1. Чистит почти прозрачный мусор (красная/жёлтая кайма после удаления фона).
  2. Находит на листе отдельные "острова" непрозрачных пикселей (кот, звёздочки, подписи).
  3. Подписи выкидывает, мелкие эффекты (звёзды, шлейфы) приклеивает к ближайшему коту.
  4. Приводит все листы к одному масштабу и кладёт каждый кадр на одинаковый холст,
     лапами на одну линию — чтобы в анимации кот не дёргался.

Запуск (из корня проекта), в конце — имя кота:
  python Tools/slice_sprites.py murzik
  python Tools/slice_sprites.py belchik
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ART_DIR = Path(__file__).resolve().parent.parent / "Assets" / "Art"

CANVAS_W, CANVAS_H = 640, 512   # одинаковый холст для всех кадров
CAT_HEIGHT = 400                # рост кота в стойке (в пикселях) после масштабирования
FLOOR_MARGIN = 16               # лапы стоят на 16 px выше нижнего края холста

# Настройки по котам. Для каждого листа: имя анимации, ожидаемое число кадров,
# полосы с подписями (y_от, y_до), ручные "разрезы" для слипшихся кадров
# и flip=True, если кот на листе смотрит влево (все кадры приводим к "лицом вправо").
# Для общих листов, где оба кота на одной картинке:
#   path       — полный путь к файлу (если лист лежит не в папке кота),
#   pick       — какой кадр по счёту взять (0 — левый кот, 1 — правый),
#   scale_like — взять масштаб у другой анимации этого кота (присед низкий — по нему рост не посчитать).
BLOCK_SHEET = r"C:\Users\clover\Desktop\block.png"
CROUCH_SHEET = r"C:\Users\clover\Desktop\prigibanie.png"
CHARACTERS = {
    "murzik": dict(
        src=Path(r"C:\Users\clover\Desktop\cats\MURZIK\murz"),
        out="Murzik",
        sheets=[
            dict(file="image 1.png", anim="idle", frames=4, label_bands=[(835, 910)]),
            dict(file="2.png", anim="punch", frames=8, label_bands=[(445, 512), (932, 1000)]),
            dict(file="3.png", anim="kick", frames=8, label_bands=[(386, 446), (810, 875)],
                 wipe=[(0, 391, 1774, 446)]),           # подпись KICK_04 слиплась с KICK_08 — стираем полосу целиком
            dict(file="4.png", anim="hit", frames=8, label_bands=[(405, 485), (790, 870)],
                 split=[(11, 540, 901, 800)],            # HIT_05 и HIT_06 слиплись — разделяем
                 wipe=[(1300, 400, 1774, 484)]),         # подпись HIT_04 слиплась с HIT_08
            dict(file="win.png", anim="win", frames=4, label_bands=[], optional=True),  # победа — когда будет нарисована
            dict(path=BLOCK_SHEET, anim="block", frames=2, pick=0, label_bands=[]),
            dict(path=CROUCH_SHEET, anim="crouch", frames=2, pick=0, label_bands=[], scale_like="block"),
        ],
    ),
    "belchik": dict(
        src=Path(r"C:\Users\clover\Desktop\белчик"),
        out="Belchik",
        sheets=[
            dict(file="1.png", anim="idle", frames=4, label_bands=[(808, 925)], flip=True),  # стойка смотрит влево
            dict(file="2.png", anim="punch", frames=8, label_bands=[]),
            dict(file="3.png", anim="kick", frames=8, label_bands=[]),
            dict(file="4.png", anim="hit", frames=8, label_bands=[]),
            dict(file="win.png", anim="win", frames=4, label_bands=[], optional=True),
            dict(path=BLOCK_SHEET, anim="block", frames=2, pick=1, label_bands=[], flip=True),
            dict(path=CROUCH_SHEET, anim="crouch", frames=2, pick=1, label_bands=[], flip=True, scale_like="block"),
        ],
    ),
}


def clean_alpha(a):
    """Пиксели с alpha < 10 — невидимый мусор, обнуляем."""
    a = a.copy()
    a[a[..., 3] < 10] = 0
    return a


def split_merged(a, lab, n, x0, y0, x1, y1):
    """Разделяет двух слипшихся котов в прямоугольнике (x0,y0)-(x1,y1).
    Идея: "сжимаем" силуэт (эрозия), пока он не распадётся на 2 больших куска-зерна,
    затем каждый пиксель отдаём ближайшему зерну."""
    region = (slice(y0, y1), slice(x0, x1))
    target = np.bincount(lab[region].ravel())[1:].argmax() + 1   # самый большой объект в зоне
    obj = lab == target
    solid = obj & (a[..., 3] >= 128)
    for it in range(1, 60):
        seeds, k = ndi.label(ndi.binary_erosion(solid, iterations=it))
        sizes = np.bincount(seeds.ravel())[1:]
        big = np.nonzero(sizes > 5000)[0] + 1
        if len(big) >= 2:
            break
    else:
        raise SystemExit("не удалось разделить слипшиеся кадры")
    seeds = np.where(np.isin(seeds, big[:2]), seeds, 0)
    # Для каждого пикселя — индексы ближайшего пикселя-зерна
    _, (iy, ix) = ndi.distance_transform_edt(seeds == 0, return_indices=True)
    nearest = seeds[iy, ix]
    lab[obj & (nearest == big[1])] = n + 1
    return n + 1


def find_frames(img, cfg):
    a = img
    for (x0, y0, x1, y1) in cfg.get("wipe", []):
        a[y0:y1, x0:x1] = 0

    mask = ndi.binary_dilation(a[..., 3] >= 24, iterations=4)
    lab, n = ndi.label(mask)
    for (x0, y0, x1, y1) in cfg.get("split", []):
        n = split_merged(a, lab, n, x0, y0, x1, y1)
    boxes = ndi.find_objects(lab)

    cats, smalls = [], []
    for i, s in enumerate(boxes, start=1):
        h, w = s[0].stop - s[0].start, s[1].stop - s[1].start
        if h * w > 60_000:
            cats.append({"ids": [i], "y0": s[0].start, "y1": s[0].stop, "x0": s[1].start, "x1": s[1].stop})
        else:
            smalls.append((i, s))

    for i, s in smalls:
        y0, y1, x0, x1 = s[0].start, s[0].stop, s[1].start, s[1].stop
        # Подпись = маленький объект, целиком лежащий в полосе подписей
        if any(b0 <= y0 and y1 <= b1 for b0, b1 in cfg["label_bands"]):
            continue
        # Иначе — эффект (звезда, шлейф): приклеиваем к ближайшему коту, если он рядом
        cy, cx = (y0 + y1) / 2, (x0 + x1) / 2
        def dist(c):
            dx = max(c["x0"] - cx, 0, cx - c["x1"])
            dy = max(c["y0"] - cy, 0, cy - c["y1"])
            return (dx * dx + dy * dy) ** 0.5
        best = min(cats, key=dist)
        # Всё, что целиком ниже лап кота, — номер кадра/подпись, а не эффект
        if y0 >= best["y1"] - 8 or y1 > best["y1"] + 2:
            continue
        if dist(best) < 90:
            best["ids"].append(i)

    if len(cats) != cfg["frames"]:
        raise SystemExit(f"{cfg['file']}: нашли {len(cats)} кадров, ожидали {cfg['frames']}")

    # Порядок кадров: по строкам сверху вниз, внутри строки слева направо
    cats.sort(key=lambda c: c["y1"])
    rows, row = [], [cats[0]]
    for c in cats[1:]:
        if abs(c["y1"] - row[0]["y1"]) < 150:
            row.append(c)
        else:
            rows.append(row)
            row = [c]
    rows.append(row)
    ordered = [c for r in rows for c in sorted(r, key=lambda c: c["x0"])]

    frames = []
    for c in ordered:
        keep = np.isin(lab, c["ids"]) & (a[..., 3] > 0)
        frame = np.where(keep[..., None], a, 0).astype(np.uint8)
        ys, xs = np.nonzero(frame[..., 3])
        frames.append(frame[ys.min():ys.max() + 1, xs.min():xs.max() + 1])
    return frames


def anchor_x(frame):
    """Горизонтальная опора кадра — центр масс плотных пикселей тела.
    Полупрозрачные шлейфы удара не учитываются, поэтому вытянутая лапа почти не сдвигает кота."""
    solid = frame[..., 3] >= 250
    xs = np.nonzero(solid)[1]
    return float(xs.mean())


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in CHARACTERS:
        raise SystemExit(f"Укажи кота: {', '.join(CHARACTERS)}")
    prefix = sys.argv[1]
    char = CHARACTERS[prefix]
    out = ART_DIR / char["out"]
    out.mkdir(parents=True, exist_ok=True)
    report = []
    scales = {}  # масштаб каждой анимации — чтобы другие листы могли его "одолжить" (scale_like)
    for cfg in char["sheets"]:
        path = Path(cfg["path"]) if "path" in cfg else char["src"] / cfg["file"]
        if cfg.get("optional") and not path.exists():
            report.append(f"(пропуск: нет файла {path.name})")
            continue
        img = clean_alpha(np.array(Image.open(path).convert("RGBA")))
        frames = find_frames(img, cfg)
        if "pick" in cfg:
            frames = [frames[cfg["pick"]]]  # с общего листа берём только своего кота
        if cfg.get("flip"):
            frames = [np.ascontiguousarray(f[:, ::-1]) for f in frames]  # зеркалим каждый кадр по горизонтали
        # Масштаб листа: по первому кадру (обычная стойка) приводим кота к CAT_HEIGHT,
        # либо берём готовый масштаб у другой анимации
        if "scale_like" in cfg:
            scale = scales[cfg["scale_like"]]
        else:
            scale = CAT_HEIGHT / frames[0].shape[0]
        scales[cfg["anim"]] = scale
        for idx, f in enumerate(frames, start=1):
            pil = Image.fromarray(f)
            pil = pil.resize((max(1, round(pil.width * scale)), max(1, round(pil.height * scale))), Image.LANCZOS)
            f2 = np.array(pil)
            ax = anchor_x(f2)
            canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
            x = round(CANVAS_W / 2 - ax)
            y = CANVAS_H - FLOOR_MARGIN - pil.height
            canvas.alpha_composite(pil, (max(x, 0), max(y, 0)))
            clipped = x < 0 or y < 0 or x + pil.width > CANVAS_W
            name = f"{prefix}_{cfg['anim']}_{idx:02d}.png"
            canvas.save(out / name, optimize=True)
            report.append(f"{name}: {pil.width}x{pil.height}  x={x} y={y}{'  !! ОБРЕЗАН' if clipped else ''}")
    print("\n".join(report))
    print(f"\nГотово -> {out}")


if __name__ == "__main__":
    main()
