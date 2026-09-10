# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial repository setup: README, LICENSE (MIT), .gitignore for Godot 4 + C#/.NET
- Branch protection, PR/issue templates, labels, milestones and GitHub Project board
- Godot project initialized (Compatibility renderer, 640x360 base resolution, 16x16 tile size, Nearest texture filter)
- Input Map configured (move_up, move_down, move_left, move_right, confirm, cancel, open_menu)
- CHANGELOG.md and static README badges (license, Godot version)
- English translations for the README and the GitHub issue/PR templates
- Feature-based project folder structure (Player/, Combat/, World/, UI/, Shared/, Autoloads/)
- Entry point scene (main.tscn) and project conventions documentation (docs/CONVENTIONS.md)
- Godot project moved into a dedicated Game/ subfolder, separating it from repository metadata
- CI workflow to validate the build on every push and pull request
- CHANGELOG checklist item added to the PR template
- Basic top-down player movement (CharacterBody2D, 4-direction input via Input Map, normalized diagonal movement)
