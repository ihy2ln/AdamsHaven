using System;
using System.Collections.Generic;

namespace Game.Farm
{
    [Serializable]
    public sealed class FarmBattleReport
    {
        public string stageId = "";
        public bool victory;
        public int battlesCompleted = 1;
        public List<FarmItemStack> rewards = new List<FarmItemStack>();
    }

    public enum FarmTownVerb { SellProduce, Cook }

    [Serializable]
    public sealed class FarmTownRequest
    {
        public FarmTownVerb verb;
        public string itemId = "";
        public int quantity = 1;
    }
}
