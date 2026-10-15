# Security Policy

godot-rpg-project is a single-player game in early development, maintained by one person in their spare time. A security problem in the game, or in how this repository is set up, is still worth hearing about.

## Supported versions

Fixes land on `main` and ship in the next release. Earlier releases are not patched.

## Reporting a vulnerability

Please don't report a security problem in a public Issue or pull request. Report it privately instead, through GitHub's private vulnerability reporting:

1. Open [the reporting form](https://github.com/EGCGAP04/godot-rpg-project/security/advisories/new). It is also the **Report a vulnerability** button in the repository's security tab.
2. Describe the problem, how to reproduce it, and what it lets someone do.

Only you and the maintainer can see the report. There is no fixed response time: every report is read and answered as soon as possible. Once a fix is out, the report can be published as a security advisory, crediting you if you wish.

## What counts

- Anything that makes the game read or write files outside its own data folder, or run code that comes from a data or save file.
- The repository's automation: a workflow that could leak its token or run code it should not trust, or a secret committed to the history.

Changing your own copy of the game or of its files, such as editing a save, is not a security problem.
