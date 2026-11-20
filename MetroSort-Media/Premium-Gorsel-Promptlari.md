# Metro Sort – Premium için eksik görseller

Hepsini `key-art.png`'yi stil referansı olarak ekleyip üret ve `MetroSort-Media/Gorseller/` klasörüne **aşağıdaki dosya adlarıyla** koy. Gerisini (kesme, oyuna bağlama) ben yaparım.

Ortak stil satırı (her promptun sonuna ekli):

```text
Style: premium casual mobile game art, stylized 3D render look, soft rounded chunky shapes, glossy clay-like materials, vibrant saturated colors, warm soft global illumination, gentle rim light, subtle ambient occlusion, clean smooth gradients, no outlines, no photorealism, no text unless specified, high detail, polished and friendly.
```

---

## 1. Eksik 20 hattın istasyon resmi (en önemli)

35 hattın yalnızca 15'inin resmi var; kalan 20 hat başkalarının resmini tekrar kullanıyor (ör. "Gece Hattı" gündüz bahçesi gösteriyor). Bu resimler hem oyundaki arka duvara hem seviyeler ekranındaki hat afişine gidiyor.

Hepsi için ortak başlangıç (yalnızca **Theme** cümlesi değişir):

```text
Use the attached Metro Sort key art as a style reference for materials, color treatment, and lighting. Empty metro station interior for a mobile puzzle game background, landscape 3:2, top-down three-quarter view, no characters, no train, wide clear uninterrupted floor area in the lower two thirds, platform edge and tracks in the upper third. Keep the lower play area visually quiet. Theme: <TEMA>
```

| Dosya | Hat | Theme cümlesi |
|---|---|---|
| `misty.png` | Sisli Hat | Misty Line. Soft lavender and grey-blue tiles, gentle morning fog drifting along the tracks, frosted glass lamps glowing through mist, calm and dreamy. |
| `night.png` | Gece Hattı | Night Line. Deep navy tiled walls, glass ceiling showing a starry night sky and a big crescent moon, warm amber lamps, soft neon blue trim, cozy nighttime mood. |
| `grand.png` | Büyük Hat | Grand Line. Grand central hall with tall marble columns, huge arched windows, brass chandeliers, cream and gold palette, majestic but friendly. |
| `polar.png` | Kutup Hattı | Polar Line. Icy white and pale blue tiles, snow drifting through skylights, icicles on the arches, little penguin statues, cool sparkling light. |
| `capital.png` | Başkent Hattı | Capital Line. Stately civic station, navy and red banners, polished granite, clock tower motif, bronze railings, elegant city feel. |
| `coastal.png` | Sahil Hattı | Coastal Line. Seaside station with white and turquoise tiles, round porthole windows showing the sea, rope and lifebuoy details, seagull silhouettes. |
| `desert.png` | Çöl Hattı | Desert Line. Sandstone arches, terracotta tiles, potted cacti, warm sunset light through carved lattice windows, golden sand tones. |
| `alpine.png` | Dağ Hattı | Alpine Line. Wooden chalet beams, stone walls, windows showing snowy mountain peaks, red geranium flower boxes, crisp fresh light. |
| `tropic.png` | Tropik Hattı | Tropic Line. Lush jungle station, big palm leaves, bamboo trim, colorful tropical flowers, a small waterfall feature, bright humid light. |
| `royal.png` | Kraliyet Hattı | Royal Line. Purple velvet drapes, gold crowns and crests, red carpet along the platform, crystal chandeliers, regal and playful. |
| `lakeside.png` | Göl Kenarı Hattı | Lakeside Line. Calm lake visible through wide windows, wooden boardwalk platform, reeds and water lilies, soft reflections, gentle green and blue. |
| `canyon.png` | Kanyon Hattı | Canyon Line. Red rock canyon walls carved into the station, layered orange stone, rope lights, cactus pots, warm adventurous mood. |
| `island.png` | Ada Hattı | Island Line. Bright beach station, sand-colored tiles, palm trees, view of a small island and lighthouse through big windows, sunny and cheerful. |
| `aurora.png` | Aurora Hattı | Aurora Line. Dark teal hall under a glass dome showing green and purple northern lights, snowy edges, glowing soft lights, magical mood. |
| `golden.png` | Altın Hat | Golden Line. Shiny gold and cream art-deco station, sunburst patterns, polished brass, warm golden hour light, luxurious. |
| `spring.png` | Bahar Hattı | Spring Line. Pastel pink and mint tiles, cherry blossom trees in planters, falling petals, bright fresh daylight. |
| `summer.png` | Yaz Hattı | Summer Line. Sunny yellow and sky blue tiles, beach umbrellas, ice cream kiosk, sunflowers, bright cheerful summer light. |
| `autumn.png` | Sonbahar Hattı | Autumn Line. Warm orange and brown tiles, maple trees with red leaves, pumpkins and lanterns, falling leaves, cozy golden light. |
| `winter.png` | Kış Hattı | Winter Line. Snowy station with fir trees decorated with lights, wreaths on the arches, soft snowfall through skylights, warm cozy lamps. |
| `festival.png` | Festival Hattı | Festival Line. Colorful bunting flags, balloons, string lights, confetti on the floor, carnival-style stalls along the walls, joyful celebration. |

