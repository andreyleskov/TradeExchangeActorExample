using TradeExchangeDomain.Orders;

namespace TradeExchangeDomain
{
    public static class SymbolExtensions
    {
        public static SellOrder Sell(this Symbol symbol, decimal price, decimal amount, string id = null)
        {
            return new SellOrder(symbol, symbol.Base.Emit(price), amount, id);
        }

        public static BuyOrder Buy(this Symbol symbol, decimal price, decimal amount, string id = null)
        {
            return new BuyOrder(symbol, new Money(price, symbol.Base), amount, id);
        }
    }
}