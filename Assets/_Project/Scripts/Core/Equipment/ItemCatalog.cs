using System.Collections.Generic;
using Space.Core.Combat;

namespace Space.Core.Equipment
{
    /// <summary>
    /// Every item that exists in the game, eight per category. Items have character rather than a
    /// strict progression: a big hit comes with a short range or a long reload, a strong shield
    /// needs more power or reboots slowly, a powerful reactor makes the ship heavier or more fragile.
    /// First-pass numbers, to be balanced in playtests (the starter items match the Cargo of the
    /// « Premier contact » duel).
    /// </summary>
    public static class ItemCatalog
    {
        static List<ItemDefinition> _all;
        static Dictionary<string, ItemDefinition> _byId;

        public static IReadOnlyList<ItemDefinition> All
        {
            get
            {
                if (_all == null)
                    Build();
                return _all;
            }
        }

        public static ItemDefinition Get(string id)
        {
            if (_byId == null)
                Build();
            return id != null && _byId.TryGetValue(id, out var item) ? item : null;
        }

        public static IEnumerable<ItemDefinition> InCategory(ItemCategory category)
        {
            foreach (var item in All)
                if (item.Category == category)
                    yield return item;
        }

        // Ids of the starting ship's equipment.
        public const string StarterReactor = "reactor_f8";
        public const string StarterShield = "shield_civil";
        public const string StarterLaser = "energy_mining_laser";
        public const string StarterCannon = "physical_mass_driver";
        public const string StarterEngine = "engine_cargo";
        public const string StarterArmor = "armor_cargo";

        static void Build()
        {
            _all = new List<ItemDefinition>(64);
            AddReactors();
            AddShields();
            AddEnergyWeapons();
            AddPhysicalWeapons();
            AddMissiles();
            AddEngines();
            AddArmors();
            AddDrones();

            _byId = new Dictionary<string, ItemDefinition>(_all.Count);
            foreach (var item in _all)
                _byId.Add(item.Id, item);
        }

        static ItemDefinition Add(string id, ItemCategory category, int tier, int price, string name, string description)
        {
            var item = new ItemDefinition
            {
                Id = id, Category = category, Tier = tier, Price = price, Name = name, Description = description,
                Starter = price == 0,
            };
            _all.Add(item);
            return item;
        }

        // ---------------------------------------------------------------- Reactors: power vs weight or fragility

        static void Reactor(string id, int tier, int price, string name, int power, float speedFactor, float hullBonus, string description)
        {
            var item = Add(id, ItemCategory.Reactor, tier, price, name, description);
            item.Power = power;
            item.SpeedFactor = speedFactor;
            item.HullBonus = hullBonus;
        }

        static void AddReactors()
        {
            Reactor(StarterReactor, 1, 0, "Réacteur à fusion F-8", 8, 1f, 0f,
                "Le réacteur d'origine du Cargo. Fiable, sans surprise.");
            Reactor("reactor_compact", 1, 140, "Réacteur compact C-7", 7, 1.1f, 0f,
                "Une unité de puissance de moins, mais le vaisseau s'allège et manœuvre mieux.");
            Reactor("reactor_heavy", 1, 200, "Réacteur lourd L-10", 10, 0.88f, 0f,
                "Deux unités de plus, au prix d'une masse qui ralentit le vaisseau.");
            Reactor("reactor_salvage", 1, 180, "Générateur de récupération", 9, 0.95f, 40f,
                "Monté dans une coque blindée de récupération : un peu plus de puissance et de solidité, un peu plus lent.");
            Reactor("reactor_unstable", 2, 260, "Pile instable X-11", 11, 1f, -80f,
                "Beaucoup de puissance pour sa taille, mais une explosion latente qui fragilise la coque.");
            Reactor("reactor_overdrive", 2, 340, "Réacteur à surcharge S-12", 12, 0.85f, -50f,
                "Puissance brute : lourd et fragile, pour ceux qui veulent tout alimenter à la fois.");
            Reactor("reactor_military", 3, 520, "Cœur militaire M-10", 10, 1f, 0f,
                "Dix unités sans aucun défaut. Rare et cher.");
            Reactor("reactor_antimatter", 3, 650, "Cœur à antimatière A-14", 14, 0.9f, -120f,
                "La plus grande puissance disponible. Un coup au but au mauvais endroit, et tout saute.");
        }

