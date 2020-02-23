using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Autowatering
{
    internal class ModEntry : Mod
    {
        private readonly HashSet<HoeDirt> _hoeDirts = new HashSet<HoeDirt>();
        private readonly HashSet<IndoorPot> _indoorPots = new HashSet<IndoorPot>();
        private ModConfig _config;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.World.TerrainFeatureListChanged += OnTerrainFeatureListChanged;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (e.Button == _config.ConfigReloadKey)
            {
                _config = Helper.ReadConfig<ModConfig>();
                Monitor.Log("Config file reloaded", LogLevel.Warn);
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            var builtLocations = Game1.locations.OfType<BuildableGameLocation>()
                .SelectMany(location => location.buildings)
                .Select(building => building.indoors.Value)
                .Where(location => location != null);
            var allLocations = Game1.locations.Concat(builtLocations);

            foreach (var location in allLocations)
            {
                foreach (var indoorPot in location.objects.Values.OfType<IndoorPot>())
                    SetupIndoorPotListeners(indoorPot);

                foreach (var hoeDirt in location.terrainFeatures.Values.OfType<HoeDirt>())
                    SetupHoeDirtListeners(hoeDirt);
            }
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            foreach (var hoeDirt in _hoeDirts)
                Water(hoeDirt);
            foreach (var indoorPot in _indoorPots)
                Water(indoorPot.hoeDirt.Value, indoorPot);
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            foreach (var kvp in e.Added)
            {
                var obj = kvp.Value;
                if (obj is IndoorPot indoorPot)
                    SetupIndoorPotListeners(indoorPot);
            }
        }

        private void OnTerrainFeatureListChanged(object sender, TerrainFeatureListChangedEventArgs e)
        {
            foreach (var kvp in e.Added)
            {
                var feature = kvp.Value;
                if (feature is HoeDirt hoeDirt)
                    SetupHoeDirtListeners(hoeDirt);
            }
        }

        private void SetupIndoorPotListeners(IndoorPot indoorPot)
        {
            SetupHoeDirtListeners(indoorPot.hoeDirt.Value, indoorPot);
        }

        private void SetupHoeDirtListeners(HoeDirt hoeDirt, IndoorPot pot = null)
        {
            try
            {
                var netCrop = Helper.Reflection.GetField<NetRef<Crop>>(hoeDirt, "netCrop", true).GetValue();
                if (netCrop.Value != null)
                    TrackAndWater(hoeDirt, pot);
                netCrop.fieldChangeVisibleEvent += (_, __, crop) =>
                {
                    if (crop != null)
                        TrackAndWater(hoeDirt, pot);
                    else
                        RemoveTrack(hoeDirt, pot);
                };
            }
            catch (Exception ex)
            {
                Monitor.Log($"Could not read crop data on dirt; possible new game version?\n{ex}", LogLevel.Error);
            }
        }

        private void TrackAndWater(HoeDirt hoeDirt, IndoorPot indoorPot = null)
        {
            if (indoorPot != null)
                _indoorPots.Add(indoorPot);
            else
                _hoeDirts.Add(hoeDirt);
            Water(hoeDirt, indoorPot);
        }

        private void RemoveTrack(HoeDirt hoeDirt, IndoorPot indoorPot = null)
        {
            if (indoorPot != null)
                _indoorPots.Remove(indoorPot);
            else
                _hoeDirts.Remove(hoeDirt);
        }

        private void Water(HoeDirt hoeDirt, IndoorPot pot = null)
        {
            if (!_config.Enabled)
                return;

            hoeDirt.state.Value = HoeDirt.watered;
            if (_config.Fertilizer.HasValue)
                hoeDirt.fertilizer.Value = _config.Fertilizer.Value;
            if (pot != null)
                pot.showNextIndex.Value = true;
        }
    }
}
