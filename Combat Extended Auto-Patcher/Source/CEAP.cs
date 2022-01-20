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
        Stopwatch stopwatch = new Stopwatch();
        //used for logging
        string patchedMessage = "Patched defs of type {0} in {1:F4} seconds. {2} patched out of {3}, {4} failed.";
        int defsPatched = 0; //2
        int defsTotal = 0; //3
        int defsFailed = 0; //4
        StringBuilder failureList = new StringBuilder(); //will be logged after patchedMessage if defsFailed > 0

        public override void DefsLoaded()
        {
            if (!ModIsActive)
                return;

            try
            {
                PatchWeapons();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
            
            try
            {
                PatchApparel();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }

            try
            {
                PatchAnimals();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }

            try
            {
                PatchAliens();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private void PatchWeapons()
        {
            BeginPatch("WEAPONS");

            EndPatch("WEAPONS");
        }

        private void PatchApparel()
        {
            BeginPatch("APPAREL");

            EndPatch("APPAREL");
        }

        private void PatchAnimals()
        {
            BeginPatch("ANIMALS");

            EndPatch("ANIMALS");
        }

        private void PatchAliens()
        {
            BeginPatch("ALIENS");

            EndPatch("ALIENS");
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
