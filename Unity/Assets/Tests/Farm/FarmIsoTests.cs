using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class FarmIsoTests
    {
        [Test]
        public void FieldWorldSizeMatchesLogicalGrid()
        {
            var size = Game.Farm.FarmIso.FieldWorldSize(16, 16);

            Assert.That(size.x, Is.EqualTo(16f * Game.Farm.FarmIso.TileSize));
            Assert.That(size.y, Is.EqualTo(16f * Game.Farm.FarmIso.TileSize));
        }

        [Test]
        public void EveryGridCellRoundTripsThroughWorldCoordinates()
        {
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var world = Game.Farm.FarmIso.GridToWorld(x, y);
                var cell = Game.Farm.FarmIso.WorldToGrid(world);

                Assert.That(cell, Is.EqualTo(new Vector2Int(x, y)), $"cell {x},{y}");
            }
        }

        [Test]
        public void FieldCenterMatchesCenterGridCell()
        {
            var fieldCenter = Game.Farm.FarmIso.FieldWorldCenter(16, 16);
            var gridCenter = Game.Farm.FarmIso.GridToWorld(7.5f, 7.5f);

            Assert.That(fieldCenter.x, Is.EqualTo(gridCenter.x).Within(0.0001f));
            Assert.That(fieldCenter.z, Is.EqualTo(gridCenter.z).Within(0.0001f));
        }

        [Test]
        public void ArtCoverageIsAValidGroundContract()
        {
            Assert.That(Game.Farm.FarmIso.ArtDirtCoverage, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
        }
    }
}
