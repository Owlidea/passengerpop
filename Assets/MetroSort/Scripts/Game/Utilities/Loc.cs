using System.Collections.Generic;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Minimal localisation. English text is the key; a language table maps it to a translation and anything
    /// missing falls back to English. Use T() for fixed text and F() for text with {0}-style placeholders.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";
        public const string Turkish = "tr";

        public static string Language { get; private set; }

        static Loc() { Language = English; }

        /// <summary>saved = "" means follow the device language.</summary>
        public static void Init(string saved)
        {
            if (saved == English || saved == Turkish) Language = saved;
            else Language = Application.systemLanguage == SystemLanguage.Turkish ? Turkish : English;
        }

        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english) || Language != Turkish) return english;
            string translated;
            return Tr.TryGetValue(english, out translated) ? translated : english;
        }

        public static string F(string englishFormat, params object[] args)
        {
            return string.Format(T(englishFormat), args);
        }

        /// <summary>
        /// Level names: curated names are translated whole; generated two-word names ("Copper Junction")
        /// are translated word by word.
        /// </summary>
        public static string LevelName(string english)
        {
            if (string.IsNullOrEmpty(english) || Language != Turkish) return english;
            string whole;
            if (Tr.TryGetValue(english, out whole)) return whole;
            string[] words = english.Split(' ');
            if (words.Length != 2) return english;
            string first, second;
            if (!Tr.TryGetValue("name:" + words[0], out first) || !Tr.TryGetValue("name:" + words[1], out second)) return english;
            return first + " " + second;
        }

        private static readonly Dictionary<string, string> Tr = new Dictionary<string, string>
        {
            // Level names (1-36)
            { "First Platform", "İlk Peron" },
            { "Three of a Kind", "Üçü Bir Arada" },
            { "Next in Line", "Sıradaki" },
            { "Two Platforms", "İki Peron" },
            { "Think Ahead", "Önceden Düşün" },
            { "Rush Hour Warm-up", "Yoğun Saat Isınması" },
            { "Wide Concourse", "Geniş Salon" },
            { "Deep Queues", "Uzun Kuyruklar" },
            { "Tight Transfer", "Sıkı Aktarma" },
            { "Graduation Run", "Mezuniyet Seferi" },
            { "Fourth Line", "Dördüncü Hat" },
            { "Yellow Arrives", "Sarı Hat Geldi" },
            { "Double Red", "Çifte Kırmızı" },
            { "Crosstown", "Şehir Hattı" },
            { "Five Lanes", "Beş Şerit" },
            { "Interchange", "Aktarma" },
            { "Breather", "Nefes Molası" },
            { "Morning Peak", "Sabah Yoğunluğu" },
            { "Long Platforms", "Uzun Peronlar" },
            { "Red Line Surge", "Kırmızı Hat Akını" },
            { "Two Track Squeeze", "İki Hat Sıkışması" },
            { "Narrow Station", "Dar İstasyon" },
            { "Signal Delay", "Sinyal Gecikmesi" },
            { "Twin Surge", "Çifte Akın" },
            { "Bottleneck", "Darboğaz" },
            { "Evening Peak", "Akşam Yoğunluğu" },
            { "Full Timetable", "Dolu Tarife" },
            { "Last Train Home", "Eve Son Tren" },
            { "Grand Junction", "Büyük Kavşak" },
            { "Dispatcher's Exam", "Hareket Memuru Sınavı" },
            { "Locked Platform", "Kilitli Peron" },
            { "Temporary Platform", "Geçici Peron" },
            { "VIP Passengers", "VIP Yolcular" },
            { "Express Line", "Ekspres Hat" },
            { "Waiting Bench", "Bekleme Bankı" },
            { "Second Wave", "İkinci Dalga" },
            // Words for generated level names (37-100)
            { "name:Beacon", "Fener" }, { "name:Bridge", "Köprü" }, { "name:Canal", "Kanal" }, { "name:Castle", "Kale" },
            { "name:Copper", "Bakır" }, { "name:Crystal", "Kristal" }, { "name:Forest", "Orman" }, { "name:Garden", "Bahçe" },
            { "name:Granite", "Granit" }, { "name:Harbour", "Liman" }, { "name:Lantern", "Kandil" }, { "name:Market", "Çarşı" },
            { "name:Meadow", "Çayır" }, { "name:Orchard", "Meyvelik" }, { "name:River", "Nehir" }, { "name:Silver", "Gümüş" },
            { "name:Summit", "Zirve" }, { "name:Sunset", "Günbatımı" }, { "name:Tower", "Kule" }, { "name:Willow", "Söğüt" },
            { "name:Central", "Merkez" }, { "name:Crossing", "Geçidi" }, { "name:Depot", "Deposu" }, { "name:Gate", "Kapısı" },
            { "name:Halt", "Durağı" }, { "name:Interchange", "Aktarması" }, { "name:Junction", "Kavşağı" }, { "name:Loop", "Ringi" },
            { "All levels unlocked for testing", "Test için tüm seviyeler açıldı" },
            { "name:Parkway", "Bulvarı" }, { "name:Terminal", "Terminali" }, { "name:Foggy", "Sisli" }, { "name:Midnight", "Geceyarısı" }, { "name:Grand", "Büyük" }, { "name:Polar", "Kutup" }, { "name:Capital", "Başkent" },
            { "name:Lakeside", "Göl" }, { "name:Canyon", "Kanyon" }, { "name:Island", "Ada" }, { "name:Aurora", "Aurora" }, { "name:Golden", "Altın" },
            { "Lakeside Line", "Göl Hattı" }, { "Canyon Line", "Kanyon Hattı" }, { "Island Line", "Ada Hattı" }, { "Aurora Line", "Aurora Hattı" }, { "Golden Line", "Altın Hat" },
            { "name:Crimson", "Kızıl" }, { "name:Emerald", "Zümrüt" }, { "name:Sapphire", "Safir" }, { "name:Amber", "Kehribar" }, { "name:Ivory", "Fildişi" },
            { "Crimson Line", "Kızıl Hat" }, { "Emerald Line", "Zümrüt Hattı" }, { "Sapphire Line", "Safir Hattı" }, { "Amber Line", "Kehribar Hattı" }, { "Ivory Line", "Fildişi Hattı" },
            { "name:Coastal", "Sahil" }, { "name:Desert", "Çöl" }, { "name:Alpine", "Alp" }, { "name:Tropic", "Tropik" }, { "name:Royal", "Kraliyet" },
            { "Coastal Line", "Sahil Hattı" }, { "Desert Line", "Çöl Hattı" }, { "Alpine Line", "Alp Hattı" }, { "Tropic Line", "Tropik Hat" }, { "Royal Line", "Kraliyet Hattı" }, { "Capital Line", "Başkent Hattı" }, { "Polar Line", "Kutup Hattı" },
            { "{0} {1} train", "{0} {1} tren" }, { "{0} {1} trains", "{0} {1} tren" },
            { "A coloured turnstile only counts trains of its own line.", "Renkli turnike yalnızca kendi hattının trenlerini sayar." },
            { "Grand Line", "Büyük Hat" }, { "{0} train", "{0} tren" }, { "{0} trains", "{0} tren" },
            { "This turnstile is still closed", "Bu turnike henüz kapalı" },
            { "A closed turnstile opens after the number of trains shown on it have left.", "Kapalı turnike, üzerinde yazan sayıda tren kalkınca açılır." },
            // Chapters
            { "Central Line", "Merkez Hattı" }, { "Old Town Line", "Eski Şehir Hattı" }, { "Garden Line", "Bahçe Hattı" },
            { "Palace Line", "Saray Hattı" }, { "Harbour Line", "Liman Hattı" }, { "Misty Line", "Sisli Hat" }, { "Night Line", "Gece Hattı" },
            { "{0}  ({1}-{2})", "{0}  ({1}-{2})" },
            { "Grey passengers hide their line until they move up the queue.", "Gri yolcuların hattı, kuyrukta öne gelene kadar gizlidir." },

            // Menu and HUD
            { "PLAY", "OYNA" },
            { "Settings", "Ayarlar" },
            { "Levels", "Seviyeler" },
            { "Shop", "Mağaza" },
            { "Daily Gift", "Günlük Hediye" },
            { "Level {0}", "Seviye {0}" },
            { "Level {0}  -  {1}", "Seviye {0}  -  {1}" },
            { "Boarded {0} / {1}", "Binen {0} / {1}" },
            { "   Wave {0}/{1}", "   Dalga {0}/{1}" },
            { "   Moves left {0}", "   Kalan hamle {0}" },
            { "{0} coins", "{0} jeton" },
            { "+{0} coins", "+{0} jeton" },
            { "You have {0} coins", "{0} jetonun var" },
            { "Levels {0} - {1}", "Seviye {0} - {1}" },
            { "locked", "kilitli" },

            // Boosters
            { "Undo", "Geri Al" },
            { "Hint", "İpucu" },
            { "+Platform", "+Peron" },
            { "Shuffle", "Karıştır" },
            { "ExtraPlatform", "Ek Peron" },
            { "{0}  (you have {1})   buy 1 for {2}", "{0}  (elinde {1})   1 tane: {2} jeton" },
            { "Undo last move  ({0})", "Son hamleyi geri al  ({0})" },
            { "Add a platform  ({0})", "Peron ekle  ({0})" },
            { "Not enough coins", "Yeterli jeton yok" },
            { "Not enough coins for {0}", "{0} için yeterli jeton yok" },
            { "Nothing to undo", "Geri alınacak hamle yok" },
            { "No room for another platform in this level", "Bu seviyeye başka peron eklenemez" },
            { "No winning path from here. Try Undo.", "Buradan kazanmak mümkün değil. Geri Al'ı dene." },
            { "No winnable shuffle here. Nothing was spent.", "Kazandıran bir karıştırma yok. Hiçbir şey harcanmadı." },
            { "Something went wrong. Nothing was spent.", "Bir sorun oldu. Hiçbir şey harcanmadı." },

            // Settings
            { "Music", "Müzik" },
            { "Sound", "Ses" },
            { "Assisted taps (auto-pick platform): {0}", "Otomatik peron seçimi: {0}" },
            { "Reduced motion: {0}", "Azaltılmış hareket: {0}" },
            { "Fast trains: {0}", "Hızlı trenler: {0}" },
            { "Language: {0}", "Dil: {0}" },
            { "ON", "AÇIK" },
            { "OFF", "KAPALI" },
            { "Resume", "Devam Et" },
            { "Restart Level", "Seviyeyi Yeniden Başlat" },
            { "Back to Menu", "Menüye Dön" },
            { "Restore Purchases", "Satın Alımları Geri Yükle" },
            { "Purchases restored", "Satın alımlar geri yüklendi" },
            { "Nothing to restore", "Geri yüklenecek bir şey yok" },
            { "Close", "Kapat" },

            // Results
            { "Level Complete!", "Seviye Tamamlandı!" },
            { "Continue", "Devam" },
            { "Menu", "Menü" },
            { "Try Again", "Tekrar Dene" },
            { "Out of Moves", "Hamle Kalmadı" },
            { "Station Jammed", "İstasyon Tıkandı" },
            { "The move limit ran out before everyone boarded.", "Herkes binmeden hamle hakkı bitti." },
            { "No passenger can reach a platform. Undo a move or try again.", "Hiçbir yolcu perona geçemiyor. Bir hamleyi geri al ya da tekrar dene." },
            { "You cleared every station. More are on the way!", "Tüm istasyonları bitirdin. Yenileri yolda!" },

            // Shop and rewards
            { "Watch an ad: +{0} coins (optional)", "Reklam izle: +{0} jeton (isteğe bağlı)" },
            { "No ad available right now", "Şu an gösterilecek reklam yok" },
            { "Daily gift: +{0} coins", "Günlük hediye: +{0} jeton" },
            { "Thank you! {0} added", "Teşekkürler! {0} eklendi" },
            { "Purchase was not completed", "Satın alma tamamlanmadı" },
            { "Ads removed - thank you!", "Reklamlar kaldırıldı - teşekkürler!" },
            { "Remove Ads   {0}", "Reklamları Kaldır   {0}" },
            { "Remove Ads", "Reklamları Kaldır" },
            { "Pocket of Coins", "Bir Avuç Jeton" },
            { "Vault of Coins", "Jeton Kasası" },
            { "Achievement: {0}  +{1} coins", "Başarım: {0}  +{1} jeton" },
            { "First Departure", "İlk Kalkış" },
            { "Commuter", "Yolcu" },
            { "Dispatcher", "Hareket Memuru" },
            { "Station Master", "İstasyon Şefi" },
            { "Perfect Timetable", "Kusursuz Tarife" },
            { "Your progress was restored from a backup", "İlerlemen yedekten geri yüklendi" },

            // In-game messages
            { "VIPs never wait alone", "VIP yolcular tek başına beklemez" },
            { "That platform is still locked", "Bu peron hâlâ kilitli" },
            { "That platform is closed", "Bu peron kapalı" },
            { "The bench is full", "Bank dolu" },
            { "Wait for the train to leave", "Trenin kalkmasını bekle" },

            // Platform captions
            { "CLOSED", "KAPALI" },
            { "LOCKED\n{0} {1} train", "KİLİTLİ\n{0} {1} tren" },
            { "LOCKED\n{0} {1} trains", "KİLİTLİ\n{0} {1} tren" },
            { "{0} use left", "{0} kullanım kaldı" },
            { "{0} uses left", "{0} kullanım kaldı" },
            { "EXPRESS", "EKSPRES" },
            { "EXPRESS  ", "EKSPRES  " },
            { "EXPRESS\n{0} only", "EKSPRES\nyalnız {0}" },
            { "{0} only", "yalnız {0}" },
            { "EXTRA", "EK" },
            { "any", "herhangi" },
            { "Red", "Kırmızı" },
            { "Blue", "Mavi" },
            { "Green", "Yeşil" },
            { "Yellow", "Sarı" },
            { "Purple", "Mor" },
            { "Orange", "Turuncu" },

            // Tutorials
            { "Tap a passenger, then tap a platform.", "Bir yolcuya, sonra bir perona dokun." },
            { "Three of a colour call their train. Send the rest!", "Aynı renkten üç yolcu trenini çağırır. Kalanları da gönder!" },
            { "Gather three of the same colour on one platform.", "Aynı renkten üç yolcuyu tek peronda topla." },
            { "Keep each colour together.", "Her rengi bir arada tut." },
            { "When the front passenger leaves, the next one steps up.", "Öndeki yolcu gidince sıradaki öne geçer." },
            { "Peek at who is waiting behind.", "Arkada kimin beklediğine göz at." },
            { "Only two platforms! Each holds one colour at a time.", "Yalnızca iki peron var! Her peron aynı anda tek renk alır." },
            { "Finish a train to free its platform.", "Peronu boşaltmak için treni tamamla." },
            { "Look down the queues before you commit a platform.", "Bir perona karar vermeden önce kuyruklara bak." },
            { "A locked platform opens once the trains shown on it have left.", "Kilitli peron, üzerinde yazan trenler kalkınca açılır." },
            { "This platform closes after a few departures. Spend it wisely.", "Bu peron birkaç kalkıştan sonra kapanır. İyi değerlendir." },
            { "VIPs wear a gold halo and never wait alone. Seat a friend first.", "VIP'lerin altın halesi var ve tek başına beklemezler. Önce bir arkadaşını gönder." },
            { "The express leaves with just two, but only serves its own line.", "Ekspres iki yolcuyla kalkar ama yalnızca kendi hattına hizmet verir." },
            { "Tap a passenger, then the bench to park them. Moves are limited!", "Yolcuya, sonra banka dokunarak onu beklet. Hamleler sınırlı!" },
            { "A second wave arrives when the queues are empty.", "Kuyruklar boşalınca ikinci dalga gelir." },
            { "The long platform needs four passengers and serves one line only.", "Uzun peron dört yolcu ister ve tek hatta hizmet verir." }
        };
    }
}
