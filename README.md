# Trainer Deck

A full-screen, controller-friendly launcher for FLiNG game trainers. It's made for the ROG Ally X in Windows 11's Xbox full-screen experience, and works just as well on a desktop with a mouse and keyboard.

![Trainer Deck](art/hero-1920x1080.png)

- Scans your trainer folders and turns `ELDEN RING v1.02-v1.16 Plus 35 Trainer.exe` into **Elden Ring**, with **v1.02-v1.16** and **+35** shown on the card.
- Launches trainers as administrator, shows which ones are running, and can close them.
- Navigates fully with a controller (Xbox, the Ally's built-in pad, PlayStation and other DirectInput pads) or with keyboard and mouse.
- Ignores the controller unless Trainer Deck is the window in front, so buttons you press in a game never trigger anything here.
- Uses cover art from games installed through Steam (read locally, nothing downloaded) and can launch those games. Downloading art for other games is opt-in.
- Portable: settings and caches live next to the exe.

## Getting started

1. Build or download `TrainerDeck.exe` and put it in its own folder, e.g. `D:\Tools\Trainer Deck\`.
2. Run it once. A `Trainers` folder is created next to it.
3. Put your trainers (or their `.zip` files) in `Trainers`. Subfolders are fine. The library updates by itself.
4. Optional: add more trainer folders in **Settings › Add a trainer folder…**.

### Folder layout

```
Trainer Deck\
├── TrainerDeck.exe
├── Trainers\                 ← default trainer folder (scanned recursively)
│   ├── Elden Ring v1.16 Plus 35 Trainer.exe
│   └── Hades II\
│       └── Hades II Early Access Plus 20 Trainer.exe
├── settings.json             ← settings
├── library.json              ← favourites, renames, hidden, launch history, Steam links
├── art\                      ← images you picked with “Change art”
├── cache\                    ← icon and Steam art cache (safe to delete)
└── trainerdeck.log           ← rolling log (trainerdeck.1.log … .3.log)
```

If the exe's folder isn't writable (for example under `Program Files`), data goes to `%LOCALAPPDATA%\TrainerDeck` instead.

### Antivirus

Trainers modify game memory, so antivirus tools often flag them or quietly delete them. Trainer Deck shows a one-time tip about this. If trainers keep disappearing, add your trainer folder to **Windows Security › Virus & threat protection › Manage settings › Exclusions**. Only do this for trainers from a source you trust. When a trainer goes missing, Trainer Deck tells you it may have been quarantined.

## Controls

| Action | Controller | Keyboard |
|---|---|---|
| Move | D-pad / left stick (holding repeats) | Arrow keys |
| Launch / select (opens Cheats on a running trainer) | A (✕) | Enter |
| Back / clear search | B (○) | Esc |
| Favourite | X (□) | Ctrl+D |
| Details | Y (△) | Ctrl+I |
| Switch tab (All / Favourites / Recent) | Tap LB / RB (L1 / R1) | Ctrl+Tab |
| Jump to a letter (A–Z) | **Hold** LB or RB, pick with the d-pad, release | Just type to search |
| Page up / down | LT / RT (L2 / R2) | PgUp / PgDn |
| Change sort | View (Share) | Ctrl+S |
| Settings | Menu (Options) | Ctrl+, |
| Rescan | Settings › Rescan now | F5 |
| Full screen on/off | | F11 or Alt+Enter |

The footer always shows the buttons for whatever you used last: Xbox, PlayStation, generic pad or keyboard.

With a mouse: double-click a card to launch it, right-click for more options, and use the wheel to scroll.

### Back paddles on the ROG Ally X

The Ally X's back paddles can send keyboard keys, which is handy for trainer hotkeys. Most FLiNG trainers use **Numpad** keys (Numpad 1–9, often with Ctrl/Alt). In **Armoury Crate SE › Control Mode › Gamepad › Edit layout**, map the back buttons to the numpad keys of the cheats you use most, e.g. M1 → Numpad 1 (god mode) and M2 → Numpad 2 (infinite stamina).

## Launching

- **Launch:** trainers start as administrator with their own folder as the working directory. Most trainers need admin rights, so Windows shows a UAC prompt. Cancelling it does nothing.
- **After launch:** Trainer Deck brings the trainer's window to the front. Then it stays open, minimises (the default) or closes, per **Settings › After launching a trainer**.
- **Running trainers:** a green **Running** badge shows on each one, including trainers you started another way. Press A on a running trainer to switch to it. **Close trainer** is on the details page (Y) and the right-click menu.
- **Run Trainer Deck as administrator** (Settings): Trainer Deck asks for admin rights once when it starts, so trainers launch without a prompt each time. This also lets Close trainer work on any trainer. The trade-off is one UAC prompt every time Trainer Deck starts. Steam games are still started without admin rights.

## Cheats panel (controller remote for trainers)

FLiNG trainers expect a keyboard with a numpad. Trainer Deck lets you use them with a controller instead:

- **Opening it:** press **A** on a running trainer, or pick **Cheats** on its details page, to open its cheat list. Each cheat shows its hotkey, its on/off state and its value.
- **A:** turns a cheat on or off, or runs a one-off action such as "Edit Runes".
- **←/→:** adjust multipliers (game speed, damage…).
- **Y:** type a value, or pick from a list (teleport spots, save locations). These lists fill in once the game is running.
- **X:** assign a controller shortcut (see below).

**How it works:**
- **Current FLiNG trainers:** Trainer Deck reads the trainer's window through Windows accessibility (UI Automation) and presses the trainer's own switches. What you see is the trainer's real state, kept in sync even if you also use the keyboard.
- **Older trainers:** for example Hades v1.38, Trainer Deck reads the cheat names and presses their hotkeys for you. It can't see whether a cheat is on, so listen for the trainer's beep.
- **Administrator rights:** trainers run as administrator, and Windows only lets a program read another program's window when it has the same rights. Turn on **Settings › Run Trainer Deck as administrator** to get the full panel (the panel offers a one-click restart). Without it, the panel falls back to pressing hotkeys, using the cheat list from the last time the trainer was read.

### In-game controller shortcuts

Turn on **Settings › Cheats › In-game controller shortcuts**, then assign shortcuts per cheat with **X** in the Cheats panel. While you play, hold the shortcut button (View by default; LB + RB, a stick click or View + Menu also work) and press the assigned button.

- **Background reading:** Trainer Deck reads the controller in the background only while shortcuts are on *and* a trainer with shortcuts assigned is running. Everything else is still ignored when Trainer Deck isn't in front.
- **The game sees the buttons too.** Pick combinations the game doesn't use at the same time, or a shortcut button the game ignores.

## Steam

- **Use games installed with Steam** (on by default): Trainer Deck reads your Steam folder to find installed games. A trainer whose name matches an installed game gets that game's library art and a **Play** button on its details page. Nothing is downloaded.
- **Download art from Steam** (off by default): games you don't have installed are looked up on the Steam store and their library art is downloaded into `cache\steam`.
- **Wrong match?** Open the trainer's details and pick **Unlink Steam game**, or rename the trainer to the exact Steam name.

## Adding Trainer Deck to the Xbox app and Armoury Crate

### Xbox app (Xbox full-screen experience)

1. Open the **Xbox** app on the desktop.
2. Go to **My Library**, select the **+** button (Add a game from your PC), and browse to `TrainerDeck.exe`.
3. Trainer Deck now appears in your library and in the full-screen experience. It opens in full screen by default.

### Armoury Crate SE (ROG Ally)

1. Open **Armoury Crate SE › Game Library**.
2. Choose **Add game / Scan** (wording varies by version) and browse to `TrainerDeck.exe`.
3. To give it a cover, use the artwork in [`art/`](art): `cover-600x900.png` (portrait), `hero-1920x1080.png` (banner) and `logo-512.png`. These also work for Steam non-Steam shortcuts and launchers like Playnite.

The artwork and the app icon are drawn in code. To regenerate them, run:

```powershell
TrainerDeck.exe --export-art art
```

## Building

Requires the **.NET 8 SDK** on Windows.

```powershell
dotnet build
dotnet test
```

### Publish (single-file exe)

```powershell
dotnet publish src/TrainerDeck -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:DebugType=None -o publish
```

`publish\TrainerDeck.exe` is self-contained (about 70 MB). Users don't need .NET installed. Copy it anywhere and run it.

### Project layout

```
src/TrainerDeck.Core     no WPF; unit-tested
  Library/   FLiNG name parser, scanner, ignore rules, zip import, library.json store, queries
  Input/     gamepad state, stick digitiser, focus gate, input router, mappings, Sony/HID parsers
  Launch/    launch results and errors, running-trainer registry, startup options
  Steam/     VDF parser, Steam library/manifests, name matching
  Settings/  settings.json, portable paths
src/TrainerDeck          WPF app (MVVM, CommunityToolkit.Mvvm)
  Input/     XInput + raw HID polling, foreground check
  Launch/    ShellExecuteEx runas, process tracking, single instance
  Steam/     Steam location, art cache and downloads, game launching
  Art/       icon extraction, cover/hero loading, generated brand art
  UI/        theme, views, view models, controls
tests/TrainerDeck.Tests  xUnit
```

## Troubleshooting

- **A trainer is missing from the library:** check it isn't hidden (**Settings › Hidden trainers**) or filtered out (**Settings › Ignore patterns**). Installers and uninstallers (`unins000.exe`, `*setup*`) are skipped on purpose. Also check that your antivirus didn't remove it.
- **The controller does nothing:** Trainer Deck only reads the controller while its own window is in front and not minimised. Click into it or switch to it first.
- **Double inputs with a PlayStation pad:** Steam's desktop controller layout may be turning the pad into keyboard input. Turn off Steam Input for the desktop layout.
- **Logs:** everything worth knowing goes to `trainerdeck.log` next to the exe.
