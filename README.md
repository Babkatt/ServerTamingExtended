
# ServerTamingExtended

A server-side BepInEx mod for Valheim that makes deer, necks, and seals tameable and breedable, with additional feeding options, optional seal star breeding, detailed creature diagnostics, and optional integration with Valheim Server UI and ServersideQoL.

**Dedicated server only — no client installation required.**

**Current version:** 1.2.1  
**Tested Valheim version:** 1.0.30 (network version 42)

Based on [ServerTaming by Shuiei](https://github.com/Shuiei/valheim-server-taming).

## Features

### Creature taming

- Tame vanilla deer, necks, and seals using their configured foods.
- Animals consume one food item at a time from dropped stacks.
- Taming progresses while animals are fed and calm.
- Configurable taming duration, food types, and maximum tameable star level.
- Tamed creatures retain their vanilla appearance.
- Uses Valheim's native tamed flag without requiring custom creature prefabs or client-side modifications.
- Taming progress and tame status persist across server restarts.

### Breeding

- Breed tamed deer, necks, and seals.
- Configurable breeding intervals, pregnancy duration, population limits, and partner distance.
- Fed and calm tamed creatures can accumulate affection points when another tamed adult of the same species is nearby.
- Offspring normally inherit their parent's star level.
- Offspring are born tamed.
- Deer and neck offspring spawn as adults.
- Seals produce vanilla `Seal_Pup` offspring that mature into adults after a configurable period.
- Seal pups retain their tame status and star level when maturing.
- Seal pups count toward breeding population limits.
- Affection points and pregnancy progress persist across server restarts.

### Population management

Breeding is controlled by both local and broader population limits.

- `PopulationRange` determines the radius around each creature used for local population checks.
- `MaxCreatures` determines the maximum permitted local population.
- Active pregnancies count as additional creatures during affection checks, reserving population capacity for future offspring.
- Existing seal pups count toward the physical population.
- Wild creatures of the same species also count toward local population limits.
- Population limits are checked again when a pregnancy completes.
- If the local population is already at capacity, the pregnancy ends without producing offspring.
- `MaxTamedNearby` provides an additional population safeguard across the areas simulated around connected players.
- Individual species can override the global `MaxCreatures` setting.

Population limits are evaluated around individual creatures rather than fixed enclosures. Creatures moving into or out of the configured radius can affect breeding eligibility.

### Seal star breeding (v1.1.0)

Optional star upgrades and selective breeding for seal offspring.

**Star upgrades are disabled by default.** With the feature disabled, offspring inherit their parent's star level as before.

When `EnableStarUpgrades` is enabled:

- Seal offspring have a configurable chance to gain one additional star above their parent's level.
- The maximum offspring star level is configurable.
- Star upgrades apply only to seals; deer and neck breeding is unchanged.
- Upgrades work without enabling selective breeding.

When `SelectiveStarBreeding` is also enabled:

- A configurable limit controls how many 2-star-or-higher seals may be present within a specified radius before further high-star births are restricted.
- Both adult seals and seal pups count toward this limit.
- Once the limit is reached, new offspring are restricted to 0 stars, with a configurable chance of being born at 1 star instead.
- Existing seals are never removed or downgraded when the limit is reached.
- If the nearby high-star population drops below the limit, higher-star offspring can be produced again.
- The selective breeding limit is separate from normal creature population limits.
- High-star births are tracked during breeding updates to prevent simultaneous births from bypassing the star-specific quota.

For example, with a 100% star upgrade chance and a maximum of 2 stars:

- A 0-star parent can produce a 1-star pup.
- A 1-star parent can produce a 2-star pup.
- A 2-star parent produces offspring capped at 2 stars.
- With selective breeding enabled and the local 2-star quota reached, subsequent offspring follow the configured fallback chance instead.

**Selective breeding does not need to be enabled to use star upgrades.** Disabling selective breeding removes the star-specific population quota, but normal breeding population limits still apply.

### Feeding

- Wild creatures can be tamed using food dropped on the ground.
- Tamed creatures can automatically consume appropriate food from nearby containers.
- Food stacks decrease by one item per feeding.
- Configurable minimum food reserves in containers.
- Containers currently in use by players are skipped.
- Feeding duration is configurable independently for each supported species.

### Valheim Server UI integration

Optional integration with [Valheim Server UI](https://github.com/jonasthim/valheim-server-ui).

When enabled, ServerTamingExtended can automatically read the Gameplay Autofeed settings:

- Sign prefix
- Sign radius
- Container feeding range

This allows custom tamed creatures to use the same sign-marked feeding chests as vanilla tames.

If Valheim UI Gameplay is unavailable or its Autofeed feature is disabled, ServerTamingExtended uses its own configurable fallback settings.

No compile-time dependency on Valheim UI Gameplay is required.

### ServersideQoL TameAssist compatibility

Optional compatibility with ServersideQoL TameAssist, including:

- Taming time multiplier
- Fed duration multiplier
- Taming progress message settings
- Legacy container feeding configuration

Marked-chest feeding can operate independently of TameAssist's container-feeding toggle.

### ServersideQoL SmartDefense compatibility

ServerTamingExtended has been tested alongside ServersideQoL SmartDefense on Valheim 1.0.30.

- Tamed seals were not damaged by ballistas with `DontTargetTames` enabled.
- Custom tames can still trigger traps.
- Trap friendly-fire protection remains subject to existing SmartDefense limitations.

SmartDefense is optional and is not required for ServerTamingExtended functionality.

### Detailed diagnostics (v1.2.0)

Optional diagnostic logging provides detailed information about creature activity and breeding decisions.

Available diagnostics include:

- Animal state initialization and restoration.
- Taming progress and completion.
- Changes to the vanilla tamed flag.
- Ground food detection and consumption.
- Food ownership changes.
- Ground food stack verification.
- Container food consumption and inventory write verification.
- Hunger status and feeding duration.
- Calm and alerted status changes.
- Affection checks and successful affection gains.
- Failed breeding probability rolls.
- Breeding restrictions and population limit checks.
- Pregnancy initiation and completion.
- Offspring births, star levels, and tame status.
- Births prevented by population limits.
- Seal pup maturation.

Diagnostic logging is disabled by default.

When enabled, detailed positional diagnostics are restricted to a configurable radius around connected players. Repeated unsuccessful food searches are suppressed by default to prevent excessive log entries.

The diagnostic radius only affects logging. It does not change the simulation area or restrict actual taming, feeding, breeding, population counting, or maturation.

Basic taming completion, birth, blocked birth, and maturation announcements remain logged independently of detailed diagnostic settings.

### Remote creature status reports (v1.2.0)

The expanded `tamestatus` admin command provides a detailed snapshot of nearby supported creatures.

Reports are:

- Displayed directly in the requesting player's F5 console.
- Recorded in the server log.
- Accompanied by a brief on-screen HUD notification.
- Independent of the diagnostic logging configuration.

See [Admin commands](#admin-commands) for usage and report details.

## Installation

1. Install BepInEx on your Valheim dedicated server.
2. Download ServerTamingExtended.
3. Place `ServerTamingExtended.dll` in `BepInEx/plugins/ServerTamingExtended/`.
4. Start the dedicated server.
5. Configure the generated BepInEx configuration file as desired.

**Players do not need to install this mod.**

### Updating from an earlier version

1. Stop the dedicated server.
2. Replace the existing `ServerTamingExtended.dll` with the updated version.
3. Start the server to generate any new configuration entries.
4. Review the configuration and adjust new settings as desired.

Existing configuration values are retained. New configuration entries are generated automatically using their default values.

## Configuration

Configuration is generated automatically on first launch.

The configuration file is located at:

`BepInEx/config/Christian.ServerTamingExtended.cfg`

### General

| Setting | Default | Description |
|---|---|---|
| TickSeconds | 5 | Interval in seconds between creature updates |
| FeedRadius | 8 | Maximum distance in metres for ground feeding |
| RequireCalm | true | Require creatures to be calm during taming and affection accumulation |
| MessageRange | 30 | Radius in metres for taming and birth notifications |
| ProgressMessages | true | Display taming progress |
| MaxTameStars | -1 | Highest permitted star count; -1 allows any |
| DebugLog | false | Enable detailed diagnostic messages |
| DebugLogRadius | 15 | Radius in metres around connected players for detailed positional diagnostics; 0 disables distance filtering |
| LogHungryChecks | false | Log repeated unsuccessful food searches when diagnostic logging is enabled |

### Diagnostic configuration examples

#### Normal gameplay

```ini
[General]
DebugLog = false
DebugLogRadius = 15
LogHungryChecks = false
```

Detailed diagnostics are disabled, while normal lifecycle announcements and admin status reports remain available.

#### Local troubleshooting

```ini
[General]
DebugLog = true
DebugLogRadius = 15
LogHungryChecks = false
```

Records detailed activity for nearby creatures without repeatedly logging every unsuccessful food search.

Recommended when investigating taming, feeding, breeding, or maturation issues within a specific enclosure.

#### Unrestricted diagnostic logging

```ini
[General]
DebugLog = true
DebugLogRadius = 0
LogHungryChecks = true
```

Records detailed diagnostics throughout the areas simulated around connected players, including repeated unsuccessful food searches.

**Warning:** This configuration can generate substantial log output, particularly on servers with many connected players or large creature populations. It is intended for temporary troubleshooting.

### Containers

| Setting | Default | Description |
|---|---|---|
| UseMarkedChestFeeding | true | Enable feeding from sign-marked chests |
| UseValheimUIAutofeedSettings | true | Read Valheim UI Gameplay Autofeed settings when available |
| FeedSignPrefix | feed | Fallback sign prefix |
| FeedSignRadius | 2 | Fallback maximum sign-to-chest distance in metres |
| FeedRange | 20 | Fallback chest-to-animal feeding distance in metres |
| UseTameAssistSettings | true | Use compatible TameAssist settings when available |
| FeedFromContainersRange | 0 | Legacy chest feeding range without TameAssist |
| LeaveAtLeast | 1 | Minimum food quantity retained in containers when using local settings |
| FeedWildFromContainers | true | Allow wild animals to use containers in legacy feeding mode |

When marked-chest feeding is enabled, wild creatures require dropped food for taming. Tamed creatures may use marked chests.

### Breeding

| Setting | Default | Description |
|---|---|---|
| CheckSeconds | 30 | Base interval in seconds between breeding checks |
| LoveChance | 0.5 | Probability from 0 to 1 of gaining a love point per eligible check |
| RequiredLovePoints | 4 | Love points required for pregnancy |
| PartnerRange | 5 | Maximum partner distance in metres |
| PopulationRange | 15 | Radius in metres for local population limits |
| MaxCreatures | 8 | Default maximum nearby population, including active pregnancies during affection checks |
| MaxTamedNearby | 30 | Additional tamed population limit across areas loaded near players; 0 disables this safeguard |

Actual breeding check intervals are randomized between the configured `CheckSeconds` value and 1.5 times that value.

A breeding partner must be another tamed adult of the same species within `PartnerRange`. The partner does not need to be fed, calm, or free of pregnancy.

The creature accumulating affection must be fed and, when `RequireCalm` is enabled, calm.

### Creature settings

Each supported species has its own configuration section:

- `[Deer]`
- `[Neck]`
- `[Seal]`

| Setting | Description |
|---|---|
| Enabled | Enable taming and breeding support for this species |
| Foods | Comma-separated list of accepted food prefab names |
| TamingMinutes | Minutes of eligible taming time required |
| FedMinutes | Minutes one food item keeps the creature fed |
| Breeding | Enable breeding for tamed creatures |
| PregnancyMinutes | Minutes between pregnancy initiation and birth |
| MaxCreatures | Override the global local population limit; -1 uses the global setting |

Default creature settings:

| Creature | TamingMinutes | FedMinutes | PregnancyMinutes | MaxCreatures |
|---|---|---|---|---|
| Deer | 25 | 5 | 10 | -1 |
| Neck | 25 | 5 | 10 | -1 |
| Seal | 25 | 5 | 10 | -1 |

Effective taming and feeding durations may differ when ServersideQoL TameAssist multipliers are enabled.

Default foods:

| Creature | Food |
|---|---|
| Deer | Raspberries, blueberries, cloudberries, carrots, turnips, onions, mushrooms, yellow mushrooms |
| Neck | Raw fish |
| Seal | Raw fish |

The `[Seal]` section also includes:

| Setting | Default | Description |
|---|---|---|
| PupGrowMinutes | 60 | Game-world minutes before a seal pup born through this mod matures into an adult |

### Seal star breeding settings (v1.1.0)

All settings are located in the `[Seal]` configuration section.

| Setting | Default | Description |
|---|---|---|
| EnableStarUpgrades | false | Enable the possibility of seal offspring gaining additional stars |
| SelectiveStarBreeding | false | Enable the nearby high-star population quota |
| StarUpgradeChance | 5 | Percentage chance for offspring to gain one additional star |
| MaxOffspringStars | 2 | Maximum number of stars a seal offspring may have when star upgrades are enabled |
| MaxTwoStarSeals | 2 | Maximum number of nearby seals with 2 or more stars before selective breeding restrictions apply |
| StarCheckRadius | 15 | Radius in metres used to count nearby high-star seals |
| AtCapacityOneStarChance | 5 | Percentage chance of a 1-star offspring instead of a 0-star offspring when the high-star quota is reached |

#### Configuration dependencies

`EnableStarUpgrades` is the primary toggle.

- When `EnableStarUpgrades = false`, all new star-breeding behavior is disabled and normal inheritance is preserved.
- When `EnableStarUpgrades = true`, `StarUpgradeChance` and `MaxOffspringStars` control offspring upgrades.
- When `SelectiveStarBreeding = false`, star upgrades are unrestricted by the star-specific population quota.
- When both toggles are enabled, `MaxTwoStarSeals`, `StarCheckRadius`, and `AtCapacityOneStarChance` also take effect.

The star-specific quota limits new births, not existing animals. It does not replace the normal breeding population limit.

#### Example: Unrestricted star upgrades

```ini
[Seal]
EnableStarUpgrades = true
SelectiveStarBreeding = false
StarUpgradeChance = 100
MaxOffspringStars = 2
```

With these settings, seals can progressively produce higher-star offspring until reaching 2 stars, without a separate high-star quota.

#### Example: Selective breeding

```ini
[Seal]
EnableStarUpgrades = true
SelectiveStarBreeding = true
StarUpgradeChance = 100
MaxOffspringStars = 2
MaxTwoStarSeals = 2
StarCheckRadius = 15
AtCapacityOneStarChance = 100
```

With these settings:

- Offspring can upgrade by one star per generation until reaching 2 stars.
- Up to two nearby 2-star seals are permitted before the high-star birth restriction activates.
- Once two 2-star seals are within the configured counting radius, subsequent offspring are born at 1 star.
- Removing a 2-star seal allows higher-star offspring to be produced again when the local high-star count falls below the limit.

These examples use accelerated upgrade chances for demonstration. The default configuration keeps star upgrades disabled.

## Admin commands

### tamestatus

When using a client-side remote command utility such as ServerDevcommands, open the F5 console and enter:

```text
server tamestatus [radius]
```

Examples:

```text
server tamestatus
server tamestatus 15
server tamestatus 30
server tamestatus 100
```

| Parameter | Value |
|---|---|
| Default radius | 30 metres |
| Minimum radius | 1 metre |
| Maximum radius | 100 metres |

The search radius is centered on the requesting player.

The command displays information about nearby deer, necks, seal adults, seal pups, and eligible feeding containers.

#### Population information

- Adult and seal pup counts.
- Tamed and wild creature counts.
- Number of active pregnancies.
- Physical population.
- Effective population including pregnancies.
- Configured local breeding population limit.
- Star-level distribution.
- High-star seal count and configured selective breeding quota.

#### Individual creature information

- Creature type and ZDO identifier.
- Star level.
- Distance from the requesting player.
- Taming percentage.
- Remaining taming time for wild creatures.
- Hunger status and remaining fed duration.
- Calm or alerted status.
- Current affection points.
- Pregnancy status and remaining gestation time.
- Current breeding eligibility or reason breeding is restricted.

#### Seal pup information

- ZDO identifier.
- Star level.
- Tamed or wild status.
- Distance from the requesting player.
- Remaining time until adulthood for pups born through ServerTamingExtended.

#### Feeding container information

- Container feeding status.
- Configured feeding range.
- Number of eligible feeding containers in the collected simulation area.
- Nearby eligible feeding chest identifiers.
- Distance from the requesting player.
- Individual chest feeding range.
- Available quantities of configured creature foods.
- Whether the container is currently open and therefore skipped during feeding.

#### Example output

```text
========== STE STATUS ==========
Search radius: 30 m
Adults: 5 | Seal pups: 1

----- Seal -----
Adults: 5 | Tamed: 5 | Wild: 0 | Pregnant: 0
Physical population: 6 | Including pregnancies: 6
Configured breeding limit: 15 within 15 m per animal
Star distribution: 0-star: 3, 1-star: 1, 2-star: 2
High-star seals in report: 2 | Configured local quota: 2

Seal 1:21339 | 1-star | 4.9m | Taming 100.0% | Hungry | Calm | Affection 1/4 | Breeding paused
Seal 1:22043 | 0-star | 7.4m | Taming 100.0% | Fed 0.80m | Calm | Affection 3/4 | Ready to breed

Seal pup 678120234:27466 | 0-star | Tamed | 6.9m | 0.72m until adulthood

Chest feeding: Enabled | Range: 20m | Eligible chests: 4
Feed chest 1:21974 | 8.8m | Range 20m | FishRaw 1
Feed chest 1:21318 | 14.9m | Range 20m | empty
================================
```

Example output is abbreviated for readability.

#### Command output

The complete report is:

- Returned directly to the requesting player's F5 console using Valheim's native remote command infrastructure.
- Recorded in the dedicated server log with the `[TAMESTATUS]` prefix.
- Accompanied by a short top-left HUD notification.

Command output is independent of `DebugLog`, `DebugLogRadius`, and `LogHungryChecks`.

The report radius is centered on the player, while actual breeding population limits and selective breeding quotas are evaluated around individual creatures. Report totals therefore do not necessarily represent the exact population counted during an individual creature's breeding check.

ServerDevcommands is not required on the dedicated server for command output. A compatible client-side remote command utility is needed to submit the command from a player's console.

No ServerTamingExtended client installation is required.

## Damage and aggression compatibility

Damage and aggression behavior was tested on Valheim 1.0.30 alongside the existing server mod configuration.

ServerTamingExtended uses the vanilla tamed flag and does not introduce custom damage immunity, faction changes, or AI aggression overrides.

### Player damage

Tamed seals demonstrated protection against the following damage sources during testing:

| Damage source | Observed result |
|---|---|
| Direct melee attacks | No damage |
| Crossbow projectiles | No damage |
| Ember bombs | No damage |
| Staff of Embers direct attacks | No damage |
| Staff of Embers area-of-effect attacks | No damage |
| Thunderblood Crossbow chain lightning | No damage |

The previously documented vulnerability of tamed adult seals to player attacks could not be reproduced under the tested Valheim 1.0.30 server configuration.

These results describe observed compatibility and are not the result of a custom damage protection system implemented by ServerTamingExtended.

### Hostile creature aggression

Custom tames remain valid targets for hostile creatures, consistent with normal vanilla tame behavior.

Testing with wild necks, boars, wolves, and moose was performed to investigate aggression toward custom tames.

Normal hostile aggression toward tamed creatures was confirmed after disabling the Passive Enemies world modifier.

**Note:** Hammer Mode enables passive creature behavior, which can prevent normal hostile aggression during testing. This is a world modifier and is unrelated to ServerTamingExtended.

### Automated defenses and environmental hazards

| Damage source | Observed result |
|---|---|
| Ballistas with SmartDefense tame protection enabled | No damage observed |
| Traps | Custom tames can trigger traps |
| Campfires and environmental fire | Normal fire damage |
| Hostile creatures | Normal hostile aggression |

Custom tames are not immortal and should be protected from hostile creatures and environmental hazards just like vanilla livestock.

## Compatibility and testing

Version 1.2.0 has been tested on Valheim 1.0.30 using BepInEx 5.4.23.6.

Testing included:

- Dedicated server initialization.
- Taming progress and completion.
- Ground food stack consumption.
- Marked-chest feeding.
- Affection accumulation.
- Pregnancy progression.
- Offspring births and tame status inheritance.
- Seal pup maturation and star preservation.
- Multigenerational seal star upgrades.
- Selective breeding behavior after reaching the high-star quota.
- Population limit enforcement during affection checks.
- Population limit enforcement when pregnancies complete.
- Persistence of creature states across server restarts.
- Remote admin command execution.
- Client console and HUD output.
- Server-side command logging.
- Diagnostic radius filtering.
- Suppression of repeated unsuccessful food searches.
- Player-friendly-fire, hostile creature, and environmental damage behavior.

Optional integrations tested include:

- Valheim Server UI Gameplay Autofeed.
- ServersideQoL TameAssist.
- ServersideQoL SmartDefense.

See `CHANGELOG.md` for version history and additional testing details.

## Known limitations

- The mod does not add the vanilla `Tameable` component to creatures on player clients.
- Vanilla petting, renaming, and Follow/Stay interactions are not provided.
- Creatures do not automatically walk toward dropped food.
- Taming and breeding are processed in areas around connected players.
- Creature status reports are limited to objects collected within the server's simulated areas.
- Population limits are radius-based rather than enclosure-based. Creatures wandering apart may create additional breeding capacity.
- Custom tames remain vulnerable to hostile creatures and environmental hazards.
- Custom tames can trigger traps. Trap friendly-fire protection is subject to existing SmartDefense limitations.
- Friendly-fire protection relies on Valheim's native behavior and any applicable server-side integrations rather than custom damage immunity implemented by this mod.

## Troubleshooting

If creatures are not taming, feeding, or breeding as expected:

1. Run `server tamestatus 30` while standing near the affected creatures.
2. Check hunger status, remaining feeding duration, affection points, and breeding eligibility.
3. Verify the configured food types and feeding chest quantities.
4. Check partner distance, local population limits, and active pregnancies.
5. Enable detailed diagnostics if additional information is needed.

Recommended troubleshooting configuration:

```ini
[General]
DebugLog = true
DebugLogRadius = 15
LogHungryChecks = false
```

Reproduce the issue and inspect the dedicated server's `BepInEx/LogOutput.log` file.

Relevant log entries are identified by `ServerTamingExtended` and diagnostic categories such as:

```text
[STATE]
[STATUS]
[FEEDING]
[SATIETY]
[TAMING]
[BREEDING]
[AFFECTION]
[PREGNANCY]
[BIRTH]
[MATURATION]
[TAMESTATUS]
```

When reporting an issue, include:

- ServerTamingExtended version.
- Valheim version.
- BepInEx version.
- Relevant configuration settings.
- Relevant installed server mods.
- The output of `tamestatus`, if applicable.
- Relevant server log entries.
- A description of the expected and observed behavior.

Detailed diagnostics can be disabled again after troubleshooting.

## Credits

**Original ServerTaming:** [Shuiei](https://github.com/Shuiei/valheim-server-taming)

**ServerTamingExtended:** Babkatt

Developed with assistance from OpenAI ChatGPT.

Special thanks to Shuiei for creating the original server-side taming implementation and permitting redistribution of this extended fork.

## License

Based on ServerTaming, distributed under the MIT License. The original copyright and permission notices must be retained in redistributed copies.

See `LICENSE` for details.
