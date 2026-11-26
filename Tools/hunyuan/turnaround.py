"""Splits an orthographic turnaround sheet (several views of one object in a row on a plain backdrop) into its views
and cuts each one out into RGBA for Hunyuan3D-2mv and paint_mv.py.

The views are found as runs of picture columns that hold object pixels, separated by backdrop-only gaps; the sheet
is cut at the middle of each gap. Each view is then cut out exactly like cutout.py does (backdrop = median border
colour; backdrop pixels connected to the border are removed, holes inside the object stay solid, specks are dropped,
the edge is feathered by one pixel). The views keep the sheet's pixel scale and ground line, so they can be compared
with one another. For each view a "_filled" RGB copy is written too (every pixel outside the object takes the colour
of the nearest solid object pixel, like refprep.py), so colour samples at the silhouette never pick up backdrop.

Usage: ~/Hunyuan3D-2/.venv/bin/python Tools/hunyuan/turnaround.py <sheet.png> <out dir> [--names front left back right]
Writes <out>/<name>.png (RGBA cutout), <out>/<name>_filled.png (RGB) and <out>/views.json (crop boxes, alpha
bounding boxes in sheet pixels).
"""
import argparse
import json
import os

import numpy as np
from PIL import Image
from scipy import ndimage


def cut_out(rgb):
    """cutout.py's backdrop removal for one picture (float32 RGB, 0..255). Returns (object mask, feathered alpha)."""
    h, w, _ = rgb.shape
    border = np.concatenate([rgb[:4].reshape(-1, 3), rgb[-4:].reshape(-1, 3), rgb[:, :4].reshape(-1, 3), rgb[:, -4:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    diff = np.abs(rgb - bg).max(-1)
    lum = rgb.mean(-1)
    sat = (rgb.max(-1) - rgb.min(-1)) / np.maximum(rgb.max(-1), 1.0)
    bg_lum = float(bg.mean())
    candidate = diff < 14
    if bg_lum < 245:
        candidate |= (sat < 0.16) & (lum < bg_lum + 6) & (lum > bg_lum - 120)
    labels, _ = ndimage.label(candidate)
    edge_labels = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    background = np.isin(labels, edge_labels[edge_labels > 0])
    obj = ndimage.binary_fill_holes(~background)
    lab, n = ndimage.label(obj)
    if n > 1:
        sizes = ndimage.sum(obj, lab, range(1, n + 1))
        obj = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 0.002 * h * w])
    alpha = ndimage.gaussian_filter(obj.astype(np.float32), 0.8)
    return obj, alpha, bg


def split_by_regions(rgb, args, margin=12):
    """Fallback split for sheets whose views overlap in x: the sheet's object mask is cut into connected regions,
    the largest len(names) regions become the views (left to right), and smaller ones (a loose strand, a glasses
    arm) join the view whose region is nearest. Each crop keeps the sheet's full height and pixel scale; pixels of
    other views inside it are replaced by the backdrop before the usual cut-out."""
    h, w, _ = rgb.shape
    obj, _, bg = cut_out(rgb)
    labels, n = ndimage.label(obj)
    sizes = ndimage.sum(obj, labels, range(1, n + 1))
    order = np.argsort(sizes)[::-1]
    count = len(args.names)
    if n < count:
        raise SystemExit(f"found {n} object regions, expected {count}")
    main_ids = sorted((int(i) + 1 for i in order[:count]),
                      key=lambda i: ndimage.center_of_mass(labels == i)[1])
    owner = np.zeros(n + 1, dtype=int)
    for k, i in enumerate(main_ids):
        owner[i] = k + 1
    dist = [ndimage.distance_transform_edt(labels != i) for i in main_ids]
    for i in range(1, n + 1):
        if owner[i] == 0:
            ys, xs = np.nonzero(labels == i)
            owner[i] = int(np.argmin([d[ys, xs].min() for d in dist])) + 1
    view_of = owner[labels]                               # 0 = backdrop, k = view k (1-based)
    info = {"sheet": os.path.relpath(args.sheet), "size": [w, h], "views": {}}
    for k, name in enumerate(args.names, start=1):
        mine = view_of == k
        ys, xs = np.nonzero(mine)
        left, right = max(0, int(xs.min()) - margin), min(w, int(xs.max()) + margin + 1)
        crop = rgb[:, left:right].copy()
        others = (view_of[:, left:right] != 0) & ~mine[:, left:right]
        crop[others] = bg
        sub_obj, alpha, _ = cut_out(crop)
        out = np.dstack([crop, alpha * 255.0]).clip(0, 255).astype(np.uint8)
        Image.fromarray(out, "RGBA").save(os.path.join(args.out, name + ".png"))
        solid = alpha > 0.94
        _, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
        Image.fromarray(crop[iy, ix].clip(0, 255).astype(np.uint8), "RGB").save(os.path.join(args.out, name + "_filled.png"))
        oy, ox = np.nonzero(sub_obj)
        box = [int(ox.min()) + left, int(oy.min()), int(ox.max()) + left, int(oy.max())]
        info["views"][name] = {"crop": [left, 0, right, h], "alpha_box": box,
                               "width": box[2] - box[0] + 1, "height": box[3] - box[1] + 1}
        print(f"{name:6s} crop x {left}-{right}  object x {box[0]}-{box[2]} y {box[1]}-{box[3]}  "
              f"({box[2] - box[0] + 1} x {box[3] - box[1] + 1} px)")
    json.dump(info, open(os.path.join(args.out, "views.json"), "w"), indent=1)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("sheet")
    parser.add_argument("out")
    parser.add_argument("--names", nargs="+", default=["front", "left", "back", "right"],
                        help="view names from left to right on the sheet")
    parser.add_argument("--min-gap", type=int, default=8, help="backdrop columns needed between two views")
    args = parser.parse_args()
    os.makedirs(args.out, exist_ok=True)

    rgb = np.asarray(Image.open(args.sheet).convert("RGB")).astype(np.float32)
    h, w, _ = rgb.shape
    border = np.concatenate([rgb[:4].reshape(-1, 3), rgb[-4:].reshape(-1, 3), rgb[:, :4].reshape(-1, 3), rgb[:, -4:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    busy = (np.abs(rgb - bg).max(-1) > 14).sum(0) > 2
    runs, start = [], None
    for x in range(w):
        if busy[x] and start is None:
            start = x
        elif not busy[x] and start is not None:
            runs.append([start, x - 1])
            start = None
    if start is not None:
        runs.append([start, w - 1])
    # Merge runs split by narrow gaps (thin parts of one view).
    merged = [runs[0]]
    for r in runs[1:]:
        if r[0] - merged[-1][1] - 1 < args.min_gap:
            merged[-1][1] = r[1]
        else:
            merged.append(r)
    if len(merged) != len(args.names):
        # Views whose column ranges overlap (a hand reaching past the next figure's hair): split by connected
        # object regions instead, then blank every other view's pixels inside each crop.
        print(f"found {len(merged)} column runs {merged}, splitting by object regions instead")
        split_by_regions(rgb, args)
        return

    info = {"sheet": os.path.relpath(args.sheet), "size": [w, h], "views": {}}
    for k, (name, (a, b)) in enumerate(zip(args.names, merged)):
        left = 0 if k == 0 else (merged[k - 1][1] + a) // 2
        right = w if k == len(merged) - 1 else (b + merged[k + 1][0]) // 2 + 1
        crop = rgb[:, left:right]
        obj, alpha, _ = cut_out(crop)
        out = np.dstack([crop, alpha * 255.0]).clip(0, 255).astype(np.uint8)
        Image.fromarray(out, "RGBA").save(os.path.join(args.out, name + ".png"))
        solid = alpha > 0.94
        _, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
        Image.fromarray(crop[iy, ix].clip(0, 255).astype(np.uint8), "RGB").save(os.path.join(args.out, name + "_filled.png"))
        ys, xs = np.nonzero(obj)
        box = [int(xs.min()) + left, int(ys.min()), int(xs.max()) + left, int(ys.max())]
        info["views"][name] = {"crop": [left, 0, right, h], "alpha_box": box,
                               "width": box[2] - box[0] + 1, "height": box[3] - box[1] + 1}
        print(f"{name:6s} crop x {left}-{right}  object x {box[0]}-{box[2]} y {box[1]}-{box[3]}  "
              f"({box[2] - box[0] + 1} x {box[3] - box[1] + 1} px)")
    json.dump(info, open(os.path.join(args.out, "views.json"), "w"), indent=1)


main()
