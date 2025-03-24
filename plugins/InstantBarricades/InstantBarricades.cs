using ConVar;
using JetBrains.Annotations;
using Oxide.Core;
using Oxide.Core.Libraries;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("InstantBarricades", "4yzhen", "1.0.0")]
    [Description("A Simple Light Weight plugin to destroy wooden barricades in Supermarkets. etc")]
    public class InstantBarricades : RustPlugin
    {
        private void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity.ShortPrefabName.Contains("door_barricade_"))
            {
                entity.Kill();
            }
        }
    }
}
