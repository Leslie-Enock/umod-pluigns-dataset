using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
	[Info("Custom Genetics", "rostov114 / yoshi2", "0.17.2")]
	[Description("Allows players to change genetics for seeds in their inventory")]

	class CustomGenetics : RustPlugin
	{
		#region Variables
		private GrowableGenes growablegenes = new GrowableGenes();
		private const string CustomGenes = "customgenetics.use";
		private HashSet<char> possibleGenes = new HashSet<char>
		{
			'G',
			'Y',
			'H',
			'X',
			'W'
		};
		#endregion

		#region Configuration
		private Configuration _config;
		public class Configuration
		{
			[JsonProperty(PropertyName = "allowed seeds (full or short names)")]
			public HashSet<string> AllowedPlants = new HashSet<string>
			{
				"seed.black.berry",
				"seed.blue.berry",
				"seed.corn",
				"seed.green.berry",
				"seed.hemp",
				"seed.potato",
				"seed.pumpkin",
				"seed.red.berry",
				"seed.white.berry",
				"seed.yellow.berry",
				"seed.wheat",
				"seed.rose",
				"seed.sunflower",
				"seed.orchid",
				"clone.black.berry",
				"clone.blue.berry",
				"clone.corn",
				"clone.green.berry",
				"clone.hemp",
				"clone.potato",
				"clone.pumpkin",
				"clone.red.berry",
				"clone.white.berry",
				"clone.yellow.berry",
				"clone.wheat",
				"clone.rose",
				"clone.sunflower",
				"clone.orchid"
			};

			[JsonProperty(PropertyName = "admins bypass (true/false)")]
			public bool AdminBypass = true;

			[JsonProperty(PropertyName = "only affect the active item (true/false)")]
			public bool WholeInventory = true;

			[JsonProperty(PropertyName = "command name")]
			public string CommandName = "setgenes";
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();
				SaveConfig();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration();
		#endregion

		#region Language
		protected override void LoadDefaultMessages()
		{
			// English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "Your seeds genetics have been set to ",
				["NoPermmision"] = "you're not allowed to use this command",
				["WrongFormat"] = "Syntax error, proper format is \"/setgenes GGGGYY\"",
				["WrongGene"] = "Syntax error, invalid gene type",
				["WrongItem"] = "the item you're holding is not a valid seed",
			}, this, "en");

			// French
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "La génétique de vos graines a été réglée sur ",
				["NoPermmision"] = "vous n'êtes pas autorisé à utiliser cette commande",
				["WrongFormat"] = "Erreur de syntaxe, le format correct est \"/setgenes GGGGYY\"",
				["WrongGene"] = "Erreur de syntaxe,type de gène non valide",
				["WrongItem"] = "l'article que vous tenez n'est pas une graine valide"
			}, this, "fr");

			// German
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "Ihre Samengenetik wurde eingestellt ",
				["NoPermmision"] = "Sie dürfen diesen Befehl nicht verwenden",
				["WrongFormat"] = "Syntaxfehler, das richtige Format ist \"/setgenes GGGGYY\"",
				["WrongGene"] = "Syntaxfehler, ungültiger Gentyp",
				["WrongItem"] = "Der Gegenstand, den Sie halten, ist kein gültiger Samen"
			}, this, "de");

			// Russian
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "Генетика ваших семян настроена на ",
				["NoPermmision"] = "Вам не разрешено использовать эту команду",
				["WrongFormat"] = "Ошибка синтаксиса, правильный формат \"/setgenes GGGGYY\"",
				["WrongGene"] = "Синтаксическая ошибка, недопустимый тип гена",
				["WrongItem"] = "Предмет, который вы держите, не является правильным семенем"
			}, this, "ru");

			// Spanish
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "La genética de tus semillas se ha configurado para ",
				["NoPermmision"] = "no tienes permitido usar este comando",
				["WrongFormat"] = "Error de sintaxis, el formato correcto es \"/setgenes GGGGYY\"",
				["WrongGene"] = "Error de sintaxis, tipo de gen no válido",
				["WrongItem"] = "el artículo que tienes no es una semilla válida"
			}, this, "es");

			// Ukrainian
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "Генетика вашого насіння налаштована на ",
				["NoPermmision"] = "Вам не дозволено використовувати цю команду",
				["WrongFormat"] = "Помилка синтаксису, правильний формат \"/setgenes GGGGYY\"",
				["WrongGene"] = "Синтаксична помилка, неприпустимий тип гену",
				["WrongItem"] = "Предмет, який ви тримаєте, не є насінням"
			}, this, "uk");
		}

		public void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
			player.Reply(string.Format(GetMessage(player, messageName), args));

		public void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
			player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

		public string GetMessage(IPlayer player, string messageName, params object[] args)
		{
			var message = lang.GetMessage(messageName, this, player.Id);
			return args.Length > 0 ? string.Format(message, args) : message;
		}
		#endregion

		#region Init
		private void Init()
		{
			permission.RegisterPermission(CustomGenes, this);

			AddCovalenceCommand(_config.CommandName, nameof(GetUserGenes));
		}
		#endregion

		#region manual genes
		private void GetUserGenes(IPlayer player, string command, string[] args)
		{
			BasePlayer basePlayer = player.Object as BasePlayer;

			if (!(_config.AdminBypass && player.IsAdmin) && !player.HasPermission(CustomGenes))
			{
				ReplyToPlayer(player, "NoPermmision");
				return;
			}

			if (_config.WholeInventory)
			{
				Item item = basePlayer.GetActiveItem();
				if (item == null)
				{
					ReplyToPlayer(player, "WrongItem");
					return; 
				}

				if (!_config.AllowedPlants.Contains(item.info.shortname) || _config.AllowedPlants.Contains(item.info.name))
				{
					ReplyToPlayer(player, "WrongItem");
					return;
				}
			}

			if (args.Length != 1 || args[0].Length != 6)
			{
				ReplyToPlayer(player, "WrongFormat");
				return;
			}

			char[] genes = args[0].ToUpper().ToCharArray(0, 6);
			foreach (char gene in genes)
			{
				if (!possibleGenes.Contains(gene))
				{
					ReplyToPlayer(player, "WrongGene");
					return;
				}
			}

			player.Message(lang.GetMessage("GenesSet", this, player.Id) + $" <size=18><color=#006300>{args[0]}</size></color>");
			EditGenes(basePlayer, genes);
		}
		#endregion


		#region Console Hooks
		[ConsoleCommand("customgenetics.give")]
		private void CustomGeneticsGive(ConsoleSystem.Arg args)
		{
			BasePlayer p = args?.Player() ?? null; 
			if (p != null && !p.IsAdmin) 
				return;

			if (!args.HasArgs(2))
			{
				SendReply(args, $"Syntax: customgenetics.give <player|steamid> <shortname> [amount] [gene]");
				return;
			}

			BasePlayer player = args.GetPlayer(0);
			if (player == null)
			{
				SendReply(args, $"Player '{args.Args[0]}' not found!");
				return;
			}

			string shortname = args.GetString(1);
			if (!_config.AllowedPlants.Contains(shortname))
			{
				SendReply(args, $"The item is not a valid seed!");
				SendReply(args, $"Valid seed: " + string.Join(", ", _config.AllowedPlants));
				SendReply(args, $"Syntax: customgenetics.give <player|steamid> <shortname> [gene] [amount]");
				return;
			}

			ItemDefinition definition = ItemManager.FindItemDefinition(shortname);
			if (definition == null)
			{
				SendReply(args, $"The item is not a valid! ItemDefinition not found!");
				return;
			}

			char[] genes = new char[0];
			string geneString = args.GetString(3, null);
			if (!string.IsNullOrEmpty(geneString))
			{
				if (geneString.Length != 6)
				{
					SendReply(args, $"The is not a valid gene string!");
					SendReply(args, $"Exemple valid gene string: XXXXXX");
					SendReply(args, $"Syntax: customgenetics.give <player|steamid> <shortname> [gene] [amount]");

					return;
				}

				genes = geneString.ToUpper().ToCharArray(0, 6);
				foreach (char gene in genes)
				{
					if (!possibleGenes.Contains(gene))
					{
						SendReply(args, $"{gene} - not a valid gene!");
						SendReply(args, $"Valid genes: " + string.Join(", ", possibleGenes));
						SendReply(args, $"Syntax: customgenetics.give <player|steamid> <shortname> [gene] [amount]");

						return;
					}
				}
			}

			int amount = args.GetInt(2, 1);
			Item item = ItemManager.Create(definition, amount);

			if (genes.Length > 0)
			{
				for (int i = 0; i < 6; i++)
				{
					growablegenes.Genes[i].Set(CharToGeneType(genes[i]), true);
				}

				GrowableGeneEncoding.EncodeGenesToItem(GrowableGeneEncoding.EncodeGenesToInt(growablegenes), item);
			}

			player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
		}
		#endregion

		#region Helpers
		public void EditGenes(BasePlayer player, char[] genes)
		{
			if (_config.WholeInventory)
			{
				Item item = player.GetActiveItem();
				if(item == null) 
				{
					ReplyToPlayer(player.IPlayer, "WrongItem");
					return; 
				}

				NewGenes(item, player, genes);
				return;
			}

			List<Item> allItems = Facepunch.Pool.GetList<Item>();
			player.inventory.GetAllItems(allItems);
			foreach (Item item in allItems)
				NewGenes(item, player, genes);

			Facepunch.Pool.Free<Item>(ref allItems, false);
		}

		public void NewGenes(Item item, BasePlayer player, char[] genes)
		{
			if (_config.AllowedPlants.Contains(item.info.shortname) || _config.AllowedPlants.Contains(item.info.name))
			{
				for (int i = 0; i < 6; ++i)
				{
					growablegenes.Genes[i].Set(CharToGeneType(genes[i]), true);
				}

				GrowableGeneEncoding.EncodeGenesToItem(GrowableGeneEncoding.EncodeGenesToInt(growablegenes), item);
				item.MarkDirty();
			}
			else
			{
				if (!_config.WholeInventory) return;
				ReplyToPlayer(player.IPlayer, "WrongItem");
			}
		}

		public GrowableGenetics.GeneType CharToGeneType(char gene)
		{
			switch (gene)
			{
				case 'G': return GrowableGenetics.GeneType.GrowthSpeed;
				case 'Y': return GrowableGenetics.GeneType.Yield;
				case 'H': return GrowableGenetics.GeneType.Hardiness;
				case 'X': return GrowableGenetics.GeneType.Empty;
				case 'W': return GrowableGenetics.GeneType.WaterRequirement;
				default: return GrowableGenetics.GeneType.Empty;
			}
		}
		#endregion
	}
}
