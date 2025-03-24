using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ClosestMinicopter", "GigaBit", "1.3.2")]
    [Description("Tells a player where the closest minicopter is and on what bearing")]

    class ClosestMinicopter : RustPlugin
    {
        private Dictionary<string, RaycastHit> playerData = new Dictionary<string, RaycastHit>();
        private Dictionary<string, DateTime> cooldowns = new Dictionary<string, DateTime>();
        private float searchDistance;
        private int defaultCooldown;

        protected override void LoadDefaultConfig()
        {
            Config["SearchDistance"] = 500f;
            Config["DefaultCooldown"] = 60;
            SaveConfig();
        }

        private void Init()
        {
            permission.RegisterPermission("closestminicopter.use", this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command.",
                ["NoMinicopters"] = "You are out of luck, there are no minicopters nearby - you best run",
                ["HelicopterFound"] = "The closest Helicopter is {0} meters away on a bearing of {1}°",
                ["Cooldown"] = "Please wait {0} seconds before using this command again."
            }, this);
            searchDistance = Convert.ToSingle(GetConfigValue("SearchDistance", 500f));
            defaultCooldown = Convert.ToInt32(GetConfigValue("DefaultCooldown", 60));
        }

        private void SetCooldown(BasePlayer player)
        {
            if (cooldowns.ContainsKey(player.UserIDString))
                cooldowns[player.UserIDString] = DateTime.UtcNow;
            else
                cooldowns.Add(player.UserIDString, DateTime.UtcNow);
        }

        private bool HasCooldown(BasePlayer player)
        {
            if (cooldowns.ContainsKey(player.UserIDString))
            {
                DateTime lastUsage = cooldowns[player.UserIDString];
                TimeSpan cooldownTime = TimeSpan.FromSeconds(defaultCooldown);
                DateTime cooldownEnd = lastUsage + cooldownTime;
                if (DateTime.UtcNow < cooldownEnd)
                    return true;
            }
            return false;
        }

        private TimeSpan GetRemainingCooldown(BasePlayer player)
        {
            if (cooldowns.ContainsKey(player.UserIDString))
            {
                DateTime lastUsage = cooldowns[player.UserIDString];
                TimeSpan cooldownTime = TimeSpan.FromSeconds(defaultCooldown);
                DateTime cooldownEnd = lastUsage + cooldownTime;
                TimeSpan remainingTime = cooldownEnd - DateTime.UtcNow;
                return remainingTime;
            }
            return TimeSpan.Zero;
        }

        private T GetConfigValue<T>(string key, T defaultValue)
        {
            if (Config[key] == null)
            {
                Config[key] = defaultValue;
                SaveConfig();
                return defaultValue;
            }
            return (T)Convert.ChangeType(Config[key], typeof(T));
        }

        [ChatCommand("cmini")]
		private void FindClosestMinicopter(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "closestminicopter.use"))
            {
                SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }

            if (HasCooldown(player))
            {
                TimeSpan remainingTime = GetRemainingCooldown(player);
                SendReply(player, string.Format(lang.GetMessage("Cooldown", this, player.UserIDString), remainingTime.Seconds));
                return;
            }

            RaycastHit closestMinicopter;
            if (FindClosestMinicopter(player, out closestMinicopter))
            {
                float distance = Vector3.Distance(player.transform.position, closestMinicopter.transform.position);
                float bearing = GetBearing(player.transform.position, closestMinicopter.transform.position);

                SendReply(player, string.Format(lang.GetMessage("HelicopterFound", this, player.UserIDString), Mathf.RoundToInt(distance), Mathf.RoundToInt(bearing)));
                playerData[player.UserIDString] = closestMinicopter;

                SetCooldown(player);
            }
            else
            {
                SendReply(player, lang.GetMessage("NoMinicopters", this, player.UserIDString));
                playerData.Remove(player.UserIDString);
            }
        }
      

       private bool FindClosestMinicopter(BasePlayer player, out RaycastHit closestMinicopter)
		{
			closestMinicopter = new RaycastHit();

			Vector3 origin = player.eyes.position;
			Vector3 direction = player.eyes.HeadForward();
			float closestDistance = searchDistance;

			RaycastHit[] hits = Physics.SphereCastAll(origin, searchDistance, direction);

			foreach (RaycastHit hit in hits)
			{
				BaseEntity entity = hit.GetEntity();
				if (entity != null && entity.ShortPrefabName.Contains("copter")) //Giga modified this to just copter to capture both mini and scrappy
				{
					float distance = Vector3.Distance(player.transform.position, hit.transform.position);
					if (distance < closestDistance)
					{
						closestMinicopter = hit;
						closestDistance = distance;
					}
				}
			}

			return closestMinicopter.collider != null;
		}

        private float GetBearing(Vector3 origin, Vector3 target)
        {
            Vector3 direction = (target - origin).normalized;
            float angle = Quaternion.LookRotation(direction).eulerAngles.y;
            return (angle + 360) % 360;
        }
    }
}
