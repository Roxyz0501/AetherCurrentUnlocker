# Publication checklist

This is a new standalone plugin rather than a clone or modified copy of the Questionable repository.
It reuses selected Questionable data and adapted logic, so the AGPL-3.0 obligations and attribution
documented in `README.md` and `NOTICE.md` still apply to every public release.

## Before creating the individual GitHub repository

- [ ] Human-review every source and generated-data change.
- [ ] Confirm `LICENSE`, `NOTICE.md`, README attribution, and source availability satisfy AGPL-3.0.
- [ ] Confirm the repository owner and support recipient are `Roxyz0501`.
- [ ] Replace no upstream attribution with the downstream author name.
- [ ] Confirm no API keys, tokens, cookies, webhook URLs, character data, logs, or local absolute paths are committed.
- [ ] Confirm `bin`, `obj`, `artifacts`, IDE state, and local configuration are ignored.
- [ ] Decide the final repository URL and insert it only after the repository exists.

## Human in-game verification

- [ ] Load and unload the plugin without exceptions or retained windows/callbacks.
- [ ] Open every tab at multiple UI scales; confirm the gold Support tab is readable in normal, hovered, and selected states.
- [ ] Confirm the Ko-fi button opens `https://ko-fi.com/roxyz0501` only after a click.
- [ ] Confirm current-area, expansion, individual field-current, and individual quest runs.
- [ ] Confirm teleport selection, ground-route rejection, stuck fallback, mount retry, swimming remount, pause, stop, and path recalculation.
- [ ] Confirm behavior when vnavmesh or Questionable is absent, busy, or rejects an IPC call.
- [ ] Confirm settings and selected mount remain character-specific.
- [ ] Confirm the icon appears from the final `IconUrl` in the custom repository.

## Release and shared repository

- [ ] Build with `tools/Build-Release.ps1` and verify its reported SHA-256.
- [ ] Confirm first-launch Japanese detection, English fallback, and persistence of both explicit language choices.
- [ ] Create an individual GitHub Release tagged for `0.3.0.0` and attach `AetherCurrentUnlocker-0.3.0.0.zip`.
- [ ] Replace all placeholders in `distribution/plugin-metadata.template.json`.
- [ ] Add the resulting one-entry object to the Roxyz0501 shared custom repository.
- [ ] Validate clean install and update from the published shared `repo.json`.

## AI assistance

The plugin source, documentation, release preparation, and icon were created with AI assistance under Roxyz0501's direction.
Human source review and the in-game checks above remain required before publication.
