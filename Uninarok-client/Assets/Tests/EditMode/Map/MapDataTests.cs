using System;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Map.Tests
{
    // TASKS-0005 T1.1 (FR-4, FR-5): the asset can be built in memory from flat arrays, and rejects inconsistent ones.
    public class MapDataTests
    {
        [Test]
        public void Assign_StoresTheGeometryAndTheArrays()
        {
            var data = MapTestData.Create(8, 6, origin: new Vector2(1f, 2f), rowSign: -1f, diagonalFlip: true, name: "m");
            try
            {
                Assert.AreEqual("m", data.MapName);
                Assert.AreEqual(8, data.CellsX);
                Assert.AreEqual(6, data.CellsZ);
                Assert.AreEqual(MapTestData.CellSize, data.CellSize);
                Assert.AreEqual(new Vector2(1f, 2f), data.Origin);
                Assert.AreEqual(-1f, data.RowSign);
                Assert.IsTrue(data.DiagonalFlip);
                Assert.AreEqual(48 * 19, data.CellDataBytes);
                Assert.IsFalse(data.HasSettings);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Assign_RejectsArraysOfTheWrongLength()
        {
            var data = ScriptableObject.CreateInstance<MapData>();
            try
            {
                Assert.Throws<ArgumentException>(() =>
                    data.Assign("m", 2, 2, 0.5f, Vector2.zero, 1f, false, new float[15], new byte[4], new byte[4], new byte[4]));
                Assert.Throws<ArgumentException>(() =>
                    data.Assign("m", 2, 2, 0.5f, Vector2.zero, 1f, false, new float[16], new byte[3], new byte[4], new byte[4]));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    data.Assign("m", 2, 2, 0.5f, Vector2.zero, 0f, false, new float[16], new byte[4], new byte[4], new byte[4]));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }
    }
}
