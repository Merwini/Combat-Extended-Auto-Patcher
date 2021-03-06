using CombatExtended;
using HugsLib;
using HugsLib.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reflection;
using Verse;

namespace CombatExtendedAutoPatcher
{
    [StaticConstructorOnStartup]
    public class Base : ModBase
    {
        public override string ModIdentifier
        {
            get { return "CEAutoPatcher"; }
        }

        //gun values here since multiple methods need them
        //statbases
        float gunMass = 0f;
        float accuracyTouch = 0f;
        float accuracyShort = 0f;
        float accuracyMedium = 0f;
        float accuracyLong = 0f;

        //Verb properties
        VerbProperties vp;
        string verbLabel = "";
        SoundDef soundCast = null;
        SoundDef soundCastTail = null;
        SoundDef soundAiming = null;
        TargetingParameters targetParams = null;
        RulePackDef rangedFireRulepack = null;
        float forcedMissRadius = 0f;
        int ticksBetweenBurstShots = 0;
        int burstShotCountV = 1;
        float warmupTime = 0;
        float gunRange = 0f;
        float muzzleFlashScale = 0;
        bool hasStandardCommand = true;
        bool ai_IsBuildingDestroyer = false;
        float ai_AvoidFriendlyFireRadius = 0;
        bool onlyManualCast = false;
        bool stopBurstWithoutLos = true;
        ThingDef activeProjectile;



        //projectile properties
        DamageDef projectileDamageType;
        int projectileDamage = 1;
        float projectilePenetration = 1f;

        //Misc
        gunTypes gunType;
        TechLevel gunTechLevel = TechLevel.Undefined;


        //types of guns, used for determining stats
        enum gunTypes
        {
            Bow,
            Grenade,
            Pistol,
            SMG,
            Rifle,
            Shotgun,
            Sniper,
            MachineGun,
            GrenadeLauncher,
            RocketLauncher,
            Turret,
            Other
        }

        //centralized values for determining Bulk, Worn Bulk, Armor Values
        public float skinBulkAdd = 1f;
        public float skinWulkAdd = 0.5f;
        public float slinBulkMult = 1f;
        public float skinWulkMult = 1f;

        public float midBulkAdd = 5f;
        public float midWulkAdd = 3f;
        public float midBulkMult = 1f;
        public float midWulkMult = 1f;

        public float shellBulkAdd = 7.5f;
        public float shellWulkAdd = 2.5f;
        public float shellBulkMult = 20f;
        public float shellWulkMult = 5f;

        /* didn't end up using
        public float skinSharpMult;
        public float skinBluntMult;
        public float midSharpMult;
        public float midBluntMult;
        public float shellSharpMult;
        public float shellBluntMult;
        */

        public float sharpMult = 10f;
        public float bluntMult = 40f;
        public float[] armorTechMults = new float[] { 0.25f, 0.5f, 0.75f, 1f, 2f, 3f, 4f };

        //will be used to store references to the generic ammoset def
        //public AmmoSetDef genericAmmoSet;
        public ThingDef rifleRef;

        //will be used to store references to the generic ammo defs
        //TODO a 2d array is probably better but this is much more readable/maintainable?
        public AmmoDef[][] ammoRefs = new AmmoDef[][]
        {
                new AmmoDef[6], //gun industrial ammos
                new AmmoDef[3], //gun spacer ammos
                new AmmoDef[4], //shotgun industrial ammos
                new AmmoDef[3], //shotgun spacer ammos
                new AmmoDef[3], //bow ammos
        };
        public AmmoDef[][] genericAmmos = new AmmoDef[][]
        {
                new AmmoDef[6],
                new AmmoDef[3],
                new AmmoDef[4],
                new AmmoDef[3],
                new AmmoDef[3],
        };
        public RecipeDef[][] ammoRecipeRefs = new RecipeDef[][]
        {
                new RecipeDef[6], //gun industrial ammos
                new RecipeDef[3], //gun spacer ammos
                new RecipeDef[4], //shotgun industrial ammos
                new RecipeDef[3], //shotgun spacer ammos
                new RecipeDef[3], //bow ammos
        };

        //to be used by the four patch methods
        //0 : string, type of def being patched by that method
        //1 : float, stopwatch time elapsed for that patch operation
        //2 : integer, defs of that type found
        //3 : integer, defs successfully patched
        //4 : integer, defs unsuccessfully patched
        Stopwatch stopwatchMaster = new Stopwatch();
        Stopwatch stopwatch = new Stopwatch();
        //used for logging
        string patchedMessage = "Patched defs of type {0} in {1:F4} seconds. {2} patched out of {3}, {4} failed.";
        int defsPatched = 0; //2
        int defsTotal = 0; //3
        int defsFailed = 0; //4
        StringBuilder failureList = new StringBuilder(); //will be logged after patchedMessage if defsFailed > 0
        StringBuilder pew = new StringBuilder(); //I have no idea why getting a projectile's damage requires a StringBuilder

        public List<ThingDef> weaponList = new List<ThingDef>();
        public List<ThingDef> apparelList = new List<ThingDef>();
        public List<ThingDef> animalList = new List<ThingDef>();
        public List<ThingDef> alienList = new List<ThingDef>();
        public List<ThingDef> turretList = new List<ThingDef>();
        public List<ThingDef> projectileList = new List<ThingDef>();
        public List<AmmoSetDef> ammoSetList = new List<AmmoSetDef>();
        public List<AmmoDef> ammoDefList = new List<AmmoDef>();

        enum VanillaStatBases //StatModifiers used by vanilla and not CE
        {
            AccuracyTouch,
            AccuracyShort,
            AccuracyMedium,
            AccuracyLong,
        }

        enum CEStatBases //StatModifiers used by CE but not vanilla
        {
            SightsEfficiency,
            ShotSpread,
            SwayFactor,
            Bulk,
            TicksBetweenBurstShots,
            BurstShotCount,
            Recoil,
            ReloadTime
        }

        enum SharedStatBases //StatModifiers used by both vanilla and CE
        {
            MaxHitPoints,
            Flammability,
            DeteriorationRate,
            Beauty,
            SellPriceFactor,
            MarketValue,
            Mass,
            RangedWeapon_Cooldown,
            WorkToMake
        }

        enum AmmoTypesForMake
        {
            FMJ,
            AP,
            HP,
            API,
            HE,
            Sabot
        }

        public override void DefsLoaded()
        {
            if (!ModIsActive)
                return;

            stopwatchMaster.Start();

            MakeLists();


            MakeGenericAmmos();

            PatchWeapons(weaponList);
            PatchApparel(apparelList);
            //PatchAnimals(animalList);
            PatchAliens(alienList);
            PatchTurrets(turretList);
            //TODO: patch hediffs (fix armor values etc)
            //TODO: remove generics so they don't show up during play
            
            /* making sure none of the projectiles are projectile-less
            foreach (ThingDef td in DefDatabase<ThingDef>.AllDefs)
            {
                if (td.defName.Contains("CEAP"))
                {
                    if (td.projectile == null)
                    {
                        Logger.Message(td.defName);
                    }
                }
            }
            */
            stopwatchMaster.Stop();
            Logger.Message($"Combat Extended Auto-Patcher finished in {stopwatchMaster.ElapsedMilliseconds / 1000f} seconds.");
        }

