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

        //centralized values for determining Bulk, Worn Bulk, Armor Values
        public float skinBulkAdd = 0f;
        public float skinWulkAdd = 0f;
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
        public float bluntMult = 20f;
        public float animalMult = 0.25f;
        public float neolithicMult = 0.5f;
        public float medievalMult = 0.75f;
        public float industrialMult = 1f;
        public float spacerMult = 1.5f;
        public float ultraMult = 2f;
        public float archoMult = 3f;

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
            try //TODO put the 'try' inside the for loop so it can continue; if fails
            {
                StatDef bulkPlaceholder = new StatDef(); //assigned these so the IDE would shut up about them being used unassigned
                StatDef wornBulkPlaceholder = new StatDef();
                StatDef smokePlaceholder = new StatDef();
                bool bulkFound = false;
                bool wornFound = false;
                bool smokeFound = false;
                for (int i = 0; ;i++) //this entire loop is to find references to some StatDefs, that I can't find in the code
                {
                    if (!wornFound)
                    {
                        int tempIndex = apparels[i].statBases.FindIndex(wob => wob.ToString().Contains("WornBulk"));
                        if (tempIndex >= 0) //index will be -1 if no worn bulk StatModifier exists
                        {
                            wornBulkPlaceholder = apparels[i].statBases[tempIndex].stat;
                            wornFound = true;
                        }
                    }
                    if (!bulkFound)
                    {
                        int tempIndex = apparels[i].statBases.FindIndex(wob => wob.ToString().Contains("Bulk"));
                        if (tempIndex >= 0)
                        {
                            bulkPlaceholder = apparels[i].statBases[tempIndex].stat;
                            bulkFound = true;
                        }
                    }
                    if (!smokeFound)
                    {
                        if (apparels[i].equippedStatOffsets != null) //holy crap this took me a long time to debug; I figured if there were no stat offsets it would be an empty list, not a null reference
                        {
                            int tempIndex = apparels[i].equippedStatOffsets.FindIndex(wob => wob.ToString().Contains("Smoke"));
                            if (tempIndex >= 0)
                            {
                                smokePlaceholder = apparels[i].equippedStatOffsets[tempIndex].stat;
                                smokeFound = true;
                            }
                        }
                        
                    }
                    if (smokeFound && bulkFound && wornFound)
                    {
                        break;
                    }
                    
                }
                foreach (ThingDef apparel in apparels) //TODO put a try block in this so it can continue; after exceptions
                {
                    defsTotal++;
                    float newBulk = 0f;
                    float newWornBulk = 0f;
                    float techMult = 1f;

                    if (apparel.statBases.FindIndex(wob => wob.ToString().Contains("WornBulk")) == -1) //unpatched apparel (or poor patches) will have no WornBulk element in its statBases list
                    {
                        switch (apparel.techLevel)
                        {
                            case TechLevel.Animal: techMult = animalMult;
                                break;
                            case TechLevel.Neolithic: techMult = neolithicMult;
                                break;
                            case TechLevel.Medieval: techMult = medievalMult;
                                break;
                            case TechLevel.Industrial: techMult = industrialMult;
                                break;
                            case TechLevel.Spacer: techMult = spacerMult;
                                break;
                            case TechLevel.Ultra: techMult = ultraMult;
                                break;
                            case TechLevel.Archotech: techMult = archoMult;
                                break;
                            default: techMult = 1f;
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
                        if (sharpIndex >= 0)
                        {
                            apparel.statBases[bluntIndex].value *= bluntMult * techMult;
                        }

                        StatModifier statModBulk = new StatModifier();
                        statModBulk.stat = bulkPlaceholder;
                        statModBulk.value = newBulk;

                        StatModifier statModWornBulk = new StatModifier();
                        statModWornBulk.stat = wornBulkPlaceholder;
                        statModWornBulk.value = newWornBulk;

                        apparel.statBases.Add(statModWornBulk);
                        apparel.statBases.Add(statModBulk);

                        StatModifier statModSmoke = new StatModifier();
                        statModSmoke.stat = smokePlaceholder;
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
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                //failureList.AppendLine(apparel.defName)
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
