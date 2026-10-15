# Knowledge Workflow

[Home](Home.md)

These rules apply to everyone who reads or changes the vault: the owner, Claude and other agents.
Repository operations (approvals, branches, pull requests) are defined in [AGENTS.md](../AGENTS.md).

## Before work

Read the note of the service you work on (`Services/`) and related decisions. Notes do not replace the code:
if they disagree, the code is right and the note must be corrected.

## After work

The vault lives in the same repository as the code, so knowledge changes go into the same pull request as the code change.
Update notes when endpoints, data, dependencies or run instructions change, and record a decision when one is made.
Capture the reason, constraints and verified result, not the whole conversation. Extend an existing note before creating a new one.
If there is no new durable knowledge, no entry is needed.

## Structure

| Path | Content |
| --- | --- |
| `Services/` | One note per service, named after it without the `Northwind.`/`EShop.` prefix |
| `Decisions/` | One note per architecture decision |
| `Learning/` | Notes about technologies studied in this project |
| `_templates/` | Templates for new notes (Obsidian core Templates plugin) |

## Links and names

Use standard Markdown links with relative paths; no wikilinks.
Link source code relative to the note, e.g. from `Services/` use `../../Northwind.ProductApi/`.
File names are unique across the vault and use CamelCase.

## Properties

Every note except Home and Workflow starts with YAML properties.

| Property | Meaning |
| --- | --- |
| `type` | `index`, `service`, `decision` or `topic` |
| `verified` | Date of the last check against source, `YYYY-MM-DD` |
| `status` | Decisions only: `proposed`, `accepted` or `superseded` |
| `issue` | Optional: related GitHub issue number |

## Format

Write in English. Keep type and method names as in the source. Label hypotheses and open questions explicitly.
Do not store secrets, passwords or connection strings with credentials.

## Obsidian

Shared settings live in `.obsidian/`; per-user state (workspace layout, hotkeys, plugins) is ignored by Git.
No community plugins are required. New notes come from `_templates/`.
