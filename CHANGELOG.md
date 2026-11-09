# Changelog

All notable changes to ServerTamingExtended will be documented here.

ServerTamingExtended is based on [ServerTaming by Shuiei](https://github.com/Shuiei/valheim-server-taming).

## [1.0.0] - 2026-11-09

### Added
- Server-side taming and breeding support for vanilla seals.
- Seal pups (`Seal_Pup`) that mature into adult seals after a configurable duration.
- Seal pup population tracking for breeding limits.
- Optional integration with Valheim Server UI Gameplay Autofeed.
- Automatic detection and use of Valheim UI's sign prefix, sign radius, and container feeding range.
- Independent fallback settings for marked-chest feeding.
- Support for using marked feeding chests without enabling TameAssist's container-feeding feature.

### Changed
- Ground food consumption now removes one item at a time from dropped stacks.
- Marked-chest feeding is restricted to tamed creatures; wild creatures use dropped food.
- Preserved compatibility with ServersideQoL TameAssist's taming multipliers, feeding duration multipliers, and progress messages.

### Known Issues
- Tamed seals remain vulnerable to player attacks because vanilla seals are marked as aggravatable.
- Custom tames do not support vanilla petting, naming, or Follow/Stay interactions without additional client-side functionality.

### Credits
- Original ServerTaming implementation by Shuiei.
- Extensions and testing by Babkatt, with assistance from ChatGPT.