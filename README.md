# Direct Connect

Direct Connect adds a **Join via IP** option to BIG WALK's *Join Game* page, lowers network latency for direct sessions and tries to hide the game's warning banners about connection issues with a single key.

This project is based on [N2N Direct Connect](https://thunderstore.io/c/big-walk/p/lajilaji/N2NDirectConnect/) by **lajilaji**.

> [!WARNING]
> **This mod was created with the help of AI (Claude by Anthropic).**
> The code was generated and refactored with an AI assistant and tested in-game by the author, but it has not been audited line by line. Use it at your own risk and please report anything that behaves strangely.

## Features

- **Join via IP** – a new section between *Join via code* and *Join via friends list*, styled like the game's own UI:
  - IPv4 (`192.168.1.10`), IPv6 (`2001:db8::1`) or a domain name (`game.example.com`, `localhost`).
  - Password field
  - Domain names are resolved in the background; the section greys out with a loading indicator meanwhile.
  - *Join via code* and the friends list keep working exactly as before.
- **Lower latency for IP sessions** – raises Mirror's send rate from 30 Hz to 60 Hz (configurable up to 120 Hz) and sends reliable data every frame instead of every 10 ms. 
> **WARNING:** Every user should have the exact same send rate as it can cause issues.
- **Hide warnings** – press **F5** to hide/show the game's *"WARNING: …"* banners (e.g. *"No network connection"*, which concerns Epic Online Services, not direct sessions).
- **Translated** into 15 languages; follows the game's language automatically.

## Manual Installation

1. Install **BepInEx 6 (Bleeding Edge) – Unity IL2CPP x64** into the BIG WALK game folder.
2. Start the game **once** and close it, so BepInEx can generate its files.
3. Copy `BigWalk.DirectConnect.dll` to `BIG WALK\BepInEx\plugins\BigWalk.DirectConnect\`.
4. Start the game – the *Join Game* page now has a **Join via IP** section.

**Linux / Steam Deck (Proton):** add this to the game's launch options:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

## Configuration

Settings are in `BepInEx\config\kamiloo13.bigwalk.direct-connect.cfg` (created on first launch).

| Section | Setting | Default | Description |
|---|---|---|---|
| General | `Language` | `auto` | Language of the mod's texts (`auto` or a code like `en`, `pl`, `de`). |
| Network | `SendRateMode` | `DirectIpOnly` | When to raise the send rate: `DirectIpOnly`, `Always` or `Off`. |
| Network | `SendRate` | `60` | Send rate in Hz (30–120). |
| Network | `FlushKcpEveryFrame` | `true` | Send reliable data every frame. |
| Warnings | `ToggleKey` | `F5` | Key that hides/shows warning banners. |
| Warnings | `HiddenOnStart` | `false` | Hide warning banners from the start. |

## Important

- **THE HOST MUST BE REACHABLE ON UDP PORT 7777.** Either forward the port on the host's router or use a VPN such as Tailscale, ZeroTier or similar and join with the VPN address.
- **THE HOST AND EVERYONE JOINING BY IP SHOULD USE THE MOD WITH THE SAME `SendRate`.** Mismatched values can cause jittery movement.
- Joining by IP works between PC players only; console players can still join via code.
- **BEFORE REPORTING A BUG TO THE GAME'S DEVELOPERS, UNINSTALL ALL MODS** and check that the problem still happens. Mod issues should be reported to the mod author, not to House House.
- A higher send rate uses more bandwidth and a little more CPU on the host.

## Help

- **Where is my game folder?** In Steam: right-click BIG WALK → *Manage* → *Browse local files*.
- **Something doesn't work?** Check `BepInEx\LogOutput.log` – every part of the mod logs what it does (look for `Direct Connect`).
- **Warning banner not hidden?** Press F5 and look for `Hiding banner` lines in the log.

## Building from source

1. .NET SDK 6+; BepInEx installed in the game and the game started once (`BepInEx\interop` must exist).
2. Check `GamePath` in `BigWalk.DirectConnect.csproj` (or pass `-p:GamePath="..."`).
3. `dotnet build -c Release` in this folder (or open `BigWalk.DirectConnect.slnx`).

The DLL is copied to `BepInEx\plugins\BigWalk.DirectConnect\` automatically.

```
Plugin.cs          entry point + configuration
ModController.cs   per-frame driver
UI/                Join Game page: patch, section builder, layout and input-field helpers
Localization/      translations + game/system language detection
Networking/        address validation, DNS resolution, connecting, password auto-fill
Tuning/            send rate tuner, KCP flush
Warnings/          warning-banner hiding + key input (legacy Input Manager / Input System)
Common/            reflection helpers
```

## Credits

- **lajilaji** – original [N2N Direct Connect](https://thunderstore.io/c/big-walk/p/lajilaji/N2NDirectConnect/) mod.
- Code written with the assistance of **Claude** (Anthropic).
- BIG WALK by House House, published by Panic. This mod is not affiliated with or endorsed by them.