        private void MakeLists() // I know that making lists first is just using extra cpu cycles, but makes it way easier to read/debug/maintain
        {
            stopwatch.Start();
            Logger.Message("Combat Extended Auto-Patcher list-making has started.");

            foreach (AmmoSetDef asd in DefDatabase<AmmoSetDef>.AllDefs)
            {
                /*if (asd.defName.Equals("CEAP_Generic_Gun_AmmoSet"))
                {
                    genericAmmoSet = asd;
                }*/
                try
                {
                    ammoSetList.Add(asd);
                }
                catch (Exception ex)
                {
                    Logger.Message($"Error for {0}: " + ex.ToString(), asd.defName);
                    continue;
                }
            }

            foreach (AmmoDef ad in DefDatabase<AmmoDef>.AllDefs)
            {
                if (ad.defName.Contains("556x45mmNATO"))
                {
                    switch (ad.defName)
                    {
                        case "Ammo_556x45mmNATO_FMJ":
                            ammoRefs[0][0] = ad;
                            break;
                        case "Ammo_556x45mmNATO_AP":
                            ammoRefs[0][1] = ad;
                            break;
                        case "Ammo_556x45mmNATO_HP":
                            ammoRefs[0][2] = ad;
                            break;
                        case "Ammo_556x45mmNATO_Incendiary":
                            ammoRefs[0][3] = ad;
                            break;
                        case "Ammo_556x45mmNATO_HE":
                            ammoRefs[0][4] = ad;
                            break;
                        case "Ammo_556x45mmNATO_Sabot":
                            ammoRefs[0][5] = ad;
                            break;
                        default:
                            break;
                    }
                }
                else if (ad.defName.Contains("6x24mmCharged"))
                {
                    switch (ad.defName)
                    {
                        case "Ammo_6x24mmCharged":
                            ammoRefs[1][0] = ad;
                            break;
                        case "Ammo_6x24mmCharged_AP":
                            ammoRefs[1][1] = ad;
                            break;
                        case "Ammo_6x24mmCharged_Ion":
                            ammoRefs[1][2] = ad;
                            break;
                    }
                }
                else if (ad.defName.Contains("12Gauge"))
                {
                    switch (ad.defName)
                    {
                        case "Ammo_12Gauge_Buck":
                            ammoRefs[2][0] = ad;
                            break;
                        case "Ammo_12Gauge_Slug":
                            ammoRefs[2][1] = ad;
                            break;
                        case "Ammo_12Gauge_Beanbag":
                            ammoRefs[2][2] = ad;
                            break;
                        case "Ammo_12Gauge_ElectroSlug":
                            ammoRefs[2][3] = ad;
                            break;
                        case "Ammo_12GaugeCharged":
                            ammoRefs[3][0] = ad;
                            break;
                        case "Ammo_12GaugeCharged_Slug":
                            ammoRefs[3][1] = ad;
                            break;
                        case "Ammo_12GaugeCharged_Ion":
                            ammoRefs[3][2] = ad;
                            break;
                    }
                }
            }

            foreach (RecipeDef rd in DefDatabase<RecipeDef>.AllDefs)
            {
                if (rd.defName.Contains("556x45mmNATO"))
                {
                    switch (rd.defName)
                    {
                        case "MakeAmmo_556x45mmNATO_FMJ":
                            ammoRecipeRefs[0][0] = rd;
                            break;
                        case "MakeAmmo_556x45mmNATO_AP":
                            ammoRecipeRefs[0][1] = rd;
                            break;
                        case "MakeAmmo_556x45mmNATO_HP":
                            ammoRecipeRefs[0][2] = rd;
                            break;
                        case "MakeAmmo_556x45mmNATO_Incendiary":
                            ammoRecipeRefs[0][3] = rd;
                            break;
                        case "MakeAmmo_556x45mmNATO_HE":
                            ammoRecipeRefs[0][4] = rd;
                            break;
                        case "MakeAmmo_556x45mmNATO_Sabot":
                            ammoRecipeRefs[0][5] = rd;
                            break;
                    }
                }
                else if (rd.defName.Contains("6x24mmCharged"))
                {
                    switch (rd.defName)
                    {
                        case "MakeAmmo_6x24mmCharged":
                            ammoRecipeRefs[1][0] = rd;
                            break;
                        case "MakeAmmo_6x24mmCharged_AP":
                            ammoRecipeRefs[1][1] = rd;
                            break;
                        case "MakeAmmo_6x24mmCharged_Ion":
                            ammoRecipeRefs[1][2] = rd;
                            break;
                    }
                }
                else if (rd.defName.Contains("12Gauge"))
                {
                    switch (rd.defName)
                    {
                        case "MakeAmmo_12Gauge_Buck":
                            ammoRecipeRefs[2][0] = rd;
                            break;
                        case "MakeAmmo_12Gauge_Slug":
                            ammoRecipeRefs[2][1] = rd;
                            break;
                        case "MakeAmmo_12Gauge_Beanbag":
                            ammoRecipeRefs[2][2] = rd;
                            break;
                        case "MakeAmmo_12Gauge_ElectroSlug":
                            ammoRecipeRefs[2][3] = rd;
                            break;
                        case "MakeAmmo_12GaugeCharged":
                            ammoRecipeRefs[3][0] = rd;
                            break;
                        case "MakeAmmo_12GaugeCharged_Slug":
                            ammoRecipeRefs[3][1] = rd;
                            break;
                        case "MakeAmmo_12GaugeCharged_Ion":
                            ammoRecipeRefs[3][2] = rd;
                            break;
                    }
                }
            }

            try //TODO put the try inside the foreach, not the other way around
            {
                foreach (ThingDef td in DefDatabase<ThingDef>.AllDefs)                 
                {
                    //finds the generic ammos and stores references to them
                    //TODO refactor, try should be inside the foreach loop
                    if (td.defName.Contains("Gun")) //TODO use a FindIndex instead of foreach unless I end up needing to find more
                    {
                        switch (td.defName)
                        {
                            case "Gun_AssaultRifle": //formerly used CEAP_Gun_Generic
                                rifleRef = td;
                                break;
                            default:
                                break;
                        }
                    }
                    if ((td.category != null) && (td.category == ThingCategory.Projectile))
                    {
                        projectileList.Add(td);
                        continue;
                    }
                    if (td.IsWeapon)
                    {
                        weaponList.Add(td);
                        continue;
                    }
                    if (td.IsApparel)
                    {
                        apparelList.Add(td);
                        continue;
                    }
                    /*else if (td.IsPawn?)
                    {
                        animalList.Add(td);
                        continue;
                    }*/
                    /*else if (td.IsAlien?)
                    {
                        alienList.Add(td);
                        continue;
                    }*/
                    if ((td.thingClass != null) && (td.thingClass.ToString().Contains("TurretGun")))
                    {
                        turretList.Add(td);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
            finally
            {
                stopwatch.Stop();
                Logger.Message($"Combat Extended Auto-Patcher has finished making lists in {stopwatch.ElapsedMilliseconds / 1000f} seconds.");
                stopwatch.Reset();
            }
            
            foreach (ThingDef pawn in DefDatabase<ThingDef>.AllDefs)
            {
                //List<ThingDef> allPawnDefs = DefDatabase<ThingDef>.AllDefs.Where(thing => Helpers.IsSupertypeOf(typeof(Pawn), thing.thingClass)).ToList();
                /*public static bool IsSupertypeOf(Type baseType, Type currentType)
                {
                    return baseType.IsAssignableFrom(currentType);
                }*/
                if (typeof(Pawn).IsAssignableFrom(pawn.thingClass))
                {
                    if (pawn.tools.Any(tool => !typeof(ToolCE).IsAssignableFrom(tool.GetType()))) //I'm not actually sure if this is the best way to do it
                    {
                    alienList.Add(pawn);
                    }
                }
            }
        }

        private void PatchWeapons(List<ThingDef> weapons)
        {
            BeginPatch("WEAPONS");

            foreach (ThingDef weapon in weapons)
            {
                try
                {
                    if (weapon.IsRangedWeapon)
                    {
                        //any patched ranged weapons will have their original verbs removed and the CE one added, so we can find unpatched ranged weapons by checking their verbs
                        if (weapon.Verbs.Any(vp =>
                                                {
                                                    if (vp.verbClass == null)
                                                        return false;
                                                    else if (vp.verbClass.ToString().EqualsIgnoreCase("Verse.Verb_Shoot")
                                                             || vp.verbClass.ToString().EqualsIgnoreCase("Verse.Verb_ShootOneUse")
                                                             || vp.verbClass.ToString().EqualsIgnoreCase("Verse.Verb_ShootLaunchProjectile"))
                                                        return true;
                                                    else
                                                        return false;
                                                }))
                        {
                            defsTotal++;
                            //TODO:
                            //Burst size will be size for Burst fire mode, 2x for suppressive (if burst > 1)
                            ScrapeGunStats(weapon);
                            PatchStatBases(weapon);
                            PatchVerb(weapon);
                            AddCompsAmmoUser(weapon);
                            AddCompsFireModes(weapon);
                            PatchGunTools(weapon);
                            PatchModExtensions(weapon);
                            

                            /*if (weapon.Verbs[0].verbClass.ToString().EqualsIgnoreCase("Verse.Verb_ShootOneUse")
                               || weapon.Verbs[0].verbClass.ToString().EqualsIgnoreCase("Verse.Verb_ShootLaunch Projectile"))
                            {
                                i dunno, don't add ammouser. maybe restructure this. TODO
                            }
                            else
                            {
                                CombatExtended.CompProperties_AmmoUser ammoUser = new CombatExtended.CompProperties_AmmoUser();
                                weapon.comps.Add(ammoUser);
                            }*/


                            //TODO might need separate if blocks for each of the vanilla verbs

                            ClearGunStats();
                            defsPatched++;
                        }

                    }
                    else if (weapon.IsMeleeWeapon)
                    {
                        for (int i = 0; i < weapon.tools.Count; i++)
                        {
                            if (!typeof(CombatExtended.ToolCE).IsAssignableFrom(weapon.tools[i].GetType()))
                            {
                                weapon.tools[i] = PatchTool(weapon.tools[i]);
                            }
                        }
                        //most also have equippedStatOffsets MeleeCritChance MeleeParryChance and MeleeDodgeChance, but these are not required
                    }
                    else
                    {
                        Logger.Message("Weapon {1} is neither ranged nor melee. Freaking the fuck out.", weapon.defName); //TODO don't say 'freaking the fucking out'
                        defsFailed++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                    failureList.AppendLine(weapon.defName);
                    defsFailed++;
                    continue;
                }
            }
            EndPatch("WEAPONS");
        }

        private void PatchModExtensions(ThingDef weapon)
        {
            if (weapon.modExtensions==null)
            {
                weapon.modExtensions = new List<DefModExtension>();
            }
            weapon.modExtensions.Add(rifleRef.modExtensions[0]); //TODO: change values before adding
        }

        private void PatchGunTools(ThingDef weapon)
        {
            weapon.tools = rifleRef.tools; //TODO different tools based on gunType
        }

        private void PatchVerb(ThingDef needsVerbs)
        {
            bool isAPawn = typeof(Pawn).IsAssignableFrom(needsVerbs.thingClass);

            List<VerbProperties> newVerbs = new List<VerbProperties>();
            //int verbCounter = 0;
            foreach (VerbProperties vp in needsVerbs.Verbs)
            {
                try
                {
                    VerbPropertiesCE newVPCE = new VerbPropertiesCE();
                    if ((vp.verbClass.ToString().Equals("Verse.Verb_Shoot")) || (vp.verbClass.ToString().Equals("Verse.Verb_ShootOneUse")) || vp.verbClass.ToString().Equals("Verse.Verb_LaunchProjectile"))
                    {
                        newVPCE.verbClass = typeof(CombatExtended.Verb_ShootCE);
                        newVPCE.label = vp.label;
                        newVPCE.soundCast = vp.soundCast;
                        newVPCE.soundCastTail = vp.soundCastTail;
                        newVPCE.soundAiming = vp.soundAiming;
                        newVPCE.muzzleFlashScale = vp.muzzleFlashScale;
                        newVPCE.hasStandardCommand = vp.hasStandardCommand;
                        newVPCE.range = vp.range;
                        newVPCE.ticksBetweenBurstShots = vp.ticksBetweenBurstShots;
                        newVPCE.warmupTime = vp.warmupTime;
                        newVPCE.targetParams = vp.targetParams;
                        newVPCE.rangedFireRulepack = vp.rangedFireRulepack;
                        newVPCE.ai_IsBuildingDestroyer = vp.ai_IsBuildingDestroyer;
                        newVPCE.ai_AvoidFriendlyFireRadius = vp.ai_AvoidFriendlyFireRadius;
                        newVPCE.onlyManualCast = vp.onlyManualCast;
                        newVPCE.stopBurstWithoutLos = vp.stopBurstWithoutLos;
                        //newVPCE.ForcedMissRadius = vp.ForcedMissRadius; //not used by Verb_ShootCE
                        newVPCE.burstShotCount = vp.burstShotCount * 2;
                        activeProjectile = vp.defaultProjectile;
                        newVPCE.defaultProjectile = vp.defaultProjectile;
                        newVPCE.defaultProjectile.thingClass = typeof(CombatExtended.BulletCE);
                        newVPCE.defaultProjectile.projectile = ConvertPP(newVPCE.defaultProjectile.projectile);
                        FindProjectile(needsVerbs);
                        /*if (vp.verbClass.ToString().Equals("Verse.Verb_ShootOneUse"))
                            {
                                TODO extra logic for other verbs
                            }*/
                        /*if (vp.verbClass.ToString().Equals("Verse.Verb_LaunchProjectile"))
                            {
                                TODO
                            }*/
                        newVerbs.Add(newVPCE);
                    }
                    else
                    {
                        newVerbs.Add(vp);
                    }
                    

                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    //verbCounter++;
                }

            }
            for (int i = 0; i < newVerbs.Count; i++)
            {
                needsVerbs.Verbs[i] = newVerbs[i];
            }
        }

        private ProjectilePropertiesCE ConvertPP(ProjectileProperties ppHolder)
        {
            ProjectilePropertiesCE ppceHolder = new ProjectilePropertiesCE();

            ppceHolder.speed = ppHolder.speed;
            ppceHolder.ai_IsIncendiary = ppHolder.ai_IsIncendiary;
            ppceHolder.explosionEffect = ppHolder.explosionEffect;
            ppceHolder.explosionDamageFalloff = ppHolder.explosionDamageFalloff;
            ppceHolder.explosionChanceToStartFire = ppHolder.explosionChanceToStartFire;
            ppceHolder.applyDamageToExplosionCellsNeighbors = ppHolder.applyDamageToExplosionCellsNeighbors;
            ppceHolder.postExplosionSpawnThingCount = ppHolder.postExplosionSpawnThingCount;
            ppceHolder.postExplosionSpawnChance = ppHolder.postExplosionSpawnChance;
            ppceHolder.postExplosionSpawnThingDef = ppHolder.postExplosionSpawnThingDef;
            ppceHolder.preExplosionSpawnChance = ppHolder.preExplosionSpawnChance;
            ppceHolder.preExplosionSpawnThingDef = ppHolder.preExplosionSpawnThingDef;
            ppceHolder.preExplosionSpawnThingCount = ppHolder.preExplosionSpawnThingCount;
            ppceHolder.explosionDelay = ppHolder.explosionDelay;
            ppceHolder.soundAmbient = ppHolder.soundAmbient;
            ppceHolder.flyOverhead = ppHolder.flyOverhead;
            ppceHolder.explosionRadius = ppHolder.explosionRadius;
            ppceHolder.damageDef = ppHolder.damageDef; // TODO DamageDefOf.Stun seems to not actually injure the target, just stun them. might need to account for that
            ppceHolder.stoppingPower = ppHolder.stoppingPower;
            ppceHolder.alwaysFreeIntercept = ppHolder.alwaysFreeIntercept;
            ppceHolder.shadowSize = ppHolder.shadowSize;
            ppceHolder.soundHitThickRoof = ppHolder.soundHitThickRoof;
            ppceHolder.soundExplode = ppHolder.soundExplode;
            ppceHolder.soundImpactAnticipate = ppHolder.soundImpactAnticipate;
            ppceHolder.arcHeightFactor = ppHolder.arcHeightFactor;
            ppceHolder.armorPenetrationBlunt = 1;
            ppceHolder.armorPenetrationSharp = 1; //TODO maybe change these? only applicable if default projectile is used, which it never is
            SetDamage(ppceHolder, ppHolder.GetDamageAmount(1f, null));
            ppceHolder.secondaryDamage = ExtraToSecondary(ppHolder.extraDamages);
            return ppceHolder;
        }

        private static List<SecondaryDamage> ExtraToSecondary(List<ExtraDamage> ed)
        {
            List<SecondaryDamage> newSDList = new List<SecondaryDamage>();
            if (!(ed == null) && !(ed.Count == 0))
            {
                for (int i = 0; i < ed.Count; i++)
                {
                    SecondaryDamage newSD = new SecondaryDamage();
                    newSD.def = ed[i].def;
                    newSD.amount = (int)(ed[i].amount + 0.5f); //extra 0.5f is because casting to int is a floor round
                    newSD.chance = ed[i].chance;
                    newSDList.Add(newSD);
                }
            }

            return newSDList;
        }

        private void ScrapeGunStats(ThingDef weapon)
        {
            FindProjectile(weapon); //TODO remove this if re-implement ScrapeVerbs
            gunType = DetermineGunType(weapon);
            weapon.description += "\n\n CEAP thinks this gun is a: " + gunType.ToString();
            projectileDamageType = activeProjectile.projectile.damageDef;
            projectileDamage = activeProjectile.projectile.GetDamageAmount(1, pew); //TODO learn Reflection to see if it is applicable here
            projectilePenetration = activeProjectile.projectile.GetArmorPenetration(1, pew);
            int gunMassIndex;
            gunMassIndex = weapon.statBases.FindIndex(i => i.stat == StatDefOf.Mass); //StatDef.Named("Mass"));
            if (gunMassIndex >= 0)
            {
                gunMass = weapon.statBases[gunMassIndex].value;
            }
            /*int gunTickBBSIndex;
            gunTickBBSIndex = weapon.statBases.FindIndex(i => i.stat == StatDef.Named("TicksBetweenBurstShots"));
            if (gunTickBBSIndex >= 0)
            {
                ticksBetweenBurstShots = (int)weapon.statBases[gunTickBBSIndex].value;
            }*/
            accuracyTouch = weapon.statBases[weapon.statBases.FindIndex(i => i.stat == StatDef.Named("AccuracyTouch"))].value;
            accuracyShort = weapon.statBases[weapon.statBases.FindIndex(i => i.stat == StatDef.Named("AccuracyShort"))].value;
            accuracyMedium = weapon.statBases[weapon.statBases.FindIndex(i => i.stat == StatDef.Named("AccuracyMedium"))].value;
            accuracyLong = weapon.statBases[weapon.statBases.FindIndex(i => i.stat == StatDef.Named("AccuracyLong"))].value;
            gunTechLevel = weapon.techLevel;
        }

        private void FindProjectile(ThingDef weapon)
        {
            activeProjectile = weapon.Verbs[0].defaultProjectile;
        }

        private void AddCompsAmmoUser(ThingDef weapon) //TODO WIP 
        {
            CombatExtended.CompProperties_AmmoUser newAUComp = new CombatExtended.CompProperties_AmmoUser();
            newAUComp.magazineSize = weapon.Verbs[0].burstShotCount * 5;
            newAUComp.reloadTime = 4f;//TODO change based on gun type
            newAUComp.reloadOneAtATime = false; //TODO heuristic
            newAUComp.throwMote = true; //TODO wtf is a mote
            newAUComp.ammoSet = GenerateAmmo(weapon); //TODO finish generator method and use that instead
            newAUComp.loadedAmmoBulkFactor = 0;
            newAUComp.compClass = typeof(CombatExtended.CompAmmoUser);
            weapon.comps.Add(newAUComp);
        }

        private void AddCompsFireModes(ThingDef weapon) // TODO WIP 
        {
            //comps.Add(CombatExtended.CompProperties_FireModes)
            CombatExtended.CompProperties_FireModes newFMComp = new CombatExtended.CompProperties_FireModes();
            //newFMComp.compClass = typeof(CombatExtended.CompProperties_FireModes);
            if (weapon.Verbs[0].burstShotCount > 1)
            {
                newFMComp.aimedBurstShotCount = (int)(weapon.Verbs[0].burstShotCount / 2);
            }
            else
            {
                newFMComp.aimedBurstShotCount = 1;
            }
            newFMComp.aiUseBurstMode = true; //sure?
            newFMComp.noSingleShot = false; //TODO figure out what types of CE guns don't have this
            newFMComp.noSnapshot = false; //TODO same as above
            newFMComp.aiAimMode = AimMode.Snapshot; //TODO if statement based on gun type?
            weapon.comps.Add(newFMComp);
            
            //Logger.Message("afterfm " +weapon.defName + " has comps:");
        }

        private void AddMoreCEComps(ThingDef weapon) // TODO there are so many, probably need a method for each eventually
        {

        }

        private void ClearGunStats()
        {
            //clear statbases
            gunMass = 0;
            accuracyTouch = 0;
            accuracyShort = 0;
            accuracyMedium = 0;
            accuracyLong = 0;

            //clear verb properties
            verbLabel = "";
            soundCast = null;
            soundCastTail = null;
            soundAiming = null;
            forcedMissRadius = 0;
            ticksBetweenBurstShots = 15;
            burstShotCountV = 1;
            warmupTime = 0;
            muzzleFlashScale = 0;
            hasStandardCommand = true;
            gunRange = 0;
            activeProjectile = null;
            targetParams = null;
            rangedFireRulepack = null;
            ai_IsBuildingDestroyer = false;
            ai_AvoidFriendlyFireRadius = 0;
            onlyManualCast = false;
            stopBurstWithoutLos = true;

            //clear projectile properties
            projectileDamageType = null;
            projectileDamage = 0;
            projectilePenetration = 0;

            //clear misc properties
            gunTechLevel = TechLevel.Undefined;
            gunType = gunTypes.Other;
        }

        private void PatchStatBases(ThingDef weapon)
        {
            List<StatModifier> newStatBases = new List<StatModifier>();
            foreach (string statMod in Enum.GetNames(typeof(SharedStatBases)))
            {
                int index = weapon.statBases.FindIndex(sm => sm.stat.ToString().EqualsIgnoreCase(statMod));
                if (index < 0)
                {
                    continue;
                }
                else
                {
                    StatModifier newStatMod = new StatModifier();
                    newStatMod.stat = StatDef.Named(statMod);
                    newStatMod.value = weapon.statBases[index].value;
                    newStatBases.Add(newStatMod);
                    continue;
                }
            }

            //TODO: logic to turn vanilla stats into CE stats
            //sights efficiency, float, ex: bows 0.6, small industrial arms 0.7, assault rifle 1.0, sniper rifle 3.5, charge rifle 1.1, charge smg 1.1, positive correlation to accuracy, tech level seems to be a factor, 
            StatModifier sightsEfficiency = new StatModifier();
            sightsEfficiency.stat = StatDef.Named("SightsEfficiency");
            //ShotSpread: float, ex: charge rifle 0.12, FAL 0.06, sniper rifles 0.02-0.04, negatively correlates to accuracy
            StatModifier shotSpread = new StatModifier();
            shotSpread.stat = StatDef.Named("ShotSpread");
            //SwayFactor: float, ex: charge smg 0.75, judge 0.91, ak47 1.23, charge rifle 1.2, seems to positively correlate to mass, lower if "2 handed". need to categorize guns?
            StatModifier swayFactor = new StatModifier();
            swayFactor.stat = StatDef.Named("SwayFactor");
            //Bulk: float, for "1 handed" guns seems to be = mass, for "2 handed" = 2 * mass
            StatModifier gunBulk = new StatModifier();
            gunBulk.stat = StatDef.Named("Bulk");
            //Recoil: float, ex: USAS-12 2.36, charge smg 1.2, flamethrower 0.85, FAL 2.07. seems to negatively correlate to mass. maybe damage / mass * x?
            StatModifier recoil = new StatModifier();
            recoil.stat = StatDef.Named("Recoil");
            //ReloadTime: in seconds
            //StatModifier reloadTime = new StatModifier();
            //reloadTime.stat = StatDef.Named("ReloadTime");


            float gunTechModFlat = (((float)weapon.techLevel - (float)TechLevel.Industrial) * 0.1f);
            float gunTechModPercent = (1 - gunTechModFlat);
            float ssAccuracyMod = (accuracyLong * 0.1f);
            float seDefault = 1f + gunTechModFlat;
            float recoilTechMod = (1 - (((float)weapon.techLevel - 3) * 0.2f));

            switch (gunType)
            {
                case gunTypes.Bow:
                    sightsEfficiency.value = 0.6f;
                    shotSpread.value = 1f;
                    swayFactor.value = 2f;
                    gunBulk.value = 2f * gunMass;
                    //reloadTime.value = 1f;
                    break;
                case gunTypes.Pistol:
                    shotSpread.value = (0.2f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = 0.7f + gunTechModFlat;
                    swayFactor.value = gunMass * 0.9f;
                    gunBulk.value = 1f * gunMass;
                    //reloadTime.value = 4f;
                    break;
                case gunTypes.SMG:
                    shotSpread.value = (0.17f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = 0.7f + gunTechModFlat;
                    swayFactor.value = gunMass * 0.8f;
                    gunBulk.value = 1f * gunMass;
                    recoil.value = (2f - (gunMass * 0.1f)) * recoilTechMod;
                    //reloadTime.value = 4f;
                    break;
                case gunTypes.Shotgun:
                    shotSpread.value = (0.17f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = gunMass * 0.4f;
                    gunBulk.value = 2f * gunMass;
                    //reloadTime.value = 4f;
                    break;
                case gunTypes.Rifle:
                    shotSpread.value = (0.13f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = gunMass * 0.35f;
                    gunBulk.value = 2f * gunMass;
                    recoil.value = (1.8f - (gunMass * 0.1f)) * recoilTechMod;
                    //reloadTime.value = 4f;
                    break;
                case gunTypes.MachineGun:
                    shotSpread.value = (0.13f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = gunMass * 0.17f;
                    gunBulk.value = 1.5f * gunMass;
                    recoil.value = (2.3f - (gunMass * 0.1f)) * recoilTechMod;
                    //reloadTime.value = 8f; //TODO placeholder, actual value will be mag size * 0.04
                    break;
                case gunTypes.Sniper:
                    shotSpread.value = (0.1f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = 2.6f + gunTechModFlat;
                    swayFactor.value = 2f - (gunMass * 0.1f); //unlike other guns, sniper rifles are more steady as they get heavier
                    gunBulk.value = 2f * gunMass;
                    break;
                case gunTypes.GrenadeLauncher:
                    shotSpread.value = 0.122f + (forcedMissRadius * 0.02f);
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = (float)Math.Sqrt(gunMass) * 0.6f;
                    gunBulk.value = 2f * gunMass;
                    break;
                case gunTypes.RocketLauncher:
                    shotSpread.value = 0.2f;
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = 1f + (gunMass * 0.1f);
                    gunBulk.value = 2f * gunMass;
                    break;
                case gunTypes.Turret:
                    shotSpread.value = (0.1f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = 1f; //since they aren't carried, we can't expect any sort of consistency for turret mass, so we can't factor it in
                    gunBulk.value = 2f * gunMass; //would only matter if a mod exists that lets pawns carry turret guns, but doesn't hurt to be ready for that edge case
                    recoil.value = 1f; //again, can't factor in mass for the same reason as sway
                    break;
                case gunTypes.Grenade:
                    sightsEfficiency.value = 0.65f; //grenades don't have shot spread or weapon sway
                    break;
                default:
                    shotSpread.value = shotSpread.value = (0.15f - ssAccuracyMod) * gunTechModPercent; //arbitrarily decided to put it between SMG and rifle
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = gunMass * 0.5f;
                    gunBulk.value = 2f * gunMass;
                    recoil.value = 1f;
                    break;
            }


            //TicksBetweenBurstShots is part of the verb_shoot in xml, but somehow ends up in statbases. rimworld is confusing. int ex: 4 for LMGs, 10 for AR, 12 for CR
            StatModifier ticksBBS = new StatModifier();
            ticksBBS.stat = StatDef.Named("TicksBetweenBurstShots");
            ticksBBS.value = ticksBetweenBurstShots;
            
            //BurstShotCount as above. int, ex: AR 3, LMG 10, pistols 1
            StatModifier burstShotCount = new StatModifier();
            burstShotCount.stat = StatDef.Named("BurstShotCount");
            if (weapon.Verbs[0].burstShotCount == 1)
                burstShotCount.value = 1;
            else
                burstShotCount.value = 2 * weapon.Verbs[0].burstShotCount;

            if (gunType != gunTypes.Grenade)
            {
                newStatBases.Add(shotSpread);
                newStatBases.Add(swayFactor);
            }
            newStatBases.Add(sightsEfficiency);
            newStatBases.Add(gunBulk);
            newStatBases.Add(ticksBBS);
            newStatBases.Add(burstShotCount);
            newStatBases.Add(recoil);
            //newStatBases.Add(reloadTime);

            weapon.statBases = newStatBases;
        }
        private gunTypes DetermineGunType(ThingDef weapon)
        {
            //a turret is tagged as TurretGun, because it inherits that from BaseWeaponTurret
            if (weapon.weaponTags.Contains("TurretGun"))
                return gunTypes.Turret;
            //a bow is a pre-industrial ranged weapon with a burst count of 1. Let's hope there aren't many edge cases
            else if ((weapon.techLevel.CompareTo(TechLevel.Medieval) <= 0) && (burstShotCountV == 1))
                return gunTypes.Bow;
            //a grenade uses a different verb from most weapons
            else if (weapon.Verbs[0].verbClass.ToString().EqualsIgnoreCase("Verse.Verb_ShootLaunchProjectile"))
                return gunTypes.Grenade;
            //grenade launchers have a forced miss radius but are reusable
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (weapon.Verbs[0].ForcedMissRadius != 0) && (weapon.Verbs[0].verbClass.ToString().EqualsIgnoreCase("Verse.Verb_Shoot")))
                return gunTypes.GrenadeLauncher;
            //rocket launchers have a forced miss radius and aren't reusable i.e. their verbClass is Verse.Verb_ShootOneUse
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (weapon.Verbs[0].ForcedMissRadius != 0) && (weapon.Verbs[0].verbClass.ToString().EqualsIgnoreCase("Verse.Verb_ShootOneUse")))
                return gunTypes.RocketLauncher;
            //a shotgun is an industrial or higher weapon and has one of the following: shotgun in its defname, label, or description, OR shotgun or gauge in its projectile
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && ((weapon.defName.IndexOf("shotgun", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                                                                                || (weapon.label.IndexOf("shotgun", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                                                                                || (weapon.description.IndexOf("shotgun", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                                                                                || (weapon.Verbs[0].defaultProjectile.ToString().IndexOf("shotgun", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                                                                                || (weapon.Verbs[0].defaultProjectile.ToString().IndexOf("gauge", 0, StringComparison.CurrentCultureIgnoreCase) != -1)))
                return gunTypes.Shotgun;
            //a pistol is an industrial or higher weapon with burst count 1 and either a range < 26 OR mass < 2
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (weapon.Verbs[0].burstShotCount == 1) && ((gunRange < 26) || (gunMass < 2)))
                return gunTypes.Pistol;
            // a sniper is an industrial or higher weapon with burst count 1 and either a range >= 26 OR mass 
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (weapon.Verbs[0].burstShotCount == 1) && ((gunRange >= 26) || (gunMass > 2)))
                return gunTypes.Sniper;
            //an SMG is an industrial or higher weapon with burst count > 1 and a range < 23
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (weapon.Verbs[0].burstShotCount > 1) && (weapon.Verbs[0].range < 23))
                return gunTypes.SMG;
            //a rifle is an industrial or higher weapon with burst count > 1 but <= 6 and a range >= 23
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (weapon.Verbs[0].burstShotCount > 1) && (weapon.Verbs[0].burstShotCount <= 6) && (weapon.Verbs[0].range >= 23))
                return gunTypes.Rifle;
            //a machine gun is an industrial or higher weapon with burst count > 1 and either burst count >= 6 OR mass > 6?
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (weapon.Verbs[0].burstShotCount > 1) && ((weapon.Verbs[0].burstShotCount > 6) || (gunMass > 6)))
                return gunTypes.MachineGun;
            else
                return gunTypes.Other;
        }

        private void PatchApparel(List<ThingDef> apparels)
        {
            BeginPatch("APPAREL"); 
                    
            foreach (ThingDef apparel in apparels)
            {
                float newBulk = 0f;
                float newWornBulk = 0f;
                float techMult = 1f;
                try
                {
                    defsTotal++;
                    if (apparel.statBases.FindIndex(wob => wob.ToString().Contains("Bulk")) == -1) //unpatched apparel (or poorly made patches) will have no Bulk element in its statBases list
                    {
                        switch (apparel.techLevel)
                        {
                            case TechLevel.Animal:
                                techMult = armorTechMults[0];
                                break;
                            case TechLevel.Neolithic:
                                techMult = armorTechMults[1];
                                break;
                            case TechLevel.Medieval:
                                techMult = armorTechMults[2];
                                break;
                            case TechLevel.Industrial:
                                techMult = armorTechMults[3];
                                break;
                            case TechLevel.Spacer:
                                techMult = armorTechMults[4];
                                break;
                            case TechLevel.Ultra:
                                techMult = armorTechMults[5];
                                break;
                            case TechLevel.Archotech:
                                techMult = armorTechMults[6];
                                break;
                            default:
                                techMult = 1f;
                                break;
                        }

                        //bool isSkin = false;
                        bool isMid = false;
                        bool isShell = false;

                        foreach (ApparelLayerDef ald in apparel.apparel.layers)
                        {
                            int massIndex = apparel.statBases.FindIndex(wob => wob.ToString().Contains("Mass"));
                            float mass = 0;
                            if (massIndex >= 0)
                            {
                                mass = apparel.statBases[massIndex].value;
                            }
                            if (ald == ApparelLayerDefOf.OnSkin || ald.ToString().ToUpper().Contains("SKIN") || ald.ToString().ToUpper().Contains("STRAPPED"))
                            {
                                //isSkin = true;
                            }
                            if (ald == ApparelLayerDefOf.Middle || ald.ToString().ToUpper().Contains("MID"))
                            {
                                isMid = true;
                                if (mass > 2)
                                {
                                    newBulk += midBulkAdd;
                                    newWornBulk += midWulkAdd;
                                }
                            }
                            if (ald == ApparelLayerDefOf.Shell || ald.ToString().ToUpper().Contains("SHELL") || ald.ToString().ToUpper().Contains("OUTER")) //had to add extra conditions to try to account for modded alien layers
                            {
                                isShell = true;
                                if (mass > 2)
                                {
                                    if (newWornBulk == 0)
                                    {
                                        newBulk += shellBulkAdd;
                                        newWornBulk += shellWulkAdd;
                                    }
                                    else
                                    {
                                        newBulk *= shellBulkMult;
                                        newWornBulk *= shellWulkMult;
                                    }
                                }
                            }
                        }

                        int sharpIndex = apparel.statBases.FindIndex(ars => ars.ToString().Contains("ArmorRating_Sharp"));
                        int bluntIndex = apparel.statBases.FindIndex(ars => ars.ToString().Contains("ArmorRating_Blunt"));

                        //using ifs avoids apparel with no armor values?
                        if (sharpIndex >= 0)
                        {
                            apparel.statBases[sharpIndex].value *= sharpMult * techMult;
                        }
                        if (bluntIndex >= 0)
                        {
                            apparel.statBases[bluntIndex].value *= bluntMult * techMult;
                        }

                        StatModifier statModBulk = new StatModifier();
                        statModBulk.stat = StatDef.Named("Bulk");
                        statModBulk.value = newBulk;

                        StatModifier statModWornBulk = new StatModifier();
                        statModWornBulk.stat = StatDef.Named("WornBulk");
                        statModWornBulk.value = newWornBulk;

                        apparel.statBases.Add(statModWornBulk);
                        apparel.statBases.Add(statModBulk);

                        StatModifier statModSmoke = new StatModifier();
                        statModSmoke.stat = StatDef.Named("SmokeSensitivity");
                        statModSmoke.value = -1;

                        if (apparel.apparel.bodyPartGroups != null)
                        {
                            if (apparel.apparel.bodyPartGroups.Any(bpgd =>
                                                                        {
                                                                            if (bpgd.label == null)
                                                                                return false;
                                                                            else if (bpgd.label.ToUpper().Contains("EYES") || bpgd.label.ToUpper().Contains("FULL"))
                                                                                return true;
                                                                            else
                                                                                return false;
                                                                        }))
                            {
                                if (apparel.equippedStatOffsets == null)
                                {
                                    apparel.equippedStatOffsets = new List<StatModifier>();
                                }
                                if (techMult >= 1)
                                {
                                    apparel.equippedStatOffsets.Add(statModSmoke);
                                }
                            }

                            if (techMult > 1 && isMid && isShell) //TODO: make this check to see if it is non-headgear (bodyPartGroups.label ??? maybe Torso Neck Shoulders Arms Legs, or some subset thereof)
                            {
                                //TODO:add weapon handling, bulk capacity, carry weight on high-tech armor
                            }
                        }

                        defsPatched++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                    failureList.AppendLine(apparel.defName);
                    defsFailed++;
                    continue;
                }
            }
            EndPatch("APPAREL");
        }

        private void PatchAliens(List<ThingDef> aliens)
        {
            BeginPatch("ALIENS");
            foreach (ThingDef alien in aliens)
            {
                //TODO check if CE compatible - use melee verbs as reference?
                //fix melee verbs
                //fix built-in ranged attacks
                //fix natural armor values
                //something to do with organs?
                //don't forget to make sure they carry the right ammo
                try
                {
                    defsTotal++;
                    int tempIndexSharp = alien.statBases.FindIndex(ars => ars.ToString().Contains("ArmorRating_Sharp"));
                    int tempIndexBlunt = alien.statBases.FindIndex(ars => ars.ToString().Contains("ArmorRating_Blunt"));

                    if (tempIndexSharp >= 0)
                    {
                        alien.statBases[tempIndexSharp].value *= sharpMult;
                    }    
                    if (tempIndexBlunt >= 0)
                    {
                        alien.statBases[tempIndexBlunt].value *= bluntMult;
                    }
                    List<Tool> newTools = new List<Tool>();
                    foreach (Tool tool in alien.tools)
                    {
                        ToolCE newToolCE = PatchTool(tool);

                        newTools.Add(newToolCE);
                    }
                    alien.tools = newTools;

                    int tempIndex = alien.Verbs.FindIndex(vs => vs.verbClass.ToString().Contains("Verb_Shoot"));
                    if (tempIndex >=0)
                    {
                        VerbPropertiesCE newVerb = new VerbPropertiesCE();
                    }

                    PatchVerb(alien);
                    defsPatched++;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                    failureList.AppendLine(alien.defName);
                    defsFailed++;
                    continue;
                }
                finally
                {
                }
            }
            EndPatch("ALIENS");
        }

        private ToolCE PatchTool(Tool tool)
        {
            ToolCE newToolCE = new ToolCE();
            newToolCE.label = tool.label;
            newToolCE.capacities = tool.capacities;
            newToolCE.power = tool.power * 2f; //x2 seems to be the general rule
            newToolCE.cooldownTime = tool.cooldownTime; //seems to sometimes be higher, sometimes lower. keeping same is safest
            newToolCE.linkedBodyPartsGroup = tool.linkedBodyPartsGroup;
            if (tool.armorPenetration >= 0)
            {
                newToolCE.armorPenetrationSharp = tool.armorPenetration * sharpMult;
                newToolCE.armorPenetrationBlunt = tool.armorPenetration * bluntMult;
            }

            return newToolCE;
        }

        private void PatchTurrets(List<ThingDef> turrets)
        {
            //magic numbers
            float defaultBulk = 25f;
            float defaultShootAccuracy = 1f;
            float defaultAimAccuracy = 1f;
            float defaultSightsAccuracy = 0.5f;

            BeginPatch("TURRETS");
            foreach (ThingDef td in turrets)
            {
                defsTotal++;
                try
                {
                    //setting fill percent to 0.85 gives cover height of 1.49m, enough to shoot over most cover and be shot over
                    if (td.fillPercent < 0.85)
                    {
                        td.fillPercent = 0.85f;
                    }


                    //remove refuelability, if it exists
                    td.comps.RemoveAll(x => x.GetType() == typeof(CompProperties_Refuelable));

                    //change class
                    td.thingClass = typeof(Building_TurretGunCE);

                    if (!td.comps.Any(x => x.GetType() == typeof(CompProperties_Mannable)))
                    {
                        StatUtility.SetStatValueInList(ref td.statBases, StatDefOf.ShootingAccuracyTurret, defaultShootAccuracy);
                        StatUtility.SetStatValueInList(ref td.statBases, CE_StatDefOf.AimingAccuracy, defaultAimAccuracy);
                    }


                    //artillery needs sights efficiency, gun turrets use the stat of the turret weapon
                    if (td.building.buildingTags.Any(x => x.Equals("Artillery")))
                    {
                        StatUtility.SetStatValueInList(ref td.statBases, CE_StatDefOf.SightsEfficiency, defaultSightsAccuracy);
                    }

                    //will only need a bulk value if it can be minified
                    if (td.Minifiable)
                    {
                        StatUtility.SetStatValueInList(ref td.statBases, CE_StatDefOf.Bulk, defaultBulk);
                    }


                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                    defsFailed++;
                }
            }
            EndPatch("TURRETS");

        }


        public AmmoSetDef GenerateAmmo(ThingDef weapon) //TODO WIP
        {
            AmmoSetDef newAmmoSet = new AmmoSetDef();
            List<AmmoLink> newAmmoLinks = new List<AmmoLink>();
            List<AmmoDef> newAmmos = new List<AmmoDef>();
            List<ThingDef> newProjectiles = new List<ThingDef>();

            newAmmoSet.defName = ("CEAP_Ammo_For_" + weapon.defName);
            newAmmoSet.label = ("Ammo set for " + weapon.label);
            newAmmoSet.description = ("A procedurally-generated ammo set for the " + weapon.label);

            ThingCategoryDef newAmmoCat = new ThingCategoryDef();
            newAmmoCat.defName = weapon.defName + "_AmmoCat";
            newAmmoCat.label = weapon.label + " Ammo";
            newAmmoCat.childThingDefs = new List<ThingDef>();
            newAmmoCat.childCategories = new List<ThingCategoryDef>(); //TODO not sure yet if this is better to leave null or initialize empty
            newAmmoCat.childSpecialFilters = new List<SpecialThingFilterDef>(); //TODO not sure yet if this is better to leave null or initialize empty
            newAmmoCat.parent = ThingCategoryDef.Named("Ammo");
            newAmmoCat.iconPath = "UI/Icons/ThingCategories/Ammo";
            newAmmoCat.resourceReadoutRoot = false;

            switch (gunType)
            {
                /*case gunTypes.Bow:
                    {
                        newAmmoSet.isMortarAmmoSet = false;
                        break;
                    }
                case gunTypes.Shotgun:
                    {
                        newAmmoSet.isMortarAmmoSet = false;
                        break;
                    }
                case gunTypes.GrenadeLauncher:
                    {
                        newAmmoSet.isMortarAmmoSet = false;
                        break;
                    }
                case gunTypes.RocketLauncher:
                    {
                        newAmmoSet.isMortarAmmoSet = false;
                        break;
                    }*/
                default:
                    {
                        newAmmoSet.isMortarAmmoSet = false;
                        if (false)//((weapon.techLevel - TechLevel.Spacer) >= 0) //TODO re-implement after testing
                        {
                            //newAmmoCat.parent = ThingCategoryDef.Named("AmmoAdvanced");
                            //charge ammo
                        }
                        else
                        {
                            for (int i = 0; i < 6; i++)
                            {//make the projectiles
                                ThingDef newProjectile = new ThingDef();
                                newProjectile.graphicData = new GraphicData();
                                ProjectilePropertiesCE newPPCE = new ProjectilePropertiesCE();
                                int damageHolder = projectileDamage;
                                SetBaseBullet(newProjectile);
                                newProjectile.graphicData.texPath = "Things/Projectile/Bullet_Small";
                                newProjectile.graphicData.graphicClass = typeof(Graphic_Single);
                                newPPCE.damageDef = DamageDefOf.Bullet;
                                newPPCE.speed = 185;
                                newPPCE.secondaryDamage = new List<SecondaryDamage>();
                                newPPCE.dropsCasings = true;
                                newPPCE.explosionDamageFalloff = true;
                                newPPCE.armorPenetrationSharp = projectilePenetration * sharpMult * 2; //TODO better formula
                                newPPCE.armorPenetrationBlunt = projectilePenetration * bluntMult * 2; //TODO 
                                newPPCE.flyOverhead = false;
                                //SecondaryDamage newSDBase = new SecondaryDamage();
                                //newSDBase.def = projectileDamageType ?? DamageDefOf.Bullet;
                                //newSDBase.amount = (int)(projectileDamage + 0.5f); //since casting to int always rounds down, the extra 0.5 will make any damage x.5 essentially round up instead 
                                //newSDBase.chance = 1;

                                switch (i)
                                {
                                    case 0:
                                        {//FMJ
                                            newProjectile.defName = ("CEAP_FMJ_Bullet_" + weapon.defName);
                                            newProjectile.label = (weapon.label + " FMJ bullet");
                                            break;
                                        }
                                    case 1:
                                        {//AP
                                            newProjectile.defName = ("CEAP_AP_Bullet_" + weapon.defName);
                                            newProjectile.label = (weapon.label + " AP bullet");
                                            damageHolder = (int)(damageHolder * 0.66f + 0.5f);
                                            //newSDBase.amount = (int)(newSDBase.amount * 0.66f + 0.5f);
                                            newPPCE.armorPenetrationSharp *= 2;
                                            break;
                                        }
                                    case 2:
                                        {//HP
                                            newProjectile.defName = ("CEAP_HP_Bullet_" + weapon.defName);
                                            newProjectile.label = (weapon.label + " HP bullet");
                                            damageHolder = (int)(damageHolder * 1.33f + 0.5f);
                                            //newSDBase.amount = (int)(newSDBase.amount * 1.33f + 0.5f);
                                            newPPCE.armorPenetrationSharp *= 0.5f;
                                            break;
                                        }
                                    case 3:
                                        {//API
                                            newProjectile.defName = ("CEAP_API_Bullet_" + weapon.defName);
                                            newProjectile.label = (weapon.label + " AP-I bullet");
                                            damageHolder = (int)(damageHolder * 0.66f + 0.5f);
                                            //newSDBase.amount = (int)(newSDBase.amount * 0.66f + 0.5f);
                                            SecondaryDamage newSD2 = new SecondaryDamage();
                                            newSD2.def = DamageDefOf.Burn;
                                            newSD2.amount = (int)(projectileDamage * 0.33f + 0.5f);
                                            newSD2.chance = 1;
                                            newPPCE.secondaryDamage.Add(newSD2);
                                            newPPCE.armorPenetrationSharp *= 2;
                                            break;
                                        }
                                    case 4:
                                        {//HE
                                            newProjectile.defName = ("CEAP_HE_Bullet_" + weapon.defName);
                                            newProjectile.label = (weapon.label + " HE bullet");
                                            SecondaryDamage newSD2 = new SecondaryDamage();
                                            newSD2.def = DamageDefOf.Bomb;
                                            newSD2.amount = (int)(projectileDamage * 0.66f + 0.5f);
                                            newSD2.chance = 1;
                                            newPPCE.secondaryDamage.Add(newSD2);
                                            break;
                                        }
                                    case 5:
                                        {//Sabot
                                            newProjectile.defName = ("CEAP_Sabot_Bullet_" + weapon.defName);
                                            newProjectile.label = (weapon.label + " Sabot bullet");
                                            damageHolder = (int)(damageHolder * 0.5f + 0.5f);
                                            //newSDBase.amount = (int)(newSDBase.amount * 0.5f + 0.5f);
                                            newPPCE.armorPenetrationSharp *= 3.5f;
                                            newPPCE.armorPenetrationBlunt *= 1.3f;
                                            newPPCE.speed *= 1.5f;
                                            break;
                                        }
                                    default:
                                        {
                                            break;
                                        }

                                }

                                SetDamage(newPPCE, damageHolder);

                                //newSDBase.amount += 1; //to account for the base damage being -1
                                //newPPCE.secondaryDamage.Add(newSDBase);
                                //TODO block to convert ExtraDamages to SecondaryDamages and add them
                                newProjectile.projectile = newPPCE;
                                InjectedDefHasher.GiveShortHashToDef(newProjectile, typeof(ThingDef)); //TODO understand this better
                                newProjectiles.Add(newProjectile);
                                DefGenerator.AddImpliedDef<ThingDef>(newProjectile);
                                //Logger.Message(weapon.defName + " " + newProjectile.defName);
                            }

                            /*
                            for (int i = 0; i < 6; i++)
                            {//make the ammos
                                AmmoDef newAmmo;
                                StatModifier mv;
                                StatModifier ms;
                                StatModifier bk;
                                InitializeNewAmmo(newProjectiles, i, out newAmmo, out mv, out ms, out bk);

                                RecipeDef newRecipe = MakeRecipeBase(newAmmo);
                                newRecipe.workAmount = 2800;
                                switch (i)
                                {
                                    case 0:
                                        {//FMJ
                                            newAmmo.defName = ("CEAP_FMJ_Ammo_" + weapon.defName);
                                            newAmmo.label = ("FMJ Ammo for the " + weapon.label);
                                            newAmmo.description = ("Full metal jacket ammo for the " + weapon.label + ". Balanced on damage and penetration.");
                                            break;
                                        }
                                    case 1:
                                        {//AP
                                            newAmmo.defName = ("CEAP_AP_Ammo_" + weapon.defName);
                                            newAmmo.label = ("AP Ammo for the " + weapon.label);
                                            newAmmo.description = ("Armor piercing ammo for the " + weapon.label + ". Reduced damage, but increased sharp penetration.");
                                            break;
                                        }
                                    case 2:
                                        {//HP
                                            newAmmo.defName = ("CEAP_HP_Ammo_" + weapon.defName);
                                            newAmmo.label = ("HP Ammo for the " + weapon.label);
                                            newAmmo.description = ("Hollow point ammo for the " + weapon.label + ". Increased damage, but reduced sharp penetration.");
                                            break;
                                        }
                                    case 3:
                                        {//API
                                            newAmmo.defName = ("CEAP_API_Ammo_" + weapon.defName);
                                            newAmmo.label = ("AP-I Ammo for the " + weapon.label);
                                            newAmmo.description = ("Incendiary ammo for the " + weapon.label + ". Decreased base damage, but adds burn.");
                                            mv.value *= 2;
                                            newRecipe.workAmount *= 2;
                                            newRecipe.researchPrerequisite = ResearchProjectDef.Named("CE_AdvancedAmmo");
                                            break;
                                        }
                                    case 4:
                                        {//HE
                                            newAmmo.defName = ("CEAP_HE_Ammo_" + weapon.defName);
                                            newAmmo.label = ("HE Ammo for the " + weapon.label);
                                            newAmmo.description = ("High explosive ammo for the " + weapon.label + ". Deals bonus bomb damage.");
                                            mv.value *= 2;
                                            newRecipe.workAmount *= 2;
                                            newRecipe.researchPrerequisite = ResearchProjectDef.Named("CE_AdvancedAmmo");
                                            break;
                                        }
                                    case 5:
                                        {//Sabot
                                            newAmmo.defName = ("CEAP_Sabot_Ammo_" + weapon.defName);
                                            newAmmo.label = ("Sabot Ammo for the " + weapon.label);
                                            newAmmo.description = ("Sabot ammo for the " + weapon.label + ". Reduced damage, but greatly increased sharp penetration.");
                                            mv.value *= 2;
                                            newRecipe.workAmount *= 2;
                                            newRecipe.researchPrerequisite = ResearchProjectDef.Named("CE_AdvancedAmmo");
                                            break;
                                        }
                                    default:
                                        {
                                            break;
                                        }
                                }



                                newAmmo.thingCategories = new List<ThingCategoryDef>();
                                newAmmo.thingCategories.Add(newAmmoCat);
                                InjectedDefHasher.GiveShortHashToDef(newAmmo, typeof(AmmoDef));
                                newAmmos.Add(newAmmo);
                                DefGenerator.AddImpliedDef<AmmoDef>(newAmmo);
                                //Logger.Message(weapon.defName + " " + newAmmo.defName);
                            

                            //finish the recipe
                            newRecipe.defName = "Make" + newAmmo.defName;
                                newRecipe.label = "make " + newAmmo.label + " cartridge x500";
                                newRecipe.description = "Craft 500 " + newAmmo.label + " cartridges.";
                                newRecipe.jobString = "Making " + newAmmo.label + " cartridges.";
                                newRecipe.ingredients = ammoRecipeRefs[0][i].ingredients;
                                newRecipe.fixedIngredientFilter = ammoRecipeRefs[0][i].fixedIngredientFilter;
                                newRecipe.defaultIngredientFilter = ammoRecipeRefs[0][i].defaultIngredientFilter;
                                newRecipe.products = new List<ThingDefCountClass>();
                                newRecipe.products.Add(new ThingDefCountClass(newAmmo, 500));
                                InjectedDefHasher.GiveShortHashToDef(newRecipe, typeof(RecipeDef));
                                DefGenerator.AddImpliedDef<RecipeDef>(newRecipe);
                            }*/
                        }
                        break;
                    }

            }

            //for (int i = 0; i < newAmmos.Count; i++)
            for (int i = 0; i < genericAmmos[0].Length; i++)
            {
                //newAmmos[i].projectile = newProjectiles[i].projectile;
                //AmmoLink al = new AmmoLink(newAmmos[i],newProjectiles[i]);
                AmmoLink al = new AmmoLink(genericAmmos[0][i], newProjectiles[i]);
                newAmmoLinks.Add(al);
                //newAmmoCat.childThingDefs.Add(newAmmos[i]);
                newAmmoCat.childThingDefs.Add(genericAmmos[0][i]);
            }

            newAmmoCat.treeNode = new TreeNode_ThingCategory(newAmmoCat);
            InjectedDefHasher.GiveShortHashToDef(newAmmoCat, typeof(ThingCategoryDef));
            DefGenerator.AddImpliedDef<ThingCategoryDef>(newAmmoCat);

            newAmmoSet.ammoTypes = newAmmoLinks;
            InjectedDefHasher.GiveShortHashToDef(newAmmoSet, typeof(Def));
            DefGenerator.AddImpliedDef<AmmoSetDef>(newAmmoSet);
            //Logger.Message(weapon.defName + " " + newAmmoSet.defName);
            return newAmmoSet;
        }

        private static void SetDamage(ProjectilePropertiesCE newPPCE, int damage)
        {
            //experimental reflection attempt
            Type tpp = typeof(ProjectileProperties);
            FieldInfo dab = tpp.GetField("damageAmountBase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            dab.SetValue(newPPCE, (int)damage);
        }

        private void InitializeNewAmmo(List<ThingDef> newProjectiles, int i, out AmmoDef newAmmo, out StatModifier mv, out StatModifier ms, out StatModifier bk)
        {
            newAmmo = new AmmoDef();
            newAmmo.category = ThingCategory.Item;
            newAmmo.resourceReadoutPriority = ResourceCountPriority.Middle;
            newAmmo.useHitPoints = true;
            List<StatModifier> newSM = new List<StatModifier>();
            StatModifier mhp = new StatModifier();
            mhp.stat = StatDef.Named("MaxHitPoints");
            mhp.value = 100;
            newSM.Add(mhp);
            StatModifier beauty = new StatModifier();
            beauty.stat = StatDef.Named("Beauty");
            beauty.value = -5;
            newSM.Add(beauty);
            StatModifier flammability = new StatModifier();
            flammability.stat = StatDef.Named("Flammability");
            flammability.value = 1;
            newSM.Add(flammability);
            StatModifier deteriorationRate = new StatModifier();
            deteriorationRate.stat = StatDef.Named("DeteriorationRate");
            deteriorationRate.value = 2;
            newSM.Add(deteriorationRate);
            ms = new StatModifier();
            ms.stat = StatDef.Named("Mass");
            ms.value = 0.013f;
            newSM.Add(ms);
            bk = new StatModifier();
            bk.stat = StatDef.Named("Bulk");
            bk.value = 0.02f;
            newSM.Add(bk);
            mv = new StatModifier();
            mv.stat = StatDef.Named("MarketValue");
            mv.value = 0.06f;
            newSM.Add(mv);
            newAmmo.statBases = newSM;
            newAmmo.selectable = true;
            newAmmo.altitudeLayer = AltitudeLayer.Item;
            newAmmo.stackLimit = 5000;
            newAmmo.soundInteract = ammoRefs[0][i].soundInteract;
            newAmmo.soundDrop = ammoRefs[0][i].soundDrop;
            newAmmo.soundPickup = ammoRefs[0][i].soundPickup;
            newAmmo.comps = new List<CompProperties>(); ;
            newAmmo.comps.Add(new CompProperties_Forbiddable());
            newAmmo.alwaysHaulable = true;
            newAmmo.drawGUIOverlay = true;
            newAmmo.rotatable = false;
            newAmmo.pathCost = 15;
            newAmmo.tradeTags = new List<string>();
            newAmmo.tradeTags.Add("CE_Ammo");
            newAmmo.tradeability = Tradeability.All;
            newAmmo.tickerType = TickerType.Normal;
            newAmmo.cookOffSpeed = 0.2f;
            newAmmo.cookOffFlashScale = 10;
            newAmmo.cookOffSound = ammoRefs[0][i].cookOffSound;
            newAmmo.cookOffTailSound = ammoRefs[0][i].cookOffTailSound;
            newAmmo.techLevel = TechLevel.Industrial;
            newAmmo.thingCategories = ammoRefs[0][i].thingCategories; // TODO not sure if I need to generate a new unique category for each ammo. Maybe?
                                                                      //TODO if (CE ammo  system is enabled)
            newAmmo.menuHidden = false; //Hides from spawning in debug menu; CE AmmoInjector.cs toggles this based on mod settings
            newAmmo.destroyOnDrop = false; //deletes if dropped on ground; CE AmmoInjector.cs toggles this based on mod settings 
            //newAmmo.tradeTags.Add("CE_AutoEnableTrade"); //sets tradeability
            //newAmmo.tradeTags.Add("CE_AutoEnableCrafting"); //injects recipes
                                                            //TODO end of lines that need to be changed if ammo system is disabled
            newAmmo.graphicData = new GraphicData();
            newAmmo.graphicData.texPath = ammoRefs[0][i].graphicData.texPath;
            newAmmo.graphicData.graphicClass = ammoRefs[0][i].graphicData.graphicClass;
            newAmmo.ammoClass = ammoRefs[0][i].ammoClass;
            newAmmo.cookOffProjectile = newProjectiles[i];
            newAmmo.detonateProjectile = newProjectiles[i];
            newAmmo.thingClass = typeof(CombatExtended.AmmoThing);
            newAmmo.tradeTags = new List<string>();
            newAmmo.tradeTags.Add("CE_AutoEnableTrade");
            newAmmo.tradeTags.Add("CE_AutoEnableCrafting");
        }

        private void MakeGenericAmmos()
        {//doing it in code is harder than just doing xml, but it keeps them safe from CE's ammo injector disabling/hiding them
            MakeGunIndustrialAmmos();
        }

        private ThingCategoryDef MakeAmmoCategory()
        {
            ThingCategoryDef newAmmoCat = new ThingCategoryDef();
            newAmmoCat.childThingDefs = new List<ThingDef>();
            newAmmoCat.childCategories = new List<ThingCategoryDef>(); //TODO not sure yet if this is better to leave null or initialize empty
            newAmmoCat.childSpecialFilters = new List<SpecialThingFilterDef>(); //TODO not sure yet if this is better to leave null or initialize empty
            newAmmoCat.parent = ThingCategoryDef.Named("Ammo");
            newAmmoCat.iconPath = "UI/Icons/ThingCategories/Ammo";
            newAmmoCat.resourceReadoutRoot = false;

            return newAmmoCat;
        }

        private void MakeGunIndustrialAmmos()
        {
            ThingCategoryDef industrialAmmoCat = MakeAmmoCategory();
            industrialAmmoCat.defName = "CEAP_Generic_Industrial_AmmoCat";
            industrialAmmoCat.label = "Generic Industrial Ammos";

            for (int i = 0; i < ammoRefs[0].Length; i++)
            {
                AmmoDef newAmmo = new AmmoDef();
                newAmmo.category = ThingCategory.Item;
                newAmmo.resourceReadoutPriority = ResourceCountPriority.Middle;
                newAmmo.useHitPoints = true;
                newAmmo.statBases = new List<StatModifier>();
                StatUtility.SetStatValueInList(ref newAmmo.statBases, StatDefOf.MaxHitPoints, 100);
                StatUtility.SetStatValueInList(ref newAmmo.statBases, StatDefOf.Beauty, -5);
                StatUtility.SetStatValueInList(ref newAmmo.statBases, StatDefOf.Flammability, 1);
                StatUtility.SetStatValueInList(ref newAmmo.statBases, StatDefOf.DeteriorationRate, 2);
                StatUtility.SetStatValueInList(ref newAmmo.statBases, StatDefOf.Mass, 0.013f);
                StatUtility.SetStatValueInList(ref newAmmo.statBases, CE_StatDefOf.Bulk, 0.02f);
                float mv = 0.06f;
                if (i < 3)
                {
                    StatUtility.SetStatValueInList(ref newAmmo.statBases, StatDefOf.MarketValue, mv);
                }
                else
                {
                    StatUtility.SetStatValueInList(ref newAmmo.statBases, StatDefOf.MarketValue, mv*2);
                }
                newAmmo.selectable = true;
                newAmmo.altitudeLayer = AltitudeLayer.Item;
                newAmmo.stackLimit = 5000;
                newAmmo.soundInteract = ammoRefs[0][i].soundInteract;
                newAmmo.soundDrop = ammoRefs[0][i].soundDrop;
                newAmmo.soundPickup = ammoRefs[0][i].soundPickup;
                newAmmo.comps = new List<CompProperties>(); ;
                newAmmo.comps.Add(new CompProperties_Forbiddable());
                newAmmo.alwaysHaulable = true;
                newAmmo.drawGUIOverlay = true;
                newAmmo.rotatable = false;
                newAmmo.pathCost = 15;
                newAmmo.tradeTags = new List<string>();
                newAmmo.tradeTags.Add("CE_Ammo");
                newAmmo.tradeability = Tradeability.All;
                newAmmo.tickerType = TickerType.Normal;
                newAmmo.cookOffSpeed = 0.2f;
                newAmmo.cookOffFlashScale = 10;
                newAmmo.cookOffSound = ammoRefs[0][i].cookOffSound;
                newAmmo.cookOffTailSound = ammoRefs[0][i].cookOffTailSound;
                newAmmo.techLevel = TechLevel.Industrial;
                newAmmo.thingCategories = ammoRefs[0][i].thingCategories; // TODO not sure if I need to generate a new unique category for each ammo. Maybe?
                                                                          //TODO if (CE ammo  system is enabled)
                newAmmo.menuHidden = false; //Hides from spawning in debug menu; CE AmmoInjector.cs toggles this based on mod settings
                newAmmo.destroyOnDrop = false; //deletes if dropped on ground; CE AmmoInjector.cs toggles this based on mod settings 
                                               //newAmmo.tradeTags.Add("CE_AutoEnableTrade"); //sets tradeability
                                               //newAmmo.tradeTags.Add("CE_AutoEnableCrafting"); //injects recipes
                                               //TODO end of lines that need to be changed if ammo system is disabled
                newAmmo.graphicData = new GraphicData();
                newAmmo.graphicData.texPath = ammoRefs[0][i].graphicData.texPath;
                newAmmo.graphicData.graphicClass = ammoRefs[0][i].graphicData.graphicClass;
                newAmmo.ammoClass = ammoRefs[0][i].ammoClass;
                //newAmmo.cookOffProjectile = newProjectiles[i]; //TODO maybe use 5.56?
                //newAmmo.detonateProjectile = newProjectiles[i];
                newAmmo.thingClass = typeof(CombatExtended.AmmoThing);
                newAmmo.tradeTags = new List<string>();
                newAmmo.tradeTags.Add("CE_AutoEnableTrade");
                newAmmo.tradeTags.Add("CE_AutoEnableCrafting");

                RecipeDef newRecipe = MakeRecipeBase(newAmmo);
                newRecipe.workAmount = 2800;

                switch (i)
                {
                    case 0:
                        {//FMJ
                            newAmmo.defName = ("CEAP_Generic_FMJ");
                            newAmmo.label = ("Generic FMJ Ammo");
                            newAmmo.description = ("Generic full metal jacket ammo. Balanced on damage and penetration.");
                            break;
                        }
                    case 1:
                        {//AP
                            newAmmo.defName = ("CEAP_Generic_AP");
                            newAmmo.label = ("Generic AP Ammo");
                            newAmmo.description = ("Generic armor piercing ammo. Reduced damage, but increased sharp penetration.");
                            break;
                        }
                    case 2:
                        {//HP
                            newAmmo.defName = ("CEAP_Generic_HP");
                            newAmmo.label = ("Generic HP Ammo");
                            newAmmo.description = ("Generic hollow point ammo. Increased damage, but reduced sharp penetration.");
                            break;
                        }
                    case 3:
                        {//API
                            newAmmo.defName = ("CEAP_Generic_API");
                            newAmmo.label = ("Generic AP-I Ammo");
                            newAmmo.description = ("Generic incendiary ammo. Decreased base damage, but adds burn.");
                            newRecipe.workAmount *= 2;
                            newRecipe.researchPrerequisite = ResearchProjectDef.Named("CE_AdvancedAmmo");
                            break;
                        }
                    case 4:
                        {//HE
                            newAmmo.defName = ("CEAP_Generic_HE");
                            newAmmo.label = ("Generic HE Ammo");
                            newAmmo.description = ("Generic high explosive ammo. Deals bonus bomb damage.");
                            newRecipe.workAmount *= 2;
                            newRecipe.researchPrerequisite = ResearchProjectDef.Named("CE_AdvancedAmmo");
                            break;
                        }
                    case 5:
                        {//Sabot
                            newAmmo.defName = ("CEAP_Generic_Sabot");
                            newAmmo.label = ("Generic sabot Ammo");
                            newAmmo.description = ("Generic sabot ammo. Reduced damage, but greatly increased sharp penetration.");
                            newRecipe.workAmount *= 2;
                            newRecipe.researchPrerequisite = ResearchProjectDef.Named("CE_AdvancedAmmo");
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }

                newAmmo.thingCategories = new List<ThingCategoryDef>();
                newAmmo.thingCategories.Add(industrialAmmoCat);
                InjectedDefHasher.GiveShortHashToDef(newAmmo, typeof(AmmoDef));
                genericAmmos[0][i] = newAmmo;
                DefGenerator.AddImpliedDef<AmmoDef>(newAmmo);

                //finish the recipe
                newRecipe.defName = "Make" + newAmmo.defName;
                newRecipe.label = "make " + newAmmo.label + " cartridge x500";
                newRecipe.description = "Craft 500 " + newAmmo.label + " cartridges.";
                newRecipe.jobString = "Making " + newAmmo.label + " cartridges.";
                newRecipe.ingredients = ammoRecipeRefs[0][i].ingredients;
                newRecipe.fixedIngredientFilter = ammoRecipeRefs[0][i].fixedIngredientFilter;
                newRecipe.defaultIngredientFilter = ammoRecipeRefs[0][i].defaultIngredientFilter;
                newRecipe.products = new List<ThingDefCountClass>();
                newRecipe.products.Add(new ThingDefCountClass(newAmmo, 500));
                InjectedDefHasher.GiveShortHashToDef(newRecipe, typeof(RecipeDef));
                DefGenerator.AddImpliedDef<RecipeDef>(newRecipe);
            }
        }

        private void SetBaseBullet(ThingDef bullet)
        {
            bullet.category = ThingCategory.Projectile;
            bullet.tickerType = TickerType.Normal;
            bullet.altitudeLayer = AltitudeLayer.Projectile;
            bullet.thingClass = typeof(CombatExtended.BulletCE);
            bullet.useHitPoints = false;
            bullet.neverMultiSelect = true;
            bullet.graphicData.shaderType = ShaderTypeDefOf.Transparent;
        }

        private RecipeDef MakeRecipeBase(AmmoDef ammo)
        {
            RecipeDef newRecipe = new RecipeDef();
            newRecipe.workSpeedStat = ammoRecipeRefs[0][0].workSpeedStat;
            newRecipe.effectWorking = ammoRecipeRefs[0][0].effectWorking;
            newRecipe.soundWorking = ammoRecipeRefs[0][0].soundWorking;
            newRecipe.allowMixingIngredients = ammoRecipeRefs[0][0].allowMixingIngredients;
            newRecipe.workAmount = ammoRecipeRefs[0][0].workAmount;
            newRecipe.workSkill = ammoRecipeRefs[0][0].workSkill;
            newRecipe.targetCountAdjustment = ammoRecipeRefs[0][0].targetCountAdjustment;
            newRecipe.recipeUsers = new List<ThingDef>();
            newRecipe.recipeUsers.Add(CE_ThingDefOf.AmmoBench);
            newRecipe.conceptLearned = ammoRecipeRefs[0][0].conceptLearned;
            newRecipe.unfinishedThingDef = ammoRecipeRefs[0][0].unfinishedThingDef;
            newRecipe.ingredients = new List<IngredientCount>();
            return newRecipe;
        }

        public void ProcessSettings()
        {
            //TODO make some user settings and deal with them here
        }

        private void BeginPatch(string defCat)
        {
            stopwatch.Start();
            Logger.Message($"Attempting to patch {defCat} Defs");
        }

        private void EndPatch(string defCat)
        {
            stopwatch.Stop();
            Logger.Message(patchedMessage, defCat, stopwatch.ElapsedMilliseconds / 1000f, defsPatched, defsTotal, defsFailed);
            if (defsFailed != 0)
            {
                Logger.Error($"Failed to patch the following {defCat} defs: \n {failureList}");
                failureList.Clear();
            }
            stopwatch.Reset();
            defsPatched = 0;
            defsTotal = 0;
            defsFailed = 0;
        }
        
    }

    [Serializable]
    public class StatBaseEx : Exception
    {
        public StatBaseEx() : base() { }
        public StatBaseEx(string missingMod) : base(missingMod) 
        {
            MissingMod = missingMod;
        }
        public StatBaseEx(string missingBase, Exception inner) : base(missingBase, inner)
        {
            MissingMod = missingBase;
            Inner = inner;
        }

        public string MissingMod { get; }
        public Exception Inner { get; }
    }

    public class PropertyCopier<TParent, TChild> where TParent : class //this has failed to work, TODO remove
                                            where TChild : class
    {
        public static void Copy(TParent parent, TChild child)
        {
            var parentProperties = parent.GetType().GetProperties();
            var childProperties = child.GetType().GetProperties();

            foreach (var parentProperty in parentProperties)
            {
                foreach (var childProperty in childProperties)
                {
                    if (parentProperty.Name == childProperty.Name && parentProperty.PropertyType == childProperty.PropertyType)
                    {
                        try
                        {
                            childProperty.SetValue(child, parentProperty.GetValue(parent));
                            break;
                        }
                        catch(Exception ex)
                        {
                            continue;
                        }
                        
                    }
                }
            }
        }
    }
}
