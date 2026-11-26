# Passenger Pop – mağaza paket görselleri

Mağazadaki Başlangıç Paketi ve üç paket bu paftadaki kendi çizimlerini kullanıyor (2026-11-26'da aktarıldı):
hediye kutusu, bavul, kondüktör çantası ve jetonlu vagon. Paftayı yeniden üretirsen aynı adla `Gorseller/`
klasörüne koy ve aşağıdaki komutla yeniden kes.

Önceki setlerdeki gibi üret: `key-art.png` stil referansı olarak eklensin. Çıktıyı
`MetroSort-Media/Gorseller/shop-packs.png` adıyla koy; oyuna aktarmayı ben yaparım (gri zemin silinir, her figür
ayrı sprite olarak kesilir).

Önemli: zemin düz açık gri olsun, figürler eşit aralıklı ve birbirine değmesin, yazı olmasın.

## shop-packs.png: dört paket, tek pafta

Soldan sağa büyüdükçe zenginleşen dört paket: Başlangıç (mor hediye kutusu), Yolcu (küçük bavul), Kondüktör
(kondüktör çantası), İstasyon Şefi (jetonla dolu altın vagon).

```text
Use the attached Passenger Pop key art as a style reference for materials, color treatment, and lighting. Landscape sprite sheet with exactly four shop offer illustrations in one row, equally spaced, same front three-quarter angle, each centered in its quarter with ample margins and clear gaps between them, on a plain flat light grey background, no text, no frames, no shadows on the ground. Each one a bit richer than the one before, left to right: (1) STARTER PACK: a cheerful violet gift box with a big glossy golden bow, the lid lifted a little with gold coins and soft sparkles peeking out, a small cream train ticket tag hanging from the bow; (2) TRAVELLER BUNDLE: a small rounded tan leather travel suitcase standing open, a neat pile of gold coins inside and a few coins spilling over the edge; (3) CONDUCTOR BUNDLE: a navy blue conductor's satchel with a shiny gold buckle and a small navy conductor cap resting on top, overflowing with gold coins; (4) STATION MASTER BUNDLE: a chunky open-top golden toy train wagon on two tiny rail pieces, heaped high with gold coins and a few sparkling gems, the biggest and most dazzling of the four. Coins everywhere are the same round gold coins with a raised star, as in the key art. Bold simple silhouettes that still read at 180 pixels wide.
Style: premium casual mobile game art, stylized 3D render look, soft rounded chunky shapes, glossy clay-like materials, vibrant saturated colors, warm soft global illumination, gentle rim light, subtle ambient occlusion, clean smooth gradients, no outlines, no photorealism, no text unless specified, high detail, polished and friendly.
```

## Oyuna aktarma

`Tools/art/reward_states.py` paftayı figürlerin arasındaki en geniş boşluklardan dörde böler ve
`Resources/Art` içine `pack_starter`, `pack_traveller`, `pack_conductor` ve `pack_station` olarak yazar:

```bash
Blender --background --python Tools/art/reward_states.py
```

Gri zemin silinir; kondüktör çantasının kayışı içinde ve vagonun rayları arasında kalan zemin parçaları da.
Çantanın ve vagonun parlamaları jetonlara değdiği için zemin rengine karışmış gri bir hâle bırakıyordu; bu ikisinde
parlamalar saydam ışığa çevrilir, böylece koyu kartlarda kirli leke görünmez. Mağaza kartları görseli ürün
kimliğine göre seçer (`UIManager.PackArt`).
