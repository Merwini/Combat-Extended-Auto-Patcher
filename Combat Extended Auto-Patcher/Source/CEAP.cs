using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using System.Diagnostics;
using Verse;
using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using CombatExtended;
using UnityEngine;
using RimWorld;

namespace CEAP
{
    [StaticConstructorOnStartup]
    public class Base : ModBase
    {
        public override string ModIdentifier
        {
            get { return "CEAP"; }
        }


        //Customizable settings? Change to get these numbers from user settable Mod Options
        public float oneHandCutoff = 2f; //TODO: make a Mass below this add the CE_OneHandedWeapon and CE_Sidearm weaponTags
        public float heavyCutoff = 5.5f; //TODO: Mass at or above this adds GunHeavy weaponTags
        public float shotgunCutoff = 16f; //range below this value is classed as a shotgun
        public float sniperCutoff = 40f; //TODO: range above this value adds SniperRifle weaponTags

        //gun values here since multiple methods need them
        float gunMass = 0f;
        float gunRange = 0f;
        float accuracyTouch = 0f;
        float accuracyShort = 0f;
        float accuracyMedium = 0f;
        float accuracyLong = 0f;
        float forcedMissRadius = 0f;
        int ticksBetweenBurstShots = 0;
        int burstShotCountV = 1;
        gunTypes gunType;

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

        public float skinSharpMult;
        public float skinBluntMult;
        public float midSharpMult;
        public float midBluntMult;
        public float shellSharpMult;
        public float shellBluntMult;

        public float sharpMult = 10f;
        public float bluntMult = 40f;
        public float animalMult = 0.25f;
        public float neolithicMult = 0.5f;
        public float medievalMult = 0.75f;
        public float industrialMult = 1f;
        public float spacerMult = 2f;
        public float ultraMult = 3f;
        public float archoMult = 4f;

        //will be used to store references to the generic ammoset def
        public AmmoSetDef genericAmmoSet;

        //will be used to store references to the generic ammo defs
        //TODO a 2d array is probably better but this is much more readable/maintainable?
        public ThingDef[] genericAmmos = new ThingDef[3];

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

        public List<ThingDef> weaponList = new List<ThingDef>();
        public List<ThingDef> apparelList = new List<ThingDef>();
        public List<ThingDef> animalList = new List<ThingDef>();
        public List<ThingDef> alienList = new List<ThingDef>();
        public List<ThingDef> turretList = new List<ThingDef>();

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

        public override void DefsLoaded()
        {
            if (!ModIsActive)
                return;

            stopwatchMaster.Start();

            MakeLists();
            PatchWeapons(weaponList);
            PatchApparel(apparelList);
            PatchAnimals(animalList);
            PatchAliens(alienList);
            PatchTurrets(turretList);
            //TODO: patch hediffs (fix armor values etc)

            stopwatchMaster.Stop();
            Logger.Message($"Combat Extended Auto-Patcher finished in {stopwatchMaster.ElapsedMilliseconds / 1000f} seconds.");
        }

