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
            "Central Line", "Old Town Line", "Garden Line", "Palace Line", "Harbour Line", "Misty Line", "Night Line", "Grand Line", "Polar Line", "Capital Line",
            "Coastal Line", "Desert Line", "Alpine Line", "Tropic Line", "Royal Line",
            "Lakeside Line", "Canyon Line", "Island Line", "Aurora Line", "Golden Line",
            "Crimson Line", "Emerald Line", "Sapphire Line", "Amber Line", "Ivory Line",
            "Northern Line", "Southern Line", "Eastern Line", "Western Line", "Uptown Line",
            "Spring Line", "Summer Line", "Autumn Line", "Winter Line", "Festival Line"
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
            Make(new Color(0.56f, 0.20f, 0.27f), new Color(0.90f, 0.86f, 0.78f), new Color(0.72f, 0.90f, 0.86f)), // burgundy, marble and mint
            Make(new Color(0.22f, 0.62f, 0.78f), new Color(0.88f, 0.93f, 0.97f), new Color(1.00f, 0.84f, 0.70f)), // ice blue, snow and peach
            Make(new Color(0.78f, 0.60f, 0.18f), new Color(0.93f, 0.90f, 0.82f), new Color(0.72f, 0.80f, 0.97f)), // gold, ivory and royal blue
            Make(new Color(0.10f, 0.60f, 0.62f), new Color(0.93f, 0.88f, 0.74f), new Color(0.66f, 0.90f, 0.92f)), // turquoise, sand and aqua
            Make(new Color(0.80f, 0.45f, 0.20f), new Color(0.92f, 0.82f, 0.62f), new Color(0.70f, 0.86f, 0.84f)), // desert orange and oasis teal
            Make(new Color(0.18f, 0.42f, 0.34f), new Color(0.80f, 0.82f, 0.84f), new Color(0.86f, 0.94f, 0.80f)), // pine green and stone
            Make(new Color(0.78f, 0.24f, 0.50f), new Color(0.90f, 0.94f, 0.84f), new Color(1.00f, 0.90f, 0.55f)), // magenta, leaf and mango
            Make(new Color(0.36f, 0.20f, 0.56f), new Color(0.90f, 0.86f, 0.92f), new Color(0.98f, 0.86f, 0.50f)), // royal purple and gold
            Make(new Color(0.24f, 0.46f, 0.74f), new Color(0.84f, 0.90f, 0.88f), new Color(0.98f, 0.92f, 0.70f)), // lake blue, reed and sunlight
            Make(new Color(0.62f, 0.28f, 0.20f), new Color(0.90f, 0.76f, 0.64f), new Color(0.74f, 0.88f, 0.96f)), // red rock and sky
            Make(new Color(0.96f, 0.56f, 0.40f), new Color(0.95f, 0.93f, 0.84f), new Color(0.62f, 0.90f, 0.86f)), // coral, shell and lagoon
            Make(new Color(0.16f, 0.30f, 0.42f), new Color(0.62f, 0.70f, 0.78f), new Color(0.62f, 0.96f, 0.78f)), // night teal and aurora green
            Make(new Color(0.70f, 0.52f, 0.12f), new Color(0.96f, 0.92f, 0.78f), new Color(1.00f, 0.80f, 0.74f)), // old gold and rose
            Make(new Color(0.72f, 0.12f, 0.18f), new Color(0.94f, 0.88f, 0.86f), new Color(0.80f, 0.90f, 0.98f)), // crimson and pale blue
            Make(new Color(0.08f, 0.52f, 0.36f), new Color(0.86f, 0.92f, 0.86f), new Color(0.99f, 0.90f, 0.66f)), // emerald and cream
            Make(new Color(0.12f, 0.24f, 0.68f), new Color(0.84f, 0.88f, 0.96f), new Color(0.74f, 0.94f, 0.96f)), // sapphire and ice
            Make(new Color(0.86f, 0.52f, 0.08f), new Color(0.96f, 0.90f, 0.76f), new Color(0.82f, 0.88f, 0.70f)), // amber and sage
            Make(new Color(0.42f, 0.38f, 0.34f), new Color(0.97f, 0.95f, 0.90f), new Color(0.90f, 0.80f, 0.96f)), // ivory, charcoal and lilac
            Make(new Color(0.30f, 0.50f, 0.66f), new Color(0.90f, 0.93f, 0.95f), new Color(0.98f, 0.86f, 0.80f)), // northern steel blue
            Make(new Color(0.92f, 0.40f, 0.30f), new Color(0.97f, 0.90f, 0.78f), new Color(0.70f, 0.92f, 0.82f)), // southern coral
            Make(new Color(0.50f, 0.26f, 0.60f), new Color(0.92f, 0.88f, 0.84f), new Color(0.98f, 0.88f, 0.56f)), // eastern plum and saffron
            Make(new Color(0.60f, 0.40f, 0.24f), new Color(0.88f, 0.84f, 0.74f), new Color(0.70f, 0.84f, 0.96f)), // western leather and sky
            Make(new Color(0.12f, 0.14f, 0.20f), new Color(0.80f, 0.83f, 0.88f), new Color(0.56f, 0.94f, 0.92f)), // uptown black and neon
            Make(new Color(0.94f, 0.52f, 0.66f), new Color(0.92f, 0.96f, 0.88f), new Color(0.80f, 0.94f, 0.72f)), // blossom pink and new leaf
            Make(new Color(0.98f, 0.70f, 0.14f), new Color(0.96f, 0.94f, 0.80f), new Color(0.60f, 0.86f, 0.98f)), // sun yellow and pool blue
            Make(new Color(0.72f, 0.34f, 0.12f), new Color(0.92f, 0.84f, 0.70f), new Color(0.98f, 0.78f, 0.46f)), // rust and maple
            Make(new Color(0.40f, 0.56f, 0.72f), new Color(0.94f, 0.96f, 0.99f), new Color(0.80f, 0.90f, 1.00f)), // frost blue and snow
            Make(new Color(0.84f, 0.16f, 0.44f), new Color(0.96f, 0.92f, 0.86f), new Color(0.66f, 0.94f, 0.80f))  // festival pink and mint
        };

        /// <summary>
        /// Which painted station wall (Resources/Textures/wall_N) a chapter uses, or -1 for the modelled wall.
        /// Paintings exist for the first five lines and for Crimson to Uptown (chapters 20-29).
        /// </summary>
        public static int PaintingFor(int chapter)
        {
            if (chapter >= 0 && chapter < 5) return chapter;
            if (chapter >= 20 && chapter < 30) return chapter - 15;
            return -1;
        }

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
