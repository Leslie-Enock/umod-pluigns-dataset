/*
 * Copyright (c) 2023 Bazz3l
 *
 * Moveable CCTV a cannot be copied, edited and/or (re)distributed without the express permission of Bazz3l.
 * Discord bazz3l
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 *
 */

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Movable CCTV", "Bazz3l", "1.1.2")]
    [Description("Allow players to control placed cctv cameras using WASD")]
    internal class MovableCCTV : CovalencePlugin
    {
        #region Fields

        private const string PERM_USE = "movablecctv.use";
        private static MovableCCTV _plugin;
        private PluginConfig _config;

        #endregion

        #region Config

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null) throw new JsonException();
                
                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("Config was updated");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning("Invalid config, default config has been loaded.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private class PluginConfig
        {
            public float RotateSpeed = 0.2f;
            public string TextColor = "1 1 1 0.5";
            public int TextSize = 14;
            public string AnchorMin = "0.293 0.903";
            public string AnchorMax = "0.684 0.951";

            public string ToJson() => JsonConvert.SerializeObject(this);
            
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }
        
        #endregion

        #region Lang
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Description", "Control the camera using WASD" }
            }, this);
        }
        
        #endregion

        #region Oxide Hooks

        private void OnServerInitialized() => CheckCctv();

        private void Init()
        {
            permission.RegisterPermission(PERM_USE, this);
            
            _plugin = this;
        }
        
        private void Unload()
        {
            CctvCamMover.RemoveAll();
            UI.RemoveAll();
            
            _plugin = null;
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var cctvRc = go.ToBaseEntity() as CCTV_RC;
            if (cctvRc != null && !cctvRc.IsStatic())
                cctvRc.hasPTZ = true;
        }

        private void OnBookmarkControlStarted(ComputerStation computerStation, BasePlayer player, string bookmarkName, IRemoteControllable entity)
        {
            if (!HasPermission(player))
                return;
            
            UI.RemoveUI(player);
            
            var cctvRc = entity as CCTV_RC;
            if (cctvRc == null|| cctvRc.IsStatic() || cctvRc.rcControls != RemoteControllableControls.None || BecomeMovableWasBlocked(cctvRc, player))
                return;
            
            player.GetOrAddComponent<CctvCamMover>();
            
            UI.CreateUI(player, Lang("Description", player.UserIDString));
        }
        
        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, CCTV_RC cctvRc)
        {
            if (!HasPermission(player))
                return;
            
            if (player.HasComponent<CctvCamMover>())
                player.GetComponent<CctvCamMover>()?.DestroyImmediate();
            
            UI.RemoveUI(player);
        }
        
        #endregion

        #region Cctv Cam Mover

        private void CheckCctv()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var cctv = entity as CCTV_RC;
                if (cctv != null && !cctv.IsStatic())
                    cctv.hasPTZ = true;
            }
        }
        
        private class CctvCamMover : MonoBehaviour
        {
            public static void RemoveAll()
            {
                foreach (var player in BasePlayer.activePlayerList)
                    player.GetComponent<CctvCamMover>()?.DestroyMe();
            }
            
            private ComputerStation _station;
            private BasePlayer _player;

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                _station = _player.GetMounted() as ComputerStation;
            }

            private void FixedUpdate()
            {
                var cctvRc = GetControlledCctv(_station);
                if (cctvRc == null || cctvRc.IsStatic())
                    return;

                var y = _player.serverInput.IsDown(BUTTON.FORWARD) ? 1f : (_player.serverInput.IsDown(BUTTON.BACKWARD) ? -1f : 0f);
                var x = _player.serverInput.IsDown(BUTTON.LEFT) ? -1f : (_player.serverInput.IsDown(BUTTON.RIGHT) ? 1f : 0f);

                var inputState = new InputState();
                inputState.current.mouseDelta.y = y * _plugin._config.RotateSpeed;
                inputState.current.mouseDelta.x = x * _plugin._config.RotateSpeed;
                
                cctvRc.UserInput(inputState, new CameraViewerId(_player.userID, 0));
            }

            public void DestroyMe() => Destroy(this);
            
            public void DestroyImmediate() => DestroyImmediate(this);

            private CCTV_RC GetControlledCctv(ComputerStation computerStation)
            {
                if (computerStation == null || computerStation.IsDestroyed)
                    return null;
                
                return computerStation.currentlyControllingEnt.Get(serverside: true) as CCTV_RC;
            }
        }
        
        #endregion

        #region UI

        private static class UI
        {
            private const string PANEL_NAME = "MovableCCTV";

            public static void CreateUI(BasePlayer player, string description = "")
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiPanel {
                    Image =
                    {
                        Color = "0 0 0 0"
                    },
                    RectTransform = {
                        AnchorMin = _plugin._config.AnchorMin,
                        AnchorMax = _plugin._config.AnchorMax
                    }
                }, "Overlay", PANEL_NAME);
                
                container.Add(new CuiLabel
                {
                    Text = {
                        FontSize = _plugin._config.TextSize,
                        Color = _plugin._config.TextColor,
                        Text  = description,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform = {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }, PANEL_NAME);
                
                CuiHelper.AddUi(player, container);
            }
            
            public static void RemoveUI(BasePlayer player) => CuiHelper.DestroyUi(player, PANEL_NAME);

            public static void RemoveAll()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    RemoveUI(player);
            }
        }
        
        #endregion

        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        private bool HasPermission(BasePlayer player) => permission.UserHasPermission(player.UserIDString, PERM_USE);
        
        private bool BecomeMovableWasBlocked(CCTV_RC cctvRc, BasePlayer player)
        {
            object hookResult = Interface.CallHook("OnCCTVMovableBecome", cctvRc, player);
            return hookResult is bool && (bool)hookResult == false;
        }
        
        #endregion
    }
}
