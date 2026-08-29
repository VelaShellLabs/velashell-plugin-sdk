# Documentation index

This repository holds the **contract layer** documentation only. Chinese version: [`../docs/`](../docs/).

| Document | Contents |
| --- | --- |
| [sdk-reference.md](sdk-reference.md) | **SDK reference**: package layout, entry contract, capability surface, SDK version history, test doubles, loading model |
| [../docs/release-process.md](../docs/release-process.md) | **How this repository releases**: release flow, NuGet trusted-publishing setup, apiLevel and Avalonia pin discipline (Chinese only) |

## What is not here

After the 2026-08-27 split, the author-facing documents moved to the repositories that ship the
packages they describe:

| Document | Where it lives now |
| --- | --- |
| **Development guide** (tutorial: writing your first plugin) | [velashell-plugin-templates / docs-en/dev-guide.md](https://github.com/VelaShellLabs/velashell-plugin-templates/blob/main/docs-en/dev-guide.md) |
| **Packaging and publishing** (`.vpx`, signing, the marketplace) | [velashell-plugin-templates / docs-en/publishing.md](https://github.com/VelaShellLabs/velashell-plugin-templates/blob/main/docs-en/publishing.md) |
| **`vela-plugin` manual** | [velashell-plugin-cli / docs-en/cli.md](https://github.com/VelaShellLabs/velashell-plugin-cli/blob/main/docs-en/cli.md) |

Each of those carries the **version banner of its own package**, which is exactly why it has to
live next to that package: keeping them here would mean every CLI release needs a commit in this
repository — the coupling the split was meant to remove.

The plugin system's **architecture documents** (process model, IPC protocol, permissions, UI
extension, threat model, roadmap — the 01–15 series) stay in the host repository:
<https://github.com/joesdu/VelaShell/tree/main/docs/plugins>

Those describe the **host side**. Read them to understand why plugins look the way they do;
you do not need them to write one.
