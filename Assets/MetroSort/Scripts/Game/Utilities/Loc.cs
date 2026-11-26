using System.Collections.Generic;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Minimal localisation. English text is the key; a language table maps it to a translation and anything
    /// missing falls back to English. Use T() for fixed text and F() for text with {0}-style placeholders.
    /// Turkish lives in this file; the other tables are in LocDe/LocFr/LocEs/LocPt/LocIt.cs.
    /// </summary>
    public static partial class Loc
    {
        public const string English = "en";
        public const string Turkish = "tr";
        public const string German = "de";
        public const string French = "fr";
        public const string Spanish = "es";
        public const string Portuguese = "pt";
        public const string Italian = "it";

        /// <summary>Every language the game speaks, in the order the language picker lists them.</summary>
        public static readonly string[] Languages = { English, Turkish, German, French, Spanish, Portuguese, Italian };

        public static string Language { get; private set; }

        static Loc() { Language = English; }

        /// <summary>The language's own name, as the picker shows it whatever the current language is.</summary>
        public static string NativeName(string language)
        {
            switch (language)
            {
                case Turkish: return "Türkçe";
                case German: return "Deutsch";
                case French: return "Français";
                case Spanish: return "Español";
                case Portuguese: return "Português";
                case Italian: return "Italiano";
                default: return "English";
            }
        }

        // Device languages the game speaks, and the language each one opens in. Anything else opens in English.
        private static readonly Dictionary<SystemLanguage, string> DeviceLanguages = new Dictionary<SystemLanguage, string>
        {
            { SystemLanguage.Turkish, Turkish },
            { SystemLanguage.German, German },
            { SystemLanguage.French, French },
            { SystemLanguage.Spanish, Spanish },
            { SystemLanguage.Portuguese, Portuguese },
            { SystemLanguage.Italian, Italian },
        };

        /// <summary>
        /// A language picked in Settings always wins. Never chosen (saved = ""), the game opens in the device's
        /// language when it speaks it, and in English everywhere else.
        /// </summary>
        public static void Init(string saved)
        {
            if (System.Array.IndexOf(Languages, saved) >= 0)
            {
                Language = saved;
                return;
            }
            string spoken;
            Language = DeviceLanguages.TryGetValue(Application.systemLanguage, out spoken) ? spoken : English;
        }

        private static Dictionary<string, Dictionary<string, string>> _tables;

        /// <summary>The current language's table; null for English. Built on first use, after every table exists.</summary>
        private static Dictionary<string, string> Table
        {
            get
            {
                if (Language == English) return null;
                if (_tables == null)
                    _tables = new Dictionary<string, Dictionary<string, string>>
                    {
                        { Turkish, Tr }, { German, De }, { French, Fr }, { Spanish, Es }, { Portuguese, Pt }, { Italian, It },
                    };
                Dictionary<string, string> table;
                return _tables.TryGetValue(Language, out table) ? table : null;
            }
        }

        public static string T(string english)
        {
            Dictionary<string, string> table = Table;
            if (string.IsNullOrEmpty(english) || table == null) return english;
            string translated;
            return table.TryGetValue(english, out translated) ? translated : english;
        }

        public static string F(string englishFormat, params object[] args)
        {
            return string.Format(T(englishFormat), args);
        }

        /// <summary>
        /// Level names: curated names are translated whole. Generated two-word names ("Copper Junction") are built
        /// from their words: "name:" + the second word is the place, and the first word describes it. English and
        /// Turkish keep the English order; German makes one compound word ("Kupfer" + "Kreuz" = "Kupferkreuz");
        /// French, Spanish, Portuguese and Italian put the place first ("Puente" + "de Cobre"). A "mod:" entry
        /// gives the first word the form it takes in the name ("Frühlings", "de Cobre"); without one the "name:"
        /// word is used in the English order.
        /// </summary>
        public static string LevelName(string english)
        {
            Dictionary<string, string> table = Table;
            if (string.IsNullOrEmpty(english) || table == null) return english;
            string whole;
            if (table.TryGetValue(english, out whole)) return whole;
            string[] words = english.Split(' ');
            if (words.Length != 2) return english;
            string place, modifier;
            if (!table.TryGetValue("name:" + words[1], out place) || string.IsNullOrEmpty(place)) return english;
            bool own = table.TryGetValue("mod:" + words[0], out modifier);
            if (!own && !table.TryGetValue("name:" + words[0], out modifier)) return english;
            if (Language == German) return modifier + char.ToLowerInvariant(place[0]) + place.Substring(1);
            if (own && PlaceFirst) return place + " " + modifier;
            return modifier + " " + place;
        }

        /// <summary>Languages whose generated names put the place before its description.</summary>
        private static bool PlaceFirst
        {
            get { return Language == French || Language == Spanish || Language == Portuguese || Language == Italian; }
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
            { "name:Northern", "Kuzey" }, { "name:Southern", "Güney" }, { "name:Eastern", "Doğu" }, { "name:Western", "Batı" }, { "name:Uptown", "Yukarı Şehir" },
            { "Northern Line", "Kuzey Hattı" }, { "Southern Line", "Güney Hattı" }, { "Eastern Line", "Doğu Hattı" }, { "Western Line", "Batı Hattı" }, { "Uptown Line", "Yukarı Şehir Hattı" },
            { "name:Spring", "Bahar" }, { "name:Summer", "Yaz" }, { "name:Autumn", "Sonbahar" }, { "name:Winter", "Kış" }, { "name:Festival", "Festival" },
            { "Spring Line", "Bahar Hattı" }, { "Summer Line", "Yaz Hattı" }, { "Autumn Line", "Sonbahar Hattı" }, { "Winter Line", "Kış Hattı" }, { "Festival Line", "Festival Hattı" },
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
            { "Trains differ in length: each needs as many passengers as it has seats.", "Trenlerin boyu farklı: her tren koltuk sayısı kadar yolcu ister." },
            { "The number on a coming train is its seats.", "Gelen trenin üstündeki sayı koltuk sayısıdır." },
            { "A family travels together: they need two seats side by side.", "Aile birlikte yolculuk eder: yan yana iki koltuk ister." },
            { "No room on the train? They wait on the bench together.", "Trende yer yoksa bankta birlikte beklerler." },

            // Menu and HUD
            { "PLAY", "OYNA" },
            { "Settings", "Ayarlar" },
            { "Levels", "Seviyeler" },
            { "Shop", "Mağaza" },
            { "Language", "Dil" },
            { "Assisted taps", "Yardımlı dokunma" },
            { "Reduced motion", "Az hareket" },
            { "Fast trains", "Hızlı trenler" },
            { "Free +{0} coins", "Bedava +{0} jeton" },
            { "Watch a short video", "Kısa bir video izle" },
            { "Watch", "İzle" },
            { "Our Games", "Oyunlarımız" },
            { "Get", "İndir" },
            { "Try {0} too", "{0} oyununu da dene" },
            { "Free", "Ücretsiz" },
            { "Boosters", "Güçlendiriciler" },
            { "BEST VALUE", "EN İYİ FIRSAT" },
            { "Play without ad breaks", "Reklamsız oyna" },
            { "Done", "Alındı" },
            { "Daily Gift", "Hediye" },
            { "Lives", "Canlar" },
            { "Restart", "Yeniden Başlat" },
            { "Paused", "Duraklatıldı" },
            { "Continue  +1 platform", "Devam Et  +1 Peron" },
            { "Watch: free continue", "İzle: Ücretsiz Devam" },
            { "{0} passengers left!", "{0} yolcu kaldı!" },
            { "Hard Level", "Zor Seviye" },
            { "Super Hard Level", "Çok Zor Seviye" },
            { "Ready to open!", "Açılmaya hazır!" },
            { "{0} more stars to open", "Açılmasına {0} yıldız kaldı" },
            { "+{0}% more", "+%{0} daha fazla" },
            { "FREE", "ÜCRETSİZ" },
            { "Free coins", "Bedava Jeton" },
            { "Star Chest", "Yıldız Sandığı" },
            { "Star chest opened!", "Yıldız sandığı açıldı!" },
            { "{0} passengers", "{0} yolcu" },
            { "{0} moves", "{0} hamle" },
            { "Super!", "Süper!" },
            { "Great!", "Harika!" },
            { "Amazing!", "Muhteşem!" },
            { "Perfect!", "Mükemmel!" },
            { "Watch: +{0} coins", "İzle: +{0} jeton" },
            { "Full", "Dolu" },
            { "All lives are full!", "Canların dolu!" },
            { "Next life in {0}", "Sonraki can: {0}" },
            { "Unlimited lives: {0}", "Sonsuz can: {0}" },
            { "1 h unlimited  {0}", "1 saat sonsuz  {0}" },
            { "Unlimited lives started!", "Sonsuz can başladı!" },
            { "BEST", "EN İYİ" },
            { "Refill  {0}", "Doldur  {0}" },
            { "Lives refilled!", "Canlar doldu!" },
            { "Watch an ad: +1 life", "Reklam izle: +1 can" },
            { "+1 life", "+1 can" },
            { "Next gift\n{0:00}:{1:00}:{2:00}", "Sonraki hediye\n{0:00}:{1:00}:{2:00}" },
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
            { "Privacy", "Gizlilik" },
            { "Privacy options are not available right now", "Gizlilik seçenekleri şu an açılamıyor" },
            { "Credits", "Emeği Geçenler" },
            { "A StagFunny game", "Bir StagFunny oyunu" },
            { "Sound effects", "Ses efektleri" },
            { "Font", "Yazı tipi" },
            { "Software", "Yazılım" },
            { "Made with Unity", "Unity ile yapıldı" },
            { "Close", "Kapat" },

            // Results
            { "Level Complete!", "Seviye Tamamlandı!" },
            { "Continue", "Devam" },
            { "Menu", "Menü" },
            { "Try Again", "Tekrar Dene" },
            { "Out of Moves", "Hamle Kalmadı" },
            { "Station Jammed", "İstasyon Tıkandı" },
            { "passengers left", "yolcu kaldı" },
            { "Leaving the level also costs a life", "Seviyeden çıkmak da 1 can harcar" },
            { "The bench is full and no front passenger has a train.", "Bank dolu ve öndeki yolcuların treni burada değil." },
            { "No passenger can reach a platform.", "Hiçbir yolcu perona geçemiyor." },
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
            { "Starter Pack", "Başlangıç Paketi" },
            { "Traveller Bundle", "Yolcu Paketi" },
            { "Conductor Bundle", "Kondüktör Paketi" },
            { "Station Master Bundle", "İstasyon Şefi Paketi" },
            { "Bundles", "Paketler" },
            { "Coins", "Jetonlar" },
            { "ONE-TIME OFFER", "TEK SEFERLİK FIRSAT" },
            { "MOST POPULAR", "EN POPÜLER" },
            { "Ends in {0}", "Kalan süre {0}" },
            { "{0} h", "{0} sa" },
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
            { "A family needs two free seats", "Ailenin iki boş koltuğa ihtiyacı var" },
            { "Their train is not here yet and the bench is full", "Treni henüz gelmedi ve bank dolu" },
            { "Wait for the train to leave", "Trenin kalkmasını bekle" },

            // Platform captions
            { "CLOSED", "KAPALI" },
            { "Next trains", "Sıradaki trenler" },
            { "Last trains", "Bunlar son trenler" },
            { "The bench is full and no front passenger has a train. Undo a move or try again.", "Bank dolu ve öndeki yolcuların hiçbirinin treni burada değil. Bir hamleyi geri al ya da tekrar dene." },
            { "Tap a passenger: they board the train of their colour.", "Bir yolcuya dokun: kendi rengindeki trene biner." },
            { "A full train departs. Keep them coming!", "Dolan tren kalkar. Yolcuları göndermeye devam et!" },
            { "When a train leaves, the next one on the board pulls in.", "Bir tren kalkınca tablodaki sıradaki tren gelir." },
            { "Watch the next trains at the top.", "Üstteki sıradaki trenleri takip et." },
            { "No train for them yet? They wait on the bench.", "Treni henüz gelmedi mi? Yolcu bankta bekler." },
            { "Their train will collect them when it arrives.", "Treni gelince onu banktan alır." },
            { "If the bench fills up, the station jams. Choose who waits!", "Bank dolarsa istasyon tıkanır. Kimin bekleyeceğini iyi seç!" },
            { "Plan ahead: send the passengers whose trains come first.", "Önünü gör: treni önce gelecek yolcuları gönder." },
            { "Trains {0} / {1}", "Trenler {0} / {1}" },
            { "{0} line train", "{0} hat treni" },
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
