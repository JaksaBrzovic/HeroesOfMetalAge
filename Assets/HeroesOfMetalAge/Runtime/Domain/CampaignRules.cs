using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// Campaign commands and derived stats. Every command validates completely before it changes anything, so a rejection
    /// leaves the campaign untouched.
    /// </summary>
    public sealed class CampaignRules
    {
        public const int MAXIMUM_STACK_COUNT = 100000;
        public const int MAXIMUM_MORALE = 3;

        private readonly DefinitionCatalog catalog;

        public CampaignRules(DefinitionCatalog catalog)
        {
            this.catalog = catalog;
        }

        private MissionDefinition Mission => catalog.Mission;

        public CampaignState NewCampaign()
        {
            HeroDefinition hero = catalog.Hero(Mission.HeroId);
            CampaignState state = new CampaignState
            {
                Day = 1,
                Gold = Mission.StartingGold,
                Position = Mission.Start,
                Movement = hero.DailyMovement,
                HeroId = hero.Id,
                MissionId = Mission.Id,
                TownOwnerId = Mission.ClanId,
                Explored = new ExploredMap(Mission.Width, Mission.Height)
            };

            Scout(state);
            state.ArmyData.AddRange(hero.StartingArmy);
            foreach (string id in Mission.RecruitmentIds)
            {
                state.StockData.Add(id, catalog.Unit(id).WeeklyGrowth);
            }

            foreach (MapObjectDefinition dwelling in Dwellings())
            {
                state.DwellingStockData.Add(dwelling.Id, dwelling.WeeklyGrowth);
            }

            return state;
        }

        #region DERIVED STATS
        public int Level(CampaignState campaign)
        {
            return (int)Math.Min(int.MaxValue, 1L + campaign.Experience / catalog.Hero(campaign.HeroId).ExperiencePerLevel);
        }

        public int Attack(CampaignState campaign)
        {
            HeroDefinition hero = catalog.Hero(campaign.HeroId);
            long attack = (long)hero.BaseAttack + (long)(Level(campaign) - 1) * hero.AttackPerLevel + EquippedBonus(campaign, EquippedStat.Attack)
                + ShrineBonus(campaign, MapObjectKind.AttackShrine);
            return (int)Math.Min(int.MaxValue, attack);
        }

        public int Defense(CampaignState campaign)
        {
            HeroDefinition hero = catalog.Hero(campaign.HeroId);
            long defense = (long)hero.BaseDefense + (Level(campaign) - 1) / hero.DefenseLevelInterval + EquippedBonus(campaign, EquippedStat.Defense)
                + ShrineBonus(campaign, MapObjectKind.DefenseShrine);
            return (int)Math.Min(int.MaxValue, defense);
        }

        /// <summary>The army's morale from worn artefacts and map objects, between -3 and +3.</summary>
        public int Morale(CampaignState campaign)
        {
            long morale = (long)EquippedBonus(campaign, EquippedStat.Morale) + campaign.MoraleBonus;
            return (int)Math.Max(-MAXIMUM_MORALE, Math.Min(MAXIMUM_MORALE, morale));
        }

        /// <summary>The week of a campaign day, from 1.</summary>
        public int Week(CampaignState campaign)
        {
            return (campaign.Day - 1) / Mission.DaysPerWeek + 1;
        }

        public int SpeedBonus(CampaignState campaign)
        {
            return EquippedBonus(campaign, EquippedStat.Speed);
        }

        public int DailyMovement(CampaignState campaign)
        {
            return catalog.Hero(campaign.HeroId).DailyMovement;
        }

        /// <summary>Recruitment price per creature; the owned-town discount rounds up to whole gold.</summary>
        public int Price(CampaignState campaign, string unitId)
        {
            double multiplier = campaign.IsTownOwned ? Mission.OwnedPriceMultiplier : 1d;
            return Math.Max(1, (int)Math.Ceiling(catalog.Unit(unitId).Cost * multiplier));
        }

        /// <summary>True if the army can take the stack: merge into the same unit type or use a free slot.</summary>
        public bool CanAdd(IReadOnlyList<Troop> army, string unitId, int count = 1)
        {
            if (!catalog.HasUnit(unitId) || count <= 0 || count > MAXIMUM_STACK_COUNT)
            {
                return false;
            }

            int index = IndexOf(army, unitId);
            if (index >= 0)
            {
                return (long)army[index].Count + count <= MAXIMUM_STACK_COUNT;
            }

            return army.Count < Mission.ArmySlots;
        }
        #endregion

        #region COMMANDS
        public CommandResult Buy(CampaignState campaign, string unitId, int count)
        {
            if (!IsAtTown(campaign) || campaign.IsHeroHome)
            {
                return CommandResult.Reject("A visiting hero must be present to recruit.");
            }

            if (!Mission.IsRecruitable(unitId) || count <= 0 || count > campaign.Stock(unitId))
            {
                return CommandResult.Reject("Invalid recruitment quantity or stock.");
            }

            if (!CanAdd(campaign.Army, unitId, count))
            {
                return CommandResult.Reject("The army has no capacity for these recruits.");
            }

            long total = (long)Price(campaign, unitId) * count;
            if (total > campaign.Gold)
            {
                return CommandResult.Reject("Not enough gold.");
            }

            Add(campaign.ArmyData, new Troop(unitId, count));
            campaign.StockData[unitId] -= count;
            campaign.Gold -= (int)total;
            campaign.HasRecruited = true;
            return CommandResult.Ok();
        }

        public CommandResult Join(CampaignState campaign, EncounterDefinition encounter)
        {
            if (encounter.Kind != EncounterKind.Friendly || campaign.IsCompleted(encounter.Id))
            {
                return CommandResult.Reject("This group has already been resolved.");
            }

            List<Troop> merged = Merge(campaign.Army, encounter.Army);
            if (merged == null)
            {
                return CommandResult.Reject("The army cannot hold this group.");
            }

            campaign.ArmyData.Clear();
            campaign.ArmyData.AddRange(merged);
            campaign.CompletedData.Add(encounter.Id);
            return CommandResult.Ok();
        }

        /// <param name="isNewWeek">True when this day change also grew the recruitment stocks.</param>
        public CommandResult EndDay(CampaignState campaign, out bool isNewWeek)
        {
            isNewWeek = false;
            if (campaign.Day == int.MaxValue)
            {
                return CommandResult.Reject("Campaign day limit reached.");
            }

            bool isWeekEnd = campaign.Day % Mission.DaysPerWeek == 0;
            if (isWeekEnd && (WouldStockOverflow(campaign) || WouldDwellingOverflow(campaign)))
            {
                return CommandResult.Reject("Recruitment stock limit reached.");
            }

            campaign.Day++;
            campaign.Movement = DailyMovement(campaign);
            if (isWeekEnd)
            {
                foreach (string id in Mission.RecruitmentIds)
                {
                    campaign.StockData[id] += catalog.Unit(id).WeeklyGrowth;
                }

                foreach (MapObjectDefinition dwelling in Dwellings())
                {
                    campaign.DwellingStockData[dwelling.Id] = campaign.DwellingStock(dwelling.Id) + dwelling.WeeklyGrowth;
                }
            }

            isNewWeek = isWeekEnd;
            return CommandResult.Ok();
        }

        /// <summary>Moves one whole stack between the visiting army and the home garrison of an owned town.</summary>
        public CommandResult Transfer(CampaignState campaign, bool isFromGarrison, int index)
        {
            if (!IsAtTown(campaign) || !campaign.IsTownOwned || campaign.IsHeroHome)
            {
                return CommandResult.Reject("Both armies must belong to you and a visitor must be present.");
            }

            List<Troop> source = isFromGarrison ? campaign.GarrisonData : campaign.ArmyData;
            List<Troop> target = isFromGarrison ? campaign.ArmyData : campaign.GarrisonData;
            if (index < 0 || index >= source.Count || !CanAdd(target, source[index].UnitId, source[index].Count))
            {
                return CommandResult.Reject("Invalid transfer or destination capacity.");
            }

            Add(target, source[index]);
            source.RemoveAt(index);
            return CommandResult.Ok();
        }

        /// <summary>Toggles the hero between visiting and home; the two armies merge into the hero's new side.</summary>
        public CommandResult StationHero(CampaignState campaign)
        {
            if (!IsAtTown(campaign) || !campaign.IsTownOwned)
            {
                return CommandResult.Reject("This town is not yours.");
            }

            List<Troop> source = campaign.IsHeroHome ? campaign.GarrisonData : campaign.ArmyData;
            List<Troop> target = campaign.IsHeroHome ? campaign.ArmyData : campaign.GarrisonData;
            List<Troop> merged = Merge(target, source);
            if (merged == null)
            {
                return CommandResult.Reject("The combined army exceeds capacity.");
            }

            target.Clear();
            target.AddRange(merged);
            source.Clear();
            campaign.IsHeroHome = !campaign.IsHeroHome;
            return CommandResult.Ok();
        }

        /// <summary>
        /// Equips an inventory artefact in the first free place that fits it (a ring takes the left hand, then the right);
        /// when every fitting place is taken it replaces the first, and that artefact goes back to the inventory.
        /// </summary>
        public CommandResult Equip(CampaignState campaign, int index)
        {
            if (index < 0 || index >= campaign.Inventory.Count)
            {
                return CommandResult.Reject("Artefact is not in the inventory.");
            }

            EquipmentPlace[] places = Equipment.Places(catalog.Artefact(campaign.Inventory[index]).Slot);
            EquipmentPlace place = places[0];
            foreach (EquipmentPlace candidate in places)
            {
                if (campaign.Equipped(candidate) == null)
                {
                    place = candidate;
                    break;
                }
            }

            return EquipAt(campaign, index, place);
        }

        /// <summary>Equips an inventory artefact in a place it fits; whatever was worn there goes back to the inventory.</summary>
        public CommandResult EquipAt(CampaignState campaign, int index, EquipmentPlace place)
        {
            if (index < 0 || index >= campaign.Inventory.Count || !Enum.IsDefined(typeof(EquipmentPlace), place))
            {
                return CommandResult.Reject("Artefact is not in the inventory.");
            }

            string id = campaign.Inventory[index];
            if (!Equipment.Fits(catalog.Artefact(id).Slot, place))
            {
                return CommandResult.Reject("That artefact is not worn there.");
            }

            string previous = campaign.Equipped(place);
            campaign.InventoryData.RemoveAt(index);
            if (previous != null)
            {
                campaign.InventoryData.Add(previous);
            }

            campaign.EquippedData[(int)place] = id;
            return CommandResult.Ok();
        }

        public CommandResult Unequip(CampaignState campaign, EquipmentPlace place)
        {
            if (!Enum.IsDefined(typeof(EquipmentPlace), place))
            {
                return CommandResult.Reject("Unknown equipment place.");
            }

            string id = campaign.Equipped(place);
            if (id == null)
            {
                return CommandResult.Reject("Nothing is worn there.");
            }

            campaign.InventoryData.Add(id);
            campaign.EquippedData[(int)place] = null;
            return CommandResult.Ok();
        }

        public CommandResult Move(CampaignState campaign, WorldPosition next, WorldMap map)
        {
            if (campaign.IsHeroHome || !map.CanStep(campaign.Position, next))
            {
                return CommandResult.Reject("Invalid movement step.");
            }

            int cost = map.StepCost(campaign.Position, next);
            if (campaign.Movement < cost)
            {
                return CommandResult.Reject("Movement spent. End the day to continue.");
            }

            campaign.Position = next;
            campaign.Movement -= cost;
            Scout(campaign);
            return CommandResult.Ok();
        }

        /// <summary>
        /// Rolls the spoils of a won battle without changing the campaign: gold around the encounter's amount (always at
        /// least 1) and at most one artefact. A legendary artefact is forced when the previous victories brought none for
        /// as long as <see cref="RewardPolicy.LegendaryGuaranteeVictories"/> allows. Artefacts the hero does not own yet
        /// are preferred.
        /// </summary>
        /// <param name="victoriesWithoutLegendary">Victories in a row that brought no legendary artefact.</param>
        public VictoryRewards RollRewards(CampaignState campaign, EncounterDefinition encounter, IRandomSource random, int victoriesWithoutLegendary)
        {
            RewardPolicy policy = Mission.Rewards;
            double factor = 1 + (random.NextValue() * 2 - 1) * policy.GoldVariation;
            int gold = (int)Math.Max(1, Math.Min(int.MaxValue, Math.Round(encounter.RewardGold * factor, MidpointRounding.ToEven)));

            // a guarded treasure is the victory's artefact, whatever the dice say
            if (encounter.RewardArtefactId != null)
            {
                bool isGuardedLegendary = catalog.Artefact(encounter.RewardArtefactId).Rarity == ArtefactRarity.Legendary;
                return new VictoryRewards(gold, new[] { encounter.RewardArtefactId }, isGuardedLegendary);
            }

            ArtefactRarity? rarity = null;
            bool isLegendaryDue = policy.LegendaryGuaranteeVictories > 0 && victoriesWithoutLegendary >= policy.LegendaryGuaranteeVictories - 1;
            if (isLegendaryDue && HasArtefactOf(ArtefactRarity.Legendary))
            {
                rarity = ArtefactRarity.Legendary;
            }
            else if (random.NextValue() < policy.ArtefactChance)
            {
                double roll = random.NextValue();
                rarity = roll < policy.LegendaryChance ? ArtefactRarity.Legendary
                    : roll < policy.LegendaryChance + policy.EpicChance ? ArtefactRarity.Epic
                    : roll < policy.LegendaryChance + policy.EpicChance + policy.RareChance ? ArtefactRarity.Rare
                    : ArtefactRarity.Common;
            }

            List<string> artefacts = new List<string>();
            string artefactId = rarity.HasValue ? PickArtefact(campaign, rarity.Value, random) : null;
            if (artefactId != null)
            {
                artefacts.Add(artefactId);
            }

            bool hasLegendary = artefactId != null && catalog.Artefact(artefactId).Rarity == ArtefactRarity.Legendary;
            return new VictoryRewards(gold, artefacts, hasLegendary);
        }

        /// <summary>Applies battle survivors, experience and the rolled rewards exactly once per encounter.</summary>
        public CommandResult ApplyVictory(CampaignState campaign, EncounterDefinition encounter, IReadOnlyList<Troop> survivors, VictoryRewards rewards)
        {
            if (campaign.IsCompleted(encounter.Id))
            {
                return CommandResult.Reject("Victory has already been applied.");
            }

            if (rewards.Gold < 0 || (long)campaign.Gold + rewards.Gold > int.MaxValue || (long)campaign.Experience + encounter.RewardExperience > int.MaxValue)
            {
                return CommandResult.Reject("Reward exceeds campaign limits.");
            }

            foreach (string id in rewards.ArtefactIds)
            {
                if (!catalog.HasArtefact(id))
                {
                    return CommandResult.Reject("Unknown reward artefact: " + id);
                }
            }

            campaign.ArmyData.Clear();
            campaign.ArmyData.AddRange(survivors);
            campaign.Gold += rewards.Gold;
            campaign.Experience += encounter.RewardExperience;
            campaign.InventoryData.AddRange(rewards.ArtefactIds);
            // the blessings of map objects last until the next won battle
            campaign.MoraleBonus = 0;

            campaign.CompletedData.Add(encounter.Id);
            if (encounter.Kind == EncounterKind.Town)
            {
                campaign.TownOwnerId = CampaignState.PLAYER_OWNER_ID;
            }

            return CommandResult.Ok();
        }
        #endregion

        #region DEVELOPMENT
        public void DebugPrepareTown(CampaignState campaign)
        {
            campaign.Position = Mission.Town.Position;
            Scout(campaign);
        }

        /// <summary>Lifts the fog of war from the whole map for good.</summary>
        public void DebugRevealMap(CampaignState campaign)
        {
            campaign.Explored.RevealAll();
        }

        public void DebugGold(CampaignState campaign, int amount)
        {
            campaign.Gold = (int)Math.Min(int.MaxValue, (long)campaign.Gold + Math.Max(0, amount));
        }

        public void DebugArtefacts(CampaignState campaign)
        {
            foreach (ArtefactDefinition artefact in catalog.Artefacts)
            {
                campaign.InventoryData.Add(artefact.Id);
            }
        }
        #endregion

        #region RESTORE
        /// <summary>Rebuilds a campaign from a save record. Throws <see cref="ArgumentException"/> for anything inconsistent.</summary>
        public CampaignState Restore(CampaignSnapshot snapshot)
        {
            ValidateHeader(snapshot);
            WorldPosition position = new WorldPosition(snapshot.x, snapshot.y);
            if (!new WorldMap(Mission).IsWalkable(position))
            {
                throw new ArgumentException("Invalid hero position.");
            }

            CampaignState result = new CampaignState
            {
                MissionId = snapshot.missionId,
                HeroId = snapshot.heroId,
                Day = snapshot.day,
                Gold = snapshot.gold,
                Experience = snapshot.experience,
                Movement = snapshot.movement,
                Position = position,
                TownOwnerId = snapshot.townOwnerId,
                IsHeroHome = snapshot.heroHome,
                HasRecruited = snapshot.recruited,
                MoraleBonus = snapshot.morale,
                Explored = string.IsNullOrEmpty(snapshot.explored)
                    ? new ExploredMap(Mission.Width, Mission.Height)
                    : ExploredMap.Decode(snapshot.explored, Mission.Width, Mission.Height)
            };

            Scout(result);

            RestoreStacks(snapshot.army, result.ArmyData);
            RestoreStacks(snapshot.garrison, result.GarrisonData);
            RestoreInventory(snapshot, result);
            RestoreCompleted(snapshot, result);
            RestoreStock(snapshot, result);
            RestoreMapObjects(snapshot, result);
            ValidateArmies(snapshot, result);
            return result;
        }

        /// <summary>Explores what the hero sees from where he stands; a mission without fog of war shows the whole map.</summary>
        private void Scout(CampaignState campaign)
        {
            if (Mission.ScoutingRadius == 0)
            {
                campaign.Explored.RevealAll();
                return;
            }

            campaign.Explored.Reveal(campaign.Position, Mission.ScoutingRadius);
        }

        private void ValidateHeader(CampaignSnapshot snapshot)
        {
            bool isValid = snapshot != null
                && snapshot.version == CampaignSnapshot.CURRENT_VERSION
                && snapshot.missionId == Mission.Id
                && catalog.HasHero(snapshot.heroId)
                && snapshot.day >= 1
                && snapshot.gold >= 0
                && snapshot.experience >= 0
                && snapshot.movement >= 0
                && snapshot.movement <= catalog.Hero(snapshot.heroId).DailyMovement
                && snapshot.inventory != null
                && snapshot.completed != null
                && snapshot.stock != null
                && snapshot.equipped != null
                && snapshot.equipped.Count == Equipment.PLACE_COUNT
                && snapshot.visited != null
                && snapshot.keys != null
                && snapshot.dwellings != null
                && snapshot.mills != null
                && Math.Abs(snapshot.morale) <= MAXIMUM_MORALE
                && (snapshot.townOwnerId == CampaignState.PLAYER_OWNER_ID || snapshot.townOwnerId == Mission.ClanId);
            if (!isValid)
            {
                throw new ArgumentException("Invalid campaign header or schema.");
            }
        }

        private void RestoreStacks(List<StackSnapshot> snapshots, List<Troop> target)
        {
            if (snapshots == null || snapshots.Count > Mission.ArmySlots)
            {
                throw new ArgumentException("Invalid army slots.");
            }

            foreach (StackSnapshot stack in snapshots)
            {
                // duplicate unit types would have been merged by the rules, so they cannot come from a valid save
                if (stack == null || !CanAdd(target, stack.unitId, stack.count) || IndexOf(target, stack.unitId) >= 0)
                {
                    throw new ArgumentException("Invalid army stack.");
                }

                target.Add(new Troop(stack.unitId, stack.count));
            }
        }

        private void RestoreInventory(CampaignSnapshot snapshot, CampaignState result)
        {
            foreach (string id in snapshot.inventory)
            {
                if (!catalog.HasArtefact(id))
                {
                    throw new ArgumentException("Unknown inventory artefact.");
                }

                result.InventoryData.Add(id);
            }

            for (int place = 0; place < Equipment.PLACE_COUNT; place++)
            {
                string id = snapshot.equipped[place];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (!catalog.HasArtefact(id) || !Equipment.Fits(catalog.Artefact(id).Slot, (EquipmentPlace)place))
                {
                    throw new ArgumentException("Invalid equipped artefact.");
                }

                result.EquippedData[place] = id;
            }
        }

        private void RestoreCompleted(CampaignSnapshot snapshot, CampaignState result)
        {
            foreach (string id in snapshot.completed)
            {
                if (!Mission.HasEncounter(id) || !result.CompletedData.Add(id))
                {
                    throw new ArgumentException("Invalid completed encounter.");
                }
            }
        }

        private void RestoreStock(CampaignSnapshot snapshot, CampaignState result)
        {
            foreach (StockSnapshot stock in snapshot.stock)
            {
                bool isValid = stock != null
                    && Mission.IsRecruitable(stock.unitId)
                    && stock.count >= 0
                    && stock.count <= MAXIMUM_STACK_COUNT
                    && !result.StockData.ContainsKey(stock.unitId);
                if (!isValid)
                {
                    throw new ArgumentException("Invalid recruitment stock.");
                }

                result.StockData.Add(stock.unitId, stock.count);
            }

            // saves from before a unit became recruitable start with one week of growth
            foreach (string id in Mission.RecruitmentIds)
            {
                if (!result.StockData.ContainsKey(id))
                {
                    result.StockData.Add(id, catalog.Unit(id).WeeklyGrowth);
                }
            }
        }

        private void RestoreMapObjects(CampaignSnapshot snapshot, CampaignState result)
        {
            foreach (string id in snapshot.visited)
            {
                if (!Mission.HasObject(id) || !result.VisitedData.Add(id))
                {
                    throw new ArgumentException("Invalid visited map object.");
                }
            }

            foreach (string id in snapshot.keys)
            {
                if (!IsKnownKey(id) || !result.KeyData.Add(id))
                {
                    throw new ArgumentException("Invalid key.");
                }
            }

            RestoreCounts(snapshot.dwellings, result.DwellingStockData, MapObjectKind.Dwelling, MAXIMUM_STACK_COUNT);
            RestoreCounts(snapshot.mills, result.MillWeekData, MapObjectKind.Mill, int.MaxValue);
            foreach (MapObjectDefinition dwelling in Dwellings())
            {
                if (!result.DwellingStockData.ContainsKey(dwelling.Id))
                {
                    result.DwellingStockData.Add(dwelling.Id, dwelling.WeeklyGrowth);
                }
            }
        }

        private void RestoreCounts(List<CountSnapshot> counts, Dictionary<string, int> target, MapObjectKind kind, int maximum)
        {
            foreach (CountSnapshot count in counts)
            {
                MapObjectDefinition mapObject = count != null ? Mission.FindObject(count.id) : null;
                bool isValid = mapObject != null && mapObject.Kind == kind && count.count >= 0 && count.count <= maximum && !target.ContainsKey(count.id);
                if (!isValid)
                {
                    throw new ArgumentException("Invalid " + kind + " record.");
                }

                target.Add(count.id, count.count);
            }
        }

        private bool IsKnownKey(string keyId)
        {
            foreach (MapObjectDefinition mapObject in Mission.Objects)
            {
                if (mapObject.Kind == MapObjectKind.QuestGiver && mapObject.KeyId == keyId)
                {
                    return true;
                }
            }

            return false;
        }

        private void ValidateArmies(CampaignSnapshot snapshot, CampaignState result)
        {
            if (snapshot.heroHome && (!result.IsTownOwned || result.Army.Count != 0 || !IsAtTown(result)))
            {
                throw new ArgumentException("Invalid home hero.");
            }

            if (!result.IsTownOwned && result.Garrison.Count > 0)
            {
                throw new ArgumentException("Player garrison in a foreign town.");
            }

            if (result.Army.Count == 0 && result.Garrison.Count == 0)
            {
                throw new ArgumentException("Campaign has no army.");
            }
        }

        #endregion

        private static int IndexOf(IReadOnlyList<Troop> army, string unitId)
        {
            for (int i = 0; i < army.Count; i++)
            {
                if (army[i].UnitId == unitId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void Add(List<Troop> army, Troop troop)
        {
            int index = IndexOf(army, troop.UnitId);
            if (index < 0)
            {
                army.Add(troop);
                return;
            }

            army[index] = new Troop(troop.UnitId, checked(army[index].Count + troop.Count));
        }

        private bool IsAtTown(CampaignState campaign)
        {
            return campaign.Position == Mission.Town.Position;
        }

        private IEnumerable<MapObjectDefinition> Dwellings()
        {
            foreach (MapObjectDefinition mapObject in Mission.Objects)
            {
                if (mapObject.Kind == MapObjectKind.Dwelling)
                {
                    yield return mapObject;
                }
            }
        }

        private bool WouldDwellingOverflow(CampaignState campaign)
        {
            foreach (MapObjectDefinition dwelling in Dwellings())
            {
                if ((long)campaign.DwellingStock(dwelling.Id) + dwelling.WeeklyGrowth > MAXIMUM_STACK_COUNT)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>What the visited shrines of a kind added to the hero for good.</summary>
        private int ShrineBonus(CampaignState campaign, MapObjectKind kind)
        {
            int bonus = 0;
            foreach (MapObjectDefinition shrine in Mission.Objects)
            {
                if (shrine.Kind == kind && campaign.IsVisited(shrine.Id))
                {
                    bonus = checked(bonus + (kind == MapObjectKind.AttackShrine ? shrine.AttackBonus : shrine.DefenseBonus));
                }
            }

            return bonus;
        }

        private bool WouldStockOverflow(CampaignState campaign)
        {
            foreach (string id in Mission.RecruitmentIds)
            {
                if ((long)campaign.Stock(id) + catalog.Unit(id).WeeklyGrowth > MAXIMUM_STACK_COUNT)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The target army with every source stack added, or null if anything does not fit.</summary>
        private List<Troop> Merge(IReadOnlyList<Troop> target, IReadOnlyList<Troop> source)
        {
            List<Troop> merged = new List<Troop>(target);
            foreach (Troop troop in source)
            {
                if (!CanAdd(merged, troop.UnitId, troop.Count))
                {
                    return null;
                }

                Add(merged, troop);
            }

            return merged;
        }

        private int EquippedBonus(CampaignState campaign, EquippedStat stat)
        {
            int bonus = 0;
            for (int place = 0; place < Equipment.PLACE_COUNT; place++)
            {
                string id = campaign.Equipped((EquipmentPlace)place);
                if (id != null)
                {
                    bonus = checked(bonus + StatBonus(catalog.Artefact(id), stat));
                }
            }

            return bonus;
        }

        private bool HasArtefactOf(ArtefactRarity rarity)
        {
            foreach (ArtefactDefinition artefact in catalog.Artefacts)
            {
                if (artefact.Rarity == rarity)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A random artefact of the rarity, preferring unowned ones; falls back to any rarity when none is authored. Artefacts
        /// placed in the mission (guarded treasures, captives' gifts, pickups) are found only there, never rolled.
        /// </summary>
        private string PickArtefact(CampaignState campaign, ArtefactRarity rarity, IRandomSource random)
        {
            HashSet<string> placed = PlacedArtefacts();
            List<ArtefactDefinition> rollable = catalog.Artefacts.Where(artefact => !placed.Contains(artefact.Id)).ToList();
            List<ArtefactDefinition> candidates = rollable.FindAll(artefact => artefact.Rarity == rarity);
            if (candidates.Count == 0)
            {
                candidates.AddRange(rollable);
            }

            List<ArtefactDefinition> unowned = candidates.FindAll(artefact => !campaign.Owns(artefact.Id));
            List<ArtefactDefinition> pool = unowned.Count > 0 ? unowned : candidates;
            if (pool.Count == 0)
            {
                return null;
            }

            int index = Math.Min(pool.Count - 1, (int)(random.NextValue() * pool.Count));
            return pool[index].Id;
        }

        private HashSet<string> PlacedArtefacts()
        {
            HashSet<string> placed = new HashSet<string>(StringComparer.Ordinal);
            foreach (EncounterDefinition encounter in Mission.Encounters)
            {
                if (encounter.RewardArtefactId != null)
                {
                    placed.Add(encounter.RewardArtefactId);
                }
            }

            foreach (MapObjectDefinition mapObject in Mission.Objects)
            {
                if (mapObject.ArtefactId != null)
                {
                    placed.Add(mapObject.ArtefactId);
                }
            }

            return placed;
        }

        private static int StatBonus(ArtefactDefinition artefact, EquippedStat stat)
        {
            switch (stat)
            {
                case EquippedStat.Attack:
                    return artefact.AttackBonus;
                case EquippedStat.Defense:
                    return artefact.DefenseBonus;
                case EquippedStat.Morale:
                    return artefact.MoraleBonus;
                default:
                    return artefact.SpeedBonus;
            }
        }

        private enum EquippedStat
        {
            Attack,
            Defense,
            Speed,
            Morale
        }
    }
}
