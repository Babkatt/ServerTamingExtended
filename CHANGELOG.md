
# Changelog

All notable changes to ServerTamingExtended will be documented here.

ServerTamingExtended is based on [ServerTaming by Shuiei](https://github.com/Shuiei/valheim-server-taming).

## [1.1.0] - 2026-11-09

### Added
- Optional star upgrades for seal offspring.
- Configurable probability for offspring to gain one additional star above their parent's level.
- Configurable maximum offspring star level.
- Optional selective breeding system that limits new high-star seal offspring based on the nearby population.
- Configurable maximum number of nearby 2-star-or-higher seals and the radius used to count them.
- Configurable chance of producing a 1-star offspring instead of a 0-star offspring when the high-star quota is reached.
- High-star population counting includes both adult seals and seal pups.
- Tracking of high-star births during breeding updates to prevent simultaneous births from bypassing the configured quota.

### Changed
- Seal offspring can now gain additional stars when star upgrades are enabled.
- Seal offspring continue to retain their star level when maturing into adults.
- Selective breeding restrictions affect new births only; existing seals are not removed or downgraded.
- When the high-star population drops below the configured limit, higher-star births become possible again.
- Star-specific breeding restrictions operate independently of normal creature population limits.

### Configuration
- Added `EnableStarUpgrades` (default: false).
- Added `SelectiveStarBreeding` (default: false).
- Added `StarUpgradeChance` (default: 5%).
- Added `MaxOffspringStars` (default: 2).
- Added `MaxTwoStarSeals` (default: 2).
- Added `StarCheckRadius` (default: 15 metres).
- Added `AtCapacityOneStarChance` (default: 5%).

### Compatibility
- Star upgrades and selective breeding are disabled by default, preserving existing offspring star inheritance behavior.
- Star breeding changes apply only to seals.
- No client installation required.
- Existing taming, feeding, breeding, Valheim Server UI Autofeed integration, and ServersideQoL TameAssist compatibility are retained.

### Known Issues
- Tamed adult seals remain vulnerable to player attacks because the vanilla adult seal prefab is marked as aggravatable.
- Custom tames do not support vanilla petting, naming, or Follow/Stay interactions without additional client-side functionality.

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
