# Shared custom repository integration

This directory contains the single-entry metadata template to hand to the Roxyz0501 shared Dalamud custom repository.
It is not a standalone `repo.json`.

## Fixed values

- InternalName: `AetherCurrentUnlocker`
- Name: `Aether Current Navigator`
- AssemblyVersion: `0.3.1.0`
- DalamudApiLevel: `15`
- Author: `Roxyz0501`
- Release ZIP: `AetherCurrentUnlocker-0.3.1.0.zip`
- License: `AGPL-3.0-only`
- Dependencies:
  - `vnavmesh`: required for field travel and routing.
  - `Questionable`: required when aether-current quests are enabled; optional for field-current-only use.
  - `TextAdvance` and `Lifestream`: indirect requirements of Questionable, not called directly by this plugin.

## Published values

- Source repository: `https://github.com/Roxyz0501/AetherCurrentUnlocker`
- Release ZIP: `https://github.com/Roxyz0501/AetherCurrentUnlocker/releases/download/v0.3.1.0/AetherCurrentUnlocker-0.3.1.0.zip`
- Icon: `https://raw.githubusercontent.com/Roxyz0501/AetherCurrentUnlocker/main/AetherCurrentUnlocker/images/icon.png`

Use the same release asset URL for `DownloadLinkInstall` and `DownloadLinkUpdate`. The published object is
included in the shared repository's JSON array.
