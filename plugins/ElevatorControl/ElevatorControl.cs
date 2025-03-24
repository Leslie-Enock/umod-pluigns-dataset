using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using ProtoBuf;
using UnityEngine;
/* 
Thanks to Magma Networks for sponsoring this plugin!
magmanetworks.cc
*/
namespace Oxide.Plugins {
    [Info ("Elevator Control", "ghostr", "1.0.2")]
    [Description ("Change elevator settings")]

    public class ElevatorControl : RustPlugin {
        private Configuration config;

        public class Configuration {
            [JsonProperty (PropertyName = "ElevatorSpeed")]
            public float ElevatorSpeed { get; set; } = 1f;
            [JsonProperty (PropertyName = "DoesNeedPower")]
            public bool DoesNeedPower { get; set; } = true;
        }

        protected override void LoadConfig () {
            base.LoadConfig ();
            try {
                config = Config.ReadObject<Configuration> ();
                if (config == null) {
                    throw new JsonException ();
                }
            } catch {
                string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}";
                Puts ($"Could not load a valid configuration file, creating a new configuration file at {configPath}.json");
                Config.WriteObject (config, false, $"{configPath}_invalid.json");
                LoadDefaultConfig ();
            }
            SaveConfig ();
        }

        protected override void LoadDefaultConfig () => config = new Configuration ();

        protected override void SaveConfig () => Config.WriteObject (config);

        private void OnServerInitialized () {
            List<Elevator> elevators = Resources.FindObjectsOfTypeAll<Elevator> ().Where (b => b.PrefabName == "assets/prefabs/deployable/elevator/elevator.prefab").ToList ();
            foreach (Elevator elevator in elevators)
                elevator.LiftSpeedPerMetre = config.ElevatorSpeed;
        }

        void OnEntitySpawned (BaseNetworkable entity) {
            var Entity = entity as BaseEntity;
            if (Entity == null) return;
            if (Entity.name == "assets/prefabs/deployable/elevator/elevator.prefab") {
                timer.Once (.1f, () => { //This is to allow elevator to exist
                    Elevator elevator = Entity as Elevator;
                    if (!config.DoesNeedPower) {
                        IOEntity IOChild = null;
                        foreach (BaseEntity child in elevator.children) {
                            IOEntity oEntity = child as IOEntity;
                            IOEntity oEntity1 = oEntity;
                            if (oEntity == null) { continue; }
                            IOChild = oEntity1; //Same code the game uses to find the proper child 
                        }

                        IOChild.SetFlag (BaseEntity.Flags.Reserved8, true); //Sets flag 8(IsPowered) to true
                    }
                    elevator.LiftSpeedPerMetre = config.ElevatorSpeed;
                });
            }
        }

    }
}