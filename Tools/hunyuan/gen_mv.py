"""Multi-view image-to-3D shapes (Passenger Pop passengers; taken from the Crate Pop project) with Hunyuan3D-2mv (shape model only, like gen.py).

Takes the views of one object cut out of an orthographic turnaround (turnaround.py): front, left, back and right,
where "left" shows the object's left side (the object faces the picture's left edge) and "right" its right side.
Any subset of the four works. Hunyuan3D-2mv recentres and rescales each view on its own, so the views only need
clean alpha. Uses the step-distilled turbo multi-view model (5 steps) with the FlashVDM turbo decoder from
tencent/Hunyuan3D-2; the mesh is cleaned (floaters, degenerate faces) and reduced like gen.py, and saved as GLB.

Setup (once, besides gen.py's): ~/.cache/hy3dgen/tencent/Hunyuan3D-2mv/hunyuan3d-dit-v2-mv-turbo/{config.yaml,
model.fp16.safetensors} (4.9 GB), downloaded with huggingface_hub.hf_hub_download(local_dir=...).

Usage:
  PYTORCH_ENABLE_MPS_FALLBACK=1 ~/Hunyuan3D-2/.venv/bin/python Tools/hunyuan/gen_mv.py \
      --views MetroSort-Media/3D/yolcu-hd-1 --out MetroSort-Media/3D/yolcu-hd-1.glb [--octree 380] [--seeds 1234 42]
With several seeds, each shape is written next to --out as <name>_s<seed>.glb.
"""
import argparse
import os
import sys
import time

import torch
from PIL import Image

sys.path.insert(0, os.path.expanduser("~/Hunyuan3D-2"))
from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline  # noqa: E402
from hy3dgen.shapegen.postprocessors import DegenerateFaceRemover, FaceReducer, FloaterRemover  # noqa: E402

VIEWS = ("front", "left", "back", "right")

parser = argparse.ArgumentParser()
parser.add_argument("--views", required=True, help="folder with front.png, left.png, back.png, right.png (RGBA)")
parser.add_argument("--out", required=True, help="output .glb")
parser.add_argument("--use", nargs="*", default=list(VIEWS), help="which views to use")
parser.add_argument("--steps", type=int, default=5)
parser.add_argument("--octree", type=int, default=380)
parser.add_argument("--seeds", type=int, nargs="+", default=[1234])
parser.add_argument("--faces", type=int, default=60000)
args = parser.parse_args()
os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)

images = {}
for view in VIEWS:
    path = os.path.join(args.views, view + ".png")
    if view in args.use and os.path.exists(path):
        images[view] = Image.open(path).convert("RGBA")
if not images:
    raise SystemExit(f"no views found in {args.views}")
print("views: " + ", ".join(f"{k} {v.size[0]}x{v.size[1]}" for k, v in images.items()), flush=True)

device = "mps" if torch.backends.mps.is_available() else "cpu"
t0 = time.time()
pipeline = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
    "tencent/Hunyuan3D-2mv", subfolder="hunyuan3d-dit-v2-mv-turbo", use_safetensors=True, variant="fp16",
    device=device, dtype=torch.float16)
pipeline.enable_flashvdm(mc_algo="mc")
print(f"loaded on {device} in {time.time() - t0:.0f} s", flush=True)

stem, ext = os.path.splitext(args.out)
for seed in args.seeds:
    start = time.time()
    mesh = pipeline(image=dict(images), num_inference_steps=args.steps, octree_resolution=args.octree,
                    num_chunks=20000, generator=torch.manual_seed(seed), output_type="trimesh")[0]
    mesh = FloaterRemover()(mesh)
    mesh = DegenerateFaceRemover()(mesh)
    mesh = FaceReducer()(mesh, max_facenum=args.faces)
    path = args.out if len(args.seeds) == 1 else f"{stem}_s{seed}{ext or '.glb'}"
    mesh.export(path)
    print(f"seed {seed:6d} {time.time() - start:6.1f} s  {len(mesh.faces):6d} faces  -> {path}", flush=True)
    if device == "mps":
        torch.mps.empty_cache()
