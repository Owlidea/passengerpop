# "Our Games" shelf: the same in every StagFunny game

Each game shows the studio's other games in two places. The list is not built into the games: it comes from the
studio site, so a newly released game appears everywhere without an app update.

Reference implementation: Passenger Pop (Unity), `Assets/MetroSort/Scripts/Game/Monetization/MoreGames.cs` and
the "the studio's other games" part of `Assets/MetroSort/Scripts/Game/UI/UIManager.cs`.

## The feed

`GET https://stagfunny.com/games.json` (built from `src/data/games.ts` of the site repo, `~/Desktop/stagfunny`).

```json
{
  "version": 1,
  "games": [
    {
      "slug": "slingshire",
      "name": { "en": "Slingshire" },
      "tagline": { "en": "One shot. A whole castle.", "tr": "Tek atış. Koca bir kale." },
      "status": "live",
      "icon": "https://stagfunny.com/games/slingshire/icon.png",
      "page": "https://stagfunny.com/games/slingshire/",
      "color": "#FFA41B",
      "ios": "https://apps.apple.com/app/id6832535285",
      "android": "https://play.google.com/store/apps/details?id=com.owlidea.slingshire"
    }
  ]
}
```

- `name` and `tagline` are per language (`en`, `tr`, `de`, `es`, `fr`, `pt`); `en` is always there. Use the
  game's own language and fall back to `en`.
- `ios` and `android` are store links, or `null` while the game is not in that store.
- `icon` is a 256 px PNG.

Slugs: `slingshire`, `passenger-pop`, `pofu`, `harf-kombo`.

## Rules every game follows

1. **Leave yourself out** (compare `slug` with the game's own).
2. **Show only games with a link for this phone's store** (`ios` on iPhone, `android` on Android). A game with
   no link is not shown at all, so nothing is ever a dead button.
3. **Never block on the network.** Start from a copy of the feed shipped in the app, then the last downloaded
   copy, then fetch (10 s timeout) and save. Icons are cached on disk too. Offline, the shelf still works.
4. **Hide the entry point when the list is empty.**
5. **Open the store with the plain link** (the system browser / store app). No reward for installing: Apple does
   not allow paying players to download other apps.
6. Only on menus and between levels, never during play, and not on a screen that is about to show an
   interstitial ad.

## Where it shows

- **Main menu:** a quiet "Our Games" button; it opens a panel with one row per game: icon, name, tagline and a
  "Get" button.
- **Win screen:** on every fifth level, one slim row under the buttons: icon, "Try {name} too", "Get". It
  cycles through the games.

Texts (translate into every language the game ships): "Our Games", "Get", "Try {0} too".

Turkish: "Oyunlarımız", "İndir", "{0} oyununu da dene".

## Design

- White rounded card rows on the game's own panel style; the game's usual green call-to-action for "Get".
- No glossy highlight bands on buttons or behind icons (studio-wide rule).
