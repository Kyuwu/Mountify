<div align="center">
  <img src="Mountify/images/icon.png" alt="Mountify" width="128" />

  # Mountify

  **Spotify, in sync with your mount.**

  No more alt-tabbing to change your music mid-ride. No more missing the beat when you want to change your jam mid-pull.
  Mountify ties your Spotify playback to your mount state — it handles everything automatically.

</div>

---

## What it does

When you **mount**, Mountify can:
- Resume Spotify playback (or switch to a specific playlist)
- Mute in-game BGM and ambient sounds so your music comes through cleanly
- Fade volume back up to your preferred riding level
- Skip to the next track or enable shuffle — every ride starts fresh

When you **dismount**, Mountify can:
- Dim Spotify volume instead of pausing (music keeps playing, just quieter)
- Pause playback entirely
- Restore game audio after a short delay so the dismount fanfare gets to play
- Print the current track or a paused notice to chat

The live volume slider in the plugin window means you never need to open Spotify just to nudge the volume.

---

## Features

| Feature | Details |
|---|---|
| Auto-resume on mount | Resumes from where you left off, or switches to a configured playlist URI |
| Auto-pause / dim on dismount | Pause, or just lower the volume and keep playing |
| Fade transitions | Smooth volume ramps between mounted and dismounted levels |
| BGM & ambient mute | Cuts in-game music while mounted so Spotify sounds clean |
| Smart resume | Only resumes if Spotify was already playing when you last dismounted |
| Skip / shuffle on mount | Fresh track every ride |
| Live volume slider | Control Spotify volume right inside the plugin window |
| Mount delay | Wait a few seconds after mounting before music kicks in (hear the fanfare first) |
| Configurable poll interval | Balance responsiveness vs. API calls |
| Auto-disable rules | Combat, PvP, duties, cutscenes, crafting — Mountify stands down automatically |
| Chat notifications | Print track name or paused notice to /echo |
| `/mf` command | Full playback control from the chat box — no window needed |

---

## Setup

1. Go to [developer.spotify.com](https://developer.spotify.com) → Dashboard → **Create app**
2. Add `http://127.0.0.1:5004/callback` as a Redirect URI (use `127.0.0.1`, not `localhost`)
3. Copy your **Client ID**
4. Open Mountify in-game (`/mountify` or `/mf`), paste the Client ID, and click **Connect**
5. Log in through the browser that opens — done

Credentials are saved and restored automatically on every plugin reload. No need to re-auth.

---

## Commands

| Command | Action |
|---|---|
| `/mf` | Open / close the window |
| `/mf next` | Skip to next track |
| `/mf prev` | Skip to previous track |
| `/mf pause` | Pause |
| `/mf play` | Resume |
| `/mf toggle` | Toggle play/pause |
| `/mf vol <0-100>` | Set Spotify volume |
| `/mf track` | Print current track to chat |
| `/mf enable` / `/mf disable` | Toggle Mountify on or off |
| `/mf help` | List all subcommands |

---

## Requirements

- [Dalamud](https://github.com/goatcorp/Dalamud) plugin framework
- A Spotify account (free or Premium — volume control requires Spotify Premium)
- Windows

## Credits
- [Lightless](https://git.lightless-sync.org) theme made by abel <3
- Built on the [Dalamud](https://github.com/goatcorp/Dalamud) plugin framework
- Spotify integration via [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET)