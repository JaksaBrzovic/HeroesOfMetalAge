using System;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>What a visit to a map object gave the hero; zero or null for what it did not give.</summary>
    public sealed class VisitOutcome
    {
        internal VisitOutcome(MapObjectDefinition mapObject)
        {
            Object = mapObject;
        }

        public MapObjectDefinition Object { get; }
        /// <summary>False when the object had already given what it gives (a known shrine, a mill already paid this week).</summary>
        public bool IsRewarding { get; internal set; }
        public int Gold { get; internal set; }
        public int Experience { get; internal set; }
        public string ArtefactId { get; internal set; }
        public string KeyId { get; internal set; }
        public int AttackBonus { get; internal set; }
        public int DefenseBonus { get; internal set; }
        public int MoraleBonus { get; internal set; }
    }

    /// <summary>
    /// The adventure map's objects and the mission's goals. Every command validates completely before it changes anything,
    /// like <see cref="CampaignRules"/>.
    /// </summary>
    public sealed class MapObjectRules
    {
        private readonly DefinitionCatalog catalog;
        private readonly CampaignRules rules;

        public MapObjectRules(DefinitionCatalog catalog, CampaignRules rules)
        {
            this.catalog = catalog;
            this.rules = rules;
        }

        private MissionDefinition Mission => catalog.Mission;

        #region VISITS
        /// <summary>
        /// The visit of an object that needs no choice: everything but the chest, the dwelling and the exit. The hero stands
        /// on the object, or for a border gate next to it (he passes through once it opened).
        /// </summary>
        public CommandResult Visit(CampaignState campaign, MapObjectDefinition mapObject, out VisitOutcome outcome)
        {
            outcome = new VisitOutcome(mapObject);
            CommandResult where = CheckPosition(campaign, mapObject);
            if (!where.IsSuccess)
            {
                return where;
            }

            switch (mapObject.Kind)
            {
                case MapObjectKind.GoldPile:
                    return TakeGold(campaign, mapObject, outcome);
                case MapObjectKind.Artefact:
                case MapObjectKind.Prison:
                    return TakeArtefact(campaign, mapObject, outcome);
                case MapObjectKind.AttackShrine:
                case MapObjectKind.DefenseShrine:
                    return LearnAtShrine(campaign, mapObject, outcome);
                case MapObjectKind.MoraleShrine:
                    return Bless(campaign, mapObject, outcome);
                case MapObjectKind.Mill:
                    return CollectMill(campaign, mapObject, outcome);
                case MapObjectKind.QuestGiver:
                    return MeetQuestGiver(campaign, mapObject, outcome);
                case MapObjectKind.BorderGate:
                    return OpenGate(campaign, mapObject, outcome);
                case MapObjectKind.Oracle:
                    outcome.IsRewarding = campaign.VisitedData.Add(mapObject.Id);
                    return CommandResult.Ok();
                default:
                    return CommandResult.Reject(mapObject.Name + " needs a choice.");
            }
        }

        /// <summary>Opens a chest once, taking either its gold or its experience.</summary>
        public CommandResult OpenChest(CampaignState campaign, MapObjectDefinition chest, bool isTakingGold, out VisitOutcome outcome)
        {
            outcome = new VisitOutcome(chest);
            if (chest.Kind != MapObjectKind.TreasureChest || campaign.IsVisited(chest.Id))
            {
                return CommandResult.Reject("The chest is already open.");
            }

            CommandResult where = CheckPosition(campaign, chest);
            if (!where.IsSuccess)
            {
                return where;
            }

            if (isTakingGold ? (long)campaign.Gold + chest.Gold > int.MaxValue : (long)campaign.Experience + chest.Experience > int.MaxValue)
            {
                return CommandResult.Reject("The treasure exceeds campaign limits.");
            }

            if (isTakingGold)
            {
                campaign.Gold += chest.Gold;
                outcome.Gold = chest.Gold;
            }
            else
            {
                campaign.Experience += chest.Experience;
                outcome.Experience = chest.Experience;
            }

            campaign.VisitedData.Add(chest.Id);
            outcome.IsRewarding = true;
            return CommandResult.Ok();
        }

        /// <summary>Buys creatures from a dwelling's own stock at the unit's full price.</summary>
        public CommandResult Recruit(CampaignState campaign, MapObjectDefinition dwelling, int count)
        {
            if (dwelling.Kind != MapObjectKind.Dwelling || dwelling.UnitId == null)
            {
                return CommandResult.Reject("Nobody can be recruited here.");
            }

            CommandResult where = CheckPosition(campaign, dwelling);
            if (!where.IsSuccess)
            {
                return where;
            }

            if (count <= 0 || count > campaign.DwellingStock(dwelling.Id))
            {
                return CommandResult.Reject("Invalid recruitment quantity or stock.");
            }

            if (!rules.CanAdd(campaign.Army, dwelling.UnitId, count))
            {
                return CommandResult.Reject("The army has no capacity for these recruits.");
            }

            long total = (long)catalog.Unit(dwelling.UnitId).Cost * count;
            if (total > campaign.Gold)
            {
                return CommandResult.Reject("Not enough gold.");
            }

            int index = campaign.ArmyData.FindIndex(troop => troop.UnitId == dwelling.UnitId);
            if (index < 0)
            {
                campaign.ArmyData.Add(new Troop(dwelling.UnitId, count));
            }
            else
            {
                campaign.ArmyData[index] = new Troop(dwelling.UnitId, campaign.ArmyData[index].Count + count);
            }

            campaign.DwellingStockData[dwelling.Id] = campaign.DwellingStock(dwelling.Id) - count;
            campaign.Gold -= (int)total;
            return CommandResult.Ok();
        }

        /// <summary>How many creatures the hero could buy at a dwelling now: limited by stock, gold and army room.</summary>
        public int AffordableAtDwelling(CampaignState campaign, MapObjectDefinition dwelling)
        {
            if (dwelling.UnitId == null)
            {
                return 0;
            }

            int byGold = campaign.Gold / Math.Max(1, catalog.Unit(dwelling.UnitId).Cost);
            int count = Math.Min(campaign.DwellingStock(dwelling.Id), byGold);
            return count > 0 && rules.CanAdd(campaign.Army, dwelling.UnitId, count) ? count : 0;
        }
        #endregion

        #region GOALS
        public bool IsReached(CampaignState campaign, ObjectiveDefinition objective)
        {
            switch (objective.Kind)
            {
                case ObjectiveKind.DefeatEncounter:
                    return campaign.IsCompleted(objective.TargetId);
                case ObjectiveKind.VisitObject:
                    return campaign.IsVisited(objective.TargetId);
                case ObjectiveKind.OwnArtefact:
                    return campaign.Owns(objective.TargetId);
                default:
                    return rules.Level(campaign) >= objective.Value;
            }
        }

        public int ReachedCount(CampaignState campaign)
        {
            int reached = 0;
            foreach (ObjectiveDefinition objective in Mission.Objectives)
            {
                reached += IsReached(campaign, objective) ? 1 : 0;
            }

            return reached;
        }

        /// <summary>True once every required goal is reached.</summary>
        public bool CanFinish(CampaignState campaign)
        {
            foreach (ObjectiveDefinition objective in Mission.Objectives)
            {
                if (objective.IsRequired && !IsReached(campaign, objective))
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        private static CommandResult CheckPosition(CampaignState campaign, MapObjectDefinition mapObject)
        {
            WorldPosition hero = campaign.Position;
            WorldPosition target = mapObject.Position;
            bool isNext = Math.Abs(hero.X - target.X) <= 1 && Math.Abs(hero.Y - target.Y) <= 1;
            bool isThere = mapObject.Kind == MapObjectKind.BorderGate ? isNext : hero == target;
            return isThere ? CommandResult.Ok() : CommandResult.Reject("The hero is not at " + mapObject.Name + ".");
        }

        private static CommandResult TakeGold(CampaignState campaign, MapObjectDefinition pile, VisitOutcome outcome)
        {
            if (campaign.IsVisited(pile.Id))
            {
                return CommandResult.Reject("The gold is gone.");
            }

            if ((long)campaign.Gold + pile.Gold > int.MaxValue)
            {
                return CommandResult.Reject("The gold exceeds campaign limits.");
            }

            campaign.Gold += pile.Gold;
            campaign.VisitedData.Add(pile.Id);
            outcome.Gold = pile.Gold;
            outcome.IsRewarding = true;
            return CommandResult.Ok();
        }

        private CommandResult TakeArtefact(CampaignState campaign, MapObjectDefinition mapObject, VisitOutcome outcome)
        {
            if (campaign.IsVisited(mapObject.Id))
            {
                return CommandResult.Reject(mapObject.Name + " has nothing more to give.");
            }

            if (mapObject.ArtefactId != null && !catalog.HasArtefact(mapObject.ArtefactId))
            {
                return CommandResult.Reject("Unknown artefact: " + mapObject.ArtefactId);
            }

            if (mapObject.ArtefactId != null)
            {
                campaign.InventoryData.Add(mapObject.ArtefactId);
            }

            campaign.VisitedData.Add(mapObject.Id);
            outcome.ArtefactId = mapObject.ArtefactId;
            outcome.IsRewarding = true;
            return CommandResult.Ok();
        }

        private static CommandResult LearnAtShrine(CampaignState campaign, MapObjectDefinition shrine, VisitOutcome outcome)
        {
            // a shrine teaches once; the bonus itself is derived from the visited shrines (see CampaignRules.Attack)
            if (campaign.VisitedData.Add(shrine.Id))
            {
                outcome.AttackBonus = shrine.Kind == MapObjectKind.AttackShrine ? shrine.AttackBonus : 0;
                outcome.DefenseBonus = shrine.Kind == MapObjectKind.DefenseShrine ? shrine.DefenseBonus : 0;
                outcome.IsRewarding = true;
            }

            return CommandResult.Ok();
        }

        private static CommandResult Bless(CampaignState campaign, MapObjectDefinition shrine, VisitOutcome outcome)
        {
            int blessed = Math.Min(CampaignRules.MAXIMUM_MORALE, shrine.MoraleBonus);
            if (blessed > campaign.MoraleBonus)
            {
                outcome.MoraleBonus = blessed - campaign.MoraleBonus;
                campaign.MoraleBonus = blessed;
                outcome.IsRewarding = true;
            }

            return CommandResult.Ok();
        }

        private CommandResult CollectMill(CampaignState campaign, MapObjectDefinition mill, VisitOutcome outcome)
        {
            int week = rules.Week(campaign);
            if (campaign.MillWeek(mill.Id) >= week)
            {
                return CommandResult.Ok();
            }

            if ((long)campaign.Gold + mill.Gold > int.MaxValue)
            {
                return CommandResult.Reject("The gold exceeds campaign limits.");
            }

            campaign.Gold += mill.Gold;
            campaign.MillWeekData[mill.Id] = week;
            outcome.Gold = mill.Gold;
            outcome.IsRewarding = true;
            return CommandResult.Ok();
        }

        private static CommandResult MeetQuestGiver(CampaignState campaign, MapObjectDefinition giver, VisitOutcome outcome)
        {
            if (campaign.VisitedData.Add(giver.Id))
            {
                if (giver.KeyId != null)
                {
                    campaign.KeyData.Add(giver.KeyId);
                }

                outcome.KeyId = giver.KeyId;
                outcome.IsRewarding = true;
            }

            return CommandResult.Ok();
        }

        private static CommandResult OpenGate(CampaignState campaign, MapObjectDefinition gate, VisitOutcome outcome)
        {
            if (campaign.IsVisited(gate.Id))
            {
                return CommandResult.Ok();
            }

            if (!campaign.HasKey(gate.KeyId))
            {
                return CommandResult.Reject(gate.Name + " bars the way. Find its key.");
            }

            campaign.VisitedData.Add(gate.Id);
            outcome.IsRewarding = true;
            return CommandResult.Ok();
        }
    }
}
