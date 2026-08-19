# Third-party notices

Copyright (C) 2026 Roxyz0501 for the original portions of Aether Current Navigator.

## Questionable

This project contains data and small adapted logic derived from
[PunishXIV/Questionable](https://github.com/PunishXIV/Questionable), which is distributed under the
GNU Affero General Public License version 3.

The original Aether Current Navigator implementation and the downstream modifications were created
by Roxyz0501 with AI assistance and last materially modified on 2026-08-20. Copyright in the reused
Questionable material remains with its respective upstream copyright holders. This project is not
presented as an official Questionable release.

Affected files:

- `AetherCurrentUnlocker/Data/FieldCurrentCatalog.g.cs` — generated from `Questionable/QuestPaths/**`.
- `AetherCurrentUnlocker/Data/AetheryteCatalog.cs` — selected territory, aetheryte and world-coordinate data derived from `Questionable/Data/AetheryteData.cs`.
- `AetherCurrentUnlocker/Data/AetherCurrentDataService.cs` — aether-current quest identifiers derived from `Questionable/Data/QuestData.cs`.
- `AetherCurrentUnlocker/Automation/AutomationController.cs` — mount-blocking status checks adapted from `Questionable/Functions/GameFunctions.cs` and its mount evaluation behavior.
- `AetherCurrentUnlocker/Ipc/QuestionableIpc.cs` — interoperates with the public IPC endpoints exposed by Questionable; no Questionable assembly is redistributed.

Questionable was originated by Liza Carvelli and is maintained by the Puni.sh team and its
contributors. The complete AGPL-3.0 license text is included in `LICENSE`. This project is distributed
as AGPL-3.0-only so the covered material and the combined work are provided under compatible terms.

## vnavmesh and Dalamud

The plugin communicates with an installed vnavmesh plugin through its public Dalamud IPC endpoints.
It builds against Dalamud, FFXIVClientStructs and Lumina APIs supplied by the local Dalamud SDK.
Those projects and binaries are not copied into this source tree or bundled in `latest.zip`.

## Plugin icon

`AetherCurrentUnlocker/images/icon.png` is an original image generated with OpenAI image generation
for this project under the direction of Roxyz0501. It does not contain a game logo or a copied UI asset.
