using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Space.Core.Equipment;
using Space.Core.Levels;
using Space.Core.Sectors;

namespace Space.Core.Tests
{
    public class SectorTests
    {
        static IEnumerable<uint> Seeds()
        {
            for (var seed = 1u; seed <= 60u; seed++)
                yield return seed * 2654435761u;
        }

        [Test]
        public void MapsAreDeterministicForASeed()
        {
            string Shape(SectorMap map) => string.Join("|", map.Beacons.Select(b =>
                $"{b.Kind}{b.Column}{b.Row:0.00}{b.Level}:{string.Join(",", b.Links)}"));
            Assert.AreEqual(Shape(SectorMapGenerator.Generate(42u, 3)), Shape(SectorMapGenerator.Generate(42u, 3)));
            Assert.IsTrue(Seeds().Any(seed => Shape(SectorMapGenerator.Generate(seed, 3)) != Shape(SectorMapGenerator.Generate(42u, 3))));
        }

        [Test]
        public void MapsGoFromOneStartToOneExit()
        {
            foreach (var seed in Seeds())
            {
                var map = SectorMapGenerator.Generate(seed, 3);
                Assert.AreEqual(BeaconKind.Start, map.Beacons[map.Start].Kind);
                Assert.AreEqual(BeaconKind.Exit, map.Beacons[map.Exit].Kind);
                Assert.AreEqual(0, map.Beacons[map.Start].Column);
                Assert.That(map.Columns, Is.InRange(SectorMapGenerator.MinMiddleColumns + 2, SectorMapGenerator.MaxMiddleColumns + 2));
                Assert.AreEqual(1, map.Beacons.Count(b => b.Kind == BeaconKind.Start));
                Assert.AreEqual(1, map.Beacons.Count(b => b.Kind == BeaconKind.Exit));
                Assert.AreEqual(map.Columns - 1, map.JumpsToExit(map.Start), $"seed {seed}: one jump per column");
            }
        }

        [Test]
        public void EveryBeaconHasAWayInAndAWayForward()
        {
            foreach (var seed in Seeds())
            {
                var map = SectorMapGenerator.Generate(seed, 3);
                for (var i = 0; i < map.Beacons.Count; i++)
                {
                    var beacon = map.Beacons[i];
                    foreach (var to in beacon.Links)
                        Assert.That(map.Beacons[to].Column - beacon.Column, Is.InRange(0, 1), $"seed {seed}: forward or sideways only");
                    if (i != map.Exit)
                        Assert.IsTrue(beacon.Links.Any(to => map.Beacons[to].Column == beacon.Column + 1), $"seed {seed}: beacon {i} leads on");
                    if (i != map.Start)
                        Assert.IsTrue(map.Beacons.Any(b => b.Links.Contains(i) && b.Column == beacon.Column - 1), $"seed {seed}: beacon {i} has a way in");
                }
            }
        }

        [Test]
        public void EverySectorHasAShopAwayFromTheStartAndFightsHaveALevel()
        {
            foreach (var seed in Seeds())
            {
                var map = SectorMapGenerator.Generate(seed, 3);
                var shops = map.Beacons.Where(b => b.Kind == BeaconKind.Shop).ToList();
                Assert.That(shops.Count, Is.InRange(1, SectorMapGenerator.MaxShops), $"seed {seed}");
                Assert.IsTrue(shops.All(b => b.Column >= 2), $"seed {seed}");
                Assert.IsTrue(map.Beacons.Where(b => b.Kind == BeaconKind.Elite).All(b => b.Column >= 2), $"seed {seed}");
                foreach (var beacon in map.Beacons.Where(b => b.Kind == BeaconKind.Combat || b.Kind == BeaconKind.Elite))
                    Assert.That(beacon.Level, Is.InRange(0, 2), $"seed {seed}");
            }
            Assert.IsTrue(Seeds().Any(seed => SectorMapGenerator.Generate(seed, 3).Beacons.Any(b => b.Kind == BeaconKind.Elite)));
        }

        [Test]
        public void SideLinksAllowLongerWays()
        {
            var longest = 0;
            foreach (var seed in Seeds())
            {
                var map = SectorMapGenerator.Generate(seed, 3);
                longest = System.Math.Max(longest, LongestWay(new SectorRun(map)));
            }
            Assert.Greater(longest, 6, "some sectors can take a detour");
        }

