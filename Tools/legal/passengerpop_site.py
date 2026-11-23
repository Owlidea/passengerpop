"""Writes the Passenger Pop legal site (GitHub Pages, like Owlidea/slingshire): EN at the root, TR under tr/."""
import html, os, sys
OUT = sys.argv[1]
UPDATED = {'en': 'November 22, 2026', 'tr': '22 Kasım 2026'}
COPY = '© 2026 Owlidea Bilişim Teknolojileri Ltd. Şti.'
MAIL = 'support@owlidea.com'
CSS = """:root{--bg:#0c2830;--card:#113843;--text:#e9f6f8;--muted:#9fc4cc;--gold:#f6c744;--line:#1d5361}
@media (prefers-color-scheme: light){:root{--bg:#eef7f8;--card:#ffffff;--text:#0c2830;--muted:#4a6a72;--gold:#a26c00;--line:#cfe2e6}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--text);font:16px/1.6 -apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif}
main{max-width:760px;margin:0 auto;padding:32px 16px 64px}
header{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-bottom:24px}
.brand{font-weight:800;font-size:20px;color:var(--gold);text-decoration:none}
nav a{color:var(--muted);margin-left:14px;text-decoration:none;font-size:14px}
nav a:hover{color:var(--gold)}
h1{font-size:30px;margin:0 0 4px}
.updated{color:var(--muted);font-size:14px;margin:0 0 24px}
.lead{font-size:18px;margin:0 0 24px}
section{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px 20px;margin:0 0 12px}
h2{font-size:18px;margin:0 0 6px;color:var(--gold)}
p{margin:0}
a{color:var(--gold)}
footer{color:var(--muted);font-size:13px;margin-top:32px;text-align:center}"""

NAV = {
    'en': [('support.html', 'Support'), ('privacy.html', 'Privacy'), ('terms.html', 'Terms'), ('tr/{page}', 'Türkçe')],
    'tr': [('support.html', 'Destek'), ('privacy.html', 'Gizlilik'), ('terms.html', 'Koşullar'), ('../{page}', 'English')],
}

def page(lang, name, title, h1, body, updated=True, lead=None):
    nav = '\n'.join(f'<a href="{href.format(page=name)}">{label}</a>' for href, label in NAV[lang])
    parts = [f'<!doctype html>\n<html lang="{lang}">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1">',
             f'<title>{html.escape(title)} · Passenger Pop</title>\n<style>{CSS}\n</style>\n</head>\n<body>\n<main>\n<header>',
             f'<a class="brand" href="index.html">Passenger Pop</a>\n<nav>\n{nav}\n</nav>\n</header>', f'<h1>{html.escape(h1)}</h1>']
    if updated: parts.append(f'<p class="updated">{"Last updated" if lang == "en" else "Son güncelleme"}: {UPDATED[lang]}</p>')
    if lead: parts.append(f'<p class="lead">{lead}</p>')
    for h, p in body:
        parts.append(f'<section><h2>{html.escape(h)}</h2><p>{p}</p></section>')
    parts.append(f'<footer>{COPY}</footer>\n</main>\n</body>\n</html>\n')
    folder = OUT if lang == 'en' else os.path.join(OUT, 'tr')
    os.makedirs(folder, exist_ok=True)
    open(os.path.join(folder, name), 'w', encoding='utf-8').write('\n'.join(parts))

