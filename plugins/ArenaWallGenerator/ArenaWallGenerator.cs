using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

/*
Added permission arenawallgenerator.allowed
Added ice walls as an option
Added square arena as an option
Added triangle arena as an option
- Patch by magus2621
*/

namespace Oxide.Plugins
{
    [Info("ArenaWallGenerator", "nivex", "1.0.6")]
    [Description("An easy to use arena wall generator.")]
    class ArenaWallGenerator : RustPlugin
    {
        private string hewwPrefab;
        private string heswPrefab;
        private string heiwPrefab;
        private const string permName = "arenawallgenerator.allowed";
        private const int wallMask = Layers.Mask.Terrain | Layers.Mask.World | Layers.Mask.Default | Layers.Mask.Construction | Layers.Mask.Deployed;
        private StoredData storedData = new StoredData();
        private List<BaseCombatEntity> _walls = new List<BaseCombatEntity>();
        private bool newSave;

        public enum Shape
        {
            Circle,
            Square,
            Triangle
        }

        public class StoredData
        {
            public int Seed = 0;
            public readonly Dictionary<string, float> Arenas = new Dictionary<string, float>();
            public StoredData() { }
        }

        private void OnNewSave(string filename) => newSave = true;

        private void OnServerSave()
        {
            SaveData();
        }

        private void Unload()
        {
            SaveData();
        }

