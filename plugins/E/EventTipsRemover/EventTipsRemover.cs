using System.Collections.Generic;
using System.Collections;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("Event Tips Remover", "Razor", "1.0.4")]
    [Description("Remove new GameTip messages.")]
    public class EventTipsRemover : RustPlugin
    {
        List<PuzzleReset> puzzleReset = new List<PuzzleReset>();
        List<TriggeredEventPrefab> _triggeredEventPrefab = new List<TriggeredEventPrefab>();

        #region Init
        private void OnServerInitialized()
        {
            //This Was The Only Way To Find PuzzleReset as no hooks available currenty in DoReset()
            foreach (var GateToRemove in GameObject.FindObjectsOfType<PuzzleReset>())
            {
                if (GateToRemove.broadcastResetMessage)
                {
                    GateToRemove.broadcastResetMessage = false;
                    puzzleReset.Add(GateToRemove);
                }
            }
        }

        private void Unload()
        {
            foreach (var GateToRemove in puzzleReset)
            {
                if (GateToRemove != null)
                    GateToRemove.broadcastResetMessage = true;
            }

            foreach (var GateToRemove in _triggeredEventPrefab)
            {
                if (GateToRemove != null)
                    GateToRemove.shouldBroadcastSpawn = true;
            }
        }
        #endregion

        #region Hooks
        private void OnEventTrigger(TriggeredEventPrefab eventTrig)
        {
            if (eventTrig.shouldBroadcastSpawn)
            {
                if (!_triggeredEventPrefab.Contains(eventTrig))
                    _triggeredEventPrefab.Add(eventTrig);
                eventTrig.shouldBroadcastSpawn = false;
            }
        }

        private object OnExcavatorResourceSet(ExcavatorArm arm, string str, BasePlayer player)
        {
            if (str == "HQM")
                arm.resourceMiningIndex = 0;
            else if (str == "Sulfur")
                arm.resourceMiningIndex = 1;
            else if (str == "Stone")
                arm.resourceMiningIndex = 2;
            else if (str == "Metal")
                arm.resourceMiningIndex = 3;
            if (arm.IsOn())
                return null;
            BeginMining(arm);

            return false;
        }

        public void BeginMining(ExcavatorArm arm)
        {
            if (!arm.IsPowered())
                return;
            arm.SetFlag(BaseEntity.Flags.On, true);
            arm.InvokeRepeating(new Action(arm.ProduceResources), arm.resourceProductionTickRate, arm.resourceProductionTickRate);

            ExcavatorServerEffects.SetMining(true);
            Interface.CallHook("OnExcavatorMiningToggled", (object)arm);
        }
        #endregion

    }
}
    