        // ---------------------------------------------------------------- Shields: strength vs power vs recharge

        static void Shield(string id, int tier, int price, string name, float max, float rechargePerPower, float reboot,
            int powerCapacity, float speedFactor, string description)
        {
            var item = Add(id, ItemCategory.Shield, tier, price, name, description);
            item.ShieldMax = max;
            item.ShieldRechargePerPower = rechargePerPower;
            item.ShieldRebootSeconds = reboot;
            item.PowerCapacity = powerCapacity;
            item.SpeedFactor = speedFactor;
        }

        static void AddShields()
        {
            Shield(StarterShield, 1, 0, "Bouclier civil B-150", 150f, 1.5f, 10f, 4, 1f,
                "Le bouclier d'origine : protection moyenne, recharge moyenne.");
            Shield("shield_quick", 1, 160, "Écran rapide E-100", 100f, 3f, 5f, 3, 1f,
                "Faible, mais il se recharge deux fois plus vite et se relève en 5 s.");
            Shield("shield_frugal", 1, 170, "Bouclier économe B-130", 130f, 1.8f, 10f, 2, 1f,
                "Fonctionne à plein avec seulement 2 unités de puissance : libère le réacteur pour les armes.");
            Shield("shield_smuggler", 1, 150, "Voile de contrebandier", 90f, 2.5f, 3f, 2, 1.05f,
                "Presque aucune protection, mais il ne reste jamais longtemps à terre et pèse peu.");
            Shield("shield_rampart", 2, 320, "Rempart R-260", 260f, 1.2f, 14f, 5, 1f,
                "Très solide, gourmand en puissance. Une fois brisé, il met longtemps à revenir.");
            Shield("shield_cycler", 2, 300, "Bouclier à cycles C-180", 180f, 2f, 7f, 4, 1f,
                "Un peu plus fort que l'origine, et des cycles de redémarrage courts.");
            Shield("shield_fleet", 2, 380, "Bouclier de flotte F-220", 220f, 1.6f, 10f, 4, 0.95f,
                "Équipement militaire réformé : solide, mais ses générateurs alourdissent le vaisseau.");
            Shield("shield_aegis", 3, 560, "Égide lourde EL-350", 350f, 1f, 16f, 6, 1f,
                "Une forteresse, qui demande 6 unités de puissance et se recharge lentement.");
        }

        // ---------------------------------------------------------------- Weapons

        static WeaponStats Laser(float range, float shieldDamage, float hullDamage, float cooldown, float speed, float spread, int draw) => new()
        {
            Type = DamageType.Laser, Range = range, ShieldDamage = shieldDamage, Damage = hullDamage, Cooldown = cooldown,
            Ammo = -1, ProjectileSpeed = speed, SpreadDegrees = spread, PowerDraw = draw,
        };

        static WeaponStats Ion(float range, float shieldDamage, float cooldown, float speed, float spread, int draw) => new()
        {
            Type = DamageType.Ion, Range = range, ShieldDamage = shieldDamage, Damage = 0f, Cooldown = cooldown,
            Ammo = -1, ProjectileSpeed = speed, SpreadDegrees = spread, PowerDraw = draw,
        };

        static WeaponStats Cannon(float range, float damage, float cooldown, int ammo, float speed, float spread, int draw) => new()
        {
            Type = DamageType.Kinetic, Range = range, Damage = damage, Cooldown = cooldown, Ammo = ammo,
            ProjectileSpeed = speed, SpreadDegrees = spread, PowerDraw = draw,
        };

        static WeaponStats Missile(float range, float hullDamage, float shieldDamage, int ammo, float cooldown, float speed,
            float spread, int draw, bool orderedOnly) => new()
        {
            Type = DamageType.Missile, Range = range, Damage = hullDamage, ShieldDamage = shieldDamage, Ammo = ammo,
            Cooldown = cooldown, ProjectileSpeed = speed, SpreadDegrees = spread, PowerDraw = draw, OrderedTargetOnly = orderedOnly,
        };

