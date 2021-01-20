﻿using System;
using System.Reflection;
using Harmony;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Entities;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewStates;
using Base.Core;
using PhoenixPoint.Geoscape.Entities.Abilities;
using Base.UI;
using PhoenixPoint.Common.UI;
using UnityEngine.UI;
using PhoenixPoint.Geoscape.Entities.Research;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities.PhoenixBases;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Geoscape.Entities.Sites;
using PhoenixPoint.Geoscape.View.DataObjects;
using PhoenixPoint.Geoscape.View;
using PhoenixPoint.Geoscape.Levels.Factions.Archeology;
using Base.Defs;
using UnityEngine.EventSystems;
using PhoenixPoint.Geoscape.Levels.Objectives;

namespace AssortedAdjustments.Patches.UIEnhancements
{
    internal static class ExtendedAgendaTracker
    {
        internal static bool fetchedSiteNames = false;
        internal static string unexploredSiteName = "UNEXPLORED SITE";
        internal static string explorationSiteName = "EXPLORATION SITE";
        internal static string scavengingSiteName = "SCAVENGING SITE";
        internal static string ancientSiteName = "ANCIENT SITE";

        internal static string actionExploring = "INVESTIGATES";
        internal static string actionTraveling = "TRAVELS TO";
        internal static string actionRepairing = "REPAIRING:";
        internal static string actionExcavating = "EXCAVATING:";
        internal static string actionAcquire = "SECURE";

        internal static Sprite aircraftSprite = null;
        internal static Sprite ancientSiteProbeSprite = null;
        internal static Sprite archeologyLabSprite = null;
        internal static Sprite phoenixFactionSprite = null;

        internal static Color vehicleTrackerColor = new Color32(251, 191, 31, 255);
        internal static Color manufactureTrackerColor = new Color32(235, 110, 42, 255);
        internal static Color researchTrackerColor = new Color32(42, 245, 252, 255);
        internal static Color excavationTrackerColor = new Color32(93, 153, 106, 255);
        internal static Color facilityTrackerColor = new Color32(185, 185, 185, 255);
        internal static Color defaultTrackerColor = new Color32(155, 155, 155, 255);
        internal static UIFactionDataTrackerElement trackerElementDefault = null;

