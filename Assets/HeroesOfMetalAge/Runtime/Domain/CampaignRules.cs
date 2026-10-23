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
        public const int MAXIMUM_LUCK = 3;
        public const int MAXIMUM_MANA = 100000;

        private readonly DefinitionCatalog catalog;

        public CampaignRules(DefinitionCatalog catalog)
        {
            this.catalog = catalog;
        }

        private MissionDefinition Mission => catalog.Mission;
        private HeroPolicy Policy => catalog.HeroPolicy;

        public CampaignState NewCampaign()
        {
            HeroDefinition hero = catalog.Hero(Mission.HeroId);
            CampaignState state = new CampaignState
            {
                Day = 1,
                Gold = Mission.StartingGold,
                Position = Mission.Start,
                HeroId = hero.Id,
                MissionId = Mission.Id,
                TownOwnerId = Mission.ClanId,
                Explored = new ExploredMap(Mission.Width, Mission.Height)
            };

            GiveStartingMagicAndSkills(state, hero);
            state.Movement = DailyMovement(state);
            state.Mana = MaximumMana(state);
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
            return Policy.Experience.Level(campaign.Experience);
        }

        /// <summary>Attack: the hero's base, level-ups, worn artefacts and shrines.</summary>
        public int Attack(CampaignState campaign)
        {
            HeroDefinition hero = catalog.Hero(campaign.HeroId);
            long attack = (long)hero.BaseAttack + campaign.PrimaryGain(PrimaryStat.Attack) + EquippedBonus(campaign, EquippedStat.Attack)
                + ShrineBonus(campaign, MapObjectKind.AttackShrine);
            return (int)Math.Min(int.MaxValue, attack);
        }

        public int Defense(CampaignState campaign)
        {
            HeroDefinition hero = catalog.Hero(campaign.HeroId);
            long defense = (long)hero.BaseDefense + campaign.PrimaryGain(PrimaryStat.Defense) + EquippedBonus(campaign, EquippedStat.Defense)
                + ShrineBonus(campaign, MapObjectKind.DefenseShrine);
            return (int)Math.Min(int.MaxValue, defense);
        }

        /// <summary>Spell power: how strong and how long the hero's spells are.</summary>
        public int SpellPower(CampaignState campaign)
        {
            HeroDefinition hero = catalog.Hero(campaign.HeroId);
            long power = (long)hero.BaseSpellPower + campaign.PrimaryGain(PrimaryStat.SpellPower) + EquippedBonus(campaign, EquippedStat.SpellPower);
            return (int)Math.Min(int.MaxValue, power);
        }

        /// <summary>Knowledge: how much mana the hero holds.</summary>
        public int Knowledge(CampaignState campaign)
        {
            HeroDefinition hero = catalog.Hero(campaign.HeroId);
            long knowledge = (long)hero.BaseKnowledge + campaign.PrimaryGain(PrimaryStat.Knowledge) + EquippedBonus(campaign, EquippedStat.Knowledge);
            return (int)Math.Min(int.MaxValue, knowledge);
        }

        public int Primary(CampaignState campaign, PrimaryStat stat)
        {
            switch (stat)
            {
                case PrimaryStat.Attack:
                    return Attack(campaign);
                case PrimaryStat.Defense:
                    return Defense(campaign);
                case PrimaryStat.SpellPower:
                    return SpellPower(campaign);
                default:
                    return Knowledge(campaign);
            }
        }

        /// <summary>The army's morale from worn artefacts, map objects and Leadership, between -3 and +3.</summary>
        public int Morale(CampaignState campaign)
        {
            long morale = (long)EquippedBonus(campaign, EquippedStat.Morale) + campaign.MoraleBonus + (int)SkillValue(campaign, SecondarySkill.Leadership);
            return (int)Math.Max(-MAXIMUM_MORALE, Math.Min(MAXIMUM_MORALE, morale));
        }

        /// <summary>The army's luck from worn artefacts, map objects and the Luck skill, between -3 and +3.</summary>
        public int Luck(CampaignState campaign)
        {
            long luck = (long)EquippedBonus(campaign, EquippedStat.Luck) + campaign.LuckBonus + (int)SkillValue(campaign, SecondarySkill.Luck);
            return (int)Math.Max(-MAXIMUM_LUCK, Math.Min(MAXIMUM_LUCK, luck));
        }

        /// <summary>A secondary skill's value for the hero at the level known; 0 when not known (see the hero rules asset).</summary>
        public double SkillValue(CampaignState campaign, SecondarySkill skill)
        {
            return Policy.Value(skill, campaign.SkillLevelOf(skill));
        }

        /// <summary>Knowledge times the mana per point, raised by Intelligence.</summary>
        public int MaximumMana(CampaignState campaign)
        {
            double mana = (double)Knowledge(campaign) * Policy.ManaPerKnowledge * (1d + SkillValue(campaign, SecondarySkill.Intelligence));
            return (int)Math.Max(0, Math.Min(MAXIMUM_MANA, Math.Floor(mana + 1e-9)));
        }

        /// <summary>Mana regained every day: the rules' base plus Mysticism.</summary>
        public int ManaRegeneration(CampaignState campaign)
        {
            return Policy.ManaRegeneration + (int)SkillValue(campaign, SecondarySkill.Mysticism);
        }

        /// <summary>Gold Estates bring every day.</summary>
        public int DailyIncome(CampaignState campaign)
        {
            return (int)SkillValue(campaign, SecondarySkill.Estates);
        }

        /// <summary>What Pathfinding cuts from rough terrain for this hero.</summary>
        public TerrainRelief Pathfinding(CampaignState campaign)
        {
            int percent = (int)Math.Round(SkillValue(campaign, SecondarySkill.Pathfinding) * 100d);
            return new TerrainRelief(percent, Policy.UnpenalizedTerrainCost);
        }

        /// <summary>The highest spell level the hero can learn: Wisdom's level, else the rules' level without Wisdom.</summary>
        public int MaximumSpellLevel(CampaignState campaign)
        {
            return Math.Max(Policy.SpellLevelWithoutWisdom, (int)SkillValue(campaign, SecondarySkill.Wisdom));
        }

        public bool CanLearn(CampaignState campaign, SpellDefinition spell)
        {
            return spell.Level <= MaximumSpellLevel(campaign);
        }

        /// <summary>A rough measure of an army's fighting strength: every creature's health times its average damage.</summary>
        public double ArmyStrength(IReadOnlyList<Troop> army)
        {
            double strength = 0d;
            foreach (Troop troop in army)
            {
                UnitDefinition unit = catalog.Unit(troop.UnitId);
                strength += (double)troop.Count * unit.Health * (unit.MinimumDamage + unit.MaximumDamage) / 2d;
            }

            return strength;
        }

        /// <summary>
        /// Diplomacy: the gold a hostile army asks for joining the hero, or -1 when it will not join. Only armies without a
        /// commander consider it, and only when the hero's army is strong enough and the army is willing: each army's
        /// willingness is fixed by its ID, so asking again never changes the answer, while a better Diplomacy convinces more.
        /// </summary>
        public int JoinPrice(CampaignState campaign, EncounterDefinition encounter)
        {
            SkillLevel diplomacy = campaign.SkillLevelOf(SecondarySkill.Diplomacy);
            if (encounter.Kind != EncounterKind.Sentinel || encounter.Commander != null || campaign.IsCompleted(encounter.Id) || diplomacy == SkillLevel.None)
            {
                return -1;
            }

            if (ArmyStrength(campaign.Army) < ArmyStrength(encounter.Army) * Policy.JoinStrengthRatio
                || Disposition(encounter.Id) >= Policy.Value(SecondarySkill.Diplomacy, diplomacy))
            {
                return -1;
            }

            return WholeGold(ArmyValue(encounter.Army) * Policy.JoinCostMultiplier);
        }

        /// <summary>The gold surrendering costs: a share of the surviving army's price, cut by Diplomacy.</summary>
        public int SurrenderPrice(CampaignState campaign, IReadOnlyList<Troop> survivors)
        {
            double discount = Policy.SurrenderDiscount(campaign.SkillLevelOf(SecondarySkill.Diplomacy));
            return WholeGold(ArmyValue(survivors) * Policy.SurrenderCostMultiplier * (1d - discount));
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

        /// <summary>Movement points per day on land: the hero's, raised by Logistics.</summary>
        public int DailyMovement(CampaignState campaign)
        {
            double movement = catalog.Hero(campaign.HeroId).DailyMovement * (1d + SkillValue(campaign, SecondarySkill.Logistics));
            return (int)Math.Max(1, Math.Min(int.MaxValue, Math.Round(movement, MidpointRounding.AwayFromZero)));
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
            campaign.Gold = (int)Math.Min(int.MaxValue, (long)campaign.Gold + DailyIncome(campaign));
            // mana regrows up to the maximum; mana above it (from a full restore before Intelligence was lost) stays
            int maximumMana = MaximumMana(campaign);
            if (campaign.Mana < maximumMana)
            {
                campaign.Mana = Math.Min(maximumMana, campaign.Mana + ManaRegeneration(campaign));
            }
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

            int cost = map.StepCost(campaign.Position, next, Pathfinding(campaign));
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
        /// <param name="manaLeft">The hero's mana after the battle; negative keeps the campaign's.</param>
        public CommandResult ApplyVictory(CampaignState campaign, EncounterDefinition encounter, IReadOnlyList<Troop> survivors, VictoryRewards rewards,
            int manaLeft = -1)
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
            campaign.LuckBonus = 0;
            if (manaLeft >= 0)
            {
                campaign.Mana = Math.Min(MAXIMUM_MANA, manaLeft);
            }

            campaign.CompletedData.Add(encounter.Id);
            if (encounter.Kind == EncounterKind.Town)
            {
                campaign.TownOwnerId = CampaignState.PLAYER_OWNER_ID;
            }

            return CommandResult.Ok();
        }

        /// <summary>Diplomacy: the hostile army joins for its price (see <see cref="JoinPrice"/>) and counts as resolved.</summary>
        public CommandResult Hire(CampaignState campaign, EncounterDefinition encounter)
        {
            int price = JoinPrice(campaign, encounter);
            if (price < 0)
            {
                return CommandResult.Reject(encounter.Name + " will not join you.");
            }

            if (price > campaign.Gold)
            {
                return CommandResult.Reject("Not enough gold.");
            }

            List<Troop> merged = Merge(campaign.Army, encounter.Army);
            if (merged == null)
            {
                return CommandResult.Reject("The army cannot hold this group.");
            }

            campaign.ArmyData.Clear();
            campaign.ArmyData.AddRange(merged);
            campaign.Gold -= price;
            campaign.CompletedData.Add(encounter.Id);
            return CommandResult.Ok();
        }

        /// <summary>
        /// Ends a battle against an enemy hero by paying them off: the surviving army leaves with the hero, the enemy stays
        /// on the map. Only a commander accepts a surrender.
        /// </summary>
        public CommandResult ApplySurrender(CampaignState campaign, EncounterDefinition encounter, IReadOnlyList<Troop> survivors, int manaLeft)
        {
            if (encounter.Commander == null)
            {
                return CommandResult.Reject("Only an enemy hero accepts a surrender.");
            }

            if (survivors.Count == 0 || survivors.Count > Mission.ArmySlots)
            {
                return CommandResult.Reject("Nobody is left to surrender.");
            }

            int price = SurrenderPrice(campaign, survivors);
            if (price > campaign.Gold)
            {
                return CommandResult.Reject("Not enough gold to surrender (" + price + ").");
            }

            campaign.ArmyData.Clear();
            campaign.ArmyData.AddRange(survivors);
            campaign.Gold -= price;
            campaign.Mana = Math.Max(0, Math.Min(MAXIMUM_MANA, manaLeft));
            return CommandResult.Ok();
        }

        /// <summary>Restores the hero's mana to the maximum, keeping anything above it.</summary>
        public void RestoreMana(CampaignState campaign)
        {
            campaign.Mana = Math.Max(campaign.Mana, MaximumMana(campaign));
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

        /// <summary>Gives exactly the experience the next level needs.</summary>
        public void DebugLevel(CampaignState campaign)
        {
            long next = Policy.Experience.Threshold(Level(campaign) + 1);
            campaign.Experience = (int)Math.Min(int.MaxValue, Math.Max(campaign.Experience, next));
        }

        /// <summary>A spell book with every spell and full mana.</summary>
        public void DebugSpells(CampaignState campaign)
        {
            campaign.HasSpellBook = true;
            foreach (SpellDefinition spell in catalog.Spells)
            {
                campaign.SpellData.Add(spell.Id);
            }

            RestoreMana(campaign);
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
            RestoreHero(snapshot, result);
            if (result.Movement > DailyMovement(result))
            {
                throw new ArgumentException("Invalid movement.");
            }

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
                && (snapshot.version == CampaignSnapshot.CURRENT_VERSION || snapshot.version == CampaignSnapshot.OLDEST_MIGRATED_VERSION)
                && snapshot.missionId == Mission.Id
                && catalog.HasHero(snapshot.heroId)
                && snapshot.day >= 1
                && snapshot.gold >= 0
                && snapshot.experience >= 0
                && snapshot.movement >= 0
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
                && Math.Abs(snapshot.luck) <= MAXIMUM_LUCK
                && (snapshot.townOwnerId == CampaignState.PLAYER_OWNER_ID || snapshot.townOwnerId == Mission.ClanId);
            if (!isValid)
            {
                throw new ArgumentException("Invalid campaign header or schema.");
            }
        }

        /// <summary>
        /// Skills, level-ups, primary stats, the spell book and mana. A version 4 save, from before heroes grew this way, gets
        /// the hero's starting skills and spells, full mana and every level-up since level 1 still to be granted.
        /// </summary>
        private void RestoreHero(CampaignSnapshot snapshot, CampaignState result)
        {
            HeroDefinition hero = catalog.Hero(result.HeroId);
            if (snapshot.version == CampaignSnapshot.OLDEST_MIGRATED_VERSION)
            {
                GiveStartingMagicAndSkills(result, hero);
                result.Mana = MaximumMana(result);
                return;
            }

            result.LuckBonus = snapshot.luck;
            List<int> gains = snapshot.primaryGains ?? new List<int>();
            int level = Level(result);
            bool isValidGrowth = gains.Count == CampaignState.PRIMARY_STAT_COUNT
                && gains.All(gain => gain >= 0)
                && snapshot.levelsGranted >= 1
                && snapshot.levelsGranted <= level
                && gains.Sum(gain => (long)gain) == snapshot.levelsGranted - 1
                && snapshot.levelsWithoutWisdom >= 0
                && snapshot.mana >= 0
                && snapshot.mana <= MAXIMUM_MANA;
            if (!isValidGrowth)
            {
                throw new ArgumentException("Invalid hero growth.");
            }

            for (int i = 0; i < gains.Count; i++)
            {
                result.PrimaryGainData[i] = gains[i];
            }

            result.LevelsGranted = snapshot.levelsGranted;
            result.LevelsWithoutWisdom = snapshot.levelsWithoutWisdom;
            foreach (SkillSnapshot skill in snapshot.skills ?? new List<SkillSnapshot>())
            {
                HeroSkill known = ParseSkill(skill);
                if (known == null || known.Level == SkillLevel.None || result.SkillLevelOf(known.Skill) != SkillLevel.None)
                {
                    throw new ArgumentException("Invalid skill.");
                }

                result.SkillData.Add(known);
            }

            if (result.SkillData.Count > Policy.MaximumSkills)
            {
                throw new ArgumentException("Too many skills.");
            }

            RestorePendingLevelUp(snapshot, result);
            result.HasSpellBook = snapshot.spellBook;
            foreach (string spellId in snapshot.spells ?? new List<string>())
            {
                if (!catalog.HasSpell(spellId) || !result.SpellData.Add(spellId))
                {
                    throw new ArgumentException("Invalid spell.");
                }
            }

            if (!result.HasSpellBook && result.SpellData.Count > 0)
            {
                throw new ArgumentException("Spells without a spell book.");
            }

            result.Mana = snapshot.mana;
        }

        // a waiting level-up belongs to the last granted level and offers each skill at the level it would reach
        private void RestorePendingLevelUp(CampaignSnapshot snapshot, CampaignState result)
        {
            if (snapshot.pendingLevel == 0)
            {
                return;
            }

            bool isValid = snapshot.pendingLevel == result.LevelsGranted
                && snapshot.pendingLevel > 1
                && Enum.TryParse(snapshot.pendingStat, out PrimaryStat stat)
                && Enum.IsDefined(typeof(PrimaryStat), stat)
                && snapshot.pendingSkills != null
                && snapshot.pendingSkills.Count <= 2;
            if (!isValid)
            {
                throw new ArgumentException("Invalid level-up.");
            }

            List<HeroSkill> options = new List<HeroSkill>();
            foreach (SkillSnapshot option in snapshot.pendingSkills)
            {
                HeroSkill offered = ParseSkill(option);
                if (offered == null || (int)offered.Level != (int)result.SkillLevelOf(offered.Skill) + 1 || options.Any(other => other.Skill == offered.Skill))
                {
                    throw new ArgumentException("Invalid level-up option.");
                }

                options.Add(offered);
            }

            result.PendingLevelUp = new LevelUpOffer(snapshot.pendingLevel, (PrimaryStat)Enum.Parse(typeof(PrimaryStat), snapshot.pendingStat), options);
        }

        private HeroSkill ParseSkill(SkillSnapshot snapshot)
        {
            if (snapshot == null || !Enum.TryParse(snapshot.skill, out SecondarySkill skill) || !Enum.IsDefined(typeof(SecondarySkill), skill)
                || !Policy.HasSkill(skill) || snapshot.level < (int)SkillLevel.None || snapshot.level > (int)SkillLevel.Expert)
            {
                return null;
            }

            return new HeroSkill(skill, (SkillLevel)snapshot.level);
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
                case EquippedStat.Luck:
                    return artefact.LuckBonus;
                case EquippedStat.SpellPower:
                    return artefact.SpellPowerBonus;
                case EquippedStat.Knowledge:
                    return artefact.KnowledgeBonus;
                default:
                    return artefact.SpeedBonus;
            }
        }

        /// <summary>The hero's starting skills, spell book and spells.</summary>
        private static void GiveStartingMagicAndSkills(CampaignState campaign, HeroDefinition hero)
        {
            campaign.SkillData.AddRange(hero.StartingSkills);
            campaign.HasSpellBook = hero.HasSpellBook;
            foreach (string spellId in hero.StartingSpellIds)
            {
                campaign.SpellData.Add(spellId);
            }
        }

        /// <summary>What the army would cost to recruit at full price.</summary>
        private double ArmyValue(IReadOnlyList<Troop> army)
        {
            double value = 0d;
            foreach (Troop troop in army)
            {
                value += (double)catalog.Unit(troop.UnitId).Cost * troop.Count;
            }

            return value;
        }

        private static int WholeGold(double gold)
        {
            return (int)Math.Max(0, Math.Min(int.MaxValue, Math.Ceiling(gold - 1e-9)));
        }

        /// <summary>An army's fixed willingness to join, in [0, 1): a stable hash of its ID (FNV-1a), the same on every run.</summary>
        private static double Disposition(string encounterId)
        {
            uint hash = 2166136261;
            foreach (char character in encounterId)
            {
                hash = (hash ^ character) * 16777619;
            }

            return (hash % 10000u) / 10000d;
        }

        private enum EquippedStat
        {
            Attack,
            Defense,
            Speed,
            Morale,
            Luck,
            SpellPower,
            Knowledge
        }
    }
}
