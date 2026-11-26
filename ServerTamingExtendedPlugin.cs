using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ServerTamingExtended;

// Server-only taming and breeding for animals that have no Tameable component in vanilla (Deer, Neck, Seal).
// Players need no mod: everything runs on ZDO data near connected players.
//  - Taming: a hungry wild animal eats matching food lying within FeedRadius; while fed (and calm),
//    its taming time counts down. At zero the owner client is asked to set the vanilla "tamed" flag
//    (Character.RPC_SetTamed), which vanilla clients already understand (no fleeing/attacking players).
//  - Breeding: fed tamed animals with a tamed partner nearby gain love points, get pregnant and
//    give birth to a tamed adult of the same prefab (no custom offspring prefab) but pups when from a Seal.
//  - Food can also come from chests, with the same rules as ServersideQoL TameAssist's
//    FeedFromContainers (global range, per-chest range on a sign, leave-at-least). When TameAssist is
//    installed its settings and taming/fed multipliers are used, so deer, necks, and seals behave like its tames.
// Progress lives in server memory. Writing it into client-owned ZDOs would be lost to the owner's
// updates, so it is only copied into the ZDOs (without a revision bump) just before the world saves.
[BepInPlugin(Guid, "ServerTamingExtended", Version)]
public sealed class ServerTamingExtendedPlugin : BaseUnityPlugin
{
	public const string Guid = "Christian.ServerTamingExtended";

	public const string Version = "1.2.1";

	private static readonly int KeyTameLeft = "ServerTamingExtended_tameLeft".GetStableHashCode();

	private static readonly int KeyFedUntil = "ServerTamingExtended_fedUntil".GetStableHashCode();

	private static readonly int KeyLove = "ServerTamingExtended_love".GetStableHashCode();

	private static readonly int KeyPregnant = "ServerTamingExtended_pregnant".GetStableHashCode();

	private const string TameAssistGuid = "ArgusMagnus.ServersideQoL.TameAssist";

	private static ServerTamingExtendedPlugin _instance;

    private static readonly int SealPupHash = "Seal_Pup".GetStableHashCode();

    private ZNetView _sealPupView;

    private ConfigEntry<float> _sealPupGrowMinutes;

    private ConfigEntry<bool> _enableSealStarUpgrades;
    private ConfigEntry<bool> _selectiveSealStarBreeding;
    private ConfigEntry<float> _sealStarUpgradeChance;
    private ConfigEntry<int> _sealMaxOffspringStars;
    private ConfigEntry<int> _sealMaxTwoStarSeals;
    private ConfigEntry<float> _sealStarCheckRadius;
    private ConfigEntry<float> _sealAtCapacityOneStarChance;

    // Prevent multiple births in one update from exceeding the 2-star quota.
    private readonly List<Vector3> _highStarSealBirthsThisTick = new List<Vector3>();

    private static readonly int KeyPupBorn = "ServerTamingExtended_pupBorn".GetStableHashCode();

    private ConfigEntry<float> _tickSeconds;

	private ConfigEntry<float> _feedRadius;

	private ConfigEntry<bool> _requireCalm;

	private ConfigEntry<float> _messageRange;

	private ConfigEntry<bool> _progressMessages;

	private ConfigEntry<int> _maxTameStars;

	private ConfigEntry<bool> _useTameAssist;

	private ConfigEntry<bool> _feedWildFromContainers;

    private ConfigEntry<bool> _markedChestFeeding;
    private ConfigEntry<bool> _useValheimUISettings;
    private ConfigEntry<string> _feedSignPrefix;
    private ConfigEntry<float> _feedSignRadius;
    private ConfigEntry<float> _markedFeedRange;

    private const string ValheimUIGuid = "se.jonasthim.valheimui.gameplay";

    private ConfigEntry<float> _containerRange;

	private ConfigEntry<int> _leaveAtLeast;

	private ConfigEntry<float> _breedCheckSeconds;

	private ConfigEntry<float> _loveChance;

	private ConfigEntry<int> _requiredLovePoints;

	private ConfigEntry<float> _partnerRange;

	private ConfigEntry<float> _populationRange;

	private ConfigEntry<int> _maxCreatures;

	private ConfigEntry<int> _maxTamedNearby;

	private ConfigEntry<bool> _debugLog;
    private ConfigEntry<float> _debugLogRadius;

    private ConfigEntry<bool> _logHungryChecks;

    private readonly List<Vector3> _debugCenters = new List<Vector3>();
    private readonly List<Species> _species = new List<Species>();

	private readonly Dictionary<int, Species> _speciesByPrefab = new Dictionary<int, Species>();

	private readonly HashSet<int> _foodPrefabs = new HashSet<int>();

	private readonly Dictionary<ZDOID, State> _states = new Dictionary<ZDOID, State>();

	// States of animals the server just destroyed, waiting for the copy that replaces them.
	private readonly List<Orphan> _orphans = new List<Orphan>();

	private readonly List<ZDO> _sectorObjects = new List<ZDO>();

	private readonly HashSet<ZDOID> _seen = new HashSet<ZDOID>();

	private readonly List<ZDO> _animals = new List<ZDO>();

    private readonly List<ZDO> _pups = new List<ZDO>();

    private readonly List<ZDO> _foods = new List<ZDO>();

	private readonly HashSet<ZDOID> _eaten = new HashSet<ZDOID>();

	private readonly List<ZDO> _chests = new List<ZDO>();

	private readonly List<ZDO> _signs = new List<ZDO>();

	private readonly Dictionary<ZDOID, float> _chestRanges = new Dictionary<ZDOID, float>();

	private readonly Dictionary<int, PrefabKind> _kinds = new Dictionary<int, PrefabKind>();

	private Feeding _feeding = new Feeding();

	private enum PrefabKind
	{
		Other,
		Chest,
		Sign
	}

	// Container feeding and time multipliers, from TameAssist when it is installed.
	private sealed class Feeding
	{
		public bool FromTameAssist;

		public bool Containers;

		public float Range;

		public Regex SignRange;

		public int MaxRange = 64;

		public int LeaveAtLeast;

		public float TamingMultiplier = 1f;

		public float FedMultiplier = 1f;

		public string ProgressMessageType = "";
        public bool MarkedChests;
        public string MarkedSignPrefix = "";
        public float MarkedSignRadius;
    }

	private bool _resolved;

	private float _timer;

	private double _lastTick = -1.0;

	private int _ticks;

	private sealed class Species
	{
		public string Prefab;

		public ConfigEntry<bool> Enabled;

		public ConfigEntry<string> Foods;

		public ConfigEntry<float> TamingMinutes;

		public ConfigEntry<float> FedMinutes;

		public ConfigEntry<bool> Breeding;

		public ConfigEntry<float> PregnancyMinutes;

		public ConfigEntry<int> MaxCreatures;

		public int Hash;

		public string Name;

		public HashSet<int> FoodHashes = new HashSet<int>();

		public ZNetView View;
	}

	private sealed class Orphan
	{
		public int Prefab;

		public Vector3 Pos;

		public int Level;

		public float At;

		public ZDOID From;

		public State State;
	}

	private sealed class State
	{
		public float TameLeft;

		public double FedUntil;

		public int Love;

		public double Pregnant;

		public double NextBreedCheck;

		public bool TameAnnounced;

        // Diagnostic tracking only; not persisted to world saves.
        public string LastDiagnosticStatus;
        public bool? LastDiagnosticTamed;
    }

