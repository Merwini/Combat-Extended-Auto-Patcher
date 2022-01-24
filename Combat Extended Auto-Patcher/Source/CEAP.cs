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

        /* never mind, trying to come up with multipliers for different tech levels is way too mod-dependent
        public float fleshSharpMult = 10f;
        public float fleshBluntMult = 10f;
        public float medievalSharpMult;
        public float medievalBluntMult;
        public float industrialSharpMult;
        public float industrialBluntMult;
        public float spacerSharpMult;
        public float spacerBluntMult;
        public float ultraSharpMult = 30;
        public float ultraBluntMult = 80;
        public float archoSharpMult = 20;
        public float archoBluntMult =20;
        */


        public float sharpMult = 10f;
        public float bluntMult = 40f;
        public float animalMult = 0.25f;
        public float neolithicMult = 0.5f;
        public float medievalMult = 0.75f;
        public float industrialMult = 1f;
        public float spacerMult = 2f;
        public float ultraMult = 3f;
        public float archoMult = 4f;

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
                    if (td.IsWeapon)
                    {
                        weaponList.Add(td);
                    }
                    if (td.IsApparel)
                    {
                        apparelList.Add(td);
                    }
                    //if (td.IsAnimal?)
                    {
                        //animalList.Add(td);
                    }
                    //if (td.IsAlien?)
                    {
                        //alienList.Add(td);
                    }
                    if (td.thingClass.ToString().Contains("TurretGun"))
                    {
                        turretList.Add(td);
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
                        //TODO: check if already CE-compatible, has AMMO
                        //TODO: if not, add CE stats, remove vanilla stats, GenerateAmmo(weapon)
                        //Burst size will be size for Burst fire mode, 2x for suppressive (if burst > 1)
                        defsPatched++;
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
                    defsFailed++;
                }
            }
            EndPatch("WEAPONS");
        }

        private void PatchApparel(List<ThingDef> apparels)
        {
            BeginPatch("APPAREL"); 
                    
            foreach (ThingDef apparel in apparels) //TODO put a try block in this so it can continue; after exceptions
            {
                defsTotal++;
                float newBulk = 0f;
                float newWornBulk = 0f;
                float techMult = 1f;
                try //TODO put the 'try' inside the for loop so it can continue; if fails
                {
                    if (apparel.statBases.FindIndex(wob => wob.ToString().Contains("WornBulk")) == -1) //unpatched apparel (or poor patches) will have no WornBulk element in its statBases list
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

                        if (techMult > 1 && isMid && isShell)
                        {
                            //add weapon handling, toxic sensitivity, bulk capacity, carry weight
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
                        }
                        defsPatched++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                    failureList.AppendLine(apparel.defName);
                    defsFailed++;
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
                    //TODO check if cover height == 1.49m by making fillPercent = 0.85
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

        private void GenerateAmmo(ThingDef needsAmmo)
        {

        }

        public void ProcessSettings()
        {

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
}
