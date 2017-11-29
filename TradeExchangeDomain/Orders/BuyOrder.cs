namespace TradeExchangeDomain.Orders
{
    public class BuyOrder : Order
    {
        public BuyOrder(Symbol position, Money price, decimal amount, string id = null) :
            base(position, price, amount, id)
        {
            TotalToBuy = price * amount;
        }

        public BuyOrder(Order order) : this(order.Position, order.Price, order.Amount, order.Id)
        {
        }

        public Money TotalToBuy { get; }
    }
}