    private void Awake()
	{
		_instance = this;

		_tickSeconds = Config.Bind("General", "TickSeconds", 5f, "How often (seconds) animals near players are checked.");
		_feedRadius = Config.Bind("General", "FeedRadius", 8f, "Animals eat matching food lying within this many metres. They do not walk to it, so keep them penned with the food inside.");
		_requireCalm = Config.Bind("General", "RequireCalm", true, "Taming and breeding pause while the animal is alerted (fleeing or fighting), like vanilla.");
		_messageRange = Config.Bind("General", "MessageRange", 30f, "Players within this many metres see taming and birth messages.");
		_progressMessages = Config.Bind("General", "ProgressMessages", true, "Show taming progress to nearby players: at 25/50/75%, or with TameAssist's TamingProgressMessageType when TameAssist is used.");
        _debugLog = Config.Bind("General", "DebugLog", false, "Enable detailed diagnostics for feeding, stack consumption, " + "satiety, taming, animal status, affection, breeding decisions, " + "pregnancy, offspring births, and maturation. " + "Verbose; intended for troubleshooting.");
        _debugLogRadius = Config.Bind("General", "DebugLogRadius", 15f, "Only record detailed animal diagnostics within " + "this many metres of a connected player. " + "0 = unrestricted. Does not affect simulation.");
        _logHungryChecks = Config.Bind("General", "LogHungryChecks", false, "Log every unsuccessful food search. " + "False retains hunger status changes without " + "repeating unsuccessful searches every tick.");
        _maxTameStars = Config.Bind("General", "MaxTameStars", -1, "Highest star count that can be tamed; -1 means any (including the 3+ star creatures from level mods such as ServersideQoL CreatureLevelUp). Offspring keep their parent's stars.");
		_useTameAssist = Config.Bind("Containers", "UseTameAssistSettings", true, "When ServersideQoL TameAssist is installed, use its FeedFromContainers settings (range, chest sign range, LeaveAtLeast), its TamingTimeMultiplier and FedDurationMultiplier, and its TamingProgressMessageType. The two settings below are then ignored.");
		_containerRange = Config.Bind("Containers", "FeedFromContainersRange", 0f, "Without TameAssist: animals eat from chests within this many metres (0 = off).");
		_leaveAtLeast = Config.Bind("Containers", "LeaveAtLeast", 1, "Without TameAssist: keep at least this many of each food in a chest.");
		_feedWildFromContainers = Config.Bind("Containers", "FeedWildFromContainers", true, "Wild animals being tamed also eat from chests (TameAssist itself only feeds tamed animals from chests, but deer and necks can't walk to food).");
        _markedChestFeeding = Config.Bind("Containers", "UseMarkedChestFeeding", true, "Feed tamed custom creatures from sign-marked chests. When false, use the original TameAssist-style chest feeding.");
        _useValheimUISettings = Config.Bind("Containers", "UseValheimUIAutofeedSettings", true, "When Valheim UI Gameplay Autofeed is enabled, borrow its sign prefix and feeding ranges.");
        _feedSignPrefix = Config.Bind("Containers", "FeedSignPrefix", "feed", "Fallback sign prefix. Case-insensitive; empty means every chest is eligible.");
        _feedSignRadius = Config.Bind("Containers", "FeedSignRadius", 2f, "Fallback maximum distance from a feeding sign to its chest, in metres.");
        _markedFeedRange = Config.Bind("Containers", "FeedRange", 20f, "Fallback maximum feeding distance from a marked chest to an animal, in metres.");
        _breedCheckSeconds = Config.Bind("Breeding", "CheckSeconds", 30f, "How often each fed tamed animal tries to gain a love point.");
		_loveChance = Config.Bind("Breeding", "LoveChance", 0.5f, "Chance (0-1) of gaining a love point on each check when a partner is near.");
		_requiredLovePoints = Config.Bind("Breeding", "RequiredLovePoints", 4, "Love points needed to become pregnant.");
		_partnerRange = Config.Bind("Breeding", "PartnerRange", 5f, "A tamed partner of the same kind must be within this many metres.");
		_populationRange = Config.Bind("Breeding", "PopulationRange", 15f, "Radius used to count animals of the same kind for MaxCreatures.");
		_maxCreatures = Config.Bind("Breeding", "MaxCreatures", 8, "No new love points when this many animals of the same kind are within PopulationRange. Each animal's own MaxCreatures setting overrides this.");
		_maxTamedNearby = Config.Bind("Breeding", "MaxTamedNearby", 30, "No breeding when this many tamed animals of the same kind are anywhere in the area loaded around players, wherever they wandered (0 = no limit). A safety net for animals that leave the pen, where MaxCreatures only sees their small group.");
		AddSpecies("Deer", "Raspberry,Blueberries,Cloudberry,Carrot,Turnip,Onion,Mushroom,MushroomYellow", 25f, 5f, 10f);
		AddSpecies("Neck", "FishRaw", 25f, 5f, 10f);
        AddSpecies("Seal", "FishRaw", 25f, 5f, 10f);
        _sealPupGrowMinutes = Config.Bind(
			"Seal",
		    "PupGrowMinutes",
			60f,
			"Game-world minutes before a seal pup born by this mod matures into an adult."
		);
        _enableSealStarUpgrades = Config.Bind(
			"Seal", "EnableStarUpgrades", false,
			"Enable optional seal offspring star upgrades. False preserves original parent-star inheritance.");

        _selectiveSealStarBreeding = Config.Bind(
            "Seal", "SelectiveStarBreeding", false,
            "False = normal chance for +1 star, without star-specific population caps. True = limit 2-star births and produce mostly 0-star pups when the quota is full. Only applies when EnableStarUpgrades is true.");

        _sealStarUpgradeChance = Config.Bind(
            "Seal", "StarUpgradeChance", 5f,
            new ConfigDescription(
                "Percentage chance (0-100) for a pup to inherit one additional star above its parent. Only applies when EnableStarUpgrades is true.",
                new AcceptableValueRange<float>(0f, 100f)));

        _sealMaxOffspringStars = Config.Bind(
            "Seal", "MaxOffspringStars", 2,
            new ConfigDescription(
                "Maximum offspring star level when star upgrades are enabled. Does not affect existing seals. Only applies when EnableStarUpgrades is true.",
                new AcceptableValueRange<int>(0, 10)));

        _sealMaxTwoStarSeals = Config.Bind(
            "Seal", "MaxTwoStarSeals", 2,
            new ConfigDescription(
                "Maximum number of 2-star-or-higher seals and pups permitted nearby for new high-star births. Only applies when EnableStarUpgrades and SelectiveStarBreeding are true.",
                new AcceptableValueRange<int>(0, 100)));

        _sealStarCheckRadius = Config.Bind(
            "Seal", "StarCheckRadius", 15f,
            new ConfigDescription(
                "Radius in metres used to count existing 2-star-or-higher seals and pups. Only applies when EnableStarUpgrades and SelectiveStarBreeding are true.",
                new AcceptableValueRange<float>(1f, 100f)));

        _sealAtCapacityOneStarChance = Config.Bind(
            "Seal", "AtCapacityOneStarChance", 5f,
            new ConfigDescription(
                "Percentage chance (0-100) of a 1-star pup when the local 2-star quota is full; otherwise produces a 0-star pup. Only applies when EnableStarUpgrades and SelectiveStarBreeding are true.",
                new AcceptableValueRange<float>(0f, 100f)));
        new Terminal.ConsoleCommand("tamestatus", "[radius] - show server taming/breeding progress of animals near you; use through 'server tamestatus'", Status);
		Harmony harmony = new Harmony(Guid);
		harmony.PatchAll(typeof(SavePatch));
		harmony.PatchAll(typeof(DestroyPatch));
        harmony.PatchAll(typeof(RemoteCommandPatch));
    }

	private void AddSpecies(string prefab, string foods, float taming, float fed, float pregnancy)
	{
		_species.Add(new Species
		{
			Prefab = prefab,
			Enabled = Config.Bind(prefab, "Enabled", true, "Make " + prefab + " tameable and breedable."),
			Foods = Config.Bind(prefab, "Foods", foods, "Comma-separated item prefab names it eats."),
			TamingMinutes = Config.Bind(prefab, "TamingMinutes", taming, "Minutes of being fed (and calm) needed to tame it."),
			FedMinutes = Config.Bind(prefab, "FedMinutes", fed, "Minutes one food item keeps it fed."),
			Breeding = Config.Bind(prefab, "Breeding", true, "Tamed ones breed and give birth to tamed adults. Pups in the case of seals."),
			PregnancyMinutes = Config.Bind(prefab, "PregnancyMinutes", pregnancy, "Minutes from pregnancy to birth."),
			MaxCreatures = Config.Bind(prefab, "MaxCreatures", -1, "No new love points when this many " + prefab + " are within [Breeding] PopulationRange; -1 uses [Breeding] MaxCreatures.")
		});
	}

	// Prefab names can only be checked once ZNetScene has loaded them.
	private void Resolve()
	{
		_resolved = true;
        GameObject sealPup = ZNetScene.instance.GetPrefab("Seal_Pup");
        _sealPupView = sealPup != null ? sealPup.GetComponent<ZNetView>() : null;

        if (_sealPupView == null)
        {
            Logger.LogWarning("Seal_Pup prefab or ZNetView not found. Seal births will be unavailable.");
        }
        foreach (Species s in _species)
		{
			GameObject go = ZNetScene.instance.GetPrefab(s.Prefab);
			if (!s.Enabled.Value || go == null || go.GetComponent<Character>() == null)
			{
				if (s.Enabled.Value)
				{
					Logger.LogWarning("Creature prefab '" + s.Prefab + "' not found; it is disabled.");
				}
				continue;
			}
			s.Hash = s.Prefab.GetStableHashCode();
			s.Name = go.GetComponent<Character>().m_name;
            if (_debugLog.Value)
            {
                Character character = go.GetComponent<Character>();
                BaseAI ai = go.GetComponent<BaseAI>();

                Logger.LogInfo(
                    $"[TARGET DIAGNOSTIC] {s.Prefab}: " +
                    $"Character={character.GetType().Name}, " +
                    $"AI={(ai != null ? ai.GetType().Name : "none")}, " +
                    $"Faction={character.m_faction}, " +
                    $"Aggravatable={(ai != null && ai.m_aggravatable)}"
                );
            }
            s.View = go.GetComponent<ZNetView>();
			foreach (string food in s.Foods.Value.Split(',').Select(f => f.Trim()).Where(f => f.Length > 0))
			{
				GameObject item = ZNetScene.instance.GetPrefab(food);
				if (item == null || item.GetComponent<ItemDrop>() == null)
				{
					Logger.LogWarning("Food '" + food + "' for " + s.Prefab + " is not an item prefab; ignored.");
					continue;
				}
				s.FoodHashes.Add(food.GetStableHashCode());
				_foodPrefabs.Add(food.GetStableHashCode());
			}
			_speciesByPrefab[s.Hash] = s;
			Logger.LogInfo($"{s.Prefab}: {s.FoodHashes.Count} food(s), taming {s.TamingMinutes.Value} min, breeding {(s.Breeding.Value ? "on" : "off")}.");
		}
	}

