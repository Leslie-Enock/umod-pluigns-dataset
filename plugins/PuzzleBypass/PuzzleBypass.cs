using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Puzzle Bypass", "Def", "1.0.0")]
    [Description("Allows players to pass through security doors using only a keycard")]
    public class PuzzleBypass : RustPlugin
    {
        #region Fields

        private Cfg _cfg;
        private static readonly Dictionary<uint, int> DoorToKeyLevelMap = new Dictionary<uint, int> { { 4094102585u, 1 }, { 184980835u, 2 }, { 4111973013u, 3 } };
        private static readonly List<string> KeyToLang = new List<string> { "CardGreen", "CardBlue", "CardRed" };

        #endregion

        #region Config

        private class Cfg
        {
            [JsonProperty("Door close delay, if opened from Outside (seconds)")]
            public int CloseDelayOutside;
            [JsonProperty("Door close delay, if opened from Inside (seconds)")]
            public int CloseDelayInside;
            [JsonProperty("How much condition key card will lose when used (default 1)")]
            public float LoseConditionAmt;
        }

        protected override void LoadDefaultConfig()
        {
            _cfg = new Cfg
            {
                CloseDelayOutside = 180,
                CloseDelayInside = 8,
                LoseConditionAmt = 1f
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _cfg = Config.ReadObject<Cfg>();
        }

        protected override void SaveConfig() => Config.WriteObject(_cfg);

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"CardGreen", "<color=#26BE00>green</color>"},
                {"CardBlue", "<color=#0094FF>blue</color>"},
                {"CardRed", "<color=#D12700>red</color>"},
                {"NoCard", "To open this door, you must hold a {0} key card."},
                {"InvalidCard", "This {0} card does not fit this door. You need a {1} card."},
                {"ExitHint", "Hint: To exit - knock again on this door."},
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"CardGreen", "<color=#26BE00>зелёная</color>"},
                {"CardBlue", "<color=#0094FF>синяя</color>"},
                {"CardRed", "<color=#D12700>красная</color>"},
                {"NoCard", "Для прохода в Ваших руках должна быть {0} карта."},
                {"InvalidCard", "Эта {0} карта не подходит. Нужна {1} карта."},
                {"ExitHint", "Подсказка: Чтобы выйти - постучите в дверь ещё раз."},
            }, this, "ru");
        }

        private string _(string key, BasePlayer player = null, params object[] args)
        {
            var message = lang.GetMessage(key, this, player?.UserIDString);
            return message != null ? args.Length > 0 ? string.Format(message, args) : message : key;
        }

        #endregion

        #region Hooks

        private void OnDoorKnocked(Door door, BasePlayer player)
        {
            if (player == null || door == null || !door.isSecurityDoor || door.IsOpen())
                return;
            if (Vector3.Dot(-door.transform.right, (player.transform.position - door.transform.position).normalized) > 0f)
            {
                OpenSecurityDoor(door, _cfg.CloseDelayInside);
                return;
            }
            int doorCardLevel;
            if (!DoorToKeyLevelMap.TryGetValue(door.prefabID, out doorCardLevel))
                return;
            var proxcard = player.GetActiveItem()?.GetHeldEntity() as Keycard;
            if (proxcard == null || proxcard.IsBroken())
            {
                Player.Message(player, _("NoCard", player, GetNiceCardName(doorCardLevel, player)));
                return;
            }
            if (proxcard.accessLevel != doorCardLevel)
            {
                Player.Message(player, _("InvalidCard", player,
                    GetNiceCardName(proxcard.accessLevel, player), GetNiceCardName(doorCardLevel, player)));
                return;
            }
            proxcard.GetItem().LoseCondition(_cfg.LoseConditionAmt);
            OpenSecurityDoor(door, _cfg.CloseDelayOutside);
            Player.Message(player, _("ExitHint", player));
        }

        private void Unload()
        {
            foreach (var door in UnityEngine.Object.FindObjectsOfType<Door>().Where(d => d.isSecurityDoor))
                door.SetOpen(false);
        }

        #endregion

        #region Logic

        private void OpenSecurityDoor(Door door, int closeIn)
        {
            door.SetOpen(true);
            timer.In(Mathf.Max(1, closeIn), () => door.SetOpen(false));
        }

        #endregion

        #region Utils

        private string GetNiceCardName(int aLevel, BasePlayer player) => _(KeyToLang[aLevel - 1], player);

        #endregion

    }
}
