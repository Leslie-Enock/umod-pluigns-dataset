using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Stash Hider", "birthdates", "1.1.0")]
    [Description("Don't network stashes when they're hidden")]
    public class StashHider : RustPlugin
    {
        #region Variables

        [PluginReference]
        private readonly Plugin AutomatedStashTraps;

        private int LayerMask { get; } = UnityEngine.LayerMask.GetMask("Deployed");

        #endregion
        
        #region Hooks

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!input.WasJustPressed(BUTTON.USE) || Core.Random.Range(0, 6) != 0) return;
            var ray = player.eyes.HeadRay();
            RaycastHit hit;
            if (!Physics.Raycast(ray.origin, ray.direction, out hit, 3.0f, LayerMask, QueryTriggerInteraction.Ignore)) return;
            var entity = hit.GetEntity() as StashContainer;
            if (entity == null) return;
            entity.RPC_WantsUnhide(new BaseEntity.RPCMessage{player = player});
        }
        
        private void OnStashHidden(StashContainer stash, BasePlayer player)
        {
            // Wait for animation
            timer.In(1f, () => HideStash(stash));
        }

        private static void HideStash(StashContainer stash)
        {
            if (!stash.IsHidden()) return;
            stash.limitNetworking = true;
            stash.TerminateOnClient(BaseNetworkable.DestroyMode.None);
        }

        private static void ShowStash(StashContainer stash)
        {
            stash.limitNetworking = false;
            stash.SendNetworkUpdateImmediate();
            if (!stash.IsHidden()) return;
            stash.SetHidden(false); // Try play animation?
        }

        private object CanSeeStash(BasePlayer player, StashContainer stashContainer)
        {
            ShowStash(stashContainer);
            NextTick(() => stashContainer.SendNetworkUpdateImmediate());
            return null;
        }
        

        private void Unload()
        {
            foreach (var stash in GetStashes())
            {
                ShowStash(stash);
            }
        }

        private static IEnumerable<StashContainer> GetStashes()
        {
            return BaseNetworkable.serverEntities.entityList.Values.OfType<StashContainer>();
        }

        private void OnServerInitialized()
        {
            foreach (var entity in GetStashes())
            {
                if (!entity.IsHidden() || StashIsAutomatedTrap(entity)) continue;
                HideStash(entity);
            }
        }

        #endregion

        #region Helper Functions

        private bool PluginIsLoaded(Plugin plugin)
        {
            return plugin != null && plugin.IsLoaded ? true : false;
        }
        
        private bool StashIsAutomatedTrap(StashContainer stash)
        {
            if (PluginIsLoaded(AutomatedStashTraps))
                return AutomatedStashTraps.Call<bool>("StashIsAutomatedTrap", stash);
            else
                // Lang: Plugin is not loaded
            return false;
        }

        #endregion
    }
}