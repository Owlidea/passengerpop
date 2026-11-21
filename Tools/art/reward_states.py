"""Cuts the sprite sheets the game shows on its dark panels, with media_extra.py's own helpers but without rerunning
the rest of that script (it also rewrites station textures and chapter pictures):

    Blender --background --python Tools/art/reward_states.py

- chest-states.png, gift-states.png -> Art/chest_0..2, gift_0..2 (the reward box opening)
- boosters.png                      -> Art/booster_undo, booster_hint, booster_platform, booster_shuffle
- train-icons.png                   -> Art/train_icon_0..5 (the next-trains strip, in Palette.Lines order)

Sheets that are not in MetroSort-Media/Gorseller yet are skipped. Prompts: MetroSort-Media/HUD-Gorsel-Promptlari.md.
"""
import ast, os

SCRIPT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "media_extra.py")
tree = ast.parse(open(SCRIPT).read())
# Keep imports, path constants and functions; skip the module-level import steps.
keep = [n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom, ast.FunctionDef, ast.Assign))]
helpers = {"__file__": SCRIPT}
exec(compile(ast.Module(body=keep, type_ignores=[]), SCRIPT, "exec"), helpers)

# (sheet, sprites, size, bright, clean). The boxes stand in glowing light on a lit floor, hence the high `bright`
# and the halo cleanup. The icons have large white and cream areas (the platform's tiles, the shuffle arrows, the
# headlights) that a high `bright` or the cleanup would eat: they keep the default matte, which only takes the
# backdrop grey and the shadow on it.
SHEETS = [
    ("chest-states", ("chest_0", "chest_1", "chest_2"), 512, 90, True),
    ("gift-states", ("gift_0", "gift_1", "gift_2"), 512, 90, True),
    ("boosters", ("booster_undo", "booster_hint", "booster_platform", "booster_shuffle"), 256, 9, False),
    ("train-icons", tuple("train_icon_%d" % i for i in range(6)), 256, 9, False),
]
for name, names, size, bright, clean in SHEETS:
    if not os.path.exists(os.path.join(helpers["SRC"], name + ".png")):
        print("skipped", name, "(not in Gorseller yet)")
        continue
    helpers["cells"](name, names, size, bright=bright, clean=clean)