        static int LongestWay(SectorRun run)
        {
            if (run.AtExit)
                return run.Jumps;
            var best = -1;
            foreach (var to in run.Map.Beacons[run.Current].Links.ToList())
            {
                if (!run.CanJump(to))
                    continue;
                var copy = new SectorRun(run.Map) { Current = run.Current, Jumps = run.Jumps, Deadline = run.Deadline, Visited = new List<int>(run.Visited) };
                copy.Jump(to);
                best = System.Math.Max(best, LongestWay(copy));
            }
            return best;
        }

        [Test]
        public void TheShipCanNeverBeStuck()
        {
            foreach (var seed in Seeds())
            {
                var run = new SectorRun(SectorMapGenerator.Generate(seed, 3));
                var random = new Unity.Mathematics.Random(seed);
                for (var step = 0; step < 50 && !run.AtExit; step++)
                {
                    var choices = run.Map.Beacons[run.Current].Links.Where(run.CanJump).ToList();
                    Assert.IsNotEmpty(choices, $"seed {seed}: stuck at beacon {run.Current}");
                    Assert.IsTrue(run.Jump(choices[random.NextInt(choices.Count)]));
                }
                Assert.IsTrue(run.AtExit, $"seed {seed}");
            }
        }

        [Test]
        public void JumpsFollowTheLinksAndNeverGoBack()
        {
            var map = SectorMapGenerator.Generate(7u, 3);
            var run = new SectorRun(map);
            Assert.AreEqual(map.JumpsToExit(map.Start) + SectorRun.SpareJumps, run.Deadline);
            var far = map.Beacons.FindIndex(b => b.Column == 2);
            Assert.IsFalse(run.Jump(far), "not linked to the start");
            var next = map.Beacons[map.Start].Links[0];
            Assert.IsTrue(run.Jump(next));
            Assert.AreEqual(1, run.Jumps);
            Assert.IsFalse(run.CanJump(map.Start), "no way back");
            Assert.IsFalse(run.CanJump(next), "already there");
        }

        [Test]
        public void OnlyNearbyBeaconsShowWhatTheyAre()
        {
            foreach (var seed in Seeds())
            {
                var map = SectorMapGenerator.Generate(seed, 3);
                var run = new SectorRun(map);
                for (var i = 0; i < map.Beacons.Count; i++)
                {
                    var b = map.Beacons[i];
                    var expected = b.Kind == BeaconKind.Shop || b.Kind == BeaconKind.Exit || b.Kind == BeaconKind.Start ||
                                   map.Beacons[map.Start].Links.Contains(i);
                    Assert.AreEqual(expected, run.IsKindKnown(i), $"seed {seed}, beacon {i}");
                }
            }
        }

        [Test]
        public void LateDeliveriesLoseTheirBonus()
        {
            Assert.AreEqual(SectorRun.BaseBonus, SectorRun.DeliveryBonus(4, 6));
            Assert.AreEqual(SectorRun.BaseBonus, SectorRun.DeliveryBonus(6, 6));
            Assert.AreEqual(SectorRun.BaseBonus - SectorRun.BonusLostPerLateJump, SectorRun.DeliveryBonus(7, 6));
            Assert.AreEqual(0, SectorRun.DeliveryBonus(20, 6));
        }

        [Test]
        public void ElitesBringATwinForEachEnemy()
        {
            var enemies = new[]
            {
                new LevelDefinition.EnemySpawn { Ship = "Raider", DelaySeconds = 2f, Distance = 200f, AngleDegrees = 30f, Along = 50f },
            };
            var elite = EliteLevels.Reinforce(enemies);
            Assert.AreEqual(2, elite.Length);
            Assert.AreEqual("Raider", elite[1].Ship);
            Assert.AreEqual(2f + EliteLevels.TwinDelaySeconds, elite[1].DelaySeconds);
            Assert.AreEqual(200f + EliteLevels.TwinExtraDistance, elite[1].Distance);
            Assert.AreEqual(-30f, elite[1].AngleDegrees, "from the other side");
            Assert.AreEqual(enemies[0].Ship, elite[0].Ship);
            Assert.IsEmpty(EliteLevels.Reinforce(null));
        }

        [Test]
        public void EliteRewardsAreRarer()
        {
            float AverageTier(bool elite)
            {
                var total = 0;
                var count = 0;
                for (var seed = 1u; seed <= 200u; seed++)
                    foreach (var offer in RewardGenerator.Roll(ShipLoadout.Starter(), seed, 0, elite))
                    {
                        total += offer.Kind == RewardKind.Item ? ItemCatalog.Get(offer.Id).Tier : UpgradeCatalog.Get(offer.Id).Tier;
                        count++;
                    }
                return (float)total / count;
            }
            Assert.Greater(AverageTier(true), AverageTier(false) + 0.3f);
        }
    }
}
