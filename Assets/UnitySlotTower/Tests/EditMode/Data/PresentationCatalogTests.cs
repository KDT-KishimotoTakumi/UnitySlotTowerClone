using NUnit.Framework;
using UnityEngine;

namespace UnitySlotTower.Tests.EditMode.Data
{
    public sealed class PresentationCatalogTests
    {
        private PresentationCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            _catalog.SetEntries(new[]
            {
                new PresentationCatalog.Entry { id = "tank" },
                new PresentationCatalog.Entry { id = "sword" },
            });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_catalog);

        [Test]
        public void Entry_Is_Found_By_Csv_Id()
        {
            Assert.That(_catalog.TryGet("sword", out var entry) && entry.id == "sword", Is.True);
        }

        [Test]
        public void Unknown_Id_Is_Not_Found()
        {
            Assert.That(_catalog.TryGet("dragon", out _), Is.False);
        }
    }
}
