<div align="center">
  <img src="Mountify/images/icon.png" alt="Mountify" width="128" />

  # Mountify

  Spotify, in sync with your mount. No more alt-tabbing to skip a track mid-ride or changing your jam when you're about to pull.

</div>

---

## What it does

Mount up → Spotify plays. Dismount → Spotify pauses (or dims). That's the core of it.

On top of that you get BGM muting so game music doesn't bleed through, a live volume slider inside the plugin window, per-playlist switching on mount, and a bunch of toggles to make it behave exactly how you want.

**On mount:**
- Resume Spotify (or switch to a specific playlist)
- Mute in-game BGM / ambient sounds
- Fade volume up to your riding level
- Skip to next track, enable shuffle, wait a few seconds first — all optional

**On dismount:**
- Pause, or just dim the volume and keep it playing
- Restore game audio after a delay so the dismount fanfare gets to play
- Print the current track to chat if you want

---

## Install

1. Open Dalamud settings → **Experimental** → paste this URL under **Custom Plugin Repositories:**
   ```
   https://raw.githubusercontent.com/Kyuwu/Mountify/main/repo.json
   ```
2. Click **Save & Close**, then search for **Mountify** in the plugin installer and install it.

---

## Spotify Setup

One-time setup the first time you use it:

1. Go to [developer.spotify.com](https://developer.spotify.com) → Dashboard → **Create app** (any name is fine)
2. In the app settings, add this as a Redirect URI and hit Save:
   ```
   http://127.0.0.1:5004/callback
   ```
   (use `127.0.0.1` — not `localhost`)
3. Copy your **Client ID** from the app overview
4. Open Mountify in-game (`/mountify` or `/mf`), paste the Client ID, click **Connect**, and log in through the browser

Your credentials are saved and restored on every reload. You won't need to do this again.

---

## Commands

| Command | What it does |
|---|---|
| `/mf` | Open / close the window |
| `/mf next` | Next track |
| `/mf prev` | Previous track |
| `/mf pause` | Pause |
| `/mf play` | Resume |
| `/mf toggle` | Toggle play/pause |
| `/mf vol <0-100>` | Set volume |
| `/mf track` | Print current track to chat |
| `/mf enable` / `/mf disable` | Toggle Mountify on or off |
| `/mf help` | Show all subcommands |

---

## Requirements

- Dalamud plugin framework
- Spotify account — volume control requires Premium
- Windows

---

## Credits

- [Lightless](https://git.lightless-sync.org) theme made by abel <3
- Built on the [Dalamud](https://github.com/goatcorp/Dalamud) plugin framework
- Spotify integration via [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET)
