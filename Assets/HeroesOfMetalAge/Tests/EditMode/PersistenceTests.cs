using System;
using System.Collections.Generic;
using System.IO;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using HeroesOfMetalAge.Infrastructure.Persistence;
using NUnit.Framework;

namespace HeroesOfMetalAge.Tests
{
    public class PersistenceTests
    {
        private DefinitionCatalog catalog;
        private CampaignRules rules;
        private string folder;
        private string file;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentAssets.LoadCatalog();
            rules = new CampaignRules(catalog);
            folder = Path.Combine(Path.GetTempPath(), "HOMA-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            file = Path.Combine(folder, "campaign.json");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(folder, true);
        }

        [Test]
        public void Write_ReplacesAtomicallyAndKeepsBackup()
        {
            JsonCampaignStore store = new JsonCampaignStore(file);
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            store.Write(snapshot);
            snapshot.gold += 1;
            store.Write(snapshot);

            Assert.AreEqual(snapshot.gold, store.Read().gold);
            Assert.IsTrue(File.Exists(file + ".bak"));
        }

        [TestCase("{\"version\":999}")]
        [TestCase("{\"version\":2,\"day\":1}")]
        [TestCase("{not json")]
        public void UnknownOrCorruptSave_IsRejected(string content)
        {
            File.WriteAllText(file, content);

            Assert.Catch<Exception>(() => new JsonCampaignStore(file).Read());
        }

        [Test]
        public void FailedLoad_KeepsTheActiveCampaign()
        {
            MemorySaveStore store = new MemorySaveStore();
            List<Exception> errors = new List<Exception>();
            GameSession session = CampaignTestUtility.NewSession(catalog, store, errors.Add);
            session.StartMission();
            CampaignState active = session.Campaign;
            CampaignSnapshot invalid = rules.NewCampaign().Snapshot();
            invalid.army[0].unitId = "missing";
            store.Snapshot = invalid;

            session.Load();

            Assert.AreSame(active, session.Campaign);
            Assert.AreEqual(1, errors.Count);
        }
    }
}
