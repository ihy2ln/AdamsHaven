using Game.Farm;

namespace Game.Farm.Integration
{
    /// <summary>
    /// Stable farm-side handoff for the future town Market and Kitchen systems.
    /// The town owns pricing, recipes, and outputs; the farm only authorizes and
    /// consumes the produce being exported.
    /// </summary>
    public sealed class FarmTownGateway
    {
        readonly FarmSimulation _simulation;

        public FarmTownGateway(FarmSimulation simulation)
        {
            _simulation = simulation;
        }

        public FarmActionResult SellProduce(string itemId, int quantity)
        {
            return Export(FarmTownVerb.SellProduce, itemId, quantity);
        }

        public FarmActionResult CookProduce(string itemId, int quantity)
        {
            return Export(FarmTownVerb.Cook, itemId, quantity);
        }

        FarmActionResult Export(FarmTownVerb verb, string itemId, int quantity)
        {
            if (_simulation == null)
                return FarmActionResult.Fail("Farm simulation was missing.", default(FarmPosition));

            return _simulation.TransferToTown(new FarmTownRequest
            {
                verb = verb,
                itemId = itemId,
                quantity = quantity
            });
        }
    }
}
