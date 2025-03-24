using System;
using System.Collections.Generic;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("OilRigDoorsFix", "MON@H", "1.0.2")]
    [Description("Fix for always open doors on Oil Rigs")]
    public class OilRigDoorsFix : RustPlugin
    {
        private uint _prefabIDCrate;
        private readonly uint[] _prefabIDs = new uint[3];

        #region Initialization

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            uint id;
            id = StringPool.Get("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate_oilrig.prefab");
            if (id == 0)
            {
                PrintError("codelockedhackablecrate_oilrig.prefab is not found!");
                return;
            }
            _prefabIDCrate = id;

            id = StringPool.Get("assets/bundled/prefabs/static/door.hinged.security.green.prefab");
            if (id == 0)
            {
                PrintError("door.hinged.security.green.prefab is not found!");
                return;
            }
            _prefabIDs[0] = id;

            id = StringPool.Get("assets/bundled/prefabs/static/door.hinged.security.blue.prefab");
            if (id == 0)
            {
                PrintError("door.hinged.security.blue.prefab is not found!");
                return;
            }
            _prefabIDs[1] = id;

            id = StringPool.Get("assets/bundled/prefabs/static/door.hinged.security.red.prefab");
            if (id == 0)
            {
                PrintError("door.hinged.security.red.prefab is not found!");
                return;
            }
            _prefabIDs[2] = id;

            Subscribe(nameof(OnEntitySpawned));
        }

        #endregion Initialization

        #region Oxide Hooks

        private void OnEntitySpawned(HackableLockedCrate crate)
        {
            if (crate.prefabID != _prefabIDCrate)
            {
                return;
            }

            List<PressButton> pressButtons = Pool.GetList<PressButton>();
            List<Door> doors = Pool.GetList<Door>();
            Vis.Entities(crate.transform.position, 5f, doors);

            foreach (Door door in doors)
            {
                if (!door.IsOpen() || !_prefabIDs.Contains(door.prefabID))
                {
                    continue;
                }

                pressButtons.Clear();
                Vis.Entities(door.transform.position, 2f, pressButtons);
                foreach (PressButton pressButton in pressButtons)
                {
                    pressButton.SetFlag(BaseEntity.Flags.On, true, false, true);
                    pressButton.Invoke(new Action(pressButton.UnpowerTime), pressButton.pressPowerTime);
                    pressButton.SetFlag(BaseEntity.Flags.Reserved3, true, false, true);
                    pressButton.SendNetworkUpdateImmediate(false);
                    pressButton.MarkDirty();
                    pressButton.Invoke(new Action(pressButton.Unpress), pressButton.pressDuration);
                }
            }

            Pool.FreeList(ref doors);
            Pool.FreeList(ref pressButtons);
        }

        #endregion Oxide Hooks
    }
}