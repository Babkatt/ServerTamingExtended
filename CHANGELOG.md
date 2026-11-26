
# Changelog

All notable changes to ServerTamingExtended will be documented here.

ServerTamingExtended is based on [ServerTaming by Shuiei](https://github.com/Shuiei/valheim-server-taming).

## [1.2.1] - 2026-11-25

### Fixed
- Corrected a version mismatch between the compiled DLL's BepInEx plugin metadata and manifest.json.
- No gameplay or functional changes from v1.2.0.

## [1.2.0] - 2026-11-25

### Added
- Detailed, configurable diagnostic logging for taming, feeding, satiety, affection, breeding decisions, pregnancies, births, and seal pup maturation.
- Ground food stack verification to confirm that individual food items are consumed correctly.
- Chest inventory write verification to assist with troubleshooting container feeding.
- Configurable diagnostic logging radius around connected players.
- Optional logging of repeated unsuccessful food searches.
- Expanded `tamestatus` command with detailed animal and population information:
  - Adult and seal pup population counts.
  - Tamed and wild creature counts.
  - Star-level distribution and high-star population quota.
  - Individual taming percentages and remaining taming time.
  - Hunger status and remaining fed duration.
  - Calm or alerted status.
  - Affection points and breeding eligibility.
  - Pregnancy status and remaining gestation time.
  - Physical population and effective population including pregnancies.
  - Seal pup maturation progress.
  - Nearby feeding chests, feeding ranges, and available food quantities.

### Changed
- Detailed positional diagnostics are now restricted to a configurable distance around connected players.
- Repeated unsuccessful food searches are suppressed by default.
- Animal status diagnostics are recorded when an animal's status changes rather than on every update.
- Diagnostic filtering does not affect animal simulation, taming, feeding, breeding, population counting, or maturation.
- Basic taming completion, offspring birth, blocked birth, and maturation announcements remain logged regardless of diagnostic radius.
- Existing unconditional warnings and errors remain unaffected.

### Fixed
- Fixed remote execution of the `tamestatus` command on dedicated servers.
- Removed the command's dependency on ServerDevcommands being installed server-side.
- Remote command output now uses Valheim's native remote command infrastructure.
- Status reports are returned directly to the requesting player's F5 console.
- Added a brief HUD notification confirming the number of nearby adults and pups.
- Status reports are always recorded in the server log, independently of diagnostic logging settings.
- Removed the previous 20-animal limit from status reports.

### Configuration
- Expanded `DebugLog` (default: false) to control detailed diagnostic logging.
- Added `DebugLogRadius` (default: 15 metres; 0 disables distance filtering).
- Added `LogHungryChecks` (default: false).

### Commands
- `server tamestatus [radius]` - Display detailed taming, breeding, population, and feeding information for nearby animals.
- Default search radius: 30 metres.
- Supported search radius: 1-100 metres.
- Command output is independent of the configured diagnostic logging radius.

### Compatibility
- Tested with Valheim 1.0.30 (network version 42).
- Verified taming, ground food stack consumption, chest feeding, affection accumulation, pregnancy, offspring births, and seal pup maturation.
- Verified preservation of tame status and star levels through offspring birth, maturation, and server restarts.
- Verified multigenerational seal breeding and offspring star upgrades.
- Observed expected selective breeding behavior after reaching the configured high-star quota.
- Verified population limit enforcement during both affection checks and offspring delivery.
- Verified that completed pregnancies do not produce offspring when the local population limit is reached.
- Existing Valheim Server UI Autofeed and ServersideQoL TameAssist integration retained.
- Tested alongside ServersideQoL SmartDefense.
- No ServerTamingExtended client installation required.

### Damage and Aggression Testing
- Tamed seals demonstrated protection against the following player damage sources under the tested Valheim 1.0.30 server configuration:
  - Direct melee attacks.
  - Crossbow projectiles.
  - Ember bombs.
  - Direct Staff of Embers attacks.
  - Staff of Embers area-of-effect damage.
  - Thunderblood Crossbow chain lightning.
- Verified that ballistas configured with SmartDefense's `DontTargetTames` protection did not damage tamed seals during testing.
- Verified normal hostile creature aggression toward custom tames when the Passive Enemies world modifier is disabled.
- Verified that environmental fire continues to damage tamed seals, consistent with normal vanilla tame behavior.
- No custom damage immunity, faction changes, or aggression overrides have been introduced.
- The previously documented vulnerability of tamed seals to player attacks could not be reproduced under the tested server configuration.

### Known Issues
- Custom tames do not support vanilla petting, naming, or Follow/Stay interactions without additional client-side functionality.
- Custom tames can trigger traps. Friendly-fire protection for traps remains subject to existing ServersideQoL SmartDefense limitations.
- Custom tames remain vulnerable to hostile creatures and environmental hazards, consistent with normal vanilla tame behavior.


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
