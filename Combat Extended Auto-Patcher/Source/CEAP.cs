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
        public int oneHandCutoff = 1;
        public int shotgunCutoff = 16; //range below this value is classed as a shotgun
        public int sniperCutoff = 40; //range above this value is classed as a sniper rifle

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

            stopwatchMaster.Stop();
            Logger.Message($"Combat Extended Auto-Patcher finished in {stopwatchMaster.ElapsedMilliseconds / 1000f} seconds.");
        }

        private void MakeLists()
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
            try
            {
                foreach (ThingDef weapon in weapons)
                {
                    defsTotal++;
                    if (weapon.IsRangedWeapon)
                    {
                        //TODO: check if already CE-compatible, has AMMO
                        //TODO: if not, add CE stats, remove vanilla stats, GenerateAmmo(weapon)
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
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                defsFailed++;
            }
            finally
            {
                EndPatch("WEAPONS");
            }
        }

        private void PatchApparel(List<ThingDef> apparels)
        {
            BeginPatch("APPAREL"); 
            try
            {
                StatDef bulkPlaceholder;
                StatDef wornBulkPlaceholder;
                for (int i = 0; ;i++) //this entire loop is to find a reference to the StatDef WornBulk, since it apparently doesn't exist in code, instead being created by parsing the XML
                {
                    int tempIndex = apparels[i].statBases.FindIndex(wob => wob.ToString().Contains("WornBulk"));
                    if (tempIndex > 0) //index will be -1 if no worn bulk StatModifier exists
                    {
                        wornBulkPlaceholder = apparels[i].statBases[tempIndex].stat;
                        bulkPlaceholder = apparels[i].statBases[apparels[i].statBases.FindIndex(wob => wob.ToString().StartsWith("Bulk"))].stat; //I assume anything with WornBulk will also have Bulk
                        break;
                    }
                    
                }
                foreach (ThingDef apparel in apparels)
                {
                    defsTotal++;
                    float newBulk = 0f;
                    float newWornBulk = 0f;
                    if (apparel.statBases.FindIndex(wob => wob.ToString().Contains("WornBulk")) == -1) //will catch unpatched apparel, but also apparel with intentionally no wornbulk
                    {
                        foreach (ApparelLayerDef ald in apparel.apparel.layers)
                        {
                            float mass = apparel.statBases[apparel.statBases.FindIndex(wob => wob.ToString().Contains("Mass"))].value;
                            if (ald == ApparelLayerDefOf.OnSkin || ald.ToString().ToUpper().Contains("SKIN")) 
                            {
                            }
                            if (ald == ApparelLayerDefOf.Middle || ald.ToString().ToUpper().Contains("MID"))
                            {
                                if (mass > 2)
                                {
                                    newBulk += 5f;
                                    newWornBulk += 3f;
                                }
                            }
                            if (ald == ApparelLayerDefOf.Shell || ald.ToString().ToUpper().Contains("SHELL") || ald.ToString().ToUpper().Contains("OUTER")) //had to add extra conditions to try to account for modded alien layers
                            {
                                if (mass > 2)
                                {
                                    if (newWornBulk == 0)
                                    {
                                        newBulk += 7.5f;
                                        newWornBulk += 2.5f;
                                    }
                                    else
                                    {
                                        newBulk *= 20;
                                        newWornBulk *= 5;
                                    }
                                }
                            }
                        }

                        StatModifier statModBulk = new StatModifier();
                        statModBulk.stat = bulkPlaceholder;
                        statModBulk.value = newBulk;

                        StatModifier statModWornBulk = new StatModifier();
                        statModWornBulk.stat = wornBulkPlaceholder;
                        statModWornBulk.value = newWornBulk;
                        apparel.statBases.Add(statModWornBulk);
                        apparel.statBases.Add(statModBulk);
                        defsPatched++;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                defsFailed++;
            }
            finally
            {
                EndPatch("APPAREL");
            }
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
