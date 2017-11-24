namespace TradeExchangeDomain
{
    public static class SymbolExtensions
    {
        public static NewSellOrder Sell(this Symbol symbol, decimal price, decimal amount, string id = null)
        {
            return new NewSellOrder(symbol, symbol.Base.Emit(price), amount, id);
        }

        public static NewBuyOrder Buy(this Symbol symbol, decimal price, decimal amount, string id = null)
        {
            return new NewBuyOrder(symbol, new Money(price, symbol.Base), amount, id);
        }
    }
}