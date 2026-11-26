using NUnit.Framework;
using Space.Presentation.Ships;
using UnityEngine;

namespace Space.Presentation.Tests
{
    public class ShipArtTests
    {
        static ShipArt Tug()
        {
            var art = new ShipArt { DesignId = "test" };
            art.Turrets.Add(new TurretArt { Kind = TurretKind.Laser, Head = new Mesh { name = "Gun" } });
            art.Turrets.Add(new TurretArt { Kind = TurretKind.Twin, Head = new Mesh { name = "Twin" } });
            art.Turrets.Add(new TurretArt { Kind = TurretKind.Missile, Head = new Mesh { name = "Pod" } });
            return art;
        }

        [Test]
        public void EachWeaponGetsTheHeadOfItsKindOrFamily()
        {
            var art = Tug();
            Assert.AreEqual("Gun", art.BestTurret(TurretKind.Laser).Head.name);
            Assert.AreEqual("Gun", art.BestTurret(TurretKind.Ion).Head.name, "énergie");
            Assert.AreEqual("Twin", art.BestTurret(TurretKind.Twin).Head.name);
            Assert.AreEqual("Twin", art.BestTurret(TurretKind.Rail).Head.name, "canons");
            Assert.AreEqual("Twin", art.BestTurret(TurretKind.Heavy).Head.name, "canons");
            Assert.AreEqual("Pod", art.BestTurret(TurretKind.Missile).Head.name);
        }

        [Test]
        public void WithoutTheFamilyTheFirstHeadIsUsed()
        {
            var art = new ShipArt();
            art.Turrets.Add(new TurretArt { Kind = TurretKind.Twin, Head = new Mesh { name = "Twin" } });
            Assert.AreEqual("Twin", art.BestTurret(TurretKind.Missile).Head.name);
            Assert.IsNull(new ShipArt().BestTurret(TurretKind.Laser));
        }
    }
}
