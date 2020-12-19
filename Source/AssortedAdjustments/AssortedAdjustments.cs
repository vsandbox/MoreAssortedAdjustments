﻿using System;
using System.IO;
using System.Reflection;
using Harmony;
using AssortedAdjustments.Patches;

namespace AssortedAdjustments
{
    public static class AssortedAdjustments
    {
        internal static string LogPath;
        internal static string ModDirectory;
        internal static Settings Settings;
        internal static HarmonyInstance Harmony;

        // BEN: DebugLevel (0: nothing, 1: error, 2: debug, 3: info)
        internal static int DebugLevel = 3;



        // Modnix Entrypoints
        public static void SplashMod(Func<string, object, object> api)
        {
            Harmony = HarmonyInstance.Create("de.mad.AssortedAdjustments");

            ModDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            LogPath = Path.Combine(ModDirectory, "AssortedAdjustments.log");
            Logger.Initialize(LogPath, DebugLevel, ModDirectory, nameof(AssortedAdjustments));

            Settings = api("config", null) as Settings ?? new Settings();

            if (Settings.SkipIntroLogos)
            {
                //HarmonyHelpers.Patch(harmony, typeof(PhoenixGame), "BootCrt", typeof(SkipIntro), "Prefix_PhoenixGame_BootCrt");
                HarmonyHelpers.Patch(Harmony, typeof(PhoenixPoint.Common.Game.PhoenixGame), "RunGameLevel", typeof(SkipIntro), "Prefix_PhoenixGame_RunGameLevel");
            }
            if (Settings.SkipIntroMovie)
            {
                HarmonyHelpers.Patch(Harmony, typeof(PhoenixPoint.Home.View.ViewStates.UIStateHomeScreenCutscene), "EnterState", typeof(SkipIntro), null, "Postfix_UIStateHomeScreenCutscene_EnterState");
            }


            Logger.Info($"Modnix Mad.AssortedAdjustments.SplashMod initialised.");
        }

        public static void MainMod(Func<string, object, object> api)
        {
            //ModDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //LogPath = Path.Combine(ModDirectory, "AssortedAdjustments.log");
            //Logger.Initialize(LogPath, DebugLevel, ModDirectory, nameof(AssortedAdjustments));
            //Settings = api("config", null) as Settings ?? new Settings();

            Harmony.PatchAll();
            ApplyAll();
            DataHelpers.Print();


            Logger.Info($"Modnix Mad.AssortedAdjustments.MainMod initialised.");
        }



        public static void ApplyAll()
        {
            if(Settings.EnableEconomyAdjustments)
            {
                EconomyAdjustments.Apply();
            }

            if (Settings.EnableFacilityAdjustments)
            {
                FacilityAdjustments.Apply();
            }

            if (Settings.EnableSoldierAdjustments)
            {
                SoldierAdjustments.Apply();
            }

            if (Settings.EnableVehicleAdjustments)
            {
                VehicleAdjustments.Apply();
            }

            if (Settings.EnableMissionAdjustments)
            {
                MissionAdjustments.Apply();
            }
        }
    }
}