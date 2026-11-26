# Passenger Pop – Premium 3D yolcular

Amaç: Dream Games kalitesinde, sevimli ve kişilikli yolcular. Eski altı yolcu (`PassengerHD_0..5`) TRELLIS'in
bulanık dokusundan geliyor. Yenileri bu Mac'te kurulu Hunyuan3D-2 ile şekilleniyor; renkler ve yüz Blender'da
senin resimlerinden boyanıyor.

## Akış

Her yolcu için iki resim üret ve `MetroSort-Media/Gorseller/` klasörüne aşağıdaki adlarla koy:

1. **Önden resim** `yolcu-hd-N.png`: `key-art.png`'yi referans olarak ekle, **ortak başlangıç** + o yolcunun satırı.
   Dikey 2:3, aracın verebildiği en yüksek çözünürlük. Yüz bu resimden boyanır.
2. **Dört yönden görünüş** `yolcu-hd-N-turnaround.png`: 1. resmi referans olarak ekle, **dört yön promptu**
   (her yolcu için aynı). Yatay 3:2. 3D şekil bundan çıkar; sırt ve saçın arkası artık tahmin edilmez.

**1 ve 3 hazır**: oyunda `PassengerHD_6` (okul kızı) ve `PassengerHD_7` (büyükanne).

**Karar (26 Kasım 2026):** Kalan altı yolcu, eski TRELLIS yolcularının (`PassengerHD_0..5`) yerini alıyor. Oyunda yine
sekiz yolcu olacak, hepsi yeni kalitede. Her yeni yolcu hazır oldukça kendi yuvasına yazılır; oyun yolcuları
`PassengerHD_0`'dan sırayla yüklediği için başka bir ayar gerekmez:

| Resim | Oyundaki yeri | Kimin yerine |
|---|---|---|
| `yolcu-hd-2` (ofis çalışanı) | `PassengerHD_0` | eski 0 (`yolcu-a-poz`) |
| `yolcu-hd-4` (bıyıklı dede) | `PassengerHD_1` | eski 1 (`yolcu-varyant-1`) |
| `yolcu-hd-5` (sporcu genç) | `PassengerHD_2` | eski 2 (`yolcu-varyant-2`) |
| `yolcu-hd-6` (bob saçlı kadın) | `PassengerHD_3` | eski 3 (`yolcu-varyant-3`) |
| `yolcu-hd-7` (küçük çocuk) | `PassengerHD_4` | eski 4 (`yolcu-varyant-4`) |
| `yolcu-hd-8` (müzisyen) | `PassengerHD_5` | eski 5 (`yolcu-varyant-5`) |

Sıra önemli değil; hangisini önce üretirsen o yuvaya geçer.

Gerisini ben yaparım: Hunyuan3D-2 (çok açılı model) ile şekil, resimlerden renk ve yüz, sonra iskelet, şapkalar, hat
renkleri, yüz ifadeleri ve bütün animasyonlar (`Tools/art/passenger.py`).

## Kurallar (oyun için şart)

- Şapka yok: şapkaları oyun takıyor. Saç başa yakın dursun.
- A-pozu: kollar gövdeden ayrık, kolların altında beyaz boşluk.
- Elde eşya, çanta, aksesuar yok (yalnızca belirtilen gözlük).
- **Ceket düz kırmızı ve resimdeki tek kırmızı şey.** Oyun ceketi kırmızısından tanıyıp hat rengine boyuyor; kırmızı
  ayakkabı, kurdele, kravat ya da kızıl saç ceketle karışır.
- Düz beyaz zemin, gölge yok, yumuşak ve eşit ışık.

## Ortak başlangıç (önden resim)