        // Cache reflected methods
        internal static MethodInfo ___GetFreeElement = typeof(UIModuleFactionAgendaTracker).GetMethod("GetFreeElement", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static MethodInfo ___OnAddedElement = typeof(UIModuleFactionAgendaTracker).GetMethod("OnAddedElement", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static MethodInfo ___OrderElements = typeof(UIModuleFactionAgendaTracker).GetMethod("OrderElements", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static MethodInfo ___UpdateData = typeof(UIModuleFactionAgendaTracker).GetMethod("UpdateData", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
        internal static MethodInfo ___Dispose = typeof(UIModuleFactionAgendaTracker).GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance);



        /*
         **
         *** Utility methods
         ** 
        */
        private static bool GetTravelTime(GeoVehicle vehicle, out float travelTime, GeoSite target = null)
        {
            travelTime = 0f;

            if (target == null && vehicle.FinalDestination == null)
            {
                return false;
            }

            var currentPosition = vehicle.CurrentSite?.WorldPosition ?? vehicle.WorldPosition;
            var targetPosition = target == null ? vehicle.FinalDestination.WorldPosition : target.WorldPosition;
            var travelPath = vehicle.Navigation.FindPath(currentPosition, targetPosition, out bool hasTravelPath);

            if (!hasTravelPath || travelPath.Count < 2)
            {
                return false;
            }

            float distance = 0;

            for (int i = 0, len = travelPath.Count - 1; i < len;)
            {
                distance += GeoMap.Distance(travelPath[i].Pos.WorldPosition, travelPath[++i].Pos.WorldPosition).Value;
            }

            travelTime = distance / vehicle.Stats.Speed.Value;
            //Logger.Info($"[TravelAgenda_GetTravelTime] travelTime: {travelTime}");

            return true;
        }

        private static float GetExplorationTime(GeoVehicle vehicle, float hours)
        {
            try
            {
                if (vehicle == null)
                {
                    return hours;
                }

                object updateable = typeof(GeoVehicle).GetField("_explorationUpdateable", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vehicle);

                if (updateable == null)
                {
                    return hours;
                }

                NextUpdate endTime = (NextUpdate)updateable.GetType().GetProperty("NextUpdate").GetValue(updateable);
                return (float)-(vehicle.Timing.Now - endTime.NextTime).TimeSpan.TotalHours;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return hours;
            }
        }

        private static string GetSiteName(GeoSite site, GeoFaction faction)
        {
            string siteName = null;

            if (String.IsNullOrEmpty(siteName))
            {
                if (site.GetInspected(faction))
                {
                    if (site.Type == GeoSiteType.PhoenixBase)
                    {
                        siteName = site.Name;
                    }
                    else if (site.Type == GeoSiteType.Haven)
                    {
                        siteName = site.Name;
                    }
                    else if (site.Type == GeoSiteType.AlienBase)
                    {
                        GeoAlienBase alienBase = site.GetComponent<GeoAlienBase>();
                        GeoAlienBaseTypeDef alienBaseTypeDef = alienBase.AlienBaseTypeDef;

                        siteName = alienBaseTypeDef.Name.Localize();
                    }
                    else if (site.Type == GeoSiteType.Scavenging)
                    {
                        siteName = scavengingSiteName;
                    }
                    else if (site.Type == GeoSiteType.Exploration)
                    {
                        siteName = explorationSiteName;
                    }
                    else if (site.IsArcheologySite)
                    {
                        siteName = ancientSiteName;
                    }
                }
                else
                {
                    siteName = unexploredSiteName;
                }
            }

            // Last resort
            if (String.IsNullOrEmpty(siteName))
            {
                siteName = "POI";
            }

            return $"{siteName}";
        }

        private static string AppendTime(float hours)
        {
            string prefix = "   ~ ";
            string time = HoursToText(hours);
            string postfix = "";

            return $"{prefix}{time}{postfix}";
        }

        private static string HoursToText(float hours)
        {
            TimeUnit timeUnit = TimeUnit.FromHours(hours);
            TimeRemainingFormatterDef timeFormatter = new TimeRemainingFormatterDef();
            timeFormatter.DaysText = new LocalizedTextBind("{0}d", true);
            timeFormatter.HoursText = new LocalizedTextBind("{0}h", true);
            string timeString = UIUtil.FormatTimeRemaining(timeUnit, timeFormatter);

            return timeString;
        }

        private static void AddAncientSiteEncounterObjective(/*UIStateVehicleSelected uiStateVehicleSelected, */GeoPhoenixFaction geoPhoenixFaction, SiteExcavationState excavatingSite)
        {
            //GeoscapeViewContext ___Context = (GeoscapeViewContext)AccessTools.Property(typeof(GeoscapeViewState), "Context").GetValue(uiStateVehicleSelected, null);
            //GeoPhoenixFaction geoPhoenixFaction = (GeoPhoenixFaction)___Context.ViewerFaction;
            //UIModuleGeoObjectives ____objectivesModule = (UIModuleGeoObjectives)AccessTools.Property(typeof(UIStateVehicleSelected), "_objectivesModule").GetValue(__instance, null);
            //GeoscapeFactionObjectiveSystem FactionObjectiveSystem = ___Context.Level.FactionObjectiveSystem;

            if (excavatingSite.Site.ActiveMission != null)
            {
                Logger.Info($"[AddAncientSiteEncounterObjective] Adding objective for mission: {excavatingSite.Site.ActiveMission?.MissionDef.Description.Localize()} at excavatingSite: {excavatingSite.Site.Name}");

                MissionGeoFactionObjective objective = new MissionGeoFactionObjective(excavatingSite.Site.Owner, excavatingSite.Site.ActiveMission)
                {
                    //Title = $"{actionAcquire} {excavatingSite.Site.Name}",
                    Title = excavatingSite.Site.ActiveMission.MissionName,
                    Description = excavatingSite.Site.ActiveMission.MissionDescription
                };

                geoPhoenixFaction.AddObjective(objective);
            }
        }



        /*
         **
         *** Minor Patches
         ** 
        */

        // Store site labels to use them in tracker
        [HarmonyPatch(typeof(UIStateVehicleSelected), "EnterState")]
        public static class UIStateVehicleSelected_EnterState_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker;
            }

            public static void Postfix(UIStateVehicleSelected __instance)
            {
                try
                {
                    if (!fetchedSiteNames)
                    {
                        UIModuleSelectionInfoBox ____selectionInfoBoxModule = (UIModuleSelectionInfoBox)AccessTools.Property(typeof(UIStateVehicleSelected), "_selectionInfoBoxModule").GetValue(__instance, null);

                        unexploredSiteName = ____selectionInfoBoxModule.UnexploredSiteTextKey.Localize();
                        explorationSiteName = ____selectionInfoBoxModule.ExplorationSiteTextKey.Localize();
                        scavengingSiteName = ____selectionInfoBoxModule.ScavengingSiteTextKey.Localize();
                        ancientSiteName = ____selectionInfoBoxModule.AncientSiteTextKey.Localize();

                        fetchedSiteNames = true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // Store some sprites to use them in tracker and objectives list
        [HarmonyPatch(typeof(UIModuleInfoBar), "Init")]
        public static class UIModuleInfoBar_Init_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker;
            }

            public static void Postfix(UIModuleInfoBar __instance)
            {
                try
                {
                    DefRepository defRepository = GameUtl.GameComponent<DefRepository>();

                    if (aircraftSprite == null)
                    {
                        aircraftSprite = __instance.AirVehiclesLabel.transform.parent.gameObject.GetComponentInChildren<Image>(true).sprite;
                    }

                    if (ancientSiteProbeSprite == null)
                    {
                        ancientSiteProbeSprite = defRepository.DefRepositoryDef.AllDefs.OfType<ViewElementDef>().Where(def => def.name.Contains("AncientSiteProbeAbilityDef")).FirstOrDefault().SmallIcon;
                    }

                    if (archeologyLabSprite == null)
                    {
                        archeologyLabSprite = defRepository.DefRepositoryDef.AllDefs.OfType<ViewElementDef>().Where(def => def.name.Contains("ArcheologyLab_PhoenixFacilityDef")).FirstOrDefault().SmallIcon;
                    }

                    if (phoenixFactionSprite == null)
                    {
                        phoenixFactionSprite = defRepository.DefRepositoryDef.AllDefs.OfType<GeoFactionViewDef>().Where(def => def.name.Contains("Phoenix")).FirstOrDefault().FactionIcon;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // Disables the big dumb buttons for excavations and base defense
        [HarmonyPatch(typeof(UIModuleStatusBarMessages), "Update")]
        public static class UIModuleStatusBarMessages_Update_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker && AssortedAdjustments.Settings.AgendaTrackerHideStatusBar;
            }

            public static void Postfix(UIModuleStatusBarMessages __instance)
            {
                try
                {
                    // @ToDo: Put somewhere else? Destroy completely?
                    __instance.TimedEventRoot.gameObject.SetActive(false);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // Add an icon for *secondary* objectives of a faction without icon set (Environment, Inactive)
        [HarmonyPatch(typeof(GeoObjectiveElementController), "SetObjective")]
        public static class GeoObjectiveElementController_SetObjective_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker && AssortedAdjustments.Settings.AgendaTrackerShowExcavations && AssortedAdjustments.Settings.AgendaTrackerAddMissionObjective;
            }

            public static void Prefix(GeoObjectiveElementController __instance, ref Sprite icon, ref Color iconColor)
            {
                try
                {
                    if (icon == null)
                    {
                        Logger.Info($"[GeoObjectiveElementController_SetObjective_PREFIX] Icon is null, setting a custom one.");

                        // Fallback to some prepared sprite
                        icon = archeologyLabSprite;
                        iconColor = Color.white;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // Prefix the times with an "~"
        [HarmonyPatch(typeof(UIFactionDataTrackerElement), "SetTime")]
        public static class UIFactionDataTrackerElement_SetTime_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker;
            }

            public static void Postfix(UIFactionDataTrackerElement __instance)
            {
                try
                {
                    string org = __instance.TrackedTime.text;
                    string pre = "~ ";

                    __instance.TrackedTime.text = $"{pre}{org}";
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // Just an informational patch for now
        [HarmonyPatch(typeof(UIModuleFactionAgendaTracker), "Dispose")]
        public static class UIModuleFactionAgendaTracker_Dispose_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker;
            }

            public static void Prefix(UIModuleFactionAgendaTracker __instance, ref UIFactionDataTrackerElement element)
            {
                try
                {
                    Logger.Info($"[UIModuleFactionAgendaTracker_Dispose_PREFIX] element.TrackedObject: {element.TrackedObject}");
                    // Further cleanup or optimizations?
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // Displays expected durations of Move and Explore abilities to the contect menu
        [HarmonyPatch(typeof(UIModuleSiteContextualMenu), "SetMenuItems")]
        public static class UIModuleSiteContextualMenu_SetMenuItems_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker;
            }

            public static void Postfix(UIModuleSiteContextualMenu __instance, GeoSite site, List<SiteContextualMenuItem> ____menuItems)
            {
                try
                {
                    foreach (SiteContextualMenuItem item in ____menuItems)
                    {
                        GeoVehicle vehicle = item.Ability?.GeoActor as GeoVehicle;

                        if (item.Ability is MoveVehicleAbility move && move.GeoActor is GeoVehicle v && v.CurrentSite != site)
                        {
                            if (GetTravelTime(v, out float eta, site))
                            {
                                item.ItemText.text += AppendTime(eta);
                            }
                        }
                        else if (item.Ability is ExploreSiteAbility explore)
                        {
                            float hours = GetExplorationTime(vehicle, (float)site.ExplorationTime.TimeSpan.TotalHours);
                            item.ItemText.text += AppendTime(hours);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }



        /*
         **
         *** Patches that ADD NEW types of tracker items OR UPDATE these at various events
         ** 
        */

        // ADDs or UPDATEs excavation items of the tracker
        [HarmonyPatch(typeof(UIModuleStatusBarMessages), "DisplayTimedEventMessage")]
        public static class UIModuleStatusBarMessages_DisplayTimedEventMessage_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker && AssortedAdjustments.Settings.AgendaTrackerShowExcavations;
            }

            public static void Postfix(UIModuleStatusBarMessages __instance, StatusBarTimedMessage message)
            {
                try
                {
                    if (!(message.ChaseActor is GeoSite geoSite))
                    {
                        return;
                    }
                    Logger.Info($"[UIModuleStatusBarMessages_DisplayTimedEventMessage_POSTFIX] geoSite: {geoSite.Name}");
                    Logger.Debug($"[UIModuleStatusBarMessages_DisplayTimedEventMessage_POSTFIX] __instance.Context.View.CurrentViewState: {__instance.Context?.View?.CurrentViewState}");

                    if (__instance.Context?.View?.CurrentViewState is UIStateVehicleSelected uiStateVehicleSelected)
                    {
                        string siteName = geoSite.LocalizedSiteName;
                        string excavationInfo = $"{actionExcavating} {siteName}";

                        UIModuleFactionAgendaTracker ____factionTracker = (UIModuleFactionAgendaTracker)AccessTools.Property(typeof(UIStateVehicleSelected), "_factionTracker").GetValue(uiStateVehicleSelected, null);
                        List<UIFactionDataTrackerElement> ____currentTrackedElements = (List<UIFactionDataTrackerElement>)AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements").GetValue(____factionTracker);

                        foreach (UIFactionDataTrackerElement trackedElement in ____currentTrackedElements)
                        {
                            // Update
                            if (trackedElement.TrackedObject == geoSite)
                            {
                                Logger.Debug($"[UIModuleStatusBarMessages_DisplayTimedEventMessage_POSTFIX] {geoSite.Name} already tracked. Updating.");

                                trackedElement.TrackedName.text = excavationInfo;

                                //AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_needsRefresh").SetValue(____factionTracker, true);
                                ___UpdateData.Invoke(____factionTracker, null);
                                //___OrderElements.Invoke(____factionTracker, null);

                                // Return early as the first match is always the visible one
                                return;
                            }
                        }

                        // Add
                        Logger.Debug($"[UIModuleStatusBarMessages_DisplayTimedEventMessage_POSTFIX] {geoSite.Name} currently not tracked. Adding to tracker.");

                        ViewElementDef borrowedViewElementDef = GameUtl.GameComponent<DefRepository>().DefRepositoryDef.AllDefs.OfType<ViewElementDef>().Where(def => def.name.Contains("ArcheologyLab_PhoenixFacilityDef")).FirstOrDefault();
                        UIFactionDataTrackerElement freeElement = (UIFactionDataTrackerElement)___GetFreeElement.Invoke(____factionTracker, null);
                        freeElement.Init(geoSite, excavationInfo, borrowedViewElementDef, false);

                        ___OnAddedElement.Invoke(____factionTracker, new object[] { freeElement });
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // ADDs or UPDATEs vehicle related items of the tracker
        [HarmonyPatch(typeof(UIStateVehicleSelected), "OnContextualItemSelected")]
        public static class UIStateVehicleSelected_OnContextualItemSelected_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker && AssortedAdjustments.Settings.AgendaTrackerShowVehicles;
            }

            public static void Postfix(UIStateVehicleSelected __instance, GeoAbility ability)
            {
                try
                {
                    Logger.Info($"[UIStateVehicleSelected_OnContextualItemSelected_POSTFIX] ability: {ability.AbilityDef.name}");

                    GeoVehicle vehicle = null;
                    string siteName = "ERR";
                    string vehicleInfo = "ERR";

                    if (ability is MoveVehicleAbility)
                    {
                        vehicle = (GeoVehicle)ability.GeoActor;
                        siteName = GetSiteName(vehicle.FinalDestination, vehicle.Owner);
                        vehicleInfo = $"{vehicle.Name} {actionTraveling} {siteName}";
                    }
                    else if (ability is ExploreSiteAbility)
                    {
                        vehicle = (GeoVehicle)ability.GeoActor;
                        siteName = GetSiteName(vehicle.CurrentSite, vehicle.Owner);
                        vehicleInfo = $"{vehicle.Name} {actionExploring} {siteName}";
                    }

                    if (vehicle == null)
                    {
                        return;
                    }

                    UIModuleFactionAgendaTracker ____factionTracker = (UIModuleFactionAgendaTracker)AccessTools.Property(typeof(UIStateVehicleSelected), "_factionTracker").GetValue(__instance, null);
                    List<UIFactionDataTrackerElement> ____currentTrackedElements = (List<UIFactionDataTrackerElement>)AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements").GetValue(____factionTracker);

                    foreach (UIFactionDataTrackerElement trackedElement in ____currentTrackedElements)
                    {
                        // Update
                        if (trackedElement.TrackedObject == vehicle)
                        {
                            Logger.Debug($"[UIStateVehicleSelected_OnContextualItemSelected_POSTFIX] {vehicle.Name} already tracked. Updating.");

                            trackedElement.TrackedName.text = vehicleInfo;

                            //AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_needsRefresh").SetValue(____factionTracker, true);
                            //MethodInfo ___UpdateData = typeof(UIModuleFactionAgendaTracker).GetMethod("UpdateData", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
                            ___UpdateData.Invoke(____factionTracker, null);
                            ___OrderElements.Invoke(____factionTracker, null);

                            // Return early as the first match is always the visible one
                            return;
                        }
                    }

                    // Add
                    Logger.Debug($"[UIStateVehicleSelected_OnContextualItemSelected_POSTFIX] {vehicle.Name} currently not tracked. Adding to tracker.");

                    //MethodInfo ___GetFreeElement = typeof(UIModuleFactionAgendaTracker).GetMethod("GetFreeElement", BindingFlags.NonPublic | BindingFlags.Instance);
                    //MethodInfo ___OnAddedElement = typeof(UIModuleFactionAgendaTracker).GetMethod("OnAddedElement", BindingFlags.NonPublic | BindingFlags.Instance);

                    UIFactionDataTrackerElement freeElement = (UIFactionDataTrackerElement)___GetFreeElement.Invoke(____factionTracker, null);
                    freeElement.Init(vehicle, vehicleInfo, vehicle.VehicleDef.ViewElement, false);
                    //freeElement.Init(vehicle, vehicleInfo, null, false);

                    ___OnAddedElement.Invoke(____factionTracker, new object[] { freeElement });
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // ADDs or UPDATEs vehicle related items of the tracker
        // NOTE that this is EXCLUSIVELY called from UIStateVehicleSelected.OnRightClickSelection()
        [HarmonyPatch(typeof(UIStateVehicleSelected), "AddTravelSite")]
        public static class UIStateVehicleSelected_AddTravelSite_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker && AssortedAdjustments.Settings.AgendaTrackerShowVehicles;
            }

            public static void Postfix(UIStateVehicleSelected __instance, GeoSite site)
            {
                try
                {
                    GeoVehicle ___SelectedVehicle = (GeoVehicle)AccessTools.Property(typeof(UIStateVehicleSelected), "SelectedVehicle").GetValue(__instance, null);
                    MoveVehicleAbility ability = ___SelectedVehicle.GetAbility<MoveVehicleAbility>();

                    if (ability == null)
                    {
                        return;
                    }
                    GeoAbilityTarget target = new GeoAbilityTarget(site);
                    if (!ability.CanActivate(target))
                    {
                        return;
                    }

                    GeoVehicle vehicle = ___SelectedVehicle;
                    string siteName = GetSiteName(vehicle.FinalDestination, vehicle.Owner);
                    string vehicleInfo = $"{vehicle.Name} {actionTraveling} {siteName}";

                    UIModuleFactionAgendaTracker ____factionTracker = (UIModuleFactionAgendaTracker)AccessTools.Property(typeof(UIStateVehicleSelected), "_factionTracker").GetValue(__instance, null);
                    List<UIFactionDataTrackerElement> ____currentTrackedElements = (List<UIFactionDataTrackerElement>)AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements").GetValue(____factionTracker);

                    foreach (UIFactionDataTrackerElement trackedElement in ____currentTrackedElements)
                    {
                        // Update
                        if (trackedElement.TrackedObject == vehicle)
                        {
                            Logger.Debug($"[UIStateVehicleSelected_AddTravelSite_POSTFIX] {vehicle.Name} already tracked. Updating.");

                            trackedElement.TrackedName.text = vehicleInfo;

                            //AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_needsRefresh").SetValue(____factionTracker, true);
                            //MethodInfo ___UpdateData = typeof(UIModuleFactionAgendaTracker).GetMethod("UpdateData", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
                            ___UpdateData.Invoke(____factionTracker, null);
                            ___OrderElements.Invoke(____factionTracker, null);

                            // Return early as the first match is always the visible one
                            return;
                        }
                    }

                    // Add
                    Logger.Debug($"[UIStateVehicleSelected_AddTravelSite_POSTFIX] {vehicle.Name} currently not tracked. Adding to tracker.");

                    //MethodInfo ___GetFreeElement = typeof(UIModuleFactionAgendaTracker).GetMethod("GetFreeElement", BindingFlags.NonPublic | BindingFlags.Instance);
                    //MethodInfo ___OnAddedElement = typeof(UIModuleFactionAgendaTracker).GetMethod("OnAddedElement", BindingFlags.NonPublic | BindingFlags.Instance);

                    UIFactionDataTrackerElement freeElement = (UIFactionDataTrackerElement)___GetFreeElement.Invoke(____factionTracker, null);
                    freeElement.Init(vehicle, vehicleInfo, vehicle.VehicleDef.ViewElement, false);
                    //freeElement.Init(vehicle, vehicleInfo, null, false);

                    ___OnAddedElement.Invoke(____factionTracker, new object[] { freeElement });
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }



        /*
         **
         *** Patches that REMOVE custom tracker items at various events
         ** 
        */

        // REMOVES the related item when an ancient site is excavated
        [HarmonyPatch(typeof(UIStateVehicleSelected), "OnVehicleSiteExcavated")]
        public static class UIStateVehicleSelected_OnVehicleSiteExcavated_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker && AssortedAdjustments.Settings.AgendaTrackerShowExcavations;
            }

            public static void Postfix(UIStateVehicleSelected __instance, GeoPhoenixFaction faction, SiteExcavationState excavation)
            {
                try
                {
                    UIModuleFactionAgendaTracker ____factionTracker = (UIModuleFactionAgendaTracker)AccessTools.Property(typeof(UIStateVehicleSelected), "_factionTracker").GetValue(__instance, null);
                    List<UIFactionDataTrackerElement> ____currentTrackedElements = (List<UIFactionDataTrackerElement>)AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements").GetValue(____factionTracker);

                    foreach (UIFactionDataTrackerElement trackedElement in ____currentTrackedElements)
                    {
                        // Remove
                        if (trackedElement.TrackedObject == excavation.Site)
                        {
                            Logger.Debug($"[UIStateVehicleSelected_OnVehicleSiteExcavated_POSTFIX] {excavation.Site.Name} is tracked. Removing.");

                            // Dispose
                            //MethodInfo ___Dispose = typeof(UIModuleFactionAgendaTracker).GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance);
                            ___Dispose.Invoke(____factionTracker, new object[] { trackedElement });

                            // And immediately request an update (which sometimes doesn't trigger automatically because of paused state)
                            //MethodInfo ___UpdateData = typeof(UIModuleFactionAgendaTracker).GetMethod("UpdateData", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
                            //___UpdateData.Invoke(____factionTracker, null);
                        }
                    }
                    ___UpdateData.Invoke(____factionTracker, null);



                    if (AssortedAdjustments.Settings.AgendaTrackerAddMissionObjective)
                    {
                        AddAncientSiteEncounterObjective(faction, excavation);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // REMOVES the vehicle related item when an aircraft reached its destination
        [HarmonyPatch(typeof(UIStateVehicleSelected), "OnVehicleArrived")]
        public static class UIStateVehicleSelected_OnVehicleArrived_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker && AssortedAdjustments.Settings.AgendaTrackerShowVehicles;
            }

            public static void Postfix(UIStateVehicleSelected __instance, GeoVehicle vehicle, bool justPassing)
            {
                try
                {
                    if (justPassing)
                    {
                        return;
                    }

                    UIModuleFactionAgendaTracker ____factionTracker = (UIModuleFactionAgendaTracker)AccessTools.Property(typeof(UIStateVehicleSelected), "_factionTracker").GetValue(__instance, null);
                    List<UIFactionDataTrackerElement> ____currentTrackedElements = (List<UIFactionDataTrackerElement>)AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements").GetValue(____factionTracker);

                    foreach (UIFactionDataTrackerElement trackedElement in ____currentTrackedElements)
                    {
                        // Remove
                        if (trackedElement.TrackedObject == vehicle)
                        {
                            Logger.Debug($"[UIStateVehicleSelected_OnVehicleArrived_POSTFIX] {vehicle.Name} is tracked. Removing.");

                            // Dispose
                            //MethodInfo ___Dispose = typeof(UIModuleFactionAgendaTracker).GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance);



                            ___Dispose.Invoke(____factionTracker, new object[] { trackedElement });

                            // And immediately request an update (which sometimes doesn't trigger automatically because of paused state)

                        }
                    }
                    ___UpdateData.Invoke(____factionTracker, null);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        // REMOVES the vehicle related item when an aircraft finished exploration of a site
        [HarmonyPatch(typeof(UIStateVehicleSelected), "OnVehicleSiteExplored")]
        public static class UIStateVehicleSelected_OnVehicleSiteExplored_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker && AssortedAdjustments.Settings.AgendaTrackerShowVehicles;
            }

            public static void Postfix(UIStateVehicleSelected __instance, GeoVehicle vehicle)
            {
                try
                {
                    UIModuleFactionAgendaTracker ____factionTracker = (UIModuleFactionAgendaTracker)AccessTools.Property(typeof(UIStateVehicleSelected), "_factionTracker").GetValue(__instance, null);
                    List<UIFactionDataTrackerElement> ____currentTrackedElements = (List<UIFactionDataTrackerElement>)AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements").GetValue(____factionTracker);

                    foreach (UIFactionDataTrackerElement trackedElement in ____currentTrackedElements)
                    {
                        // Remove
                        if (trackedElement.TrackedObject == vehicle)
                        {
                            Logger.Debug($"[UIStateVehicleSelected_OnVehicleSiteExplored_POSTFIX] {vehicle.Name} is tracked. Removing.");

                            // Dispose
                            //MethodInfo ___Dispose = typeof(UIModuleFactionAgendaTracker).GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance);
                            ___Dispose.Invoke(____factionTracker, new object[] { trackedElement });

                            // And immediately request an update (which sometimes doesn't trigger automatically because of paused state)
                            //MethodInfo ___UpdateData = typeof(UIModuleFactionAgendaTracker).GetMethod("UpdateData", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
                            //___UpdateData.Invoke(____factionTracker, null);
                        }
                    }
                    ___UpdateData.Invoke(____factionTracker, null);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }



        /*
         **
         *** Patches that MAINTAIN the new types of tracker items throughout their lifetime
         ** 
        */

        // Updates time left of the various tracker item types and adds mouse events.
        // NOTE that the tracker items get reused multiple times for different tracked objects and we NEED to reinitialize EVERYTHING ALWAYS
        [HarmonyPatch(typeof(UIModuleFactionAgendaTracker), "UpdateData", new Type[] { typeof(UIFactionDataTrackerElement) })]
        public static class UIModuleFactionAgendaTracker_UpdateData_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker;
            }

            public static bool Prefix(UIModuleFactionAgendaTracker __instance, ref bool __result, UIFactionDataTrackerElement element, GeoFaction ____faction, GeoscapeViewContext ____context)
            {
                try
                {
                    if (element.TrackedObject is ResearchElement)
                    {
                        // Add click event to the item that focuses camera on the tracked object
                        GameObject go = element.gameObject;
                        if (!go.GetComponent<EventTrigger>())
                        {
                            go.AddComponent<EventTrigger>();
                        }
                        EventTrigger eventTrigger = go.GetComponent<EventTrigger>();
                        eventTrigger.triggers.Clear();
                        EventTrigger.Entry click = new EventTrigger.Entry();
                        click.eventID = EventTriggerType.PointerClick;
                        click.callback.AddListener((eventData) =>
                        {
                            ____context.Level.Timing.Paused = true;
                            ____context.View.ToResearchState();
                        });
                        eventTrigger.triggers.Add(click);
                    }

                    else if (element.TrackedObject is ItemManufacturing.ManufactureQueueItem)
                    {
                        // Add click event to the item that focuses camera on the tracked object
                        GameObject go = element.gameObject;
                        if (!go.GetComponent<EventTrigger>())
                        {
                            go.AddComponent<EventTrigger>();
                        }
                        EventTrigger eventTrigger = go.GetComponent<EventTrigger>();
                        eventTrigger.triggers.Clear();
                        EventTrigger.Entry click = new EventTrigger.Entry();
                        click.eventID = EventTriggerType.PointerClick;
                        click.callback.AddListener((eventData) =>
                        {
                            ____context.Level.Timing.Paused = true;
                            ____context.View.ToManufacturingState(null, null, StateStackAction.ClearStackAndPush);
                        });
                        eventTrigger.triggers.Add(click);
                    }

                    else if (element.TrackedObject is GeoVehicle vehicle)
                    {
                        // Add click event to the item that focuses camera on the tracked object
                        GameObject go = element.gameObject;
                        if (!go.GetComponent<EventTrigger>())
                        {
                            go.AddComponent<EventTrigger>();
                        }
                        EventTrigger eventTrigger = go.GetComponent<EventTrigger>();
                        eventTrigger.triggers.Clear();
                        EventTrigger.Entry click = new EventTrigger.Entry();
                        click.eventID = EventTriggerType.PointerClick;
                        click.callback.AddListener((eventData) => { ____context.View.ChaseTarget(vehicle, false); });
                        eventTrigger.triggers.Add(click);

                        if (vehicle.Travelling && GetTravelTime(vehicle, out float travelTime))
                        {
                            TimeUnit arrivalTime = TimeUnit.FromHours(travelTime);
                            //Logger.Info($"[UIModuleFactionAgendaTracker_UpdateData_PREFIX] element.TrackedObject: {element.TrackedObject}, arrivalTime: {arrivalTime}");

                            element.UpdateData(arrivalTime, true, null);
                            __result = arrivalTime <= TimeUnit.Zero;
                        }
                        else if (vehicle.IsExploringSite)
                        {
                            float explorationTimeHours = GetExplorationTime(vehicle, (float)vehicle.CurrentSite.ExplorationTime.TimeSpan.TotalHours);
                            TimeUnit explorationTimeEnd = TimeUnit.FromHours(explorationTimeHours);
                            //Logger.Info($"[UIModuleFactionAgendaTracker_UpdateData_PREFIX] element.TrackedObject: {element.TrackedObject}, explorationTimeEnd: {explorationTimeEnd}");

                            element.UpdateData(explorationTimeEnd, true, null);
                            __result = explorationTimeEnd <= TimeUnit.Zero;
                        }

                        return false;
                    }

                    else if (element.TrackedObject is GeoPhoenixFacility facility)
                    {
                        // Add click event to the item that focuses camera on the tracked object
                        GameObject go = element.gameObject;
                        if (!go.GetComponent<EventTrigger>())
                        {
                            go.AddComponent<EventTrigger>();
                        }
                        EventTrigger eventTrigger = go.GetComponent<EventTrigger>();
                        eventTrigger.triggers.Clear();
                        EventTrigger.Entry click = new EventTrigger.Entry();
                        click.eventID = EventTriggerType.PointerClick;
                        click.callback.AddListener((eventData) => { ____context.View.ChaseTarget(facility.PxBase.Site, false); });
                        eventTrigger.triggers.Add(click);

                        if (facility.IsRepairing)
                        {
                            TimeUnit repairsCarriedOut = facility.GetTimeLeftToUpdate();
                            //Logger.Info($"[UIModuleFactionAgendaTracker_UpdateData_PREFIX] element.TrackedObject: {element.TrackedObject}, repairsCarriedOut: {repairsCarriedOut}");

                            element.UpdateData(repairsCarriedOut, true, null);
                            __result = repairsCarriedOut <= TimeUnit.Zero;

                            return false;
                        }
                    }

                    else if (element.TrackedObject is GeoSite geoSite)
                    {
                        // Add click event to the item that focuses camera on the tracked object
                        GameObject go = element.gameObject;
                        if (!go.GetComponent<EventTrigger>())
                        {
                            go.AddComponent<EventTrigger>();
                        }
                        EventTrigger eventTrigger = go.GetComponent<EventTrigger>();
                        eventTrigger.triggers.Clear();
                        EventTrigger.Entry click = new EventTrigger.Entry();
                        click.eventID = EventTriggerType.PointerClick;
                        click.callback.AddListener((eventData) => { ____context.View.ChaseTarget(geoSite, false); });
                        eventTrigger.triggers.Add(click);



                        SiteExcavationState siteExcavationState = geoSite.GeoLevel.PhoenixFaction.ExcavatingSites.FirstOrDefault((SiteExcavationState s) => s.Site == geoSite);
                        TimeUnit excavationTimeEnd = TimeUnit.FromHours((float)(siteExcavationState.ExcavationEndDate - ____context.Level.Timing.Now).TimeSpan.TotalHours);
                        //Logger.Info($"[UIModuleFactionAgendaTracker_UpdateData_PREFIX] element.TrackedObject: {element.TrackedObject}, excavationTimeEnd: {excavationTimeEnd}");

                        element.UpdateData(excavationTimeEnd, true, null);
                        __result = excavationTimeEnd <= TimeUnit.Zero;

                        return false;
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    return true;
                }
            }
        }

        // A "de facto" INIT that is called on aircraft switching or (re-)entering UIStateVehicleSelected
        [HarmonyPatch(typeof(UIModuleFactionAgendaTracker), "InitialSetup")]
        public static class UIModuleFactionAgendaTracker_InitialSetup_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker;
            }

            // Store UIFactionDataTrackerElement prefab to be able to reset visuals of the items in UIFactionDataTrackerElement.Init()
            public static void Prefix(UIModuleFactionAgendaTracker __instance)
            {
                trackerElementDefault = __instance.TrackerRowPrefab;
            }

            public static void Postfix(UIModuleFactionAgendaTracker __instance, GeoFaction ____faction)
            {
                if (____faction is GeoPhoenixFaction geoPhoenixFaction)
                {
                    //MethodInfo ___GetFreeElement = typeof(UIModuleFactionAgendaTracker).GetMethod("GetFreeElement", BindingFlags.NonPublic | BindingFlags.Instance);
                    //MethodInfo ___OnAddedElement = typeof(UIModuleFactionAgendaTracker).GetMethod("OnAddedElement", BindingFlags.NonPublic | BindingFlags.Instance);

                    // Vehicles
                    if (AssortedAdjustments.Settings.AgendaTrackerShowVehicles)
                    {
                        foreach (GeoVehicle vehicle in geoPhoenixFaction.Vehicles.Where(v => v.Travelling || v.IsExploringSite))
                        {
                            Logger.Debug($"[UIModuleFactionAgendaTracker_InitialSetup_POSTFIX] Adding/Reapplying vehicle related tracker elements.");

                            UIFactionDataTrackerElement freeElement = (UIFactionDataTrackerElement)___GetFreeElement.Invoke(__instance, null);

                            string siteName = "ERR";
                            string vehicleInfo = "ERR";
                            if (vehicle.Travelling)
                            {
                                siteName = GetSiteName(vehicle.FinalDestination, vehicle.Owner);
                                vehicleInfo = $"{vehicle.Name} {actionTraveling} {siteName}";
                            }
                            else if (vehicle.IsExploringSite)
                            {
                                siteName = GetSiteName(vehicle.CurrentSite, vehicle.Owner);
                                vehicleInfo = $"{vehicle.Name} {actionExploring} {siteName}";
                            }

                            freeElement.Init(vehicle, vehicleInfo, vehicle.VehicleDef.ViewElement, false);
                            //freeElement.Init(vehicle, vehicleInfo, null, false);

                            ___OnAddedElement.Invoke(__instance, new object[] { freeElement });
                        }
                    }

                    // Facilities under repair
                    IEnumerable<GeoPhoenixFacility> facilitiesUnderRepair = geoPhoenixFaction.Bases.SelectMany((GeoPhoenixBase t) => from f in t.Layout.Facilities where f.IsRepairing && f.GetTimeLeftToUpdate() != TimeUnit.Zero select f);
                    foreach (GeoPhoenixFacility facility in facilitiesUnderRepair)
                    {
                        UIFactionDataTrackerElement freeElement = (UIFactionDataTrackerElement)___GetFreeElement.Invoke(__instance, null);

                        string facilityName = I2.Loc.LocalizationManager.GetTranslation(facility.Def.ViewElementDef.DisplayName1.LocalizationKey);
                        string facilityInfo = $"{actionRepairing} {facilityName}";
                        freeElement.Init(facility, facilityInfo, facility.Def.ViewElementDef, false);

                        ___OnAddedElement.Invoke(__instance, new object[] { freeElement });
                    }

                    // Excavations in progress
                    if (AssortedAdjustments.Settings.AgendaTrackerShowExcavations)
                    {
                        IEnumerable<SiteExcavationState> excavatingSites = geoPhoenixFaction.ExcavatingSites.Where(s => !s.IsExcavated);
                        foreach (SiteExcavationState excavatingSite in excavatingSites)
                        {
                            UIFactionDataTrackerElement freeElement = (UIFactionDataTrackerElement)___GetFreeElement.Invoke(__instance, null);

                            string siteName = excavatingSite.Site.Name;
                            string excavationInfo = $"{actionExcavating} {siteName}";

                            // Without a viewdef there are... problems. Therefore we borrow one with the correct icon
                            ViewElementDef borrowedViewElementDef = GameUtl.GameComponent<DefRepository>().DefRepositoryDef.AllDefs.OfType<ViewElementDef>().Where(def => def.name.Contains("ArcheologyLab_PhoenixFacilityDef")).FirstOrDefault();
                            freeElement.Init(excavatingSite.Site, excavationInfo, borrowedViewElementDef, false);

                            ___OnAddedElement.Invoke(__instance, new object[] { freeElement });
                        }
                    }
                }
            }
        }

        // Visual adjustments of the tracker items
        // NOTE that for custom items (vehicles and excavations so far) the text gets overridden from passed parameter too
        [HarmonyPatch(typeof(UIFactionDataTrackerElement), "Init")]
        public static class UIFactionDataTrackerElement_Init_Patch
        {
            public static bool Prepare()
            {
                return AssortedAdjustments.Settings.EnableExtendedAgendaTracker;
            }

            public static void Postfix(UIFactionDataTrackerElement __instance, object objToTrack, string text, ViewElementDef def, bool localize)
            {
                try
                {
                    //Logger.Debug($"[UIFactionDataTrackerElement_Init_POSTFIX] objToTrack: {objToTrack}, text: {text}");

                    __instance.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                    __instance.TrackedTime.alignment = TextAnchor.MiddleRight;
                    
                    __instance.TrackedTime.fontSize = 36;
                    __instance.TrackedName.fontSize = 36;

                    // As the elements are re-used and NOT recreated they have to be reset to their default visuals first.
                    // Otherwise, if no condition below is true they retain the colors of their old item, resulting in wrongly colored elements!
                    if (trackerElementDefault != null)
                    {
                        __instance.TrackedName.color = trackerElementDefault.TrackedName.color;
                        __instance.TrackedTime.color = trackerElementDefault.TrackedTime.color;
                        __instance.Icon.color = trackerElementDefault.Icon.color;
                    }
                    else
                    {
                        __instance.TrackedName.color = defaultTrackerColor;
                        __instance.TrackedTime.color = defaultTrackerColor;
                        __instance.Icon.color = defaultTrackerColor;
                    }

                    if (__instance.TrackedObject is GeoVehicle vehicle)
                    {
                        __instance.TrackedName.text = text; // Always use passed text for non-default elements as def disturbs it
                        __instance.TrackedName.color = vehicleTrackerColor;
                        __instance.TrackedTime.color = vehicleTrackerColor;
                        __instance.Icon.color = vehicleTrackerColor;

                        // Borrowed from UIModuleInfoBar, fetched at UIModuleInfoBar.Init()
                        __instance.Icon.sprite = aircraftSprite;
                    }
                    else if(__instance.TrackedObject is ResearchElement re)
                    {
                        __instance.TrackedName.color = researchTrackerColor;
                        __instance.TrackedTime.color = researchTrackerColor;
                        __instance.Icon.color = researchTrackerColor;
                    }
                    else if (__instance.TrackedObject is ItemManufacturing.ManufactureQueueItem mqi)
                    {
                        __instance.TrackedName.color = manufactureTrackerColor;
                        __instance.TrackedTime.color = manufactureTrackerColor;
                        __instance.Icon.color = manufactureTrackerColor;
                    }
                    else if (__instance.TrackedObject is GeoPhoenixFacility gpf)
                    {
                        if (gpf.IsRepairing)
                        {
                            __instance.TrackedName.text = text; // Always use passed text for non-default elements as def disturbs it
                        }

                        //__instance.TrackedName.color = facilityTrackerColor;
                        //__instance.TrackedTime.color = facilityTrackerColor;
                        //__instance.Icon.color = facilityTrackerColor;
                    }
                    else if (__instance.TrackedObject is GeoSite gs)
                    {
                        __instance.TrackedName.text = text; // Always use passed text for non-default elements as def disturbs it
                        __instance.TrackedName.color = excavationTrackerColor;
                        __instance.TrackedTime.color = excavationTrackerColor;
                        __instance.Icon.color = excavationTrackerColor;

                        // Borrowed from UIModuleInfoBar, fetched at UIModuleInfoBar.Init()
                        //__instance.Icon.sprite = excavateIcon.sprite;
                        //__instance.Icon.sprite = excavateSprite;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }
    }
}
