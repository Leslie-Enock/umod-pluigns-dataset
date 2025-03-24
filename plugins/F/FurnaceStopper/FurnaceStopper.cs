using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Furnace Stopper", "Orange", "1.0.7")]
    [Description("Stops smelting in furnaces if there are nothing to smelt")]
    public class FurnaceStopper : RustPlugin
    {
        #region Vars

        private const float checkRate = 3f;
        private const string permUse = "furnacestopper.use";

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        private void Unload()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsOfType<StopperScript>())
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        private void OnOvenToggle(BaseOven entity, BasePlayer player)
        {
            if (permission.UserHasPermission(entity.OwnerID.ToString(), permUse) == true)
            {
                if (entity.inventory.itemList.Any(x => x.info.GetComponent<ItemModCookable>() != null) == true)
                {
                    NextTick(() =>
                    {
                        if (entity.IsValid() == false)
                        {
                            return;
                        }

                        var component = entity.GetOrAddComponent<StopperScript>();
                        component.StateChanged();
                    });
                }
            }
        }

        #endregion

        #region Scripts

        private class StopperScript : MonoBehaviour
        {
            private BaseOven oven;

            private void Awake()
            {
                oven = GetComponent<BaseOven>();
            }

            public void StateChanged()
            {
                if (oven.IsOn() == true)
                {
                    if (IsInvoking(nameof(Check)) == false)
                    {
                        InvokeRepeating(nameof(Check), checkRate, checkRate);
                    }
                }
                else
                {
                    CancelInvoke(nameof(Check));
                }
            }

            private void Check()
            {
                if (oven.IsOn() == false)
                {
                    CancelInvoke(nameof(Check));
                    return;
                }

                if (oven.inventory.itemList.Any(x => x.info.GetComponent<ItemModCookable>() != null) == false)
                {
                    CancelInvoke(nameof(Check));
                    oven.StopCooking();
                }
            }
        }

        #endregion
    }
}