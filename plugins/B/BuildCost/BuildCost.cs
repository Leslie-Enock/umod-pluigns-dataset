using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
	[Info("BuildCost", "Lincoln & ignignokt84", "1.0.4")]
	[Description("Calculates the cost of building a structure and its deployables")]
	class BuildCost : CovalencePlugin
	{
		const string PermCanUse = "buildcost.use";
		private static readonly int WOOD_ID = -151838493;
		private static readonly int STONE_ID = -2099697608;
		private static readonly int METAL_ID = 69511070;
		private static readonly int HQM_ID = 317398316;

		CostAggregator aggregator = new CostAggregator();
		Dictionary<uint, int> deployableLookup = new Dictionary<uint, int>();

		void Init()
		{
			permission.RegisterPermission(PermCanUse, this);
			AddCovalenceCommand("cost", "CalculateCost");
			AddCovalenceCommand("bcost", "CalculateCost");
			AddCovalenceCommand("buildcost", "CalculateCost");
		}

		void OnServerInitialized()
		{
			BuildDeployableLookups();
		}

		void BuildDeployableLookups()
		{
			foreach (var item in ItemManager.GetItemDefinitions())
			{
				var deployable = item.GetComponent<ItemModDeployable>();
				if (deployable != null)
				{
					deployableLookup[deployable.entityPrefab.resourceID] = item.itemid;
				}
			}
		}

		void CalculateCost(IPlayer player, string command, string[] args)
		{
			Puts($"Command triggered by: {player.Name}");

			if (!player.HasPermission(PermCanUse))
			{
				player.Reply("<color=#ff0000>You do not have permission to use this command.</color>");
				return;
			}

			var basePlayer = player.Object as BasePlayer;
			if (basePlayer == null)
			{
				player.Reply("<color=#ff0000>An error occurred. Could not retrieve player.</color>");
				return;
			}

			aggregator.Reset();

			// Check if an extra argument ("twig") was provided to include twig cost on top of the current grade cost.
			bool includeTwigCost = args.Any(arg => arg.ToLower() == "twig");

			if (!GetRaycastTarget(basePlayer, out var closestEntity))
			{
				player.Reply("<color=#ff0000>No building found.</color>");
				return;
			}

			if (closestEntity is BaseEntity initialBlock)
			{
				HashSet<BuildingBlock> buildingBlocks;
				HashSet<BaseEntity> deployables;

				if (GetStructure(initialBlock, out buildingBlocks, out deployables))
				{
					CalculateBuildingBlockCost(buildingBlocks, includeTwigCost);
					player.Reply("<color=#ffc34d>【Building Block Costs】</color>");
					player.Reply(aggregator.GetFormattedCost());

					aggregator.Reset();
					CalculateDeployableCost(deployables);
					player.Reply("\n<color=#ffc34d>【Deployable Costs】</color>");
					player.Reply(aggregator.GetFormattedCost());
				}
			}
		}

		bool GetRaycastTarget(BasePlayer player, out BaseEntity closestEntity)
		{
			closestEntity = null;
			RaycastHit hit;
			if (Physics.Raycast(player.eyes.HeadRay(), out hit, 100f))
			{
				closestEntity = hit.GetEntity();
				return closestEntity != null;
			}
			return false;
		}

		bool GetStructure(BaseEntity initialBlock, out HashSet<BuildingBlock> structure, out HashSet<BaseEntity> deployables)
		{
			structure = new HashSet<BuildingBlock>();
			deployables = new HashSet<BaseEntity>();
			var checkedPositions = new List<Vector3> { initialBlock.transform.position };

			if (initialBlock is BuildingBlock)
				structure.Add(initialBlock as BuildingBlock);

			int index = 0;
			while (index < checkedPositions.Count)
			{
				var entities = new List<BaseEntity>();
				Vis.Entities(checkedPositions[index], 3f, entities);

				foreach (var entity in entities)
				{
					if (entity is BuildingBlock block && !structure.Contains(block))
					{
						structure.Add(block);
						checkedPositions.Add(block.transform.position);
					}
					else if (entity != null)
					{
						deployables.Add(entity);
					}
				}
				index++;
			}
			return true;
		}

		void CalculateBuildingBlockCost(HashSet<BuildingBlock> blocks, bool includeTwigCost)
		{
			foreach (var block in blocks)
			{
				var currentGrade = block.grade;
				List<ItemAmount> costs = new List<ItemAmount>();

				// Optionally add the basic twig cost.
				if (includeTwigCost)
				{
					var twigItem = ItemManager.FindItemDefinition(WOOD_ID);
					if (twigItem != null)
						costs.Add(new ItemAmount(twigItem, 50));
				}

				// Then add the cost for the actual grade of the block.
				switch (currentGrade)
				{
					case BuildingGrade.Enum.Twigs:
						if (!includeTwigCost)
						{
							var twigItem = ItemManager.FindItemDefinition(WOOD_ID);
							if (twigItem != null)
								costs.Add(new ItemAmount(twigItem, 50));
						}
						break;

					case BuildingGrade.Enum.Wood:
						{
							var woodItem = ItemManager.FindItemDefinition(WOOD_ID);
							if (woodItem != null)
								costs.Add(new ItemAmount(woodItem, 200));
						}
						break;

					case BuildingGrade.Enum.Stone:
						{
							var stoneItem = ItemManager.FindItemDefinition(STONE_ID);
							if (stoneItem != null)
								costs.Add(new ItemAmount(stoneItem, 300));
						}
						break;

					case BuildingGrade.Enum.Metal:
						{
							var metalItem = ItemManager.FindItemDefinition(METAL_ID);
							if (metalItem != null)
								costs.Add(new ItemAmount(metalItem, 200));
						}
						break;

					case BuildingGrade.Enum.TopTier:
						{
							var hqmItem = ItemManager.FindItemDefinition(HQM_ID);
							if (hqmItem != null)
								costs.Add(new ItemAmount(hqmItem, 25));
						}
						break;

					default:
						continue;
				}

				aggregator.AddCosts(costs);
			}
		}

		void CalculateDeployableCost(HashSet<BaseEntity> deployables)
		{
			foreach (var deployable in deployables)
			{
				if (!deployableLookup.TryGetValue(deployable.prefabID, out int itemId))
					continue;

				var blueprint = ItemManager.FindItemDefinition(itemId)?.Blueprint;
				if (blueprint != null)
				{
					aggregator.AddCosts(blueprint.ingredients);
				}
			}
		}

		class CostAggregator
		{
			public Dictionary<int, float> materialCosts = new Dictionary<int, float>
			{
				{ WOOD_ID, 0 },
				{ STONE_ID, 0 },
				{ METAL_ID, 0 },
				{ HQM_ID, 0 }
			};

			public void Reset()
			{
				materialCosts[WOOD_ID] = 0;
				materialCosts[STONE_ID] = 0;
				materialCosts[METAL_ID] = 0;
				materialCosts[HQM_ID] = 0;
			}

			public void AddCosts(List<ItemAmount> costs)
			{
				foreach (var cost in costs)
				{
					if (materialCosts.ContainsKey(cost.itemid))
					{
						materialCosts[cost.itemid] += cost.amount;
					}
				}
			}

			public string GetFormattedCost()
			{
				var formattedCost = new List<string>();

				if (materialCosts[WOOD_ID] > 0)
					formattedCost.Add($"<color=#cd9575>Wood:</color> <color=#ffffff>{materialCosts[WOOD_ID]:N0}</color>");
				if (materialCosts[STONE_ID] > 0)
					formattedCost.Add($"<color=#808080>Stone:</color> <color=#ffffff>{materialCosts[STONE_ID]:N0}</color>");
				if (materialCosts[METAL_ID] > 0)
					formattedCost.Add($"<color=#a9a9a9>Metal Fragments:</color> <color=#ffffff>{materialCosts[METAL_ID]:N0}</color>");
				if (materialCosts[HQM_ID] > 0)
					formattedCost.Add($"<color=#4682b4>High Quality Metal:</color> <color=#ffffff>{materialCosts[HQM_ID]:N0}</color>");

				return string.Join("\n", formattedCost);
			}

			public bool IsEmpty() => materialCosts.Values.All(v => v == 0);
		}
	}
}