        private void MakeLists() // I know that making lists first is just using extra cpu cycles, but makes it way easier to read/debug/maintain
        {
            stopwatch.Start();
            Logger.Message("Combat Extended Auto-Patcher list-making has started.");
            try
            {
                foreach (ThingDef td in DefDatabase<ThingDef>.AllDefs)                 
                {
                    //finds the generic ammos and stores references to them
                    //TODO find reference to the generic ammoset
                    if (td.defName.Contains("CEAP"))
                    {
                        switch (td.defName)
                        {
                            case "CEAP_Ammo_Generic_FMJ":
                                genericAmmos[0] = td;
                                break;
                            case "CEAP_Ammo_Generic_AP:":
                                genericAmmos[1] = td;
                                break;
                            case "CEAP_Ammo_Generic_HP":
                                genericAmmos[2] = td;
                                break;
                        }
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
                    //if (td.IsPawn?)
                    {
                        //animalList.Add(td);
                        //continue;
                    }
                    //if (td.IsAlien?)
                    {
                        //alienList.Add(td);
                        //continue;
                    }
                    if (td.thingClass.ToString().Contains("TurretGun"))
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
        }
        private void PatchWeapons(List<ThingDef> weapons)
        {
            BeginPatch("WEAPONS");

            foreach (ThingDef weapon in weapons)
            {
                try
                {
                    defsTotal++;
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
                            //TODO:
                            //add/remove statBases
                            //add/remove verbs
                            //generate ammo
                            //generate projectile
                            //assign ammo
                            //assign projectile
                            //add CompProperties_AmmoUser and CompProperties_FireModes
                            //Burst size will be size for Burst fire mode, 2x for suppressive (if burst > 1)
                            //add CE melee attacks for ranged weapons
                            //defsPatched++;
                            //here's where we put the ranged weapon patch logic

                            gunMass = weapon.statBases[weapon.statBases.FindIndex(i => i.stat == StatDef.Named("Mass"))].value;
                            gunRange = weapon.Verbs[0].range;
                            accuracyTouch = weapon.statBases[weapon.statBases.FindIndex(i => i.stat == StatDef.Named("AccuracyTouch"))].value;
                            accuracyShort = weapon.statBases[weapon.statBases.FindIndex(i => i.stat == StatDef.Named("AccuracyShort"))].value;
                            accuracyMedium = weapon.statBases[weapon.statBases.FindIndex(i => i.stat == StatDef.Named("AccuracyMedium"))].value;
                            accuracyLong = weapon.statBases[weapon.statBases.FindIndex(i => i.stat == StatDef.Named("AccuracyLong"))].value;
                            forcedMissRadius = weapon.Verbs[0].ForcedMissRadius;
                            ticksBetweenBurstShots = weapon.Verbs[0].ticksBetweenBurstShots;
                            burstShotCountV = weapon.Verbs[0].burstShotCount;

                            //debug Logger.Message(weapon.defName + " is a " + DetermineGunType(weapon).ToString());

                            PatchStatBases(weapon);
                            //Add comps CombatExtended.CompProperties_AmmoUser TODO implement
                            //Add comps CombatExtended.CompProperties_FireModes TODO implement
                            //PatchVerb TODO implement
                            //GenerateAmmo(weapon); implement
                            //PatchMelee TODO implement

                            //TODO might need separate if blocks for each of the vanilla verbs

                            ClearGunStats();
                        }
                        else
                        {

                        }

                    }
                    else if (weapon.IsMeleeWeapon)
                    {
                        //TODO: check if already CE-compatible, not sure how
                        //TODO: if not, add CE stats, remove vanilla stats
                        defsPatched++;
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

        private void ClearGunStats()
        {
            gunMass = 0;
            gunRange = 0;
            accuracyTouch = 0;
            accuracyShort = 0;
            accuracyMedium = 0;
            accuracyLong = 0;
            forcedMissRadius = 0;
            ticksBetweenBurstShots = 0;
            burstShotCountV = 1;
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
            StatModifier reloadTime = new StatModifier();
            reloadTime.stat = StatDef.Named("ReloadTime");


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
                    reloadTime.value = 1f;
                    break;
                case gunTypes.Pistol:
                    shotSpread.value = (0.2f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = 0.7f + gunTechModFlat;
                    swayFactor.value = gunMass * 0.9f;
                    gunBulk.value = 1f * gunMass;
                    reloadTime.value = 4f;
                    break;
                case gunTypes.SMG:
                    shotSpread.value = (0.17f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = 0.7f + gunTechModFlat;
                    swayFactor.value = gunMass * 0.8f;
                    gunBulk.value = 1f * gunMass;
                    recoil.value = (2f - (gunMass * 0.1f)) * recoilTechMod;
                    reloadTime.value = 4f;
                    break;
                case gunTypes.Shotgun:
                    shotSpread.value = (0.17f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = gunMass * 0.4f;
                    gunBulk.value = 2f * gunMass;
                    reloadTime.value = 4f;
                    break;
                case gunTypes.Rifle:
                    shotSpread.value = (0.13f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = gunMass * 0.35f;
                    gunBulk.value = 2f * gunMass;
                    recoil.value = (1.8f - (gunMass * 0.1f)) * recoilTechMod;
                    reloadTime.value = 4f;
                    break;
                case gunTypes.MachineGun:
                    shotSpread.value = (0.13f - ssAccuracyMod) * gunTechModPercent;
                    sightsEfficiency.value = seDefault;
                    swayFactor.value = gunMass * 0.17f;
                    gunBulk.value = 1.5f * gunMass;
                    recoil.value = (2.3f - (gunMass * 0.1f)) * recoilTechMod;
                    reloadTime.value = 8f; //TODO placeholder, actual value will be mag size * 0.04
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
            if (burstShotCountV == 1)
                burstShotCount.value = 1;
            else
                burstShotCount.value = 2 * burstShotCountV;

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
            newStatBases.Add(reloadTime);

            weapon.statBases = newStatBases;
        }
        private gunTypes DetermineGunType(ThingDef weapon)
        {
            //a turret is tagged as TurretGun, because it inherits that from BaseWeaponTurret
            if (weapon.weaponTags.Contains("TurretGun"))
                return gunTypes.Turret;
            //a bow is a pre-industrial ranged weapon with a burst count of 1. Let's hope there aren't many edge cases.
            //if (weapon.label.IndexOf("bow", 0, StringComparison.CurrentCultureIgnoreCase) != -1 || weapon.Verbs[0].defaultProjectile.label.IndexOf("arrow", 0, StringComparison.CurrentCultureIgnoreCase) != -1)
            else if ((weapon.techLevel.CompareTo(TechLevel.Medieval) <= 0) && (burstShotCountV == 1))
                return gunTypes.Bow;
            //a grenade uses a different verb from most weapons
            else if (weapon.Verbs[0].verbClass.ToString().EqualsIgnoreCase("Verse.Verb_ShootLaunchProjectile"))
                return gunTypes.Grenade;
            //grendade launchers have a forced miss radius but are reusable
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
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (burstShotCountV == 1) && ((gunRange < 26) || (gunMass < 2)))
                return gunTypes.Pistol;
            // a sniper is an industrial or higher weapon with burst count 1 and either a range >= 26 OR mass 
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (burstShotCountV == 1) && ((gunRange >= 26) || (gunMass > 2)))
                return gunTypes.Sniper;
            //an SMG is an industrial or higher weapon with burst count > 1 and a range < 23
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (burstShotCountV > 1) && (weapon.Verbs[0].range < 23))
                return gunTypes.SMG;
            //a rifle is an industrial or higher weapon with burst count > 1 but < 6 and a range >= 23
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (burstShotCountV > 1) && (burstShotCountV < 6) && (gunRange >= 23))
                return gunTypes.Rifle;
            //a machine gun is an industrial or higher weapon with burst count > 1 and either burst count >= 6 OR mass > 6?
            else if ((weapon.techLevel.CompareTo(TechLevel.Industrial) >= 0) && (burstShotCountV > 1) && ((burstShotCountV >= 6) || (gunMass > 6)))
                return gunTypes.MachineGun;
            else
                return gunTypes.Other;
        }

        private void PatchApparel(List<ThingDef> apparels)
        {
            BeginPatch("APPAREL"); 
                    
            foreach (ThingDef apparel in apparels)
            {
                defsTotal++;
                float newBulk = 0f;
                float newWornBulk = 0f;
                float techMult = 1f;
                try
                {
                    if (apparel.statBases.FindIndex(wob => wob.ToString().Contains("Bulk")) == -1) //unpatched apparel (or poorly made patches) will have no Bulk element in its statBases list
                    {
                        switch (apparel.techLevel)
                        {
                            case TechLevel.Animal:
                                techMult = animalMult;
                                break;
                            case TechLevel.Neolithic:
                                techMult = neolithicMult;
                                break;
                            case TechLevel.Medieval:
                                techMult = medievalMult;
                                break;
                            case TechLevel.Industrial:
                                techMult = industrialMult;
                                break;
                            case TechLevel.Spacer:
                                techMult = spacerMult;
                                break;
                            case TechLevel.Ultra:
                                techMult = ultraMult;
                                break;
                            case TechLevel.Archotech:
                                techMult = archoMult;
                                break;
                            default:
                                techMult = 1f;
                                break;
                        }

                        bool isSkin = false;
                        bool isMid = false;
                        bool isShell = false;

                        foreach (ApparelLayerDef ald in apparel.apparel.layers)
                        {
                            float mass = apparel.statBases[apparel.statBases.FindIndex(wob => wob.ToString().Contains("Mass"))].value;
                            if (ald == ApparelLayerDefOf.OnSkin || ald.ToString().ToUpper().Contains("SKIN") || ald.ToString().ToUpper().Contains("STRAPPED"))
                            {
                                isSkin = true;
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

        private void PatchAnimals(List<ThingDef> animals)
        {
            BeginPatch("ANIMALS");
            try
            {
                foreach (ThingDef animal in animals)
                {
                    //TODO check if CE-compatible
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                defsFailed++;
            }
            finally
            {
                EndPatch("ANIMALS");
            }
        }

        private void PatchAliens(List<ThingDef> aliens)
        {
            BeginPatch("ALIENS");
            try
            {
                foreach (ThingDef alien in aliens)
                {
                    //TODO check if CE compatible
                    //don't forget to make sure they carry the right ammo
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                defsFailed++;
            }
            finally
            {
                EndPatch("ALIENS");
            }
        }

        private void PatchTurrets(List<ThingDef> turrets)
        {
            BeginPatch("TURRETS");
            try
            {
                foreach (ThingDef turret in turrets)
                {
                    defsTotal++;
                    if (turret.fillPercent < 0.85)
                    {
                        turret.fillPercent = 0.85f;
                        defsPatched++;
                    }
                    //TODO: remove <comps><Class=CompProperties_Reguelable> and make it use ammo instead
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                defsFailed++;
            }
            finally
            {
                EndPatch("TURRETS");
            }
        }

        private void GenerateAmmo(ThingDef needsAmmo) //probably should return the ammo
        {
            ThingDef newAmmo = new ThingDef();
            newAmmo.defName = ("CEAP_Ammo");
            newAmmo.label = ("CEAP-generated ammo test");
            newAmmo.description = ("A generic ammo generated by CEAP");
            DefDatabase<ThingDef>.Add(newAmmo);
            ThingDef newAmmoSet = ThingDefDuplicator.CreateDeepCopy(needsAmmo);
        }

        public void ProcessSettings()
        {
            //deal with user settings here
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
}