        private void Init()
        {
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntityTakeDamage));
            permission.RegisterPermission(permName, this);
        }

        private void OnServerInitialized(bool isStartup)
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch { }

            if (storedData?.Arenas == null)
                storedData = new StoredData();

            if (newSave || BuildingManager.server.buildingDictionary.Count == 0)
            {
                if (respawnOnWipe && storedData.Seed == ConVar.Server.seed)
                {
                    foreach (var entry in storedData.Arenas)
                    {
                        API_CreateZoneWalls(entry.Key.ToVector3(), entry.Value);
                    }
                }

                storedData.Seed = ConVar.Server.seed;
                newSave = false;
                SaveData();
            }

            if (noDecay)
            {
                Subscribe(nameof(OnEntityTakeDamage));
            }
            
            if (respawnWalls)
            {
                Subscribe(nameof(OnEntityDeath));
                Subscribe(nameof(OnEntityKill));
            }

            Subscribe(nameof(OnEntitySpawned));
            hewwPrefab = StringPool.Get(1745077396);
            heswPrefab = StringPool.Get(1585379529);
            heiwPrefab = StringPool.Get(921229511);
            _walls.AddRange(BaseNetworkable.serverEntities.Where(e => e is SimpleBuildingBlock || e.prefabID == 921229511).OfType<BaseCombatEntity>());
        }

        private object OnEntityKill(IceFence fence)
        {
            if (fence.prefabID == 921229511 && fence.OwnerID != 0 && !fence.OwnerID.IsSteamId() && ArenaTerritory(fence.transform.position))
            {
                return false;
            }

            return null;
        }

        private object OnEntityKill(SimpleBuildingBlock block) => block.OwnerID != 0 && !block.OwnerID.IsSteamId() && ArenaTerritory(block.transform.position) ? false : (object)null;

        private void OnEntityDeath(IceFence fence, HitInfo hitInfo) => OnEntityDeathHandler(fence);

        private void OnEntityDeath(SimpleBuildingBlock e, HitInfo hitInfo) => OnEntityDeathHandler(e);

        private void OnEntityDeathHandler(BaseCombatEntity e)
        {
            if (e.OwnerID != 0 && !e.OwnerID.IsSteamId())
            {
                _walls.Remove(e);

                RecreateZoneWall(e.PrefabName, e.transform.position, e.transform.rotation, e.OwnerID);
            }
        }

        private void OnEntitySpawned(IceFence fence)
        {
            if (fence.prefabID == 921229511)
            {
                _walls.Add(fence);
            }
        }

        private void OnEntitySpawned(SimpleBuildingBlock e) => _walls.Add(e);

        private void OnEntityTakeDamage(IceFence fence, HitInfo hitInfo)
        {
            if (fence.prefabID == 921229511)
            {
                OnEntityTakeDamageHandler(fence, hitInfo);
            }
        }

        private void OnEntityTakeDamage(SimpleBuildingBlock ssb, HitInfo hitInfo) => OnEntityTakeDamageHandler(ssb, hitInfo);

        private void OnEntityTakeDamageHandler(BaseCombatEntity e, HitInfo hitInfo)
        {
            if (e == null || hitInfo == null || !hitInfo.damageTypes.Has(DamageType.Decay) || !ArenaTerritory(e.transform.position))
            {
                return;
            }

            hitInfo.damageTypes = new DamageTypeList();
        }

        public void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        public void RecreateZoneWall(string prefab, Vector3 pos, Quaternion rot, ulong ownerId)
        {
            if (!ArenaTerritory(pos))
            {
                return;
            }

            var e = GameManager.server.CreateEntity(prefab, pos, rot, false);

            if (e == null)
            {
                return;
            }

            e.OwnerID = ownerId;
            e.Spawn();
            e.gameObject.SetActive(true);

            if (!noDecay)
            {
                return;
            }

            DecayEntity decayEntity;
            if (e.TryGetComponent(out decayEntity))
            {
                decayEntity.decay = null;
            }
        }

        [HookMethod("ArenaTerritory")]
        public bool ArenaTerritory(Vector3 position)
        {
            foreach (var zone in storedData.Arenas)
            {
                if (Vector3Ex.Distance2D(position, zone.Key.ToVector3()) <= zone.Value + 5f)
                {
                    return true;
                }
            }

            return false;
        }

        public ulong GetHashId(string uid)
        {
            return Convert.ToUInt64(Math.Abs(uid.GetHashCode()));
        }

        public bool RemoveCustomZoneWalls(Vector3 center)
        {
            var list = new List<Vector3>();

            foreach (var zone in storedData.Arenas)
            {
                if (Vector3Ex.Distance2D(center, zone.Key.ToVector3()) <= zone.Value)
                {
                    list.Add(zone.Key.ToVector3());
                }
            }

            if (list.Count > 0)
            {
                list.Sort((x, y) => Vector3.Distance(y, x).CompareTo(Vector3.Distance(x, y)));

                string key = list.First().ToString();

                if (RemoveZoneWalls(GetHashId(key)) > 0)
                {
                    storedData.Arenas.Remove(key);
                    return true;
                }
            }

            return false;
        }

        public int RemoveZoneWalls(ulong hashId)
        {
            int removed = 0;

            if (respawnWalls)
            {
                Unsubscribe(nameof(OnEntityKill));
            }

            foreach (var e in _walls.ToList())
            {
                if (e.OwnerID == hashId)
                {
                    _walls.Remove(e);
                    e.Kill();
                    removed++;
                }
            }

            if (respawnWalls)
            {
                Subscribe(nameof(OnEntityKill));
            }

            return removed;
        }

        public List<Vector3> GetCircumferencePositions(Vector3 center, float radius, float next, float y)
        {
            var positions = new List<Vector3>();
            float degree = 0f;

            while (degree < 360f)
            {
                float angle = Mathf.Deg2Rad * degree;
                float x = center.x + radius * Mathf.Cos(angle);
                float z = center.z + radius * Mathf.Sin(angle);

                if (y == 0f)
                {
                    y = TerrainMeta.HeightMap.GetHeight(TerrainMeta.NormalizeX(x), TerrainMeta.NormalizeZ(z));
                }

                positions.Add(new Vector3(x, y, z));

                degree += next;
            }

            return positions;
        }

        public List<Vector3> GetSquarePerimeterPositions(Vector3 center, float radius, float y, float rotation)
        {
            var positions = new List<Vector3>();
            var area = Mathf.PI * Mathf.Pow(radius, 2f);
            var length = Mathf.Sqrt(area);
            var corners = GetSquareCorners(center, radius, y, rotation);
            var gap = 
                radius <= 10f ? 1f : 
                radius <= 15f ? 0.5f : 
                radius <= 20f ? 0.2175f : 
                radius <= 35f ? 1f : 
                radius <= 40f ? 0.25f :
                radius <= 60f ? 0.5f :
                0f;

            positions.AddRange(AddSides(gap, length, y, corners[0], corners[1]));
            positions.AddRange(AddSides(gap, length, y, corners[1], corners[2]));
            positions.AddRange(AddSides(gap, length, y, corners[2], corners[3]));
            positions.AddRange(AddSides(gap, length, y, corners[3], corners[0]));

            return positions;
        }

        public List<Vector3> GetTrianglePerimeterPositions(Vector3 center, float radius, float y, float rotation)
        {
            var positions = new List<Vector3>();
            float area = Mathf.PI * Mathf.Pow(radius, 2f);
            float length = Mathf.Sqrt(area / (Mathf.Sqrt(3f) / 4f));
            var corners = GetTriangleCorners(center, radius, y, rotation);
            float gap = 0.5f;

            positions.AddRange(AddSides(gap, length, y, corners[0], corners[1]));
            positions.AddRange(AddSides(gap, length, y, corners[1], corners[2]));
            positions.AddRange(AddSides(gap, length, y, corners[2], corners[0]));

            return positions;
        }

        private static List<Vector3> AddSides(float gap, float length, float y, Vector3 corner1, Vector3 corner2)
        {
            var positions = new List<Vector3>();
            for (float i = gap; i <= length - gap; i += 5)
            {
                var x = (1 - i / length) * corner1.x + i / length * corner2.x;
                var z = (1 - i / length) * corner1.z + i / length * corner2.z;

                if (y == 0f)
                {
                    y = TerrainMeta.HeightMap.GetHeight(TerrainMeta.NormalizeX(x), TerrainMeta.NormalizeZ(z));
                }

                positions.Add(new Vector3(x, y, z));
            }

            return positions;
        }

        private static List<float> AddDirections(int n, Vector3 center, Vector3 reference)
        {
            var directions = new List<float>(); // Vector3Ex.Direction
            var x = reference.x - center.x;
            var z = reference.z - center.z;
            float angle = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
            for (int i = 1; i <= n; i++)
            {
                directions.Add(angle);
            }

            return directions;
        }

        public List<Vector3> GetSquareCorners(Vector3 center, float radius, float y, float rotation)
        {
            var corners = new List<Vector3>();
            float area = Mathf.PI * Mathf.Pow(radius, 2f);
            float length = Mathf.Sqrt(area);
            var j = Mathf.Sin(Mathf.Deg2Rad * -rotation) - Mathf.Cos(Mathf.Deg2Rad * -rotation);
            var k = Mathf.Sin(Mathf.Deg2Rad * -rotation) + Mathf.Cos(Mathf.Deg2Rad * -rotation);

            var corner1 = new Vector3(center.x - length / 2 * j, 0f, center.z + length / 2 * k);
            var corner2 = new Vector3(center.x + length / 2 * k, 0f, center.z + length / 2 * j);
            var corner3 = new Vector3(center.x + length / 2 * j, 0f, center.z - length / 2 * k);
            var corner4 = new Vector3(center.x - length / 2 * k, 0f, center.z - length / 2 * j);

            corner1.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(corner1) : y;
            corner2.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(corner2) : y;
            corner3.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(corner3) : y;
            corner4.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(corner4) : y;

            corners.Add(corner1);
            corners.Add(corner2);
            corners.Add(corner3);
            corners.Add(corner4);

            return corners;
        }

        public List<Vector3> GetSquareSideCenters(Vector3 center, float radius, float y, float rotation)
        {
            var centers = new List<Vector3>();
            var corners = GetSquareCorners(center, radius, y, rotation);
            var center1 = new Vector3((corners[0].x + corners[1].x) / 2, 0f, (corners[0].z + corners[1].z) / 2);
            var center2 = new Vector3((corners[1].x + corners[2].x) / 2, 0f, (corners[1].z + corners[2].z) / 2);
            var center3 = new Vector3((corners[2].x + corners[3].x) / 2, 0f, (corners[2].z + corners[3].z) / 2);
            var center4 = new Vector3((corners[3].x + corners[0].x) / 2, 0f, (corners[3].z + corners[0].z) / 2);

            center1.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(center1) : y;
            center2.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(center2) : y;
            center3.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(center3) : y;
            center4.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(center4) : y;

            centers.Add(center1);
            centers.Add(center2);
            centers.Add(center3);
            centers.Add(center4);

            return centers;
        }

        public List<Vector3> GetTriangleCorners(Vector3 center, float radius, float y, float rotation)
        {
            var corners = new List<Vector3>();
            float area = Mathf.PI * Mathf.Pow(radius, 2f);
            float length = Mathf.Sqrt(area / (Mathf.Sqrt(3f) / 4f));
            float circleRadius = Mathf.Sqrt(3f) / 3f * length;
            int start = (int)Mathf.Round(rotation);
            var circle = new List<Vector3>();

            for (float degree = 0f; degree < 360f; degree++)
            {
                float angle = Mathf.Deg2Rad * degree;
                float x = center.x + circleRadius * Mathf.Cos(angle);
                float z = center.z + circleRadius * Mathf.Sin(angle);

                if (y == 0f)
                {
                    y = TerrainMeta.HeightMap.GetHeight(TerrainMeta.NormalizeX(x), TerrainMeta.NormalizeZ(z));
                }

                circle.Add(new Vector3(x, y, z));
            }

            var corner1 = circle[start % circle.Count];
            var corner2 = circle[(start + 120) % circle.Count];
            var corner3 = circle[(start + 240) % circle.Count];

            corner1.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(corner1) : y;
            corner2.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(corner2) : y;
            corner3.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(corner3) : y;

            corners.Add(corner1);
            corners.Add(corner2);
            corners.Add(corner3);

            return corners;
        }

        public List<Vector3> GetTriangleSideCenters(Vector3 center, float radius, float y, float rotation)
        {
            var centers = new List<Vector3>();
            var corners = GetTriangleCorners(center, radius, y, rotation);
            var center1 = new Vector3((corners[0].x + corners[1].x) / 2, 0f, (corners[0].z + corners[1].z) / 2);
            var center2 = new Vector3((corners[1].x + corners[2].x) / 2, 0f, (corners[1].z + corners[2].z) / 2);
            var center3 = new Vector3((corners[2].x + corners[0].x) / 2, 0f, (corners[2].z + corners[0].z) / 2);

            center1.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(center1) : y;
            center2.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(center2) : y;
            center3.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(center3) : y;

            centers.Add(center1);
            centers.Add(center2);
            centers.Add(center3);

            return centers;
        }

        public bool CreateZoneWalls(Vector3 center, float radius, string prefab, IPlayer p, Shape shape)
        {
            int spawned = 0;
            float minHeight = 200f;
            float maxHeight = -200f;
            var tick = DateTime.Now;
            ulong hashId = GetHashId(center.ToString());
            int raycasts = Mathf.CeilToInt(360 / radius * 0.1375f);
            var player = p.Object as BasePlayer;
            var rotation = player.eyes.rotation.eulerAngles.y;
            var directions = new List<float>();

            if (shape == Shape.Square)
            {
                var positions = GetSquarePerimeterPositions(center, radius, 0f, rotation);
                var elements = positions.Count / 4;
                var centers = GetSquareSideCenters(center, radius, 0f, rotation);

                foreach (var position in GetSquarePerimeterPositions(center, radius, 0f, rotation))
                {
                    RaycastHit hit;
                    if (Physics.Raycast(new Vector3(position.x, position.y + 200f, position.z), Vector3.down, out hit, Mathf.Infinity, wallMask))
                    {
                        maxHeight = Mathf.Max(hit.point.y, maxHeight);
                        minHeight = Mathf.Min(hit.point.y, minHeight);
                        center.y = minHeight;
                    }
                }

                foreach (var sideCenter in centers)
                {
                    directions.AddRange(AddDirections(elements, center, sideCenter));
                }
            }
            else if (shape == Shape.Triangle)
            {
                var positions = GetTrianglePerimeterPositions(center, radius, 0f, rotation);
                var elements = positions.Count / 3;
                var centers = GetTriangleSideCenters(center, radius, 0f, rotation);

                foreach (var position in GetTrianglePerimeterPositions(center, radius, 0f, rotation))
                {
                    RaycastHit hit;
                    if (Physics.Raycast(new Vector3(position.x, position.y + 200f, position.z), Vector3.down, out hit, Mathf.Infinity, wallMask))
                    {
                        maxHeight = Mathf.Max(hit.point.y, maxHeight);
                        minHeight = Mathf.Min(hit.point.y, minHeight);
                        center.y = minHeight;
                    }
                }

                foreach (var sideCenter in centers)
                {
                    directions.AddRange(AddDirections(elements, center, sideCenter));
                }
            }
            else
            {
                foreach (var position in GetCircumferencePositions(center, radius, raycasts, 0f))
                {
                    RaycastHit hit;
                    if (Physics.Raycast(new Vector3(position.x, position.y + 200f, position.z), Vector3.down, out hit, Mathf.Infinity, wallMask))
                    {
                        maxHeight = Mathf.Max(hit.point.y, maxHeight);
                        minHeight = Mathf.Min(hit.point.y, minHeight);
                        center.y = minHeight;
                    }
                }
            }

            float gap = prefab == heswPrefab ? 0.3f : 0.5f;
            int stacks = Mathf.CeilToInt((maxHeight - minHeight) / 6f) + extraWallStacks;
            float next = 360 / radius - gap;

            for (int i = 0; i < stacks; i++)
            {
                if (shape == Shape.Square)
                {
                    foreach (var position in GetSquarePerimeterPositions(center, radius, center.y, rotation).Select((Value, Index) => new { Value, Index }))
                    {
                        float groundHeight = TerrainMeta.HeightMap.GetHeight(new Vector3(position.Value.x, position.Value.y + 6f, position.Value.z));

                        if (groundHeight > position.Value.y + 10f)
                        {
                            continue;
                        }

                        if (useLeastAmount && position.Value.y - groundHeight > 6f + extraWallStacks * 6f)
                        {
                            continue;
                        }

                        var entity = GameManager.server.CreateEntity(prefab, position.Value, default(Quaternion), false);

                        if (entity == null)
                        {
                            return false;
                        }

                        entity.OwnerID = hashId;
                        var lookDirection = new Vector3(Mathf.Sin(Mathf.Deg2Rad * directions[position.Index]), 0f, Mathf.Cos(Mathf.Deg2Rad * directions[position.Index]));
                        entity.transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                        entity.Spawn();
                        entity.gameObject.SetActive(true);
                        spawned++;

                        if (stacks == i - 1)
                        {
                            RaycastHit hit;
                            if (Physics.Raycast(new Vector3(position.Value.x, position.Value.y + 6f, position.Value.z), Vector3.down, out hit, 12f, Layers.Mask.World | Layers.Mask.Terrain))
                            {
                                if (hit.collider.name.Contains("rock_") || hit.collider.name.Contains("formation_", CompareOptions.OrdinalIgnoreCase))
                                {
                                    stacks++;
                                }
                            }
                        }
                    }
                }
                else if (shape == Shape.Triangle)
                {
                    foreach (var position in GetTrianglePerimeterPositions(center, radius, center.y, rotation).Select((Value, Index) => new { Value, Index }))
                    {
                        float groundHeight = TerrainMeta.HeightMap.GetHeight(new Vector3(position.Value.x, position.Value.y + 6f, position.Value.z));

                        if (groundHeight > position.Value.y + 9f)
                        {
                            continue;
                        }

                        if (useLeastAmount && position.Value.y - groundHeight > 6f + extraWallStacks * 6f)
                        {
                            continue;
                        }

                        var entity = GameManager.server.CreateEntity(prefab, position.Value, default(Quaternion), false);

                        if (entity == null)
                        {
                            return false;
                        }

                        entity.OwnerID = hashId;
                        var lookDirection = new Vector3(Mathf.Sin(Mathf.Deg2Rad * directions[position.Index]), 0f, Mathf.Cos(Mathf.Deg2Rad * directions[position.Index]));
                        entity.transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                        entity.Spawn();
                        entity.gameObject.SetActive(true);
                        spawned++;

                        if (stacks == i - 1)
                        {
                            RaycastHit hit;
                            if (Physics.Raycast(new Vector3(position.Value.x, position.Value.y + 6f, position.Value.z), Vector3.down, out hit, 12f, Layers.Mask.World | Layers.Mask.Terrain))
                            {
                                if (hit.collider.name.Contains("rock_") || hit.collider.name.Contains("formation_", CompareOptions.OrdinalIgnoreCase))
                                {
                                    stacks++;
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (var position in GetCircumferencePositions(center, radius, next, center.y))
                    {
                        float groundHeight = TerrainMeta.HeightMap.GetHeight(new Vector3(position.x, position.y + 6f, position.z));

                        if (groundHeight > position.y + 9f)
                        {
                            continue;
                        }

                        if (useLeastAmount && position.y - groundHeight > 6f + extraWallStacks * 6f)
                        {
                            continue;
                        }

                        var entity = GameManager.server.CreateEntity(prefab, position, default(Quaternion), false);

                        if (entity == null)
                        {
                            return false;
                        }

                        entity.OwnerID = hashId;
                        entity.transform.LookAt(center, Vector3.up);
                        entity.Spawn();
                        entity.gameObject.SetActive(true);
                        spawned++;

                        if (stacks == i - 1)
                        {
                            RaycastHit hit;
                            if (Physics.Raycast(new Vector3(position.x, position.y + 6f, position.z), Vector3.down, out hit, 12f, Layers.Mask.World | Layers.Mask.Terrain))
                            {
                                if (hit.collider.name.Contains("rock_") || hit.collider.name.Contains("formation_", CompareOptions.OrdinalIgnoreCase))
                                {
                                    stacks++;
                                }
                            }
                        }
                    }
                }

                center.y += 6f;
            }

            string strPos = $"{center.x:N2} {center.y:N2} {center.z:N2}";

            if (p == null)
            {
                Puts(msg("GeneratedWalls", null, spawned, stacks, strPos, (DateTime.Now - tick).TotalSeconds));
            }
            else p.Reply(msg("GeneratedWalls", p.Id, spawned, stacks, strPos, (DateTime.Now - tick).TotalSeconds));

            return true;
        }

        private bool API_CreateZoneWalls(Vector3 center, float radius, int shape = 0)
        {
            if (CreateZoneWalls(center, radius, useWoodenWalls ? hewwPrefab : useIceWalls ? heiwPrefab : heswPrefab, null, shape == 0 ? Shape.Circle : Shape.Square))
            {
                storedData.Arenas[center.ToString()] = radius;
                return true;
            }

            return false;
        }

        private bool API_RemoveZoneWalls(Vector3 center)
        {
            return RemoveCustomZoneWalls(center);
        }

        private bool HasPermission(IPlayer player)
        {
            if (player.IsServer) return false;
            if (player.IsAdmin) return true;
            return player.HasPermission(permName);
        }
        private void CommandWalls(IPlayer p, string command, string[] args)
        {
            if (!HasPermission(p))
            {
                return;
            }

            var player = p.Object as BasePlayer;

            if (args.Length >= 1)
            {
                float radius;
                if (float.TryParse(args[0], out radius) && radius > 2f)
                {
                    if (radius > maxCustomWallRadius)
                    {
                        radius = maxCustomWallRadius;
                    }

                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, wallMask))
                    {
                        string prefab;
                        Shape shape;

                        if (args.Any(arg => arg.Contains("stone", CompareOptions.OrdinalIgnoreCase)))
                            prefab = heswPrefab;
                        else if (args.Any(arg => arg.Contains("wood", CompareOptions.OrdinalIgnoreCase)))
                            prefab = hewwPrefab;
                        else if (args.Any(arg => arg.Contains("ice", CompareOptions.OrdinalIgnoreCase)))
                            prefab = heiwPrefab;
                        else prefab = useWoodenWalls ? hewwPrefab : useIceWalls ? heiwPrefab : heswPrefab;

                        if (args.Any(arg => arg.Contains("circle", CompareOptions.OrdinalIgnoreCase)))
                            shape = Shape.Circle;
                        else if (args.Any(arg => arg.Contains("square", CompareOptions.OrdinalIgnoreCase)))
                            shape = Shape.Square;
                        else if (args.Any(arg => arg.Contains("triangle", CompareOptions.OrdinalIgnoreCase)))
                            shape = Shape.Triangle;
                        else shape = useShape.Equals("square") ? Shape.Square : useShape.Equals("triangle") ? Shape.Triangle : Shape.Circle;

                        storedData.Arenas[hit.point.ToString()] = radius;
                        CreateZoneWalls(hit.point, radius, prefab, p, shape);
                    }
                    else p.Reply(msg("FailedRaycast", p.Id));
                }
                else p.Reply(msg("InvalidNumber", p.Id, args[0]));
            }
            else
            {
                if (!RemoveCustomZoneWalls(player.transform.position))
                {
                    p.Reply(msg("WallsSyntax", player.UserIDString, chatCommandName));
                }

                foreach (var entry in storedData.Arenas)
                {
                    player.SendConsoleCommand("ddraw.text", 30f, Color.yellow, entry.Key.ToVector3(), entry.Value);
                }
            }
        }

        #region Config

        private bool Changed;
        private int extraWallStacks;
        private bool useLeastAmount;
        private float maxCustomWallRadius;
        private bool useWoodenWalls;
        private bool useIceWalls;
        private string useShape;
        private string chatCommandName;
        private bool respawnWalls;
        private bool respawnOnWipe;
        private bool noDecay;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GeneratedWalls"] = "Generated {0} arena walls {1} high at {2} in {3}ms",
                ["FailedRaycast"] = "Look towards the ground, and try again.",
                ["InvalidNumber"] = "Invalid number: {0}",
                ["WallsSyntax"] = "Use <color=orange>/{0} [radius] <circle|square|triangle> <wood|ice|stone></color>, or stand inside of an existing arena and use <color=orange>/{0}</color> to remove it.",
            }, this);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            extraWallStacks = Convert.ToInt32(GetConfig("Settings", "Extra High External Wall Stacks", 2));
            useLeastAmount = Convert.ToBoolean(GetConfig("Settings", "Create Least Amount Of Walls", false));
            maxCustomWallRadius = Convert.ToSingle(GetConfig("Settings", "Maximum Arena Radius", 300f));
            useWoodenWalls = Convert.ToBoolean(GetConfig("Settings", "Use Wooden Walls", false));
            useIceWalls = Convert.ToBoolean(GetConfig("Settings", "Use Ice Walls", false));
            useShape = Convert.ToString(GetConfig("Settings", "Use Shape", "circle"));
            chatCommandName = Convert.ToString(GetConfig("Settings", "Chat Command", "awg"));
            respawnWalls = Convert.ToBoolean(GetConfig("Settings", "Respawn Zone Walls On Death", false));
            respawnOnWipe = Convert.ToBoolean(GetConfig("Settings", "Respawn Arenas On Wipe If Same Seed", false));
            noDecay = Convert.ToBoolean(GetConfig("Settings", "Block Decay Damage", true));

            AddCovalenceCommand(chatCommandName, nameof(CommandWalls));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        public string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        public string RemoveFormatting(string source)
        {
            return source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        #endregion
    }
}