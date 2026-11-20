# Metro Sort medya paketi

Görseller yerleşik ImageGen ile üretildi. Ana görsel, diğer PNG’ler için stil referansı olarak kullanıldı. İlk set aracın ürettiği özgün çözünürlükte kaydedildi.

| PNG | İçerik |
| --- | --- |
| `key-art` | Üst bölümü logoya ayrılmış dikey ana görsel |
| `logo`, `app-icon` | METRO SORT logosu ve kare mağaza ikonu |
| `conductor` | Kondüktörün dört pozunu içeren karakter paftası |
| `central-line`, `old-town`, `garden`, `palace`, `harbour` | Beş boş istasyon arka planı |
| `coins`, `coin-chest`, `hint`, `undo`, `shuffle`, `extra-platform`, `daily-gift` | Yedi ayrı mağaza ikonu |
| `rewards` | Altın yıldız, boş yıldız yuvası, zafer kurdelesi ve ışık halesi paftası |

Ek sette 23 PNG bulunur. Dört doku 1024 × 1024 boyutunda, altı poster 3:4 dikey, on istasyon arka planı 3:2 yataydır. Zemin, duvar ve peron dokuları iki eksende, ray yatağı yatay eksende döşenir. Sandık ve hediye paftalarının her biri üç açılma hâli içerir. Ek kondüktör paftasında dört yeni poz vardır. [Ek setin üretim prompt’ları](Ek-Gorsel-Promptlari.md)

Üç özgün MIDI düzenlemesi SpessaSynth ile yerelde seslendirildi. S. Christian Collins’in GeneralUser GS enstrüman bankası ticari müzik üretimine izin verir. [GeneralUser GS lisansı](https://github.com/mrbumpy409/GeneralUser-GS/blob/main/documentation/LICENSE.txt)

Efektler harici ses örneği kullanılmadan yerel sentezle oluşturuldu. Tüm ses dosyaları 44,1 kHz, 16 bit PCM WAV. Müzikler stereo, efektler mono.

| WAV | Süre | İçerik |
| --- | ---: | --- |
| `menu-theme` | 34,909 sn | 110 BPM, 16 ölçü. Pizzicato yaylılar, marimba ve glockenspiel |
| `gameplay-loop` | 38,4 sn | 100 BPM, 16 ölçü. Marimba, naylon telli gitar ve hafif perküsyon |
| `win-jingle` | 4 sn | Bakır üflemeliler ve glockenspiel ile zafer fanfarı |
| `ui-tap` | 0,2 sn | Düğme sesi |
| `footsteps` | 0,5 sn | Hafif çizgi film adımları |
| `train-arriving` | 1,5 sn | Fren ve iki tonlu varış sinyali |
| `train-departing` | 1,5 sn | Kapı, düdük ve kalkış |
| `star-collect` | 0,6 sn | Yükselen yıldız melodisi |
| `coins-drop` | 0,8 sn | Madeni para şıngırtısı |
| `error` | 0,3 sn | Yumuşak hata sesi |
| `crowd-yay` | 1 sn | Sentezlenmiş çizgi film kalabalığı |

OpenAI koşulları, uygulanabilir hukuk ölçüsünde görsel çıktının sahipliğini kullanıcıya bırakır. [OpenAI Kullanım Koşulları](https://openai.com/policies/terms-of-use/)
