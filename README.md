# Aether Current Navigator

[日本語](README.ja.md)

A Dalamud API 15 plugin for viewing and processing field Aether Currents and Aether Current quests from a progress list and map.

- Author: `Roxyz0501`
- License: `AGPL-3.0-only`; see [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md)
- Command: `/acnav`

## Main features

- Shows field and quest progress by expansion and area from Heavensward through Dawntrail.
- Draws field currents and aetheryte candidates over the in-game map, with a coordinate-grid fallback when the map texture is unavailable.
- Processes the current area, a selected expansion one area at a time, or one selected field current/quest.
- Chooses nearby field currents and quest issuers, compares the current position with aetheryte departure routes, and uses normal Teleport when it is faster.
- Uses ground-only vnavmesh routes and rejects routes with steep vertical sections that appear to require flying.
- Recalculates from the current position on request and switches to an unused aetheryte after detecting a stuck route. It stops after every candidate in the map has failed.
- Uses the configured mount after teleport, retries mounting up to ten times without stopping navigation, and remounts after swimming when possible.
- Hands accepted quest execution to Questionable through its public IPC interface.
- Provides English and Japanese UI, statuses, errors, tooltips, dependency information, and support text.

The plugin uses the game's normal Teleport, movement, mount, target, and interaction actions. It does not alter coordinates or perform an artificial warp.

## Installation

The plugin is intended for the shared Roxyz0501 Dalamud custom repository.

1. Add `__SHARED_CUSTOM_REPOSITORY_URL__` to Dalamud's custom plugin repositories after the final URL is published.
2. Open the Dalamud plugin installer.
3. Install **Aether Current Navigator**.
4. Install and load the dependencies listed below.

`__SHARED_CUSTOM_REPOSITORY_URL__` is an explicit placeholder and must not be used as a live URL.

## Usage

Open the window with `/acnav` or the Dalamud plugin-list button.

- **Unlock current area** processes enabled current types in the current area.
- **Unlock selected expansion** processes the selected expansion and finishes one area before moving to the next.
- The play button beside a field current or quest processes only that item.
- **Recalculate route** discards the active route and calculates again from the current position.
- Stop and pause buttons control the active run where the current phase permits it.
- The map tab shows field currents, current target, and normal aetheryte candidates.
- The status tab shows whether direct and indirect plugin requirements are loaded.

## Commands

- `/acnav` — open the main window.
- `/acnav stop` — stop automation owned by this plugin.

## Settings and language

Config contains processing targets, expansion confirmation, travel mount, debug display, and display language.

On the first launch only, the plugin detects the Dalamud/game client language. A Japanese client selects and saves **日本語**; every other language or a failed detection selects and saves **English**. After this initialization, the saved choice is never replaced by automatic detection. The user can switch between **English** and **日本語**, and that explicit value remains in effect on later launches.

Processing and mount settings are stored per character using the game's content ID as the settings key. Display language is global to the plugin. The default travel mount is Mount Roulette; an unlocked mount can be selected instead.

## Dependencies

- **vnavmesh** — required for field travel and route selection.
- **Questionable** — required for Aether Current quests; optional for field-current-only use.
- **TextAdvance** — indirect dependency required by Questionable.
- **Lifestream** — indirect dependency required by Questionable.

The plugin does not permanently change dependency-plugin settings.

## Data and privacy

- The plugin saves its configuration in Dalamud's plugin configuration storage.
- It saves the game content ID used to separate per-character settings.
- It does not save character names, position history, route history, API keys, or authentication data.
- It sends no telemetry and makes no automatic external web requests.
- Game automation communicates with installed plugins through local Dalamud IPC.
- The Support tab opens `https://ko-fi.com/roxyz0501` only after the user explicitly clicks the button.

## Known limitations

- Route availability depends on the loaded vnavmesh mesh and current game terrain.
- NPC transport and special access requirements cannot always be predicted before movement.
- Quest execution after handoff depends on Questionable's supported paths and settings.
- Normal teleport fees apply. A failed or unconfirmed teleport stops automation.
- Game, Dalamud, dependency, or QuestPaths updates may require data updates and renewed in-game testing.

## Troubleshooting

- **No movement:** open the Status tab and confirm that vnavmesh is loaded and ready.
- **Quest will not start:** confirm that Questionable and its dependencies are loaded, then read the quest status shown in the list.
- **Repeated route failure:** press Recalculate route while moving. Automatic stuck recovery will then try unused aetherytes and stop if none succeeds.
- **No map image:** the coordinate-grid fallback remains usable; enable debug information and review the map/aetheryte diagnostic text.
- **Wrong UI language:** choose English or 日本語 in Config. Automatic detection runs only before the first language value is saved.

## Optional support

Development by **Roxyz0501** can be supported voluntarily at [Ko-fi](https://ko-fi.com/roxyz0501). Supporting the project does not unlock or restrict any feature.

## Building and release packaging

Use the pinned .NET SDK and locked dependencies:

```powershell
./tools/Build-Release.ps1
```

The script generates `artifacts/AetherCurrentUnlocker-<AssemblyVersion>.zip`. The release ZIP contains only:

- `AetherCurrentUnlocker.dll`
- `AetherCurrentUnlocker.deps.json`
- `AetherCurrentUnlocker.json`

Publish the ZIP on this plugin's individual GitHub Releases page. The shared custom repository entry should reference that same asset. Repository, release, and icon URLs remain placeholders in `distribution/plugin-metadata.template.json` until the individual repository exists.

## Third-party reference and attribution

This is a standalone codebase maintained by Roxyz0501. It uses or adapts selected material from [PunishXIV/Questionable](https://github.com/PunishXIV/Questionable), originated by Liza Carvelli and maintained by the Puni.sh team and contributors under the GNU Affero General Public License version 3:

- field-current positions and object IDs generated from `QuestPaths/**`;
- selected territory, aetheryte, and world-coordinate data from `AetheryteData.cs`;
- Aether Current quest identifiers from `QuestData.cs`;
- selected mount-blocking status checks and mount-evaluation behavior from `GameFunctions.cs`;
- Questionable's public IPC contract for starting, monitoring, and stopping individual quests.

Questionable assemblies are not bundled. vnavmesh and Dalamud are used through their public APIs/IPC and are not redistributed in the release ZIP. File-level attribution and the icon provenance are documented in [NOTICE.md](NOTICE.md).

## License and disclaimer

This project is distributed under `AGPL-3.0-only`. Public redistribution must include the license, notices, and corresponding complete source code as required by that license.

This is an unofficial tool and is not endorsed by Square Enix, goatcorp, Puni.sh, or the Questionable maintainers. Users are responsible for reviewing applicable game rules and using the plugin at their own risk. See [PUBLICATION_CHECKLIST.md](PUBLICATION_CHECKLIST.md) for required human review and in-game checks before release.

The source, documentation, release preparation, and plugin icon were produced with AI assistance under Roxyz0501's direction. Third-party material remains attributed to its respective copyright holders.
