using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using MetroSort.Game;
using NUnit.Framework;

namespace MetroSort.Tests
{
    /// <summary>
    /// Every language translates everything the Turkish table does (Turkish is the reference: it has been kept
    /// complete since the start), keeps the {n} placeholders of each text, and gives every level a name of its own.
    /// </summary>
    public class LocalisationTests
    {
        private static readonly Regex Placeholder = new Regex(@"\{\d+(:[^}]*)?\}");

        private static Dictionary<string, string> Table(string language)
        {
            string field = language == Loc.Turkish ? "Tr" : char.ToUpperInvariant(language[0]) + language.Substring(1);
            FieldInfo info = typeof(Loc).GetField(field, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(info, "No translation table for " + language);
            return (Dictionary<string, string>)info.GetValue(null);
        }

        private static IEnumerable<string> Translated()
        {
            foreach (string language in Loc.Languages)
                if (language != Loc.English) yield return language;
        }

        private static List<string> Placeholders(string text)
        {
            var found = new List<string>();
            foreach (Match m in Placeholder.Matches(text)) found.Add(m.Value);
            found.Sort();
            return found;
        }

        [Test]
        public void EveryLanguage_TranslatesEveryText()
        {
            Dictionary<string, string> reference = Table(Loc.Turkish);
            foreach (string language in Translated())
            {
                Dictionary<string, string> table = Table(language);
                var missing = new List<string>();
                foreach (string key in reference.Keys)
                    if (!table.ContainsKey(key)) missing.Add(key);
                Assert.IsEmpty(missing, language + " is missing: " + string.Join(" | ", missing));
            }
        }

        [Test]
        public void Translations_KeepTheirPlaceholders()
        {
            foreach (string language in Translated())
            {
                foreach (KeyValuePair<string, string> pair in Table(language))
                {
                    string where = language + ": \"" + pair.Key + "\" -> \"" + pair.Value + "\"";
                    Assert.IsFalse(string.IsNullOrWhiteSpace(pair.Value), where);
                    List<string> wanted = Placeholders(pair.Key);
                    CollectionAssert.AreEquivalent(wanted, Placeholders(pair.Value), where);
                    if (wanted.Count > 0) Assert.DoesNotThrow(() => string.Format(pair.Value, 1, 2, 3, 4), where);
                }
            }
        }

        [Test]
        public void EveryLevel_GetsANameInEveryLanguage()
        {
            var levels = new LevelManager();
            levels.Load();
            Assert.Greater(levels.Count, 1);
            try
            {
                foreach (string language in Translated())
                {
                    Loc.Init(language);
                    for (int id = 1; id <= levels.Count; id++)
                    {
                        string english = levels.Get(id).Name;
                        string name = Loc.LevelName(english);
                        Assert.AreNotEqual(english, name, language + ": level " + id + " keeps its English name");
                        // The name shares the menu line with "Level 123  -  ".
                        Assert.LessOrEqual(name.Length, 28, language + ": level " + id + " name is too long: " + name);
                    }
                }
            }
            finally
            {
                Loc.Init(Loc.English);
            }
        }

        [Test]
        public void PickedLanguage_Wins()
        {
            try
            {
                foreach (string language in Loc.Languages)
                {
                    Loc.Init(language);
                    Assert.AreEqual(language, Loc.Language);
                }
                // Never picked: the device language when the game speaks it, otherwise English.
                Loc.Init("");
                CollectionAssert.Contains(Loc.Languages, Loc.Language);
            }
            finally
            {
                Loc.Init(Loc.English);
            }
        }

        [Test]
        public void LanguagePicker_ShowsDistinctNativeNames()
        {
            var names = new HashSet<string>();
            foreach (string language in Loc.Languages)
                Assert.IsTrue(names.Add(Loc.NativeName(language)), "Two languages share the name " + Loc.NativeName(language));
        }
    }
}