        static void Weapon(string id, ItemCategory category, int tier, int price, string name, WeaponStats stats, string description)
        {
            var item = Add(id, category, tier, price, name, description);
            item.Weapon = stats;
        }

        static void AddEnergyWeapons()
        {
            const ItemCategory c = ItemCategory.EnergyWeapon;
            Weapon(StarterLaser, c, 1, 0, "Laser de minage modifié", Laser(500f, 55f, 12f, 6f, 1500f, 0.4f, 3),
                "Use le bouclier et entame un peu la coque. L'arme d'origine du Cargo.");
            Weapon("energy_ionizer", c, 1, 180, "Ioniseur I-1", Ion(520f, 95f, 6f, 2000f, 0.5f, 3),
                "Ne touche que les boucliers, mais les vide presque deux fois plus vite.");
            Weapon("energy_pulse", c, 1, 200, "Laser à impulsions P-2", Laser(380f, 30f, 8f, 2.5f, 1500f, 0.7f, 3),
                "Des tirs faibles et rapprochés, à courte portée : régulier, peu spectaculaire.");
            Weapon("energy_thermal", c, 1, 220, "Faisceau thermique T-6", Laser(300f, 40f, 40f, 6f, 1500f, 0.6f, 3),
                "Brûle la coque autant que le bouclier, mais seulement de très près.");
            Weapon("energy_heavy", c, 2, 340, "Laser lourd L-4", Laser(450f, 90f, 25f, 10f, 1400f, 0.4f, 4),
                "Un tir dévastateur toutes les 10 s. Demande 4 unités de puissance.");
            Weapon("energy_lance", c, 2, 360, "Lance longue portée", Laser(750f, 45f, 10f, 8f, 2200f, 0.2f, 3),
                "Frappe de très loin avec précision, mais chaque tir fait peu de dégâts.");
            Weapon("energy_ion_heavy", c, 2, 380, "Projecteur à ions lourd", Ion(450f, 160f, 9f, 1800f, 0.5f, 4),
                "Peut briser un bouclier moyen en un seul tir. Aucun effet sur la coque.");
            Weapon("energy_plasma", c, 3, 620, "Lance à plasma P-9", Laser(550f, 110f, 45f, 12f, 1200f, 0.5f, 5),
                "Énorme sur le bouclier comme sur la coque, mais lente et très gourmande en puissance.");
        }

        static void AddPhysicalWeapons()
        {
            const ItemCategory c = ItemCategory.PhysicalWeapon;
            Weapon(StarterCannon, c, 1, 0, "Canon à masse M-1", Cannon(450f, 85f, 5f, 8, 350f, 0.5f, 2),
                "Des obus lourds et lents, bloqués par un bouclier actif. 8 obus par combat.");
            Weapon("physical_light", c, 1, 150, "Canon léger L-2", Cannon(480f, 55f, 4f, 16, 400f, 0.5f, 1),
                "Moins de dégâts, deux fois plus d'obus, et une seule unité de puissance.");
            Weapon("physical_scatter", c, 1, 170, "Canon à mitraille", Cannon(250f, 120f, 6f, 10, 400f, 1.5f, 2),
                "Terrible à bout portant, inutile au-delà de 250 m.");
            Weapon("physical_autocannon", c, 1, 190, "Canon automatique A-3", Cannon(380f, 30f, 1.8f, 30, 450f, 1f, 2),
                "Une rafale continue de petits obus. Dispersé, mais régulier.");
            Weapon("physical_mortar", c, 2, 280, "Mortier à fragmentation", Cannon(600f, 100f, 8f, 8, 220f, 1.2f, 2),
                "Longue portée, mais ses obus très lents ratent facilement une cible en mouvement.");
            Weapon("physical_howitzer", c, 2, 360, "Obusier de flotte", Cannon(400f, 200f, 11f, 5, 250f, 0.7f, 3),
                "Cinq obus qui font chacun très mal. Rechargement interminable.");
            Weapon("physical_mass_driver_4", c, 2, 400, "Canon à masse M-4", Cannon(500f, 95f, 5f, 14, 380f, 0.45f, 3),
                "La version militaire du M-1 : un peu meilleure partout, plus gourmande.");
            Weapon("physical_railgun", c, 3, 600, "Canon électromagnétique", Cannon(800f, 140f, 10f, 6, 1400f, 0.15f, 4),
                "Précis et rapide à très longue portée. Peu de munitions, beaucoup de puissance.");
        }