	private void Update()
	{
		if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZNetScene.instance == null)
		{
			_resolved = false;
			_states.Clear();
			_orphans.Clear();
			_lastTick = -1.0;
			return;
		}
		_timer += Time.deltaTime;
		if (_timer < Mathf.Max(1f, _tickSeconds.Value))
		{
			return;
		}
		_timer = 0f;
		try
		{
			if (!_resolved)
			{
				Resolve();
			}
			Tick();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	private void Tick()
	{
		double now = ZNet.instance.GetTimeSeconds();
		float dt = _lastTick < 0.0 ? 0f : Mathf.Clamp((float)(now - _lastTick), 0f, Mathf.Max(1f, _tickSeconds.Value) * 3f);
		_lastTick = now;
		if (_speciesByPrefab.Count == 0)
		{
			return;
		}
		_feeding = ReadFeeding();
		Collect();
		UpdateChestRanges();
		_eaten.Clear();

        _highStarSealBirthsThisTick.Clear();

        foreach (ZDO zdo in _animals)
        {
            Species s = _speciesByPrefab[zdo.GetPrefab()];
            State st = GetState(zdo);
            bool tamed = zdo.GetBool(ZDOVars.s_tamed);

            if (!tamed && !CanTame(zdo))
            {
                if (ShouldDebug(zdo))
                {
                    string blocked = "Taming blocked: star level exceeds MaxTameStars.";

                    if (st.LastDiagnosticStatus != blocked)
                    {
                        Debug($"[STATUS] {Describe(zdo)}: {blocked}");
                        st.LastDiagnosticStatus = blocked;
                    }
                }

                continue;
            }

            bool calm = !_requireCalm.Value || !zdo.GetBool(ZDOVars.s_alert);

            if (now >= st.FedUntil)
            {
                TryEat(zdo, s, st, now, tamed);
            }

            bool fed = now < st.FedUntil;

            if (ShouldDebug(zdo))
            {
                // Confirm changes to the actual vanilla tamed flag.
                if (st.LastDiagnosticTamed.HasValue &&
                    st.LastDiagnosticTamed.Value != tamed)
                {
                    Debug($"[TAMING] {Describe(zdo)}: " +
                        $"TamedFlag changed from " +
                        $"{st.LastDiagnosticTamed.Value} to {tamed}.");
                }

                st.LastDiagnosticTamed = tamed;

                string status;

                if (!tamed)
                {
                    status = fed && calm
                        ? "Taming active"
                        : $"Taming paused: {(!fed ? "hungry" : "fed")}, " +
                          $"{(!calm ? "alerted" : "calm")}";
                }
                else if (!s.Breeding.Value)
                {
                    status = "Breeding disabled";
                }
                else if (st.Pregnant > 0.0)
                {
                    status = "Pregnant";
                }
                else if (!fed || !calm)
                {
                    status = $"Breeding paused: {(!fed ? "hungry" : "fed")}, " +
                        $"{(!calm ? "alerted" : "calm")}";
                }
                else
                {
                    status = "Ready for breeding checks";
                }

                if (st.LastDiagnosticStatus != status)
                {
                    Debug($"[STATUS] {Describe(zdo)}: {status}. " +
                        $"Fed={fed}, Calm={calm}, " +
                        $"Love={st.Love}/{Mathf.Max(1, _requiredLovePoints.Value)}.");

                    st.LastDiagnosticStatus = status;
                }
            }
            else
            {
                st.LastDiagnosticStatus = null;
                st.LastDiagnosticTamed = null;
            }

            if (!tamed)
            {
                UpdateTaming(zdo, s, st, fed, calm, dt);
            }
            else if (s.Breeding.Value)
            {
                UpdateBreeding(zdo, s, st, fed && calm, now);
            }
        }

        GrowSealPups(now);
        if (++_ticks % 60 == 0)
		{
			foreach (ZDOID id in _states.Keys.Where(id => ZDOMan.instance.GetZDO(id) == null).ToList())
			{
				_states.Remove(id);
			}
		}
	}

	// Animals and food in the areas simulated around connected players (vanilla only runs these too).
	private void Collect()
	{
		_animals.Clear();
		_pups.Clear();
		_foods.Clear();
		_chests.Clear();
		_signs.Clear();
		_seen.Clear();
		SimulationDistance synced = ZNet.instance.GetSyncedSimulationDistance();
		SimulationDistance near = new SimulationDistance(synced.NearSimulationDistance, 0, synced.IsClassic);
		List<Vector3> centers = ZNet.instance.GetPeers().Select(p => p.GetRefPos()).ToList();
		if (!ZNet.instance.IsDedicated())
		{
			centers.Add(ZNet.instance.GetReferencePosition());
		}
        _debugCenters.Clear();

        if (_debugLog.Value)
            _debugCenters.AddRange(centers);
        foreach (Vector3 center in centers)
		{
			_sectorObjects.Clear();
			ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(center), near, _sectorObjects);
			foreach (ZDO zdo in _sectorObjects)
			{
				if (!zdo.IsValid() || !_seen.Add(zdo.m_uid))
				{
					continue;
				}
				int prefab = zdo.GetPrefab();
				if (_speciesByPrefab.ContainsKey(prefab))
				{
					_animals.Add(zdo);
				}
                else if (prefab == SealPupHash)
                {
                    _pups.Add(zdo);
                }
                else if (_foodPrefabs.Contains(prefab) && !zdo.GetBool(ZDOVars.s_piece))
				{
					_foods.Add(zdo);
				}
				else if (_feeding.Containers)
				{
					switch (Classify(prefab))
					{
					case PrefabKind.Chest:
						_chests.Add(zdo);
						break;
					case PrefabKind.Sign:
						_signs.Add(zdo);
						break;
					}
				}
			}
		}
	}

	private State GetState(ZDO zdo)
	{
		if (!_states.TryGetValue(zdo.m_uid, out State st) && (st = Adopt(zdo)) == null)
		{
			// Values saved into the ZDO at the last world save; ZDOIDs change on every load.
			st = new State
			{
				TameLeft = zdo.GetFloat(KeyTameLeft, -1f),
				FedUntil = zdo.GetLong(KeyFedUntil, 0L) / 1000.0,
				Love = zdo.GetInt(KeyLove),
				Pregnant = zdo.GetLong(KeyPregnant, 0L) / 1000.0
			};
			_states[zdo.m_uid] = st;

            if (ShouldDebug(zdo))
            {
                double now = ZNet.instance.GetTimeSeconds();

                Debug($"[STATE] {Describe(zdo)}: " +
                    $"loaded animal state. " +
                    $"TameLeft={st.TameLeft:0.00}s, " +
                    $"FedRemaining={Math.Max(0.0, st.FedUntil - now):0.00}s, " +
                    $"Love={st.Love}/{Mathf.Max(1, _requiredLovePoints.Value)}, " +
                    $"Pregnant={st.Pregnant > 0.0}, " +
                    $"PregnancyElapsed={(st.Pregnant > 0.0 ? now - st.Pregnant : 0.0):0.00}s.");
            }
        }
        return st;
	}

	// Other server mods replace a creature's ZDO with a copy under a new ZDOID: ServersideQoL
	// CreatureLevelUp does it for 3+ star creatures to show their stars and size. The copy carries the
	// values from the last world save only, so the live state moves over to it here.
	private void Orphaned(ZDO zdo)
	{
		if (!_states.TryGetValue(zdo.m_uid, out State st))
		{
			return;
		}
		_states.Remove(zdo.m_uid);
		_orphans.RemoveAll(o => Time.time - o.At > 60f);
		_orphans.Add(new Orphan
		{
			Prefab = zdo.GetPrefab(),
			Pos = zdo.GetPosition(),
			Level = zdo.GetInt(ZDOVars.s_level, 1),
			At = Time.time,
			From = zdo.m_uid,
			State = st
		});
	}

	private State Adopt(ZDO zdo)
	{
		if (_orphans.Count == 0)
		{
			return null;
		}
		int prefab = zdo.GetPrefab();
		int level = zdo.GetInt(ZDOVars.s_level, 1);
		Vector3 pos = zdo.GetPosition();
		Orphan best = _orphans.Where(o => o.Prefab == prefab && o.Level == level && Time.time - o.At <= 60f && (o.Pos - pos).sqrMagnitude <= 100f)
			.OrderBy(o => (o.Pos - pos).sqrMagnitude).FirstOrDefault();
		if (best == null)
		{
			return null;
		}
		_orphans.Remove(best);
		_states[zdo.m_uid] = best.State;
        if (ShouldDebug(zdo))
        {
            Debug($"{zdo.m_uid}: took over the state of replaced {best.From} (level {level}).");
        }
        return best.State;
	}

	private string Describe(ZDO zdo)
	{
		Vector3 p = zdo.GetPosition();
		return $"{_speciesByPrefab[zdo.GetPrefab()].Prefab} {zdo.m_uid} ({zdo.GetInt(ZDOVars.s_level, 1) - 1} star(s), {(zdo.GetBool(ZDOVars.s_tamed) ? "tamed" : "wild")}, owner {zdo.GetOwner()}) at ({p.x:0}, {p.y:0}, {p.z:0})";
	}

    private bool ShouldDebug(Vector3 position)
    {
        if (!_debugLog.Value)
            return false;

        float radius = _debugLogRadius.Value;

        if (radius <= 0f)
            return true;

        if (ZNet.instance == null)
            return false;

        float radiusSq = radius * radius;

        foreach (Vector3 center in _debugCenters)
        {
            if ((center - position).sqrMagnitude <= radiusSq)
                return true;
        }

        return false;
    }

    private bool ShouldDebug(ZDO zdo)
    {
        return zdo != null &&
            ShouldDebug(zdo.GetPosition());
    }

