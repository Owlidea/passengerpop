using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MetroSort.Utilities;
using UnityEngine;
using UnityEngine.Networking;

namespace MetroSort.Game
{
    /// <summary>One of the studio's other games, as shown on the "Our Games" shelf.</summary>
    public sealed class StudioGame
    {
        public string Slug;
        public Dictionary<string, string> Name = new Dictionary<string, string>();
        public Dictionary<string, string> Tagline = new Dictionary<string, string>();
        public string IconUrl;
        /// <summary>The game's page in this phone's own store.</summary>
        public string StoreUrl;
        public Color Tint = Color.white;
        public Sprite Icon;

        public string LocalName { get { return Pick(Name); } }
        public string LocalTagline { get { return Pick(Tagline); } }

        private static string Pick(Dictionary<string, string> texts)
        {
            string value;
            if (texts.TryGetValue(Loc.Language, out value) && !string.IsNullOrEmpty(value)) return value;
            return texts.TryGetValue(Loc.English, out value) ? value : "";
        }
    }

    /// <summary>
    /// The studio's other games, for cross-promotion. The list lives on the studio site (built from the site's own
    /// catalogue), so a newly released game appears here without an app update. A copy shipped in Resources and
    /// the last downloaded copy cover a phone that is offline. This game leaves itself out, and so does any game
    /// without a page in this phone's store.
    /// </summary>
    public sealed class MoreGames : MonoBehaviour
    {
        public const string FeedUrl = "https://stagfunny.com/games.json";
        public const string SelfSlug = "passenger-pop";
        private const string CacheName = "more_games.json";

        public readonly List<StudioGame> Games = new List<StudioGame>();
        /// <summary>Raised when the list or an icon changes.</summary>
        public event Action Changed;

        private void Start()
        {
            TextAsset shipped = Resources.Load<TextAsset>("more_games");
            if (shipped != null) Apply(shipped.text);
            try
            {
                string cached = Path.Combine(Application.persistentDataPath, CacheName);
                if (File.Exists(cached)) Apply(File.ReadAllText(cached));
            }
            catch (Exception) { /* a broken cache file is simply ignored */ }
            StartCoroutine(Refresh());
        }

        private IEnumerator Refresh()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(FeedUrl))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success && Apply(request.downloadHandler.text))
                {
                    try { File.WriteAllText(Path.Combine(Application.persistentDataPath, CacheName), request.downloadHandler.text); }
                    catch (Exception) { }
                }
            }
            foreach (StudioGame game in Games.ToArray())
            {
                if (game.Icon != null || string.IsNullOrEmpty(game.IconUrl)) continue;
                string file = Path.Combine(Application.persistentDataPath, "more_games_" + game.Slug + ".png");
                byte[] bytes = null;
                try { if (File.Exists(file)) bytes = File.ReadAllBytes(file); } catch (Exception) { }
                if (bytes == null)
                {
                    using (UnityWebRequest request = UnityWebRequest.Get(game.IconUrl))
                    {
                        request.timeout = 15;
                        yield return request.SendWebRequest();
                        if (request.result != UnityWebRequest.Result.Success) continue;
                        bytes = request.downloadHandler.data;
                        try { File.WriteAllBytes(file, bytes); } catch (Exception) { }
                    }
                }
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes)) continue;
                texture.wrapMode = TextureWrapMode.Clamp;
                game.Icon = ToSprite(texture);
                if (Changed != null) Changed();
            }
        }

        /// <summary>Replaces the list with the feed's content. False when the text is not a usable feed.</summary>
        private bool Apply(string json)
        {
            List<StudioGame> parsed;
            try { parsed = Parse(json); }
            catch (Exception) { return false; }
            if (parsed == null) return false;
            // Icons already loaded stay with their games.
            foreach (StudioGame game in parsed)
            {
                StudioGame old = Games.Find(g => g.Slug == game.Slug);
                if (old != null) game.Icon = old.Icon;
                if (game.Icon == null)
                {
                    Texture2D shipped = Resources.Load<Texture2D>("MoreGames/" + game.Slug);
                    if (shipped != null) game.Icon = ToSprite(shipped);
                }
            }
            Games.Clear();
            Games.AddRange(parsed);
            if (Changed != null) Changed();
            return true;
        }

        private static Sprite ToSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Reads a feed: the games that can be offered on this phone, in the feed's order.</summary>
        public static List<StudioGame> Parse(string json)
        {
            Dictionary<string, object> root = JsonRead.AsDict(MiniJson.Parse(json));
            if (root == null) return null;
            var result = new List<StudioGame>();
            foreach (object entry in JsonRead.List(root, "games"))
            {
                Dictionary<string, object> d = JsonRead.AsDict(entry);
                if (d == null) continue;
                string slug = JsonRead.Str(d, "slug", "");
                if (slug.Length == 0 || slug == SelfSlug) continue;
                string store = StoreLink(JsonRead.Str(d, "ios", ""), JsonRead.Str(d, "android", ""));
                if (string.IsNullOrEmpty(store) || !store.StartsWith("https://")) continue;
                var game = new StudioGame { Slug = slug, StoreUrl = store, IconUrl = JsonRead.Str(d, "icon", "") };
                ReadTexts(d, "name", game.Name);
                ReadTexts(d, "tagline", game.Tagline);
                if (game.LocalName.Length == 0 && !game.Name.ContainsKey(Loc.English)) continue;
                Color tint;
                if (ColorUtility.TryParseHtmlString(JsonRead.Str(d, "color", ""), out tint)) game.Tint = tint;
                result.Add(game);
            }
            return result;
        }

        private static string StoreLink(string ios, string android)
        {
#if UNITY_IOS
            return ios;
#elif UNITY_ANDROID
            return android;
#else
            return string.IsNullOrEmpty(android) ? ios : android;
#endif
        }

        private static void ReadTexts(Dictionary<string, object> d, string key, Dictionary<string, string> into)
        {
            object value;
            if (!d.TryGetValue(key, out value)) return;
            Dictionary<string, object> texts = JsonRead.AsDict(value);
            if (texts == null) return;
            foreach (KeyValuePair<string, object> pair in texts)
                if (pair.Value is string) into[pair.Key] = (string)pair.Value;
        }
    }
}
