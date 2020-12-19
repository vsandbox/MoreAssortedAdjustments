﻿using System.Collections.Generic;
using System.Linq;
using Base.Core;
using Base.Defs;
using PhoenixPoint.Geoscape.Entities;

namespace AssortedAdjustments.Patches
{
    internal static class VehicleAdjustments
    {
        public static void Apply()
        {
            DefRepository defRepository = GameUtl.GameComponent<DefRepository>();

            List<GeoVehicleDef> geoVehicleDefs = defRepository.DefRepositoryDef.AllDefs.OfType<GeoVehicleDef>().ToList();
            foreach (GeoVehicleDef gvDef in geoVehicleDefs)
            {
                if (gvDef.name.Contains("Blimp"))
                {
                    gvDef.BaseStats.Speed.Value = AssortedAdjustments.Settings.AircraftBlimpSpeed;
                    gvDef.BaseStats.SpaceForUnits = AssortedAdjustments.Settings.AircraftBlimpSpace;
                }
                else if (gvDef.name.Contains("Thunderbird"))
                {
                    gvDef.BaseStats.Speed.Value = AssortedAdjustments.Settings.AircraftThunderbirdSpeed;
                    gvDef.BaseStats.SpaceForUnits = AssortedAdjustments.Settings.AircraftThunderbirdSpace;
                }
                else if (gvDef.name.Contains("Manticore"))
                {
                    gvDef.BaseStats.Speed.Value = AssortedAdjustments.Settings.AircraftManticoreSpeed;
                    gvDef.BaseStats.SpaceForUnits = AssortedAdjustments.Settings.AircraftManticoreSpace;
                }
                else if (gvDef.name.Contains("Helios"))
                {
                    gvDef.BaseStats.Speed.Value = AssortedAdjustments.Settings.AircraftHeliosSpeed;
                    gvDef.BaseStats.SpaceForUnits = AssortedAdjustments.Settings.AircraftHeliosSpace;
                }

                Logger.Info($"[VehicleAdjustments_Apply] gvDef: {gvDef.name}, GUID: {gvDef.Guid}, Speed: {gvDef.BaseStats.Speed.Value}, SpaceForUnits: {gvDef.BaseStats.SpaceForUnits}");
            }
        }
    }
}