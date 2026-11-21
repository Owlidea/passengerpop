# Metro Sort oyun içi panel görselleri (booster çubuğu ve sıradaki trenler)

Her iki görseli de önceki setlerdeki gibi üret: `key-art.png` stil referansı olarak eklensin. Çıktıları
`MetroSort-Media/Gorseller/` klasörüne aşağıdaki adlarla koy; oyuna aktarmayı ben yaparım (gri zemin otomatik
silinir, her figür ayrı sprite olarak kesilir).

Önemli: zemin düz açık gri olsun, figürler eşit aralıklı ve birbirine değmesin, yazı olmasın.

## 1. boosters.png: dört booster ikonu, tek pafta

Şimdiki ikonların en zayıfı "+Peron" (gri kutu ne olduğunu anlatmıyor). Dördünü aynı ışık ve aynı boyda üretmek
çubuğu bütünlüklü gösterir.

```text
Use the attached Metro Sort key art as a style reference for materials, color treatment, and lighting. Landscape sprite sheet with exactly four game booster icons in one row, equally spaced, same size, same front three-quarter angle, each centered in its quarter with ample margins, on a plain flat light grey background, no text, no frames, no buttons behind them. Left to right: (1) UNDO: a chunky glossy sky-blue curved arrow bending back to the left, thick and rounded like a toy, soft white highlight; (2) HINT: a glowing golden light bulb with a warm inner glow and three small sparkle stars around it; (3) EXTRA PLATFORM: a small cream-white metro station platform slab with a bright yellow safety stripe along its front edge and a big glossy green circular badge with a white plus sign on its front corner, clearly a platform piece seen from slightly above; (4) SHUFFLE: a glossy violet sphere with two thick white circular arrows chasing each other around it. Every icon reads clearly at very small size: bold silhouette, few details, strong contrast.
Style: premium casual mobile game art, stylized 3D render look, soft rounded chunky shapes, glossy clay-like materials, vibrant saturated colors, warm soft global illumination, gentle rim light, subtle ambient occlusion, clean smooth gradients, no outlines, no photorealism, no text unless specified, high detail, polished and friendly.
```

## 2. train-icons.png: altı renk mini tren

"Sıradaki trenler" şeridinde her hattın treni gösteriliyor. Altısı birebir aynı model olmalı, yalnızca rengi
değişmeli; sıra oyundaki hat sırasıdır.

```text
Use the attached Metro Sort key art as a style reference for materials, color treatment, and lighting. Landscape sprite sheet with exactly six identical cute toy metro train cars in one row, equally spaced, same size, same front three-quarter angle facing left, each centered in its sixth with clear gaps between them, on a plain flat light grey background, no text, no rails, no ground. One short rounded train car each: big front window with a soft reflection, two round headlights, a thin white stripe along the side, small dark wheels. Body colours left to right: tomato red, sky blue, fresh green, sunny yellow, violet purple, tangerine orange; all other parts identical. Bold simple silhouette that still reads at 80 pixels wide.
Style: premium casual mobile game art, stylized 3D render look, soft rounded chunky shapes, glossy clay-like materials, vibrant saturated colors, warm soft global illumination, gentle rim light, subtle ambient occlusion, clean smooth gradients, no outlines, no photorealism, no text unless specified, high detail, polished and friendly.
```

## Oyuna aktarma

İki dosya da `Gorseller/` klasörüne konunca:

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background --python Tools/art/reward_states.py
```

Betik bu paftaları da keser: `booster_undo`, `booster_hint`, `booster_platform`, `booster_shuffle` ve
`train_icon_0` … `train_icon_5`. Görseller gelene kadar oyun mevcut ikonları kullanmaya devam eder.
