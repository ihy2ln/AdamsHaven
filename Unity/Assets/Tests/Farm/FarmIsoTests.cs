using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class FarmIsoTests
    {
        [Test]
        public void FieldWorldSizeMatchesLogicalGrid()
        {
            var size = Game.Farm.FarmIso.FieldWorldSize(10, 10);

            Assert.That(size.x, Is.EqualTo(10f * Game.Farm.FarmIso.TileSize));
            Assert.That(size.y, Is.EqualTo(10f * Game.Farm.FarmIso.TileSize));
        }

        [Test]
        public void EveryGridCellRoundTripsThroughWorldCoordinates()
        {
            for (var y = 0; y < 10; y++)
            for (var x = 0; x < 10; x++)
            {
                var world = Game.Farm.FarmIso.GridToWorld(x, y);
                var cell = Game.Farm.FarmIso.WorldToGrid(world);

                Assert.That(cell, Is.EqualTo(new Vector2Int(x, y)), $"cell {x},{y}");
            }
        }

        [Test]
        public void FieldCenterMatchesCenterGridCell()
        {
            var fieldCenter = Game.Farm.FarmIso.FieldWorldCenter(10, 10);
            var gridCenter = Game.Farm.FarmIso.GridToWorld(4.5f, 4.5f);

            Assert.That(fieldCenter.x, Is.EqualTo(gridCenter.x).Within(0.0001f));
            Assert.That(fieldCenter.z, Is.EqualTo(gridCenter.z).Within(0.0001f));
        }

        [Test]
        public void ArtCoverageIsAValidGroundContract()
        {
            Assert.That(Game.Farm.FarmIso.ArtDirtCoverageX, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(Game.Farm.FarmIso.ArtDirtCoverageY, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(Game.Farm.FarmIso.ArtDirtOffsetX, Is.GreaterThanOrEqualTo(0f));
            Assert.That(Game.Farm.FarmIso.ArtDirtOffsetY, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void ScrollingCameraShowsOnlyPartOfTheTenByTenField()
        {
            var fieldHeight = Game.Farm.FarmIso.FieldWorldSize(10, 10).y;

            Assert.That(Game.Farm.FarmIso.ScrollingOrthoSize * 2f, Is.LessThan(fieldHeight));
        }
    }
}
