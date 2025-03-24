using HarmonyLib;
using Oxide.Core.Plugins;
using Rust.Ai.Gen2;
using static Rust.Ai.Gen2.FSMComponent;

namespace Oxide.Plugins
{
    [Info("Remove Animals AI", "Whipers88 @CobaltStudios", "1.1.1")]
    [Description("Removing AI only for animals (not for bots)")]
    public class RemoveAnimalsAI : RustPlugin
    {
        private void Init()
        {
            FSMComponent.workQueue.Clear();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is BaseNPC2)
                {
                    entity.Kill();
                }
                if(entity is BaseAnimalNPC baseAnimalNPC)
                {
                    if(baseAnimalNPC.HasBrain && baseAnimalNPC.TryGetComponent<BaseAIBrain>(out BaseAIBrain baseAIBrain))
                    {
                        UnityEngine.Object.Destroy(baseAIBrain);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BaseNpc), nameof(BaseNpc.TickAi)), AutoPatch]
        public static class TickAiPatch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                return false;
            }
        }
        [HarmonyPatch(typeof(LimitedTurnNavAgent), nameof(LimitedTurnNavAgent.TickSteering)), AutoPatch]
        public static class TickSteeringPatch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(TickFSMWorkQueue), "RunJob"), AutoPatch]
        public static class RunJobPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(FSMComponent component)
            {
                if (component == null)
                    return false;

                if (!component.enabled)
                    return false;

                if (component._baseEntity?.Health() != 0)
                {
                    return false;
                }
                component.Tick();
                FSMComponent.workQueue.Remove(component);
                return false;
            }
        }

        [HarmonyPatch(typeof(AIThinkManager), nameof(AIThinkManager.ProcessQueue)), AutoPatch]
        public static class ProcessQueuePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(AIThinkManager.QueueType queueType)
            {
                if (queueType == AIThinkManager.QueueType.Animal)
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(AnimalBrain), nameof(AnimalBrain.InitializeAI)), AutoPatch]
        public static class InitializeAIPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(BaseAIBrain __instance)
            {
                return false;
            }
        }

    }
}