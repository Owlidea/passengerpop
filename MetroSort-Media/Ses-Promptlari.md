# Metro Sort – Eksik Sesler

Bu dosyalar `MetroSort-Media/Ses/` klasörüne aşağıdaki **dosya adlarıyla** konmalı; `Tools/art/media_import.py` onları oyuna kopyalar (tire alt çizgiye çevrilir).

**Teknik şartlar (hepsi için):** WAV, 44.1 kHz, 16-bit. Efektler **mono**, müzikler **stereo**. Başta ve sonda sessizlik olmasın (kesilmiş). Tepe seviyesi yaklaşık -1 dBFS. Stil: Royal Match / Toon Blast gibi parlak, sevimli, "oyuncak" hissi; gerçekçi ve sert değil.

Promptlar İngilizce (ses üretim araçları İngilizceyi daha iyi anlıyor).

---

## A. Şu an bilgisayar sentezi çalan sesler (kayıt yok) – öncelik 1

| Dosya | Ne zaman çalar | Süre | Prompt |
|---|---|---|---|
| `passenger-select.wav` | Yolcuya dokunulup seçilince | 0.15–0.25 sn | `cute soft pop with a tiny upward "boop", bubbly mobile game select sound, toy-like, clean` |
| `passenger-land.wav` | Yolcu perona inince | 0.2–0.3 sn | `soft cartoon landing thump with a small springy boing, light footstep on a platform, cheerful mobile puzzle game` |
| `platform-full.wav` | Peron aynı renkle dolunca | 0.6–0.9 sn | `bright rising three-note chime with sparkle shimmer, satisfying match complete sound, glockenspiel and soft bells, casual mobile game` |
| `train-doors.wav` | Tren kapıları açılıp kapanınca | 0.6–0.8 sn | `toy subway train doors pneumatic hiss and soft slide, two-tone door chime "ding-dong" before closing, cute and light, not realistic` |
| `level-failed.wav` | Kaybetme ekranı | 1.5–2.5 sn | `gentle sad descending melody on soft marimba and muted brass "wah-wah", sympathetic not harsh, cute mobile game level failed stinger` |
| `booster.wav` | Booster kullanılınca | 0.5–0.8 sn | `magical whoosh with sparkly twinkle and soft swirl, power-up activation, bright and playful, casual mobile game` |
| `achievement.wav` | Başarım bandı çıkınca | 1.0–1.5 sn | `short celebratory fanfare with bells and a soft trumpet flourish, achievement unlocked, cheerful and premium, mobile game` |
| `coin-tick.wav` | Jeton sayacı artarken (çok kez arka arkaya çalar) | 0.05–0.1 sn | `tiny single coin tick, bright metallic clink, very short, clean, suitable for rapid repetition` |

## B. Hiç sesi olmayan anlar – öncelik 2 (geldiğinde koda ben bağlarım)

| Dosya | Ne zaman | Süre | Prompt |
|---|---|---|---|
| `hop-whoosh.wav` | Yolcu kuyruktan perona zıplarken | 0.2–0.3 sn | `quick soft cartoon whoosh jump, light airy swish upwards, cute character hop` |
| `bench-sit.wav` | Yolcu banka oturunca | 0.2–0.3 sn | `soft cushion plop, small character sitting on a padded bench, cute and muffled` |
| `gate-flap.wav` | Turnike kapakları açılınca | 0.2–0.3 sn | `small plastic turnstile flaps opening, soft mechanical clack with a tiny beep, toy-like` |
| `barrier.wav` | Kilitli peron bariyeri kalkınca/inince | 0.5–0.7 sn | `toy boom barrier arm lifting, short motor whirr with soft clunk at the end, cute mobile game` |
| `board-flip.wav` | Kalkış panosu harfleri dönerken | 0.4–0.6 sn | `split-flap departure board flipping quickly, soft rapid clicking, light and pleasant` |
| `chest-open.wav` | Sandık/hediye açılırken | 1.0–1.5 sn | `treasure chest opening with wooden creak, then magical sparkle burst and choir "aah", rewarding mobile game` |
| `mascot-pop.wav` | Kondüktör ekrana fırlayınca | 0.2–0.3 sn | `cute pop-up bounce with a cheerful whistle blip, character appearing, playful` |
| `mystery-reveal.wav` | Gizemli yolcunun rengi açılınca | 0.6–0.8 sn | `magical reveal "ta-da" with sparkle and a soft poof, mystery unveiled, cute` |
| `star-stamp.wav` | Kazanma ekranında her yıldız oturunca | 0.3–0.4 sn | `chunky star stamp impact with bright sparkle ring, satisfying, mobile game reward` |
| `platform-drop.wav` | Yeni peron yukarıdan düşünce | 0.4–0.6 sn | `heavy but cute thud with a bouncy wobble, object landing, cartoon` |

## C. Ortam ve karakter sesleri – öncelik 3 (oyunu "canlı" yapan katman)

| Dosya | Ne zaman | Süre | Prompt |
|---|---|---|---|
| `station-ambience.wav` | Oyun boyunca alçak seviyede, döngü | 30–60 sn, **kusursuz döngü** | `cozy cartoon metro station ambience, distant soft crowd murmur, faint footsteps, occasional far-away train rumble, warm and calm, seamless loop, no music` |
| `announce-chime.wav` | Tren gelmeden önce | 1.0–1.5 sn | `classic station announcement chime, three soft ascending bell tones, friendly, no voice` |
| `train-hum.wav` | Tren perondayken, döngü | 3–5 sn, döngü | `soft electric train idle hum, gentle low motor drone, cute and quiet, seamless loop` |
| `voice-yay-1.wav` … `voice-yay-3.wav` | Yolcular sevinince (3 farklı) | 0.3–0.6 sn | `cute cartoon character happy "yay!" gibberish voice, small chipmunk-like, mobile game, no real words` |
| `voice-hmm-1.wav`, `voice-hmm-2.wav` | Tedirgin/bekleyen yolcu | 0.3–0.6 sn | `cute cartoon character worried "hmm?" gibberish voice, small and soft, no real words` |
| `voice-aww.wav` | Yolcu reddedilince/üzülünce | 0.4–0.6 sn | `cute cartoon character disappointed "aww" gibberish voice, soft, no real words` |

---

**Mevcut kayıtlar (11 adet) yerinde kalıyor:** `ui_tap`, `error`, `footsteps`, `train_arriving`, `train_departing`, `win_jingle`, `coins_drop`, `star_collect`, `crowd_yay`, `menu_theme`, `gameplay_loop`.
