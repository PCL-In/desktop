# PCL-In

> A fork of **PCL Community Edition (PCL CE)**.

PCL-In is a Minecraft launcher fork tailored for non-premium users (especially those using third-party skin servers).

## What differs from upstream PCL CE

- **No forced trial mode**: Players using third-party skin servers (Authlib / YggdrasilConnect) or offline profiles no longer get forced into the limited demo mode.
- **No donation prompts**: No donation popups, no links to upstream author's Afdian page.
- **Independent update channel**: Uses GitHub Releases API to detect new releases of this fork, never overwrites your fork's modifications. When the check fails it reports the failure honestly instead of pretending you are on the latest version.
- **Built-in chat**: A new "Chat" tab in the top navigation embeds [MiniChat](https://minichat.astras.cc), so you do not have to open a browser; hide it any time under Settings → Feature hiding.
- **More resilient multiplayer**: If no announcement server is configured, or it cannot be reached, multiplayer is no longer marked unavailable — PCL-In falls back to local defaults and you can still play peer-to-peer through EasyTier / Träwelling-style relays (陶瓦).
- **UI details**: Both the top bar and the left rail can be switched to icon-only; scrollbars stay hidden until you hover them.

## Download

Visit the [Releases](https://github.com/PCL-In/desktop/releases) page: pick `PCL-In-x64.exe` for 64-bit systems, or `PCL-In-arm64.exe` for ARM64.

Every package ships with a `.sha256` checksum and a GPG signature (`.asc`).

## System Requirements

- Windows 10 1809 (build 17763) or later
- [.NET 10 Desktop Runtime](https://get.dot.net/10)
- (Optional) the built-in chat needs the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/): Windows 11 and Windows 10 with a recent Edge already have it. If it is missing the chat page offers to open in your system browser and nothing else is affected

## Credits

This project is a fork of [PCL Community Edition (PCL CE)](https://github.com/PCL-Community/PCL-CE). All rights belong to the original author [龙腾猫跃](https://github.com/Meloong-Git/PCL) and [成都瓜皮龙科技有限公司](https://www.pclc.cc/).

Third-party component licenses are listed in the `licenses[]` array of [`Plain Craft Launcher 2/metadata.json`](./Plain Craft Launcher 2/metadata.json).

## License

- Root [`LICENSE`](./LICENSE): Apache License 2.0
- [`Plain Craft Launcher 2/LICENCE`](./Plain%20Craft%20Launcher%202/LICENCE): upstream PCL Distribution Limited License