        static void AddMissiles()
        {
            const ItemCategory c = ItemCategory.Missile;
            Weapon("missile_rockets", c, 1, 160, "Roquettes non guidées", Missile(400f, 110f, 60f, 6, 3f, 600f, 1.5f, 1, false),
                "Six roquettes tirées automatiquement, imprécises.");
            Weapon("missile_hornet", c, 1, 220, "Missiles Frelon", Missile(700f, 200f, 150f, 3, 4f, 900f, 0.2f, 1, true),
                "Trois missiles guidés, tirés seulement sur la cible que tu désignes.");
            Weapon("missile_swarm", c, 1, 240, "Essaim de micro-missiles", Missile(550f, 70f, 50f, 8, 2.5f, 1100f, 0.4f, 1, false),
                "Huit petits missiles tirés automatiquement : harcèlement plutôt que coup décisif.");
            Weapon("missile_shieldpiercer", c, 2, 320, "Perce-bouclier", Missile(750f, 120f, 300f, 2, 6f, 1000f, 0.2f, 1, true),
                "Deux missiles conçus pour faire tomber un bouclier d'un coup.");
            Weapon("missile_albatross", c, 2, 360, "Missiles Albatros", Missile(1100f, 180f, 120f, 3, 7f, 700f, 0.2f, 1, true),
                "Portée immense : frapper avant même d'être à portée de l'ennemi.");
            Weapon("missile_hammer", c, 2, 380, "Torpilles Marteau", Missile(600f, 380f, 120f, 2, 8f, 450f, 0.3f, 2, true),
                "Deux torpilles lentes et dévastatrices pour la coque.");
            Weapon("missile_viper", c, 3, 520, "Missiles Vipère", Missile(800f, 150f, 150f, 4, 5f, 1200f, 0.15f, 2, true),
                "Quatre missiles rapides et précis. Équilibrés, chers.");
            Weapon("missile_black_sun", c, 3, 700, "Torpille Soleil Noir", Missile(650f, 700f, 400f, 1, 10f, 400f, 0.3f, 3, true),
                "Une seule torpille. Si elle touche, le combat est presque fini.");
        }

        // ---------------------------------------------------------------- Engines: top speed vs agility vs power

        static void Engine(string id, int tier, int price, string name, float speed, float acceleration, float turnDegrees,
            int powerCapacity, string description)
        {
            var item = Add(id, ItemCategory.Engine, tier, price, name, description);
            item.MaxSpeed = speed;
            item.Acceleration = acceleration;
            item.TurnRateDegrees = turnDegrees;
            item.PowerCapacity = powerCapacity;
        }

        static void AddEngines()
        {
            Engine(StarterEngine, 1, 0, "Propulseurs de cargo", 45f, 3f, 15f, 3,
                "Lents et lourds, mais ils font le travail.");
            Engine("engine_frugal", 1, 140, "Propulseurs économes", 40f, 2.6f, 14f, 1,
                "À pleine vitesse avec une seule unité de puissance. Un peu plus lents.");
            Engine("engine_nimble", 1, 170, "Propulseurs nerveux", 42f, 5f, 24f, 3,
                "Vitesse de pointe modeste, mais le vaisseau tourne et accélère bien mieux.");
            Engine("engine_tug", 1, 160, "Moteurs de remorqueur", 38f, 6f, 18f, 3,
                "Lents, mais une poussée énorme : démarrages et arrêts presque immédiats.");
            Engine("engine_wake", 2, 300, "Propulsion à sillage", 62f, 2.2f, 10f, 4,
                "Très rapide en ligne droite, pataud dans les virages.");
            Engine("engine_racer", 2, 320, "Moteur de course", 58f, 3.5f, 13f, 4,
                "Rapide, mais il lui faut 4 unités de puissance pour donner tout ce qu'il a.");
            Engine("engine_vector", 2, 360, "Moteurs vectoriels", 48f, 4.5f, 30f, 4,
                "Le vaisseau pivote sur place : idéal pour garder ses armes pointées.");
            Engine("engine_military", 3, 560, "Brûleurs militaires", 55f, 5f, 20f, 5,
                "Rapides et agiles à la fois, mais très gourmands en puissance.");
        }