mail = f'<a href="mailto:{MAIL}">{MAIL}</a>'
# ---------------------------------------------------------------- English
page('en', 'privacy.html', 'Privacy Policy', 'Privacy Policy', [
    ('In short', 'Passenger Pop does not ask you to create an account. We do not collect your name, email or phone number. Your progress and settings are stored only on your device.'),
    ('Data stored on your device', 'Level progress, stars, coins, boosters, lives, star chest and daily gift progress, purchased items and settings (music, sound, language, assisted taps, reduced motion, fast trains) are stored on your device. They are removed when you uninstall the app and are not transferred to another device automatically.'),
    ('Ads', 'The app shows Google AdMob ads. AdMob may process device identifiers (such as the advertising ID), your IP address and approximate location to serve and measure ads. You can change your personalised-ads choice in your device ad settings, in the consent screen the app shows, or under Settings &gt; Privacy in the game when that option is offered in your region. The &quot;Remove Ads&quot; purchase removes the ads shown between levels; optional rewarded videos remain.'),
    ('Purchases', 'Purchases are made through the App Store or Google Play. We never see your payment details. We use RevenueCat to manage purchases; it processes an anonymous user ID and purchase records to verify your purchase status.'),
    ('Children', 'Passenger Pop is not directed to children under 13 and we do not knowingly collect personal data from children.'),
    ('Third parties', 'Google (AdMob) and RevenueCat operate under their own privacy policies. We do not sell or share your data for other purposes.'),
    ('Your rights', f'Under applicable laws (GDPR, KVKK) you may access, correct and delete your data. Because we hold no data that identifies you, most data is only on your device; for questions or requests, write to us: {mail}'),
    ('Changes', 'We may update this policy. Important changes will be published on this page.'),
    ('Contact', f'Owlidea · {mail}'),
])
page('en', 'support.html', 'Support', 'Support', [
    ('Contact us', f'Email: {mail}<br>We usually reply within 2 business days.'),
    ('My Remove Ads purchase is missing', 'Tap &quot;Restore Purchases&quot; in the Settings screen on the main menu. Make sure you are signed in with the App Store or Google Play account you purchased with.'),
    ('I bought coins but they were not added', 'Close and reopen the app. If the problem continues, write to us with your purchase receipt.'),
    ('Can I move my progress to another device?', 'No. Game progress is stored only on your device. Remove Ads can be restored on your other devices.'),
    ('I would like a refund', 'Refunds are handled by the App Store or Google Play under their own policies.'),
], updated=False, lead='Have a question? We are happy to help.')
page('en', 'terms.html', 'Terms of Use', 'Terms of Use', [
    ('Acceptance', 'By downloading and using Passenger Pop you accept these terms. If you do not accept them, do not use the app.'),
    ('License', 'We grant you a limited, non-transferable license to use the app for personal, non-commercial purposes. You may not copy, reverse engineer or modify the app.'),
    ('Coins and in-game items', 'Coins, boosters and lives have no real-world value, cannot be exchanged for cash and cannot be transferred between users. Coin packs are consumable purchases; coins that have been spent are not refunded.'),
    ('Purchases and refunds', 'Purchases are made through the App Store or Google Play and prices may vary by region. Refunds are governed by the store\'s policies. &quot;Remove Ads&quot; is a one-time purchase and can be restored on another device with &quot;Restore Purchases&quot;.'),
    ('Ads', 'The app contains ads. Rewarded videos are optional; the reward is granted only when you finish the video.'),
    ('Unacceptable use', 'You may not use the app to cheat, break its security, disrupt the service or for any unlawful purpose.'),
    ('Disclaimer', 'The app is provided &quot;as is&quot;. We do not guarantee uninterrupted or error-free operation. To the extent permitted by law, we are not liable for indirect damages arising from use.'),
    ('Changes', 'We may update the app and these terms. Continued use after an update means you accept the new terms.'),
    ('Governing law', 'These terms are governed by the laws of the Republic of Türkiye. Your statutory consumer rights are preserved.'),
    ('Contact', f'For questions and support: {mail}'),
])
page('en', 'index.html', 'Home', 'Passenger Pop', [
    ('Links', '<a href="support.html">Support</a> · <a href="privacy.html">Privacy Policy</a> · <a href="terms.html">Terms of Use</a>'),
], updated=False, lead='Sort the passengers by colour and send them onto their trains. A cosy metro puzzle game: 700 levels on 35 lines, boosters, a star chest and a daily gift.')
# ---------------------------------------------------------------- Türkçe
page('tr', 'privacy.html', 'Gizlilik Politikası', 'Gizlilik Politikası', [
    ('Kısaca', 'Passenger Pop, hesap açmanızı istemez. Ad, e-posta ya da telefon numarası toplamayız. Oyun ilerlemeniz ve ayarlarınız yalnızca cihazınızda saklanır.'),
    ('Cihazda saklanan veriler', 'Seviye ilerlemesi, yıldızlar, jetonlar, güçlendiriciler, canlar, yıldız sandığı ve günlük hediye bilgisi, satın alınan ürünler ve ayarlar (müzik, ses, dil, yardımlı dokunma, az hareket, hızlı trenler) cihazınızda saklanır. Uygulamayı sildiğinizde bu veriler de silinir. Başka bir cihaza otomatik olarak aktarılmaz.'),
    ('Reklamlar', 'Uygulamada Google AdMob reklamları gösterilir. AdMob, reklam göstermek ve ölçmek için cihaz tanımlayıcılarını (ör. reklam kimliği), IP adresini ve kaba konumu işleyebilir. Kişiselleştirilmiş reklam tercihinizi cihazınızın reklam ayarlarından, uygulamanın gösterdiği izin ekranından ya da bölgenizde sunuluyorsa oyundaki Ayarlar &gt; Gizlilik bölümünden değiştirebilirsiniz. &quot;Reklamları Kaldır&quot; satın alımı seviye aralarında çıkan reklamları kaldırır; isteğe bağlı ödüllü videolar kalır.'),
    ('Satın alımlar', 'Satın alımlar App Store ya da Google Play üzerinden yapılır. Ödeme bilgilerinizi biz görmeyiz. Satın alımları yönetmek için RevenueCat hizmetini kullanırız; bu hizmet satın alma durumunuzu doğrulamak için anonim bir kullanıcı kimliği ve satın alma kayıtlarını işler.'),
    ('Çocuklar', 'Passenger Pop 13 yaşın altındaki çocuklara yönelik değildir ve bilerek çocuklardan kişisel veri toplamayız.'),
    ('Üçüncü taraflar', 'Google (AdMob) ve RevenueCat kendi gizlilik politikalarına göre çalışır. Verilerinizi başka amaçla satmayız ya da paylaşmayız.'),
    ('Haklarınız', f'KVKK ve GDPR kapsamında verilerinize erişme, düzeltme ve silme haklarınız vardır. Bizde sizi tanımlayan veri tutulmadığından çoğu veri yalnızca cihazınızdadır; yine de soru ya da talepleriniz için bize yazın: {mail}'),
    ('Değişiklikler', 'Bu politikayı güncelleyebiliriz. Önemli değişiklikler bu sayfada yayımlanır.'),
    ('İletişim', f'Owlidea · {mail}'),
])
page('tr', 'support.html', 'Destek', 'Destek', [
    ('Bize ulaşın', f'E-posta: {mail}<br>Genellikle 2 iş günü içinde yanıt veririz.'),
    ('Satın aldığım reklamsız paket görünmüyor', 'Ana menüdeki Ayarlar ekranında &quot;Satın Alımları Geri Yükle&quot; düğmesine dokunun. Satın aldığınız App Store ya da Google Play hesabıyla giriş yapmış olmanız gerekir.'),
    ('Jeton paketi aldım ama jetonlar eklenmedi', 'Uygulamayı kapatıp yeniden açın. Sorun sürerse satın alma makbuzunuzla birlikte bize yazın.'),
    ('İlerlemem başka bir cihaza taşınır mı?', 'Hayır. Oyun ilerlemesi yalnızca cihazınızda saklanır. Reklamsız paket ise geri yükleme ile diğer cihazlarınızda da açılır.'),
    ('İade almak istiyorum', 'İadeler App Store ya da Google Play üzerinden, ilgili mağazanın politikalarına göre yapılır.'),
], updated=False, lead='Sorunuz mu var? Size yardımcı olmaktan memnuniyet duyarız.')
page('tr', 'terms.html', 'Kullanım Koşulları', 'Kullanım Koşulları', [
    ('Kabul', 'Passenger Pop\'u indirerek ve kullanarak bu koşulları kabul etmiş olursunuz. Kabul etmiyorsanız uygulamayı kullanmayın.'),
    ('Lisans', 'Size uygulamayı kişisel ve ticari olmayan amaçla kullanmanız için sınırlı, devredilemez bir lisans veriyoruz. Uygulamayı kopyalayamaz, tersine mühendislik yapamaz ya da değiştiremezsiniz.'),
    ('Jetonlar ve oyun içi ürünler', 'Jetonların, güçlendiricilerin ve canların gerçek para değeri yoktur, nakde çevrilemez ve kullanıcılar arasında devredilemez. Jeton paketleri tüketilebilir satın alımlardır; harcanan jetonlar iade edilmez.'),
    ('Satın alımlar ve iade', 'Satın alımlar App Store ya da Google Play üzerinden yapılır; fiyatlar bölgenize göre değişebilir. İadeler ilgili mağazanın politikalarına tabidir. &quot;Reklamları Kaldır&quot; tek seferlik bir satın alımdır ve &quot;Satın Alımları Geri Yükle&quot; ile başka bir cihazda geri alınabilir.'),
    ('Reklamlar', 'Uygulama reklam içerir. Ödüllü videolar isteğe bağlıdır; ödül yalnızca videoyu tamamladığınızda verilir.'),
    ('Kabul edilemez kullanım', 'Uygulamayı hile yapmak, güvenliği aşmak, hizmeti aksatmak ya da yasa dışı amaçlarla kullanamazsınız.'),
    ('Garanti reddi', 'Uygulama &quot;olduğu gibi&quot; sunulur. Kesintisiz ya da hatasız çalışacağını garanti etmeyiz. Yasaların izin verdiği ölçüde, kullanımdan doğan dolaylı zararlardan sorumlu değiliz.'),
    ('Değişiklikler', 'Uygulamayı ve bu koşulları güncelleyebiliriz. Güncellemeden sonra kullanmaya devam etmeniz yeni koşulları kabul ettiğiniz anlamına gelir.'),
    ('Uygulanacak hukuk', 'Bu koşullar Türkiye Cumhuriyeti kanunlarına tabidir. Tüketici olarak yasal haklarınız saklıdır.'),
    ('İletişim', f'Soru ve destek için: {mail}'),
])
page('tr', 'index.html', 'Ana sayfa', 'Passenger Pop', [
    ('Bağlantılar', '<a href="support.html">Destek</a> · <a href="privacy.html">Gizlilik Politikası</a> · <a href="terms.html">Kullanım Koşulları</a>'),
], updated=False, lead='Yolcuları renklerine göre sırala, doğru trene bindir. Sıcacık bir metro bulmacası: 35 hatta 700 seviye, güçlendiriciler, yıldız sandığı ve günlük hediye.')
open(os.path.join(OUT, '.nojekyll'), 'w').close()
print('written', sorted(os.listdir(OUT)), sorted(os.listdir(os.path.join(OUT, 'tr'))))
