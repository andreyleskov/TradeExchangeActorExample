namespace TradeExchangeDomain.Orders
{
    public class SellOrder : Order
    {
        public Money TotalToSell;

        public SellOrder(Symbol usdbtc, Money money, decimal amount, string id = null) :
            base(usdbtc, money, amount, id)
        {
            TotalToSell = usdbtc.Target.Emit(amount);
        }

        public SellOrder(Order order) : this(order.Position, order.Price, order.Amount, order.Id)
        {
        }
    }
}