---

## 2. Ayarlar ikonları

Kare, şeffaf arka planlı, tek nesne ortada; ayarlar satırlarının solunda duracak.

```text
Use the attached Metro Sort key art as a style reference. Single game UI icon, centered, square 1:1, transparent background, no text, chunky glossy 3D cartoon object with a soft drop shadow, readable at small size: <NESNE>
```

| Dosya | NESNE |
|---|---|
| `icon-music.png` | a golden double music note |
| `icon-sound.png` | a blue loudspeaker with sound waves |
| `icon-tap.png` | a cartoon hand with a pointing finger tapping, small sparkle |
| `icon-motion.png` | a soft green leaf with a slow motion swirl |
| `icon-fast.png` | a small red toy metro train with speed lines |
| `icon-language.png` | a blue globe with a speech bubble |

---

## 3. Mağaza

| Dosya | Prompt |
|---|---|
| `no-ads.png` | `Use the attached Metro Sort key art as a style reference for color, clay materials and warm studio lighting. Show only the requested object, with no border, logo, train or surrounding scene. Single game UI icon, centered, square 1:1, transparent background: a glossy red round badge with the exact letters "AD" in white, crossed out by a thick golden diagonal bar, small sparkles. Leave sufficient negative space around the entire badge and keep the AD readable behind the bar. Chunky glossy 3D cartoon object with a soft drop shadow, readable at small size.` |
| `video-coins.png` | `Use the attached Metro Sort key art as a style reference for color, clay materials and warm studio lighting. Show only the requested object, with no border, logo, train or surrounding scene. Single game UI icon, centered, square 1:1, transparent background, no text: a small purple film clapperboard with a green play triangle, golden star-stamped coins spilling out of it. A simple unified silhouette, the green triangle remains clearly visible. Leave sufficient negative space around the whole artwork. Chunky glossy 3D cartoon object with a soft drop shadow, readable at small size.` |
| `coin-bag.png` | `Use the attached Metro Sort key art as a style reference for color, clay materials and warm studio lighting. Show only the requested object, with no border, logo, train or surrounding scene. Single game UI icon, centered, square 1:1, transparent background, no text: a bulging brown cloth sack tied with a gold rope, open at the top and overflowing with shiny star-stamped gold coins. One or two coins near the base. Clearly visible warm brown sack silhouette with soft fabric folds. Leave sufficient negative space around the whole artwork. Chunky glossy 3D cartoon object with a soft drop shadow, readable at small size.` |

`coin-bag.png` gelirse mağazaya üçüncü bir jeton paketi eklerim (Royal Match'teki gibi küçük–orta–büyük sırası).

---

## 4. Seviye haritası zemini

| Dosya | Prompt |
|---|---|
| `map-bg.png` | `Use the attached Metro Sort key art as a style reference. Portrait 9:16 decorative background for a level map screen: a soft, slightly blurred aerial view of a cute toy city at daytime with parks, rivers and tiny buildings, gentle pastel colors, low contrast so white round level markers and colored lines stay readable on top. No text, no characters, no UI.` |

---

**Öncelik:** önce 1. bölüm (20 hat resmi) — oyunda ve seviyeler ekranında en çok görülen eksik bu. Sonra 3. (mağaza), 2. (ayarlar), en son 4. (harita zemini).

---

## 5. Ana menü buton ikonları (Seviyeler, Mağaza, Günlük Hediye)

Kare 1:1, şeffaf arka plan; butonların solunda büyük durur. Üçü aynı ailede görünsün diye aynı başlangıç cümlesini kullan.

| Dosya | Prompt |
|---|---|
| `menu-levels.png` | `Use the attached Metro Sort key art as a style reference. Single game UI icon, centered, square 1:1, transparent background, no text, chunky glossy 3D cartoon object with a soft drop shadow, readable at small size: a folded colorful metro map with three thick colored lines (red, blue, green) and round white station dots, a small golden star pin on top.` |
| `menu-shop.png` | `Use the attached Metro Sort key art as a style reference. Single game UI icon, centered, square 1:1, transparent background, no text, chunky glossy 3D cartoon object with a soft drop shadow, readable at small size: a cute little shop stall with a red and white striped awning, a pile of shiny gold star coins on its counter.` |
| `menu-daily.png` | `Use the attached Metro Sort key art as a style reference. Single game UI icon, centered, square 1:1, transparent background, no text, chunky glossy 3D cartoon object with a soft drop shadow, readable at small size: a round purple gift box with a big golden ribbon bow, a small calendar page with a checkmark leaning on it, tiny sparkles.` |
