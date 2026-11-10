
# ServerTamingExtended

A server-side BepInEx mod for Valheim that makes deer, necks, and seals tameable and breedable, with additional feeding options, optional seal star breeding, and optional integration with Valheim Server UI and ServersideQoL.

**Dedicated server only — no client installation required.**

Based on [ServerTaming by Shuiei](https://github.com/Shuiei/valheim-server-taming).

## Features

### Creature taming

- Tame vanilla deer, necks, and seals using their configured foods.
- Animals consume one food item at a time from dropped stacks.
- Taming progresses while animals are fed and calm.
- Configurable taming duration, food types, and maximum tameable star level.
- Tamed creatures retain their vanilla appearance and behavior.

### Breeding

- Breed tamed deer, necks, and seals.
- Configurable breeding intervals, pregnancy duration, population limits, and partner distance.
- Offspring normally inherit their parent's star level.
- Deer and neck offspring spawn as adults.
- Seals produce vanilla `Seal_Pup` offspring that mature into adults after a configurable period.
- Seal pups retain their star level when maturing.
- Seal pups count toward breeding population limits.

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

## Installation

1. Install BepInEx on your Valheim dedicated server.
2. Download ServerTamingExtended.
3. Place `ServerTamingExtended.dll` in `BepInEx/plugins/ServerTamingExtended/`.
4. Start the dedicated server.
5. Configure the generated BepInEx configuration file as desired.

**Players do not need to install this mod.**

## Configuration

Configuration is generated automatically on first launch.

### General

| Setting | Default | Description |
|---|---|---|
| TickSeconds | 5 | Interval between creature updates |
| FeedRadius | 8 | Maximum distance for ground feeding |
| RequireCalm | true | Pause taming and breeding when alerted |
| ProgressMessages | true | Display taming progress |
| MaxTameStars | -1 | Highest permitted star count; -1 allows any |
| DebugLog | false | Enable detailed diagnostic messages |

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
| CheckSeconds | 30 | Interval between breeding checks |
| LoveChance | 0.5 | Chance to gain a love point per eligible check |
| RequiredLovePoints | 4 | Love points required for pregnancy |
| PartnerRange | 5 | Maximum partner distance |
| PopulationRange | 15 | Radius for local population limits |
| MaxCreatures | 8 | Default maximum nearby population |
| MaxTamedNearby | 30 | Additional population limit across areas loaded near players |

### Creature settings

Each supported species has its own configuration section:

- `Enabled`
- `Foods`
- `TamingMinutes`
- `FedMinutes`
- `Breeding`
- `PregnancyMinutes`
- `MaxCreatures`

Default foods:

| Creature | Food |
|---|---|
| Deer | Raspberries, blueberries, cloudberries, carrots, turnips, onions, mushrooms, yellow mushrooms |
| Neck | Raw fish |
| Seal | Raw fish |

The `Seal` section also includes `PupGrowMinutes`, which controls the maturation time for seal offspring.

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
- Once two 2-star seals are within 15 metres, subsequent offspring are born at 1 star.
- Removing a 2-star seal allows higher-star offspring to be produced again when the local high-star count falls below the limit.

These examples use accelerated upgrade chances for demonstration. The default configuration keeps star upgrades disabled.

## Admin commands

When used with ServerDevcommands:

`server tamestatus [radius]`

Displays information about nearby supported creatures, including taming progress, hunger, breeding progress, pregnancy, and available feeding containers.

ServerDevcommands is optional and is not required for normal taming, breeding, or feeding functionality.

## Known limitations

- The mod does not add the vanilla `Tameable` component to creatures on player clients.
- Vanilla petting, renaming, and Follow/Stay interactions are not provided.
- Creatures do not automatically walk toward dropped food.
- Taming and breeding are processed in areas around connected players.
- Tamed adult seals can still be damaged by player attacks because the vanilla adult seal prefab is marked as aggravatable. Player-friendly-fire protection for adult seals is not currently implemented.

## Credits

**Original ServerTaming:** [Shuiei](https://github.com/Shuiei/valheim-server-taming)

**ServerTamingExtended:** Babkatt

Developed with assistance from OpenAI ChatGPT.

Special thanks to Shuiei for creating the original server-side taming implementation and permitting redistribution of this extended fork.

## License

Based on ServerTaming, distributed under the MIT License. The original copyright and permission notices must be retained in redistributed copies.

See `LICENSE` for details.