        // ---------------------------------------------------------------- Armor: hull vs speed

        static void Armor(string id, int tier, int price, string name, float hull, float speedFactor, string description)
        {
            var item = Add(id, ItemCategory.Armor, tier, price, name, description);
            item.Hull = hull;
            item.SpeedFactor = speedFactor;
        }

        static void AddArmors()
        {
            Armor(StarterArmor, 1, 0, "Coque de cargo", 450f, 1f, "La coque d'origine : épaisse, mais pas conçue pour le combat.");
            Armor("armor_light", 1, 150, "Coque allégée", 380f, 1.12f, "Moins solide, mais le vaisseau gagne en vitesse et en agilité.");
            Armor("armor_composite", 1, 160, "Plaques composites", 540f, 0.97f, "Un peu plus de protection pour un poids raisonnable.");
            Armor("armor_modular", 1, 180, "Coque modulaire", 500f, 1f, "Un léger gain de solidité, sans aucun défaut.");
            Armor("armor_ceramic", 2, 300, "Blindage céramique", 620f, 0.92f, "Solide, un peu lourd.");
            Armor("armor_smuggler", 2, 320, "Coque de contrebandier", 470f, 1.05f, "Renforcée aux bons endroits, et plus légère que la coque d'origine.");
            Armor("armor_reactive", 2, 380, "Blindage réactif lourd", 780f, 0.82f, "Encaisse énormément, mais le vaisseau se traîne.");
            Armor("armor_cruiser", 3, 560, "Blindage de croiseur", 900f, 0.75f, "Le double de la coque d'origine. Le vaisseau devient une forteresse lente.");
        }

        // ---------------------------------------------------------------- Drones: escorts with their own trade-offs

        static void Drone(string id, int tier, int price, string name, float hull, float speed, WeaponStats weapon, string description)
        {
            var item = Add(id, ItemCategory.Drone, tier, price, name, description);
            item.DroneHull = hull;
            item.DroneSpeed = speed;
            item.Weapon = weapon;
        }

        static void AddDrones()
        {
            Drone("drone_escort", 1, 150, "Drone d'escorte E-1", 80f, 120f, Cannon(320f, 6f, 1.2f, -1, 350f, 0.8f, 1),
                "Un petit drone armé d'un canon, qui suit le vaisseau en formation.");
            Drone("drone_harrier", 1, 140, "Drone harceleur", 50f, 150f, Cannon(250f, 4f, 0.6f, -1, 400f, 1.2f, 1),
                "Fragile et rapide, il tire sans arrêt de très près.");
            Drone("drone_interceptor", 1, 180, "Drone intercepteur", 60f, 170f, Laser(300f, 8f, 4f, 1f, 1500f, 0.7f, 1),
                "Le plus rapide des drones, armé d'un petit laser.");
            Drone("drone_ion", 1, 200, "Drone ionique", 70f, 120f, Ion(380f, 25f, 2.5f, 2000f, 0.5f, 1),
                "Aide à faire tomber le bouclier ennemi. Aucun dégât à la coque.");
            Drone("drone_sentinel", 2, 260, "Drone sentinelle", 120f, 60f, Cannon(550f, 12f, 3f, -1, 500f, 0.4f, 1),
                "Lent, mais il tire de loin avec précision.");
            Drone("drone_rocket", 2, 280, "Drone lance-roquettes", 90f, 100f, Cannon(400f, 25f, 3f, 12, 500f, 1f, 1),
                "Douze roquettes qui font mal, puis plus rien.");
            Drone("drone_heavy", 2, 300, "Drone lourd L-2", 180f, 80f, Cannon(350f, 14f, 2f, -1, 380f, 0.7f, 1),
                "Plus lent, mais il encaisse deux fois plus que le drone d'escorte.");
            Drone("drone_assault", 3, 480, "Drone d'assaut A-5", 140f, 110f, Laser(400f, 15f, 8f, 2f, 1500f, 0.5f, 1),
                "Rapide, solide et bien armé : le meilleur des drones, à un prix en conséquence.");
        }
    }
}
