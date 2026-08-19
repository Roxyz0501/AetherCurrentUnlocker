# Shared custom repository integration

This directory contains the single-entry metadata template to hand to the Roxyz0501 shared Dalamud custom repository.
It is not a standalone `repo.json`.

## Fixed values

- InternalName: `AetherCurrentUnlocker`
- Name: `Aether Current Navigator`
- AssemblyVersion: `0.3.0.0`
- DalamudApiLevel: `15`
- Author: `Roxyz0501`
- Release ZIP: `AetherCurrentUnlocker-0.3.0.0.zip`
- License: `AGPL-3.0-only`
- Dependencies:
  - `vnavmesh`: required for field travel and routing.
  - `Questionable`: required when aether-current quests are enabled; optional for field-current-only use.
  - `TextAdvance` and `Lifestream`: indirect requirements of Questionable, not called directly by this plugin.

## Values injected after the individual GitHub repository exists

- `__PLUGIN_REPOSITORY_URL__`: the public URL of this plugin's individual source repository.
- `__RELEASE_ZIP_URL__`: the direct GitHub Release asset URL for `AetherCurrentUnlocker-0.3.0.0.zip`.
- `__PLUGIN_ICON_RAW_URL__`: a stable raw URL for `AetherCurrentUnlocker/images/icon.png`.

Use the same release asset URL for `DownloadLinkInstall` and `DownloadLinkUpdate`. Copy the object from
`plugin-metadata.template.json` into the shared repository's JSON array after replacing every placeholder.
Do not publish the template with unresolved placeholders as the live repository entry.
