using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using Verse;
using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using CombatExtended;
using UnityEngine;

namespace CEAP
{
    public class Base : ModBase
    {
        public override string ModIdentifier
        {
            get { return "CEAP"; }
        }
    }
}