    private void Debug(string text)
    {
        if (_debugLog.Value)
            Logger.LogInfo(text);
    }

    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.DestroyZDO))]
	private static class DestroyPatch
	{
		private static void Prefix(ZDO zdo)
		{
			try
			{
				if (zdo != null && _instance != null && _instance._speciesByPrefab.ContainsKey(zdo.GetPrefab()))
				{
					_instance.Orphaned(zdo);
				}
			}
			catch (Exception ex)
			{
				_instance?.Logger.LogError(ex);
			}
		}
	}

	private PrefabKind Classify(int prefab)
	{
		if (!_kinds.TryGetValue(prefab, out PrefabKind kind))
		{
			GameObject go = ZNetScene.instance.GetPrefab(prefab);
			kind = go == null ? PrefabKind.Other
				: go.GetComponent<Container>() != null && go.GetComponent<Piece>() != null ? PrefabKind.Chest
				: go.GetComponent<Sign>() != null ? PrefabKind.Sign
				: PrefabKind.Other;
			_kinds[prefab] = kind;
		}
		return kind;
	}

	// Mirrors TameAssist's config; read every tick so changes to its config file apply live.
	private Feeding ReadFeeding()
	{
		Feeding f = new Feeding();
		// With ServersideQoL's UnifiedConfig the entries live in the core plugin's file under "<core>.TameAssist".
		List<ConfigFile> ta = !_useTameAssist.Value || !Chainloader.PluginInfos.ContainsKey(TameAssistGuid) ? null
			: Chainloader.PluginInfos.Values.Where(i => i.Metadata.GUID.StartsWith("ArgusMagnus.ServersideQoL", StringComparison.Ordinal) && i.Instance != null)
				.Select(i => i.Instance.Config).ToList();
		if (ta != null && Get(ta, "Enabled", true))
		{
			f.FromTameAssist = true;
			f.Containers = Get(ta, "FeedFromContainers", false);
			f.Range = Get(ta, "FeedFromContainersRange", 0f);
			f.MaxRange = Get(ta, "FeedFromContainersMaxRange", 64);
			f.LeaveAtLeast = Get(ta, "FeedFromContainersLeaveAtLeast", 1);
			f.TamingMultiplier = Get(ta, "TamingTimeMultiplier", 1f);
			f.FedMultiplier = Get(ta, "FedDurationMultiplier", 1f);
			f.ProgressMessageType = Get(ta, "TamingProgressMessageType", (object)"").ToString();
			// Same pattern as ServersideQoL ContainerSigns: the prefix (with or without emoji variation selector) then a number.
			string prefix = Get(ta, "FeedFromContainersRangeSignPrefix", "");
			string bare = prefix.Replace("\ufe0f", "");
			if (prefix.Length > 0)
			{
				f.SignRange = new Regex((prefix == bare ? Regex.Escape(prefix) : "(?:" + Regex.Escape(prefix) + "|" + Regex.Escape(bare) + ")") + "(?<R>\\d+)");
			}
		}
		else
		{
			f.Range = _containerRange.Value;
			f.Containers = f.Range > 0f;
			f.LeaveAtLeast = _leaveAtLeast.Value;
		}
        if (_markedChestFeeding.Value)
        {
            f.MarkedChests = true;
            f.Containers = true;

            // Independent settings when Valheim UI Gameplay is unavailable.
            f.MarkedSignPrefix = _feedSignPrefix.Value ?? "";
            f.MarkedSignRadius = Mathf.Max(0f, _feedSignRadius.Value);
            f.Range = Mathf.Max(0f, _markedFeedRange.Value);
            f.LeaveAtLeast = Math.Max(0, _leaveAtLeast.Value);

            // Borrow live settings from Valheim UI Gameplay when enabled.
            if (_useValheimUISettings.Value &&
                Chainloader.PluginInfos.TryGetValue(ValheimUIGuid, out var pluginInfo) &&
                pluginInfo.Instance != null)
            {
                ConfigFile uiConfig = pluginInfo.Instance.Config;

                if (GetAutofeedSetting(uiConfig, "Enabled", false))
                {
                    f.MarkedSignPrefix = GetAutofeedSetting(
                        uiConfig, "SignPrefix", f.MarkedSignPrefix) ?? "";

                    f.MarkedSignRadius = Mathf.Max(0f, GetAutofeedSetting(
                        uiConfig, "SignRadius", f.MarkedSignRadius));

                    f.Range = Mathf.Max(0f, GetAutofeedSetting(
                        uiConfig, "Range", f.Range));
                }
            }
        }
        return f;
	}

	private static T Get<T>(List<ConfigFile> configs, string key, T fallback)
	{
		foreach (ConfigFile config in configs)
		{
			foreach (ConfigDefinition def in config.Keys)
			{
				if (def.Key == key && (def.Section == "TameAssist" || def.Section.EndsWith(".TameAssist", StringComparison.Ordinal)) && config[def].BoxedValue is T value)
				{
					return value;
				}
			}
		}
		return fallback;
	}

    private static T GetAutofeedSetting<T>(ConfigFile config, string key, T fallback)
    {
        foreach (ConfigDefinition definition in config.Keys)
        {
            if (definition.Section == "Autofeed" &&
                definition.Key == key &&
                config[definition].BoxedValue is T value)
            {
                return value;
            }
        }

        return fallback;
    }

    // A chest's feed range is the global range, or the number after the prefix in its text, capped at
    // MaxRange. ServersideQoL ContainerSigns keeps that text on the chest itself and copies it onto signs
    // it recreates at will, so the chest is checked first and nearby signs only as a fallback.
    private void UpdateChestRanges()
	{
		_chestRanges.Clear();
		if (!_feeding.Containers)
		{
			return;
		}
        if (_feeding.MarkedChests)
        {
            float signRadius2 = _feeding.MarkedSignRadius *
                                _feeding.MarkedSignRadius;

            foreach (ZDO chest in _chests)
            {
                bool marked = string.IsNullOrEmpty(_feeding.MarkedSignPrefix);

                if (!marked)
                {
                    Vector3 chestPos = chest.GetPosition();

                    marked = _signs.Any(sign =>
                        (sign.GetPosition() - chestPos).sqrMagnitude <= signRadius2 &&
                        sign.GetString(ZDOVars.s_text, "")
                            .Trim()
                            .StartsWith(
                                _feeding.MarkedSignPrefix,
                                StringComparison.OrdinalIgnoreCase));
                }

                if (marked && _feeding.Range > 0f)
                {
                    _chestRanges[chest.m_uid] = _feeding.Range;
                }
            }

            return;
        }
        foreach (ZDO chest in _chests)
		{
			if (SignRange(chest) is int range)
			{
				_chestRanges[chest.m_uid] = Mathf.Min(range, _feeding.MaxRange);
			}
			else if (_feeding.Range > 0f)
			{
				_chestRanges[chest.m_uid] = _feeding.Range;
			}
		}
		foreach (ZDO sign in _signs)
		{
			if (!(SignRange(sign) is int range))
			{
				continue;
			}
			Vector3 pos = sign.GetPosition();
			ZDO chest = _chests.Where(c => (c.GetPosition() - pos).sqrMagnitude <= 9f)
				.OrderBy(c => (c.GetPosition() - pos).sqrMagnitude).FirstOrDefault();
			if (chest != null && SignRange(chest) == null)
			{
				_chestRanges[chest.m_uid] = Mathf.Min(range, _feeding.MaxRange);
			}
		}
	}

	private int? SignRange(ZDO zdo)
	{
		if (_feeding.SignRange == null)
		{
			return null;
		}
		Match match = _feeding.SignRange.Match(zdo.GetString(ZDOVars.s_text));
		return match.Success && int.TryParse(match.Groups["R"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int range) ? range : null;
	}

	private float TamingSeconds(Species s)
	{
		return Mathf.Max(1f, s.TamingMinutes.Value * 60f * _feeding.TamingMultiplier);
	}

    private void TryEat(ZDO animal, Species s, State st, double now, bool tamed)
    {
        bool allowChest = tamed ||
            (!_feeding.MarkedChests && _feedWildFromContainers.Value);

        string source = TryEatFromGround(animal, s)
            ? "the ground"
            : allowChest && TryEatFromChest(animal, s)
                ? "a chest"
                : null;

        if (source != null)
        {
            st.FedUntil = now + s.FedMinutes.Value * 60.0 * _feeding.FedMultiplier;

            if (ShouldDebug(animal))
            {
                Debug($"[SATIETY] {Describe(animal)}: " +
                    $"ate from {source}. " +
                    $"BaseFedMinutes={s.FedMinutes.Value:0.00}, " +
                    $"FedMultiplier={_feeding.FedMultiplier:0.00}, " +
                    $"EffectiveFedMinutes={(st.FedUntil - now) / 60.0:0.00}, " +
                    $"FedUntil={st.FedUntil:0.00}.");
            }
        }
        else if (ShouldDebug(animal) && _logHungryChecks.Value)
        {
            Debug($"[FEEDING] {Describe(animal)}: " +
                $"hungry; no eligible food found. " +
                $"ChestFeedingAllowed={allowChest && _feeding.Containers}, " +
                $"FeedingChests={_chests.Count(c => _chestRanges.ContainsKey(c.m_uid))}.");
        }
    }

    private bool TryEatFromChest(ZDO animal, Species s)
	{
		if (!_feeding.Containers)
		{
			return false;
		}
		Vector3 pos = animal.GetPosition();
		foreach (ZDO chest in _chests.OrderBy(c => (c.GetPosition() - pos).sqrMagnitude))
		{
            if (_chestRanges.TryGetValue(chest.m_uid, out float range) &&
                (chest.GetPosition() - pos).sqrMagnitude <= range * range &&
                TakeOneFood(chest, s.FoodHashes, _feeding.LeaveAtLeast))
            {
                if (ShouldDebug(animal))
                {
                    Debug($"[FEEDING] {Describe(animal)}: " +
                        $"successfully ate from chest {chest.m_uid}. " +
                        $"Distance={Vector3.Distance(chest.GetPosition(), pos):0.00}m, " +
                        $"AllowedRange={range:0.00}m.");
                }

                return true;
            }
        }
        return false;
	}

    // Takes one food item out of a chest by editing its saved inventory bytes in place: a stack is
    // decremented or the item entry removed. Working on the bytes keeps every other item exactly as it
    // was, even items the server's ObjectDB doesn't know.
    private bool TakeOneFood(ZDO chest, HashSet<int> foods, int leaveAtLeast)
    {
        if (chest.GetBool(ZDOVars.s_inUse))
		{
			return false;
		}
		// Chests store their inventory as a raw ZPackage byte array (Container.Save).
		byte[] data = chest.GetByteArray(ZDOVars.s_items);
		if (data == null || data.Length == 0)
		{
			return false;
		}
		byte[] bytes;
		int count;
		List<(int Start, int End, int Hash, int Stack)> items = new List<(int, int, int, int)>();
		try
		{
			ZPackage pkg = new ZPackage(data);
			int version = pkg.ReadInt();
            if (version < (int)global::Version.Item.Smaller)
            {
                if (ShouldDebug(chest))
                {
                    Debug($"[FEEDING] Chest {chest.m_uid}: " +
                        $"unsupported inventory version {version}.");
                }

                return false;
            }
            count = pkg.ReadUShort();
			for (int i = 0; i < count; i++)
			{
				int start = pkg.GetPos();
				ItemDrop.ItemData item = new ItemDrop.ItemData();
				int hash = ItemDrop.ItemData.Load(pkg, item, (global::Version.Item)version);
				items.Add((start, pkg.GetPos(), hash, item.m_stack));
			}
			bytes = pkg.GetArray();
		}

        catch (Exception ex)
        {
            if (ShouldDebug(chest))
            {
                Debug($"[FEEDING] Chest {chest.m_uid}: " +
                    $"failed to deserialize inventory. {ex}");
            }

            return false;
        }

        foreach (int food in foods)
		{
			List<(int Start, int End, int Hash, int Stack)> stacks = items.Where(x => x.Hash == food && x.Stack > 0).OrderBy(x => x.Stack).ToList();
			if (stacks.Sum(x => x.Stack) - 1 < Math.Max(0, leaveAtLeast))
			{
				continue;
			}
			var target = stacks[0];
			// Item layout: int durability, byte x, byte y, byte worldLevel, byte flags, [ushort quality], [ushort stack], ...
			byte flags = bytes[target.Start + 7];
			byte[] result;
			if (target.Stack > 1 && (flags & 8) != 0)
			{
				result = (byte[])bytes.Clone();
				int offset = target.Start + 8 + ((flags & 4) != 0 ? 2 : 0);
				ushort stack = (ushort)(target.Stack - 1);
				result[offset] = (byte)(stack & 0xFF);
				result[offset + 1] = (byte)(stack >> 8);
			}
			else
			{
				result = new byte[bytes.Length - (target.End - target.Start)];
				Buffer.BlockCopy(bytes, 0, result, 0, target.Start);
				Buffer.BlockCopy(bytes, target.End, result, target.Start, bytes.Length - target.End);
				ushort remaining = (ushort)(count - 1);
				result[4] = (byte)(remaining & 0xFF);
				result[5] = (byte)(remaining >> 8);
			}
			if (!chest.IsOwner())
			{
				chest.SetOwner(ZDOMan.GetSessionID());
			}

            chest.Set(ZDOVars.s_items, result);

            if (ShouldDebug(chest))
            {
                GameObject foodPrefab = ZNetScene.instance.GetPrefab(food);
                string foodName = foodPrefab != null
                    ? foodPrefab.name
                    : food.ToString();

                byte[] verification = chest.GetByteArray(ZDOVars.s_items);
                bool writeMatches = verification != null &&
                    verification.SequenceEqual(result);

                Debug($"[FEEDING] Chest {chest.m_uid}: " +
                    $"consumed {foodName}. " +
                    $"Stack={target.Stack} -> {target.Stack - 1}, " +
                    $"InventoryWriteVerified={writeMatches}, " +
                    $"Owner={chest.GetOwner()}, " +
                    $"IsOwner={chest.IsOwner()}.");
            }

            return true;

        }
        return false;
	}

    private bool TryEatFromGround(ZDO animal, Species s)
    {
        Vector3 pos = animal.GetPosition();
        float best = _feedRadius.Value * _feedRadius.Value;
        ZDO food = null;

        foreach (ZDO item in _foods)
        {
            if (!s.FoodHashes.Contains(item.GetPrefab()) || _eaten.Contains(item.m_uid))
            {
                continue;
            }

            Vector3 d = item.GetPosition() - pos;

            if (Mathf.Abs(d.y) > 4f)
            {
                continue;
            }

            float d2 = d.x * d.x + d.z * d.z;

            if (d2 <= best)
            {
                best = d2;
                food = item;
            }
        }

        if (food == null)
        {
            return false;
        }

        if (ShouldDebug(animal))
        {
            Debug($"[FEEDING] {Describe(animal)}: " +
                $"ground food detected. " +
                $"FoodZDO={food.m_uid}, " +
                $"Distance={Mathf.Sqrt(best):0.00}m, " +
                $"Owner={food.GetOwner()}, " +
                $"ServerID={ZDOMan.GetSessionID()}.");
        }

        // Own the item before changing it so the client
        // that simulated it cannot overwrite the change.
        if (!food.IsOwner())
        {
            food.SetOwner(ZDOMan.GetSessionID());

            if (ShouldDebug(animal))
            {
                Debug($"[FEEDING] Ground food {food.m_uid}: " +
                    $"ownership requested. " +
                    $"Owner={food.GetOwner()}, " +
                    $"IsOwner={food.IsOwner()}.");
            }
        }

        GameObject prefab = ZNetScene.instance.GetPrefab(food.GetPrefab());
        ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;

        if (drop == null || drop.m_itemData == null)
        {
            if (ShouldDebug(animal))
            {
                Debug($"[FEEDING] {Describe(animal)}: " +
                    $"ground food {food.m_uid} has no valid ItemDrop data.");
            }

            return false;
        }

        ItemDrop.ItemData foodData = drop.m_itemData.Clone();
        foodData.m_stack = 0;

        // Read the actual dropped-item data using Valheim's serialization.
        ItemDrop.LoadFromZDO(foodData, food, -1);

        if (ShouldDebug(animal))
        {
            Debug($"[FEEDING] {Describe(animal)}: " +
                $"reading {prefab.name}, " +
                $"FoodZDO={food.m_uid}, " +
                $"Stack={foodData.m_stack}, " +
                $"Owner={food.GetOwner()}.");
        }

        if (foodData.m_stack <= 0)
        {
            Logger.LogWarning($"Ground food has an invalid stack count at {Format(food.GetPosition())}; skipping.");
            return false;
        }

        // Consume exactly one item.
        if (foodData.m_stack > 1)
        {
            int previousStack = foodData.m_stack;

            foodData.m_stack--;
            ItemDrop.SaveToZDO(foodData, food, -1);

            if (ShouldDebug(animal))
            {
                // Reload the ZDO into a fresh ItemData object
                // to verify the saved stack quantity.
                ItemDrop.ItemData verificationData = drop.m_itemData.Clone();
                verificationData.m_stack = 0;

                ItemDrop.LoadFromZDO(verificationData, food, -1);

                Debug($"[FEEDING] {Describe(animal)}: " +
                    $"consumed {prefab.name} from ground stack. " +
                    $"FoodZDO={food.m_uid}, " +
                    $"Before={previousStack}, " +
                    $"Expected={previousStack - 1}, " +
                    $"Actual={verificationData.m_stack}, " +
                    $"Owner={food.GetOwner()}, " +
                    $"IsOwner={food.IsOwner()}.");

                if (verificationData.m_stack != previousStack - 1)
                {
                    Debug($"[FEEDING] STACK MISMATCH: " +
                        $"FoodZDO={food.m_uid}, " +
                        $"expected {previousStack - 1}, " +
                        $"read back {verificationData.m_stack}.");
                }
            }
        }
        else
        {
            if (ShouldDebug(animal))
            {
                Debug($"[FEEDING] {Describe(animal)}: " +
                    $"consumed last {prefab.name} from ground. " +
                    $"FoodZDO={food.m_uid}, " +
                    $"Stack=1 -> 0. Destroying food ZDO.");
            }

            _eaten.Add(food.m_uid);
            ZDOMan.instance.DestroyZDO(food);
        }

        return true;
    }



    private void UpdateTaming(ZDO zdo, Species s, State st, bool fed, bool calm, float dt)
    {
        float total = TamingSeconds(s);

        if (st.TameLeft < 0f || st.TameLeft > total)
        {
            st.TameLeft = total;
        }

        if (fed && calm && st.TameLeft > 0f)
        {
            if (ShouldDebug(zdo) && st.TameLeft == total && dt > 0f)
            {
                Debug($"[TAMING] {Describe(zdo)}: " +
                    $"taming started. " +
                    $"Required time={total / 60f:0.00} min.");
            }

            int before = Step(st.TameLeft, total);
            float previousLeft = st.TameLeft;

            st.TameLeft = Mathf.Max(0f, st.TameLeft - dt);

            int after = Step(st.TameLeft, total);

            if (ShouldDebug(zdo) && st.TameLeft < previousLeft)
            {
                Debug($"[TAMING] {Describe(zdo)}: " +
                    $"progress {(1f - previousLeft / total):P1} -> " +
                    $"{(1f - st.TameLeft / total):P1}. " +
                    $"Remaining={st.TameLeft:0.0}s.");
            }

            if (_progressMessages.Value && !_feeding.FromTameAssist && after > before && after < 4)
            {
                Message(zdo.GetPosition(), MessageHud.MessageType.TopLeft, $"{s.Name}: taming {after * 25}%");
            }
        }

        // Like TameAssist: once taming has started, show progress with its message type on every update.
        if (_progressMessages.Value && _feeding.FromTameAssist && st.TameLeft > 0f && st.TameLeft < total)
        {
            string text = string.Format(fed ? "$hud_tameness {0:P0}" : "$hud_tameness {0:P0}, $hud_tamehungry", 1f - st.TameLeft / total);
            TameAssistMessage(zdo.GetPosition(), text);
        }

        if (st.TameLeft <= 0f)
        {
            Tame(zdo, s, st);
        }
    }


    // Level 1 is no star, level 3 is two stars.
    private bool CanTame(ZDO zdo)
	{
		return _maxTameStars.Value < 0 || zdo.GetInt(ZDOVars.s_level, 1) - 1 <= _maxTameStars.Value;
	}

	private static int Step(float left, float total)
	{
		return (int)((1f - left / total) * 4f);
	}

	// Repeats every tick until the flag is set, in case ownership moved while the RPC was in flight.
	private void Tame(ZDO zdo, Species s, State st)
	{
		long owner = zdo.GetOwner();
		if (owner == 0L || owner == ZDOMan.GetSessionID())
		{
			zdo.Set(ZDOVars.s_tamed, value: true);
		}
		else
		{
			ZRoutedRpc.instance.InvokeRoutedRPC(owner, zdo.m_uid, "RPC_SetTamed", true);
		}
		if (!st.TameAnnounced)
		{
			st.TameAnnounced = true;
			Message(zdo.GetPosition(), MessageHud.MessageType.Center, s.Name + " $hud_tamedone");
			Logger.LogInfo($"Tamed {s.Prefab} at {Format(zdo.GetPosition())}.");

            if (ShouldDebug(zdo))
            {
                Debug($"[TAMING] {Describe(zdo)}: " +
                    $"taming countdown completed. " +
                    $"Method={(owner == 0L || owner == ZDOMan.GetSessionID() ? "direct ZDO update" : "RPC_SetTamed")}, " +
                    $"TamedFlag={zdo.GetBool(ZDOVars.s_tamed)}.");
            }

        }
    }


    private void UpdateBreeding(ZDO zdo, Species s, State st, bool ready, double now)
    {
        if (st.Pregnant > 0.0)
        {
            if (now - st.Pregnant >= s.PregnancyMinutes.Value * 60.0)
            {
                st.Pregnant = 0.0;

                // Checked again: the herd may have grown
                // (or wandered in) during the pregnancy.
                string crowded = Crowded(zdo, s, atBirth: true);
                if (crowded != null)
                {
                    Logger.LogInfo($"{s.Prefab} birth at {Format(zdo.GetPosition())} skipped: {crowded}.");

                    if (ShouldDebug(zdo))
                    {
                        Debug($"[BREEDING] {Describe(zdo)}: " +
                            $"pregnancy completed, but birth blocked: {crowded}.");
                    }

                    return;
                }

                if (ShouldDebug(zdo))
                {
                    Debug($"[PREGNANCY] {Describe(zdo)}: " +
                        "pregnancy completed; attempting birth.");
                }

                Birth(zdo, s);
            }
            return;
        }

        if (!ready || now < st.NextBreedCheck)
        {
            return;
        }

        float interval = Mathf.Max(1f, _breedCheckSeconds.Value);
        st.NextBreedCheck = now + interval * UnityEngine.Random.Range(1f, 1.5f);

        if (ShouldDebug(zdo))
        {
            Debug($"[BREEDING] {Describe(zdo)}: " +
                $"attempting affection gain. " +
                $"Current love={st.Love}/{Mathf.Max(1, _requiredLovePoints.Value)}, " +
                $"Fed for another {(st.FedUntil - now) / 60.0:0.00} min.");
        }

        float roll = UnityEngine.Random.value;

        if (roll > _loveChance.Value)
        {
            if (ShouldDebug(zdo))
            {
                Debug($"[BREEDING] {Describe(zdo)}: " +
                    $"affection roll failed ({roll:P1}; " +
                    $"required <= {_loveChance.Value:P1}).");
            }

            return;
        }

        string full = Crowded(zdo, s, atBirth: false);
        if (full != null)
        {
            if (ShouldDebug(zdo))
            {
                Debug($"[BREEDING] {Describe(zdo)}: " +
                    $"affection blocked: {full}.");
            }

            return;
        }

        if (++st.Love >= Mathf.Max(1, _requiredLovePoints.Value))
        {
            if (ShouldDebug(zdo))
            {
                Debug($"[AFFECTION] {Describe(zdo)}: " +
                    $"gained affection point " +
                    $"{st.Love}/{Mathf.Max(1, _requiredLovePoints.Value)}.");
            }

            st.Love = 0;
            st.Pregnant = now;

            if (ShouldDebug(zdo))
            {
                Debug($"[PREGNANCY] {Describe(zdo)}: " +
                    $"became pregnant. " +
                    $"Expected gestation: {s.PregnancyMinutes.Value:0.00} min.");
            }
        }
        else if (ShouldDebug(zdo))
        {
            Debug($"[AFFECTION] {Describe(zdo)}: " +
                $"gained affection point " +
                $"{st.Love}/{Mathf.Max(1, _requiredLovePoints.Value)}.");
        }
    }


    // Why this animal can't breed now, or null. Before a love point, pregnant animals nearby count once
    // more each for the birth they will give, so many pregnancies at once can't overshoot MaxCreatures.
    // At birth only the animals themselves count, and no partner is needed.
    private string Crowded(ZDO zdo, Species s, bool atBirth)
	{
		Vector3 pos = zdo.GetPosition();
		float population2 = _populationRange.Value * _populationRange.Value;
		float partner2 = _partnerRange.Value * _partnerRange.Value;
		int count = 0;
		int tamed = 0;
		bool partner = false;
		foreach (ZDO other in _animals)
		{
			if (other.GetPrefab() != s.Hash)
			{
				continue;
			}
			bool otherTamed = other.GetBool(ZDOVars.s_tamed);
			if (otherTamed)
			{
				tamed++;
			}
			float d2 = (other.GetPosition() - pos).sqrMagnitude;
			if (d2 <= population2)
			{
				count++;
				if (!atBirth && other != zdo && _states.TryGetValue(other.m_uid, out State os) && os.Pregnant > 0.0)
				{
					count++;
				}
			}
			if (other != zdo && d2 <= partner2 && otherTamed)
			{
				partner = true;
			}
		}
        if (s.Prefab == "Seal")
        {
            foreach (ZDO pup in _pups)
            {
                float distance2 = (pup.GetPosition() - pos).sqrMagnitude;

                if (distance2 <= population2)
                {
                    count++;
                }

                if (pup.GetBool(ZDOVars.s_tamed))
                {
                    tamed++;
                }
            }
        }
        int max = s.MaxCreatures.Value >= 0 ? s.MaxCreatures.Value : _maxCreatures.Value;
		if (!atBirth && !partner)
		{
			return "no tamed partner in range";
		}
		if (count >= max)
		{
			return $"{count} of MaxCreatures {max} nearby{(atBirth ? "" : ", counting pregnancies")}";
		}
		if (_maxTamedNearby.Value > 0 && tamed >= _maxTamedNearby.Value)
		{
			return $"{tamed} tamed {s.Prefab} around players, MaxTamedNearby {_maxTamedNearby.Value}";
		}
		return null;
	}


	// Creates adult with original pup's position, rotation, star level, and tamed status, then removes the pup
    private void GrowSealPups(double now)
    {
        if (!_speciesByPrefab.TryGetValue("Seal".GetStableHashCode(), out Species seal))
        {
            return;
        }

        if (seal.View == null)
        {
            return;
        }

        double growSeconds = Mathf.Max(1f, _sealPupGrowMinutes.Value) * 60.0;

        foreach (ZDO pup in _pups)
        {
            if (!pup.IsValid() || !pup.GetBool(ZDOVars.s_tamed))
            {
                continue;
            }

            long born = pup.GetLong(KeyPupBorn, 0L);

            // Wild pups and pups not born through this mod have no timestamp.
            if (born <= 0L)
            {
                continue;
            }

            double ageSeconds = now - born / 1000.0;

            if (ageSeconds < growSeconds)
            {
                continue;
            }

            Vector3 pos = pup.GetPosition();
            int level = pup.GetInt(ZDOVars.s_level, 1);

            ZDO adult = ZDOMan.instance.CreateNewZDO(pos, seal.Hash);

            adult.Persistent = seal.View.m_persistent;
            adult.Type = seal.View.m_type;
            adult.Distant = seal.View.m_distant;

            adult.SetPrefab(seal.Hash);
            adult.SetRotation(pup.GetRotation());
            adult.Set(ZDOVars.s_tamed, true);

            if (level > 1)
            {
                adult.Set(ZDOVars.s_level, level);
            }

            adult.SetOwner(0L);

            // Remove the original pup after its adult replacement exists.
            if (!pup.IsOwner())
            {
                pup.SetOwner(ZDOMan.GetSessionID());
            }

            ZDOMan.instance.DestroyZDO(pup);

            Logger.LogInfo($"Seal pup matured into an adult at {Format(pos)}.");

            if (ShouldDebug(pos))
            {
                Debug($"[MATURATION] Seal pup matured into adult " +
                    $"{adult.m_uid} ({level - 1} star(s)) " +
                    $"at {Format(pos)}. " +
                    $"Age={ageSeconds / 60.0:0.00} min, " +
                    $"Required={growSeconds / 60.0:0.00} min, " +
                    $"Tamed={adult.GetBool(ZDOVars.s_tamed)}, " +
                    $"Owner={adult.GetOwner()}.");
            }

        }
    }


    private int SelectSealOffspringLevel(ZDO parent, Vector3 birthPosition)
    {
        int parentStars = Mathf.Max(0, parent.GetInt(ZDOVars.s_level, 1) - 1);

        // Original ServerTaming behavior: no change whatsoever.
        if (!_enableSealStarUpgrades.Value)
            return parentStars + 1;

        int maxStars = Mathf.Max(0, _sealMaxOffspringStars.Value);
        int stars = Mathf.Min(parentStars, maxStars);

        if (_selectiveSealStarBreeding.Value)
        {
            int highStarCount = CountNearbyHighStarSeals(birthPosition);

            if (highStarCount >= _sealMaxTwoStarSeals.Value)
            {
                // Quota reached: mostly 0-star, occasionally 1-star.
                float chance = Mathf.Clamp(
                    _sealAtCapacityOneStarChance.Value, 0f, 100f);

                int fallbackStars = UnityEngine.Random.value < chance / 100f ? 1 : 0;
                return Mathf.Min(fallbackStars, maxStars) + 1;
            }
        }

        // Normal inheritance, with one possible star upgrade.
        if (stars < maxStars &&
            UnityEngine.Random.value < Mathf.Clamp(
                _sealStarUpgradeChance.Value, 0f, 100f) / 100f)
        {
            stars++;
        }

        return stars + 1; // Convert stars to Valheim's internal level.
    }

    private int CountNearbyHighStarSeals(Vector3 position)
    {
        float radius = Mathf.Max(0f, _sealStarCheckRadius.Value);
        float radiusSq = radius * radius;
        int count = 0;
        int sealHash = "Seal".GetStableHashCode();

        // Existing adult seals.
        foreach (ZDO animal in _animals)
        {
            if (animal.GetPrefab() == sealHash &&
                animal.GetInt(ZDOVars.s_level, 1) >= 3 &&
                (animal.GetPosition() - position).sqrMagnitude <= radiusSq)
            {
                count++;
            }
        }

        // Existing seal pups.
        foreach (ZDO pup in _pups)
        {
            if (pup.GetInt(ZDOVars.s_level, 1) >= 3 &&
                (pup.GetPosition() - position).sqrMagnitude <= radiusSq)
            {
                count++;
            }
        }

        // High-star pups born earlier in this same update.
        foreach (Vector3 birth in _highStarSealBirthsThisTick)
        {
            if ((birth - position).sqrMagnitude <= radiusSq)
            {
                count++;
            }
        }

        return count;
    }

    // Creates the offspring directly as a ZDO (like ZNetView.Awake does) so the server does not need
    // the area loaded; the nearest client takes ownership and instantiates a vanilla tamed adult.
    // The server has no colliders loaded, so it can't see fences. Both parents are inside the pen, so
    // the offspring goes somewhere on the line between the mother and her nearest tamed partner, which
    // stays inside any convex pen; without a partner nearby it appears on the mother.
    private void Birth(ZDO parent, Species s)
	{
        bool isSeal = s.Prefab == "Seal";

        if (isSeal && _sealPupView == null)
        {
            Logger.LogWarning("Cannot spawn Seal_Pup: prefab was not resolved.");
            return;
        }

        int offspringHash = isSeal ? SealPupHash : s.Hash;
        ZNetView offspringView = isSeal ? _sealPupView : s.View;
        Vector3 mother = parent.GetPosition();
		ZDO partner = _animals.Where(a => a != parent && a.GetPrefab() == s.Hash && a.GetBool(ZDOVars.s_tamed) && (a.GetPosition() - mother).sqrMagnitude <= 100f)
			.OrderBy(a => (a.GetPosition() - mother).sqrMagnitude).FirstOrDefault();
		Vector3 pos = partner == null ? mother : Vector3.Lerp(mother, partner.GetPosition(), UnityEngine.Random.Range(0.3f, 0.7f));
		pos.y += 0.5f;
        ZDO zdo = ZDOMan.instance.CreateNewZDO(pos, offspringHash);
        zdo.Persistent = offspringView.m_persistent;
        zdo.Type = offspringView.m_type;
        zdo.Distant = offspringView.m_distant;
        zdo.SetPrefab(offspringHash);
        zdo.SetRotation(Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
		zdo.Set(ZDOVars.s_tamed, value: true);
        int level = isSeal
			? SelectSealOffspringLevel(parent, pos)
			: parent.GetInt(ZDOVars.s_level, 1);

        if (level > 1)
        {
            zdo.Set(ZDOVars.s_level, level);
        }
        if (isSeal)
        {
            zdo.Set(KeyPupBorn, (long)(ZNet.instance.GetTimeSeconds() * 1000.0));

            if (_enableSealStarUpgrades.Value &&
                _selectiveSealStarBreeding.Value &&
                level >= 3)
            {
                _highStarSealBirthsThisTick.Add(pos);
            }
        }
        zdo.SetOwner(0L);
		Message(pos, MessageHud.MessageType.Center, "A new " + s.Name + " was born");
        Logger.LogInfo($"{s.Prefab} born at {Format(pos)} ({level - 1} star(s)).");

        if (ShouldDebug(parent))
        {
            Debug($"[BIRTH] {Describe(parent)} gave birth to " +
                $"{(isSeal ? "Seal_Pup" : s.Prefab)} {zdo.m_uid} " +
                $"({level - 1} star(s)) at {Format(pos)}. " +
                $"Tamed={zdo.GetBool(ZDOVars.s_tamed)}, " +
                $"Owner={zdo.GetOwner()}, " +
                $"Partner={(partner != null ? partner.m_uid.ToString() : "none")}.");
        }

    }

    // TameAssist's MessageTypes: None, TopLeftNear, TopLeftFar, CenterNear, CenterFar, InWorld.
    private void TameAssistMessage(Vector3 pos, string text)
	{
		string type = _feeding.ProgressMessageType;
		if (type == "InWorld")
		{
			float range2 = _messageRange.Value * _messageRange.Value;
			foreach (ZNetPeer peer in ZNet.instance.GetPeers())
			{
				if ((peer.GetRefPos() - pos).sqrMagnitude <= range2)
				{
					// Same payload as DamageText.ShowText: type, position, text, from-player flag.
					ZPackage pkg = new ZPackage();
					pkg.Write((int)DamageText.TextType.Normal);
					pkg.Write(pos);
					pkg.Write(text);
					pkg.Write(false);
					ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "RPC_DamageText", pkg);
				}
			}
		}
		else if (type.StartsWith("TopLeft", StringComparison.Ordinal))
		{
			Message(pos, MessageHud.MessageType.TopLeft, text);
		}
		else if (type.StartsWith("Center", StringComparison.Ordinal))
		{
			Message(pos, MessageHud.MessageType.Center, text);
		}
	}

	private void Message(Vector3 pos, MessageHud.MessageType type, string text)
	{
		float range2 = _messageRange.Value * _messageRange.Value;
		foreach (ZNetPeer peer in ZNet.instance.GetPeers())
		{
			if ((peer.GetRefPos() - pos).sqrMagnitude <= range2)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", (int)type, text);
			}
		}
	}

	private void WriteToZdos()
	{
		foreach (KeyValuePair<ZDOID, State> pair in _states)
		{
			if (ZDOMan.instance.GetZDO(pair.Key) == null)
			{
				continue;
			}
			// No revision bump: this only needs to reach the save, not the clients.
			ZDOExtraData.Set(pair.Key, KeyTameLeft, pair.Value.TameLeft);
			ZDOExtraData.Set(pair.Key, KeyFedUntil, (long)(pair.Value.FedUntil * 1000.0));
			ZDOExtraData.Set(pair.Key, KeyLove, pair.Value.Love);
			ZDOExtraData.Set(pair.Key, KeyPregnant, (long)(pair.Value.Pregnant * 1000.0));
		}
	}

	[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.PrepareSave))]
	private static class SavePatch
	{
		private static void Prefix()
		{
			try
			{
				_instance?.WriteToZdos();
			}
			catch (Exception ex)
			{
				_instance?.Logger.LogError(ex);
			}
		}
	}

    private void Status(Terminal.ConsoleEventArgs args)
    {
        Terminal output = args.Context;

        if (ZNet.instance == null ||
            !ZNet.instance.IsServer())
        {
            Print(output,
                "Use 'server tamestatus [radius]' on a server.");
            return;
        }

        float radius = 30f;

        if (args.Args.Length > 1 &&
            float.TryParse(args.Args[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float r) &&
            !float.IsNaN(r) &&
            !float.IsInfinity(r) &&
            r > 0f)
        {
            radius = Mathf.Clamp(r, 1f, 100f);
        }

        Vector3? center = CallerPosition();

        if (!center.HasValue)
        {
            Print(output, "Could not find your position.");
            return;
        }

        if (!_resolved)
            Resolve();

        _feeding = ReadFeeding();
        Collect();
        UpdateChestRanges();

        double now = ZNet.instance.GetTimeSeconds();
        float radiusSq = radius * radius;

        List<ZDO> adults = _animals
            .Where(z => z.IsValid() &&
                (z.GetPosition() - center.Value)
                    .sqrMagnitude <= radiusSq)
            .OrderBy(z => z.GetPrefab())
            .ThenBy(z =>
                (z.GetPosition() - center.Value).sqrMagnitude)
            .ToList();

        List<ZDO> pups = _pups
            .Where(z => z.IsValid() &&
                (z.GetPosition() - center.Value)
                    .sqrMagnitude <= radiusSq)
            .ToList();

        foreach (ZDO animal in _animals)
            GetState(animal);

        Print(output, "========== STE STATUS ==========");
        Print(output, $"Search radius: {radius:0} m");
        Print(output,
            $"Adults: {adults.Count} | Seal pups: {pups.Count}");

        foreach (Species s in _species.Where(x => x.Enabled.Value))
        {
            List<ZDO> group = adults
                .Where(z => z.GetPrefab() == s.Hash)
                .ToList();

            List<ZDO> speciesPups = s.Prefab == "Seal"
                ? pups
                : new List<ZDO>();

            if (group.Count == 0 && speciesPups.Count == 0)
                continue;

            int tamed = group.Count(z =>
                z.GetBool(ZDOVars.s_tamed));

            int pregnant = group.Count(z =>
                z.GetBool(ZDOVars.s_tamed) &&
                GetState(z).Pregnant > 0.0);

            int physical = group.Count + speciesPups.Count;
            int effective = physical + pregnant;

            int max = s.MaxCreatures.Value >= 0
                ? s.MaxCreatures.Value
                : _maxCreatures.Value;

            Print(output, $"----- {s.Prefab} -----");

            Print(output,
                $"Adults: {group.Count} | Tamed: {tamed} | " +
                $"Wild: {group.Count - tamed} | " +
                $"Pregnant: {pregnant}");

            Print(output,
                $"Physical population: {physical} | " +
                $"Including pregnancies: {effective}");

            Print(output,
                $"Configured breeding limit: {max} " +
                $"within {_populationRange.Value:0} m per animal");

            string distribution = string.Join(", ",
                group.Concat(speciesPups)
                    .GroupBy(z => Mathf.Max(0,
                        z.GetInt(ZDOVars.s_level, 1) - 1))
                    .OrderBy(g => g.Key)
                    .Select(g =>
                        $"{g.Key}-star: {g.Count()}"));

            Print(output, $"Star distribution: {distribution}");

            if (s.Prefab == "Seal" &&
                _enableSealStarUpgrades.Value &&
                _selectiveSealStarBreeding.Value)
            {
                int highStars = group.Concat(speciesPups)
                    .Count(z =>
                        z.GetInt(ZDOVars.s_level, 1) >= 3);

                Print(output,
                    $"High-star seals in report: {highStars} | " +
                    $"Configured local quota: " +
                    $"{_sealMaxTwoStarSeals.Value}");
            }

            foreach (ZDO zdo in group)
            {
                State st = GetState(zdo);

                bool isTamed = zdo.GetBool(ZDOVars.s_tamed);
                bool fed = now < st.FedUntil;
                bool calm = !_requireCalm.Value ||
                    !zdo.GetBool(ZDOVars.s_alert);

                int stars = Mathf.Max(0,
                    zdo.GetInt(ZDOVars.s_level, 1) - 1);

                float distance = Vector3.Distance(
                    zdo.GetPosition(), center.Value);

                float total = TamingSeconds(s);

                float left = st.TameLeft < 0f
                    ? total
                    : Mathf.Clamp(st.TameLeft, 0f, total);

                float tamingPercent = isTamed
                    ? 100f
                    : Mathf.Clamp01(1f - left / total) * 100f;

                string foodStatus = fed
                    ? $"Fed {Math.Max(0.0, st.FedUntil - now) / 60.0:0.00}m"
                    : "Hungry";

                string breedingStatus;

                if (!isTamed)
                {
                    breedingStatus = !CanTame(zdo)
                        ? "Taming blocked by star limit"
                        : $"{left:0}s taming remaining";
                }
                else if (st.Pregnant > 0.0)
                {
                    double remaining = Math.Max(0.0,
                        s.PregnancyMinutes.Value * 60.0 -
                        (now - st.Pregnant));

                    breedingStatus =
                        $"Pregnant, {remaining / 60.0:0.00}m left";
                }
                else if (!s.Breeding.Value)
                {
                    breedingStatus = "Breeding disabled";
                }
                else if (!fed || !calm)
                {
                    breedingStatus = "Breeding paused";
                }
                else
                {
                    breedingStatus =
                        Crowded(zdo, s, atBirth: false)
                        ?? "Ready to breed";
                }

                Print(output,
                    $"{s.Prefab} {zdo.m_uid} | " +
                    $"{stars}-star | {distance:0.0}m | " +
                    $"Taming {tamingPercent:0.0}% | " +
                    $"{foodStatus} | " +
                    $"{(calm ? "Calm" : "Alerted")} | " +
                    $"Affection {st.Love}/" +
                    $"{Mathf.Max(1, _requiredLovePoints.Value)} | " +
                    breedingStatus);
            }

            foreach (ZDO pup in speciesPups)
            {
                int stars = Mathf.Max(0,
                    pup.GetInt(ZDOVars.s_level, 1) - 1);

                long born = pup.GetLong(KeyPupBorn, 0L);

                string growth = born > 0L
                    ? $"{Math.Max(0.0,
                        Mathf.Max(1f, _sealPupGrowMinutes.Value)
                        * 60.0 - (now - born / 1000.0))
                        / 60.0:0.00}m until adulthood"
                    : "No STE growth timestamp";

                Print(output,
                    $"Seal pup {pup.m_uid} | {stars}-star | " +
                    $"{(pup.GetBool(ZDOVars.s_tamed)
                        ? "Tamed" : "Wild")} | " +
                    $"{Vector3.Distance(pup.GetPosition(),
                        center.Value):0.0}m | {growth}");
            }
        }

        Print(output,
            $"Chest feeding: " +
            $"{(_feeding.Containers ? "Enabled" : "Disabled")} | " +
            $"Range: {_feeding.Range:0}m | " +
            $"Eligible chests: {_chestRanges.Count}");

        HashSet<int> allFoods = new HashSet<int>(
            _species.SelectMany(x => x.FoodHashes));

        foreach (ZDO chest in _chests
            .Where(c => _chestRanges.ContainsKey(c.m_uid) &&
                (c.GetPosition() - center.Value).sqrMagnitude
                    <= radiusSq)
            .OrderBy(c =>
                (c.GetPosition() - center.Value).sqrMagnitude))
        {
            Print(output,
                $"Feed chest {chest.m_uid} | " +
                $"{Vector3.Distance(chest.GetPosition(),
                    center.Value):0.0}m | " +
                $"Range {_chestRanges[chest.m_uid]:0}m | " +
                $"{(chest.GetBool(ZDOVars.s_inUse)
                    ? "OPEN (skipped) | " : "")}" +
                DescribeFood(chest, allFoods));
        }

        Print(output, "================================");

        ZNetPeer caller = CallerPeer();

        if (caller != null)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(
                caller.m_uid,
                "ShowMessage",
                (int)MessageHud.MessageType.TopLeft,
                $"STE: {adults.Count} adults, {pups.Count} pups. " +
                "See F5 console for details.");
        }
    }

    private static string DescribeFood(ZDO chest, HashSet<int> foods)
	{
		byte[] data = chest.GetByteArray(ZDOVars.s_items);
		if (data == null || data.Length == 0)
		{
			return "empty";
		}
		try
		{
			ZPackage pkg = new ZPackage(data);
			int version = pkg.ReadInt();
			if (version < (int)global::Version.Item.Smaller)
			{
				return $"old inventory format {version}, skipped";
			}
			int count = pkg.ReadUShort();
			Dictionary<int, int> totals = new Dictionary<int, int>();
			for (int i = 0; i < count; i++)
			{
				ItemDrop.ItemData item = new ItemDrop.ItemData();
				int hash = ItemDrop.ItemData.Load(pkg, item, (global::Version.Item)version);
				if (foods.Contains(hash))
				{
					totals[hash] = (totals.TryGetValue(hash, out int t) ? t : 0) + item.m_stack;
				}
			}
			return totals.Count == 0 ? $"none ({count} other item stacks)"
				: string.Join(", ", totals.Select(x => (ZNetScene.instance.GetPrefab(x.Key)?.name ?? x.Key.ToString(CultureInfo.InvariantCulture)) + " " + x.Value));
		}
		catch (Exception ex)
		{
			return "unreadable (" + ex.Message + ")";
		}
	}

    // Capture the requesting player's native remote command connection.
    private static ZRpc _statusCaller;

    [HarmonyPatch(typeof(ZNet), "RPC_RemoteCommand")]
    private static class RemoteCommandPatch
    {
        private static void Prefix(ZRpc __0, string __1,
            out ZRpc __state)
        {
            __state = _statusCaller;

            if (string.Equals(
                __1?.Trim().Split(' ')[0],
                "tamestatus",
                StringComparison.OrdinalIgnoreCase))
            {
                _statusCaller = __0;
            }
        }

        private static Exception Finalizer(
            Exception __exception, ZRpc __state)
        {
            _statusCaller = __state;
            return __exception;
        }
    }

    private static ZNetPeer CallerPeer()
    {
        if (_statusCaller == null || ZNet.instance == null)
            return null;

        return ZNet.instance.GetPeers()
            .FirstOrDefault(p => p.m_rpc == _statusCaller);
    }

    private static Vector3? CallerPosition()
    {
        ZNetPeer peer = CallerPeer();

        if (peer != null)
            return peer.GetRefPos();

        if (ZNet.instance != null &&
            !ZNet.instance.IsDedicated())
        {
            return ZNet.instance.GetReferencePosition();
        }

        return null;
    }

    private void Print(Terminal output, string text)
    {
        // Always record command output, independently of DebugLog.
        Logger.LogInfo($"[TAMESTATUS] {text}");

        if (_statusCaller != null && ZNet.instance != null)
        {
            ZNet.instance.RemotePrint(_statusCaller, text);
        }
        else
        {
            (output ?? (Terminal)Console.instance)?.AddString(text);
        }
    }

    private static string Format(Vector3 pos)
	{
		return $"({pos.x:0}, {pos.y:0}, {pos.z:0})";
	}
}
