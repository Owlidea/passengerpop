# Yolcu varyantları için görsel prompt'ları

Her biri ayrı bir görsel. Hepsi aynı ortak başlangıçla başlar; sonuna varyant satırı eklenir.
Üretilen görseli TRELLIS'e (huggingface.co/spaces/trellis-community/TRELLIS) yükleyip GLB'yi
`MetroSort-Media/` klasörüne `yolcu-varyant-N.glb` adıyla koy.

Önemli: şapka yok, kollar gövdeden ayrık (A-pozu), ceket kırmızı kalsın (oyun onu hat rengine boyuyor).

## Ortak başlangıç

```
3D render of one cute chibi toy character, full body, front view, standing straight in A-pose with arms held out 30 degrees from the body, open hands, legs slightly apart, feet flat. Big round head about 40 percent of the height, big round glossy black eyes, rosy cheeks. Soft rounded clay-like glossy materials, premium casual mobile game character, Pixar style, soft studio lighting, plain white background, centered, nothing else in the image, no hat.
```

## Varyantlar

1. `A girl with long brown hair in a high ponytail, gentle smile. Plain red jacket with gold buttons and white collar, dark blue knee-length skirt, white socks, brown shoes.`
2. `A girl with a round hair bun on top and short bangs, cheerful open smile. Plain red jacket with gold buttons and white collar, dark blue trousers, brown shoes.`
3. `A chubby man with a bushy brown moustache and short neat hair, round glasses, friendly smile. Plain red jacket with gold buttons and white collar, dark blue trousers, brown shoes.`
4. `A boy with curly brown hair, freckles, big grin. Plain red jacket with gold buttons and white collar, dark blue shorts, white socks, brown sneakers.`
5. `A grandma with short grey curly hair and small round glasses, kind smile. Plain red jacket with gold buttons and white collar, dark blue long skirt, brown shoes.`

## Atkuyruklu yolcu yüz pilotu

Yerleşik ImageGen ile üretildi. Referans olarak `Gorseller/yolcu-varyant-1.png` kullanıldı.

```text
Use case: stylized-concept. Original character development for Metro Sort, a polished casual puzzle game. The attached image identifies the ponytail passenger's outfit, palette and A-pose. Redesign the face and hair with distinctly improved art direction. Create a single clean character model reference on a plain white studio background. Straight front orthographic full-body view, vertical output, one figure fills the frame head to shoes. A-pose arms 30 degrees from torso with clear white gaps under both arms, hands open, relaxed fingers. No hat, no jewelry or accessories. High brown ponytail swept slightly to her left, simple sculpted hair masses with broad soft highlights, a warm red tailored railway jacket with gold buttons and distinct white collar, navy knee-length skirt, white socks and brown shoes. Face: charming stylized young adult railway passenger, face with a distinctive soft heart shape, rounded cheek volume, small dimensional nose bridge and nostrils, medium-sized expressive hazel brown eyes with cream sclera visible, carefully sculpted upper and lower eyelids with a soft visible crease, a couple of broad catchlights, expressive thick brown brows with a slight characterful asymmetry, gentle closed smile with subtly defined lips and believable smile corners, subtle warmly blended cheeks, human facial planes simplified into appealing rounded shapes. Head about 33 percent of total figure height, slightly less inflated body. Stage lighting fitted for a flagship high-end stylized 3D mobile game cinematic, warm broad key light, subtle cool bounce in the face, mild subsurface skin softness, controlled material-specific roughness, finely finished fabric and hair, no plastic skin, no giant black button eyes, no heavy makeup, no text, no logo. Distinct original character, readable facial personality at gameplay size.
```
