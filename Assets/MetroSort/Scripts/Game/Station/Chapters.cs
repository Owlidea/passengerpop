using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>Colours of one station style. Purely cosmetic.</summary>
    public sealed class StationTheme
    {
        public Color Dado, Joint, Floor, Tile, Lane, Wall, Pillar;
    }

    /// <summary>
    /// Levels are grouped into chapters of twenty. Each chapter is a metro line with its own station look,
    /// so progress feels like travelling through a network.
    /// </summary>
    public static class Chapters
    {
        public const int Size = 20;

        public static readonly string[] Names =
        {
            "Central Line", "Old Town Line", "Garden Line", "Palace Line", "Harbour Line", "Misty Line", "Night Line", "Grand Line"
        };

        public static readonly StationTheme[] Themes =
        {
            Make(new Color(0.28f, 0.55f, 0.62f), new Color(0.82f, 0.80f, 0.76f), new Color(0.66f, 0.78f, 0.93f)), // teal
            Make(new Color(0.78f, 0.44f, 0.32f), new Color(0.86f, 0.80f, 0.72f), new Color(0.97f, 0.82f, 0.62f)), // terracotta
            Make(new Color(0.33f, 0.58f, 0.40f), new Color(0.80f, 0.82f, 0.75f), new Color(0.74f, 0.90f, 0.72f)), // green
            Make(new Color(0.50f, 0.38f, 0.70f), new Color(0.81f, 0.79f, 0.85f), new Color(0.84f, 0.78f, 0.96f)), // purple
            Make(new Color(0.20f, 0.32f, 0.56f), new Color(0.78f, 0.81f, 0.85f), new Color(0.98f, 0.88f, 0.60f)), // navy and gold
            Make(new Color(0.42f, 0.46f, 0.54f), new Color(0.72f, 0.75f, 0.80f), new Color(0.82f, 0.86f, 0.92f)), // misty grey
            Make(new Color(0.16f, 0.18f, 0.36f), new Color(0.50f, 0.53f, 0.66f), new Color(0.99f, 0.80f, 0.42f)), // night indigo and lamp amber
            Make(new Color(0.56f, 0.20f, 0.27f), new Color(0.90f, 0.86f, 0.78f), new Color(0.72f, 0.90f, 0.86f))  // burgundy, marble and mint
        };

        public static int IndexFor(int levelId)
        {
            return Mathf.Clamp((levelId - 1) / Size, 0, Names.Length - 1);
        }

        private static StationTheme Make(Color dado, Color floor, Color lane)
        {
            return new StationTheme
            {
                Dado = dado,
                Joint = Color.Lerp(dado, Color.black, 0.18f),
                Floor = floor,
                Tile = Color.Lerp(floor, Color.black, 0.10f),
                Lane = lane,
                Wall = Color.Lerp(floor, Color.white, 0.25f),
                Pillar = Color.Lerp(floor, new Color(0.55f, 0.60f, 0.72f), 0.45f)
            };
        }
    }
}
