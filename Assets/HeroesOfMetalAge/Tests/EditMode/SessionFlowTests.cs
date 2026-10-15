using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using NUnit.Framework;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>Timed session flows: the end-of-day sequence, the route shrinking during the ride and changing the destination while riding.</summary>
    public class SessionFlowTests
    {
        // the test session uses 0.1 s for a travel step and for the end-of-day sequence
        private const float LESS_THAN_A_STEP = 0.05f;
        private const float MORE_THAN_A_STEP = 1f;

        [Test]
        public void EndDay_ChangesTheDayOnlyWhenTheSequenceEnds()
        {
            GameSession session = NewSession();
            int day = session.Campaign.Day;

            session.EndDay();
            Assert.IsTrue(session.IsEndingDay && session.IsBusy);
            Assert.AreEqual(day, session.Campaign.Day, "the day does not change while the hourglass runs");

            session.Tick(LESS_THAN_A_STEP);
            Assert.IsTrue(session.IsEndingDay);
            Assert.That(session.EndDayProgress, Is.GreaterThan(0f).And.LessThan(1f));
            session.Plan(NearbyCell(session));
            Assert.AreEqual(0, session.Route.Count, "commands wait while the day ends");

            session.Tick(MORE_THAN_A_STEP);
            Assert.IsFalse(session.IsEndingDay || session.IsBusy);
            Assert.AreEqual(day + 1, session.Campaign.Day);
        }

        [Test]
        public void Ride_DropsTheCellsTheHeroEntered()
        {
            GameSession session = NewSession();
            session.Plan(NearbyCell(session));
            List<WorldPosition> planned = session.Route.ToList();

            session.Travel();
            session.Tick(0f);
            Assert.AreEqual(planned[0], session.Campaign.Position);
            CollectionAssert.AreEqual(planned.Skip(1), session.Route, "the entered cell left the route");
        }

        [Test]
        public void ClickDuringTheRide_StopsInTheNextCellWithTheNewRoutePlanned()
        {
            GameSession session = NewSession();
            WorldPosition start = session.Campaign.Position;
            session.Plan(NearbyCell(session));
            WorldPosition next = session.Route[0];
            session.Travel();
            session.Tick(0f);

            session.Plan(start);
            Assert.IsTrue(session.IsTraveling, "the hero first finishes entering the cell he is riding into");
            CollectionAssert.AreEqual(session.World.FindRoute(session.Campaign, start), session.Route);

            session.Tick(MORE_THAN_A_STEP);
            Assert.IsFalse(session.IsTraveling);
            Assert.AreEqual(next, session.Campaign.Position);
            Assert.AreEqual(start, session.Route.Last(), "the new destination stays planned");

            session.Plan(start);
            Assert.IsTrue(session.IsTraveling, "clicking the planned destination again rides there");
        }

        [Test]
        public void SecondClickDuringTheRide_RidesOnToTheNewDestination()
        {
            GameSession session = NewSession();
            WorldPosition start = session.Campaign.Position;
            session.Plan(NearbyCell(session));
            session.Travel();
            session.Tick(0f);

            session.Plan(start);
            session.Plan(start);
            session.Tick(MORE_THAN_A_STEP);
            Assert.AreEqual(start, session.Campaign.Position, "the hero rode on without stopping");
        }

        private static GameSession NewSession()
        {
            GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
            session.StartMission();
            return session;
        }

        // the third cell on the way to the sentinel: open ground without sites, reachable within the first day
        private static WorldPosition NearbyCell(GameSession session)
        {
            WorldPosition sentinel = session.Catalog.Mission.Encounter(EncounterKind.Sentinel).Position;
            List<WorldPosition> way = session.World.FindRoute(session.Campaign, sentinel).Take(3).ToList();
            Assume.That(way.Count == 3 && way.All(cell => session.World.Site(session.Campaign, cell) == null), "the start has three free cells towards the sentinel");
            return way[2];
        }
    }
}
