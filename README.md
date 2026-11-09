# ServerTamingExtended

A server-side BepInEx mod for Valheim that makes deer, necks, and seals tameable and breedable, with additional feeding options and optional integration with Valheim Server UI and ServersideQoL.

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
- Offspring inherit their parent's star level.
- Deer and neck offspring spawn as adults.
- Seals produce vanilla `Seal_Pup` offspring that mature into adults after a configurable period.
- Seal pups count toward breeding population limits.

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

## Admin commands

When used with ServerDevcommands:

`server tamestatus [radius]`

Displays information about nearby supported creatures, including taming progress, hunger, breeding progress, pregnancy, and available feeding containers.

## Known limitations

- The mod does not add the vanilla `Tameable` component to creatures on player clients.
- Vanilla petting, renaming, and Follow/Stay interactions are not provided.
- Creatures do not automatically walk toward dropped food.
- Taming and breeding are processed in areas around connected players.
- Tamed seals can still be damaged by player attacks because the vanilla seal prefab is marked as aggravatable. Player-friendly-fire protection for seals is not currently implemented.

## Credits

**Original ServerTaming:** [Shuiei](https://github.com/Shuiei/valheim-server-taming)

**ServerTamingExtended:** Babkatt

Developed with assistance from OpenAI ChatGPT.

Special thanks to Shuiei for creating the original server-side taming implementation and permitting redistribution of this extended fork.

## License

Based on ServerTaming, distributed under the MIT License. The original copyright and permission notices must be retained in redistributed copies.

See `LICENSE` for details.