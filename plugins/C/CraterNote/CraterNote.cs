namespace Oxide.Plugins 
{
	[Info("Crater Note", "rostov114", "1.0.0")]
	[Description("Returns the analysis of the wells in a note. Like before.")]
	class CraterNote : RustPlugin 
	{
		private void OnAnalysisComplete(SurveyCrater crater, BasePlayer player)
		{
			ResourceDepositManager.ResourceDeposit orCreate = ResourceDepositManager.GetOrCreate(crater.transform.position);
			if (orCreate == null)
				return;

			Item item = ItemManager.CreateByName("note", 1, 0UL);
			item.text = "-Mineral Analysis-\n\n";
			float num = 10f;
			float num2 = 7.5f;
			foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resourceDepositEntry in orCreate._resources)
			{
				float num3 = 60f / num * (num2 / resourceDepositEntry.workNeeded);
				Item item2 = item;
				item2.text = string.Concat(new string[]
				{
					item2.text,
					resourceDepositEntry.type.displayName.english,
					" : ",
					num3.ToString("0.0"),
					" pM\n"
				});
			}
			item.MarkDirty();
			player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
		}
	}
}