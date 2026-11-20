# Metro Sort – Premium yolcular

Amaç: Dream Games kalitesinde, sevimli ve kişilikli yolcular. Mevcut yolcuların yüzleri ve kıyafetleri TRELLIS'in bulanık dokusundan geliyor; bunları daha iyi kaynak resimlerle ve daha iyi bir 3D üreticiyle (Hunyuan3D-2) baştan üretiyoruz.

## Akış

1. Aşağıdaki her promptu ImageGen ile üret (key-art.png'yi stil referansı olarak ekle). Çıktı: `MetroSort-Media/Gorseller/yolcu-hd-N.png`
2. https://huggingface.co/spaces/tencent/Hunyuan3D-2 sayfasını aç (giriş yapmış ol), resmi yükle, **"Gen Textured Shape"**'e bas, bitince **GLB'yi indir**.
3. GLB'yi `MetroSort-Media/yolcu-hd-N.glb` adıyla kaydet.
4. Bana haber ver: iskelet, şapkalar, hat renkleri, yüz ifadeleri ve bütün animasyonlar bu modellere aynen uygulanır.

**Kurallar (oyun için şart):** şapka yok (şapkaları oyun takıyor), A-pozu (kollar gövdeden ayrık), elinde eşya yok (aksesuarları ben ekliyorum), **ceket düz kırmızı** (oyun onu hat rengine boyuyor), beyaz düz arka plan, tam boy önden.

## Ortak başlangıç

```text
Use the attached Metro Sort key art as a style reference. Original character design for a premium casual mobile puzzle game, in the polished stylized 3D style of top mobile games: appealing chunky proportions, head about 38 percent of the total height, short rounded body, small hands and feet. Single character, full body, straight front orthographic view, standing in an A-pose with both arms held 30 degrees away from the body and clear white gaps under the arms, open relaxed hands, legs slightly apart, feet flat. Face: large expressive eyes with coloured irises, white sclera and two bright catchlights (not black button eyes), soft visible upper eyelids, expressive eyebrows, small cute nose, a clearly shaped friendly mouth, rosy cheeks, smooth soft skin. Clothing: clean simple shapes with clearly separated colour blocks, a plain solid red jacket (no pattern, no logo) with a few gold buttons and a contrasting white collar, crisp fabric folds. Clean sculpted hair with broad soft highlights. No hat, no headwear, nothing in the hands, no bag, no accessories except glasses where stated. Soft studio lighting, plain pure white background, the figure centered and filling the frame head to toe, nothing else in the image, no text.
```

## Karakterler (ortak başlangıçtan sonra ekle)

| Dosya | Karakter |
|---|---|
| `yolcu-hd-1.png` | `A cheerful schoolgirl with two short bouncy pigtails tied with red ribbons, big happy smile with a small gap tooth, navy pleated skirt, white knee socks, red sneakers.` |
| `yolcu-hd-2.png` | `A young office worker man with neat swept brown hair and a confident wink-ready grin, slim dark grey trousers, polished brown shoes, a small tie peeking out under the white collar.` |
| `yolcu-hd-3.png` | `A sweet grandma with a fluffy white bun, round gold glasses, warm closed-eye smile with crinkles, long lavender skirt, comfy brown shoes.` |
| `yolcu-hd-4.png` | `A chubby jolly grandpa with a big white moustache, bald top with white side hair, rosy nose, beaming smile, brown corduroy trousers, brown shoes.` |
| `yolcu-hd-5.png` | `A sporty teenage boy with spiky black hair, freckles and an excited open-mouth grin, dark blue jeans rolled at the ankle, white high-top sneakers.` |
| `yolcu-hd-6.png` | `A stylish young woman with a wavy auburn bob, big curious eyes, playful smile, black leggings, white sneakers.` |
| `yolcu-hd-7.png` | `A small toddler boy with a single curl of hair on top, chubby cheeks, giggling open smile, short blue overall shorts under the jacket, yellow rain boots.` |
| `yolcu-hd-8.png` | `A relaxed musician guy with curly dark hair and a little goatee, sleepy half-lidded happy eyes, olive cargo trousers, chunky brown boots.` |

## Hunyuan3D-2 ayarları

- **"Gen Textured Shape"** (dokulu) kullan; "Gen Shape" yalnızca gri model verir.
- Ayar varsa: Octree Resolution **256**, Steps **30**, Remove Background **açık**.
- Kota biterse ertesi gün devam et; her model yaklaşık 1–2 dakika GPU harcıyor.