```text
Use the attached Passenger Pop key art as a style reference. Original character design for a premium casual mobile puzzle game, in the polished stylized 3D style of top mobile games: appealing chunky proportions, head about 38 percent of the total height, short rounded body, small hands and feet. Single character, full body, straight front orthographic view, standing in an A-pose with both arms held 30 degrees away from the body and clear white gaps under the arms, open relaxed hands, legs slightly apart, feet flat. Face: large expressive eyes with coloured irises, white sclera and two bright catchlights (not black button eyes), soft visible upper eyelids, expressive eyebrows, small cute nose, a clearly shaped friendly mouth, soft pink cheeks, smooth soft skin. Clothing: clean simple shapes with clearly separated colour blocks, a plain solid red jacket (no pattern, no logo) with a few gold buttons and a contrasting white collar, crisp fabric folds. The jacket is the only red item in the picture. Clean sculpted hair close to the head with broad soft highlights. No hat, no headwear, nothing in the hands, no bag, no accessories except glasses where stated. Soft even studio lighting, no cast shadow, plain pure white background, portrait 2:3 at the highest resolution available, the figure centred and filling the frame head to toe, nothing else in the image, no text.
```

## Yolcular (ortak başlangıcın sonuna ekle)

`yolcu-hd-1.png`
```text
A cheerful schoolgirl with light skin and two short bouncy pigtails tied with yellow ribbons, big happy smile with a small gap tooth, navy pleated skirt, white knee socks, white sneakers with navy laces.
```

`yolcu-hd-2.png`
```text
A young office worker man with deep brown skin and neat short black hair, confident friendly grin, slim dark grey trousers, polished brown shoes, a small navy tie peeking out under the white collar.
```

`yolcu-hd-3.png`
```text
A sweet grandma with light skin, a fluffy white bun, round gold glasses and a warm closed-eye smile with crinkles, long lavender skirt, comfy brown shoes.
```

`yolcu-hd-4.png`
```text
A chubby jolly grandpa with tan skin, a big white moustache, bald top with white side hair, a slightly pink nose and a beaming smile, brown corduroy trousers, brown shoes.
```

`yolcu-hd-5.png`
```text
A sporty teenage boy with medium brown skin, spiky black hair, a few freckles and an excited open-mouth grin, dark blue jeans rolled at the ankle, white high-top sneakers.
```

`yolcu-hd-6.png`
```text
A stylish young woman with light olive skin and a wavy chestnut-brown bob, big curious eyes, playful smile, black leggings, white sneakers.
```

`yolcu-hd-7.png`
```text
A small toddler boy with light skin, a single curl of blond hair on top, chubby cheeks and a giggling open smile, short blue overall shorts under the jacket, yellow rain boots.
```

`yolcu-hd-8.png`
```text
A relaxed musician guy with dark brown skin, short curly black hair and a little goatee, sleepy half-lidded happy eyes, olive cargo trousers, chunky brown boots.
```

## Dört yönden görünüş (her yolcu için aynı)

Önden resmi (`yolcu-hd-N.png`) referans olarak ekle, `yolcu-hd-N-turnaround.png` adıyla kaydet.

```text
Orthographic turnaround sheet of exactly the same character as the attached picture (same face, hair, proportions, clothes and colours), standing in the same A-pose with both arms 30 degrees away from the body and clear gaps under the arms. Four full-body views side by side in one row, left to right: front view, left side view, back view, right side view. All four at exactly the same scale and height, feet on the same invisible ground line, camera at chest height, perfectly orthographic with no perspective. The back view shows the back of the hair and the plain red jacket from behind with no details. Landscape 3:2 at the highest resolution available. Plain pure white seamless background, no cast shadow, no floor, soft even lighting, sharp focus, premium casual mobile game stylized 3D look. No hat, nothing in the hands, no text, no labels, no arrows.
```

## Kontrol listesi

- Dört görünüşte de aynı kişi: yüz, saç ve kıyafet renkleri tutuyor.
- Kollar gövdeye değmiyor; ayaklar aynı çizgide.
- Kırmızı yalnızca cekette.
- Zemin bembeyaz, gölge ve yazı yok.
