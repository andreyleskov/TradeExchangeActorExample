namespace TradeExchangeDomain.Orders
{
    public class SellOrder : Order
    {

        public SellOrder(Symbol usdbtc, Money money, decimal amount, string id = null) :
            base(usdbtc, money, amount, id)
        {
            Total = usdbtc.Target.Emit(amount);
        }

        public SellOrder(Order order) : this(order.Position, order.Price, order.Amount, order.Id)
        {
        }
    }
}