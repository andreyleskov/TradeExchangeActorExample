namespace TradeExchangeDomain
{
    public class NewBuyOrder : Order
    {
        public NewBuyOrder(Symbol position, Money price, decimal amount, string id = null) :
            base(position, price, amount, id)
        {
            TotalToBuy = price * amount;
        }

        public NewBuyOrder(Order order) : this(order.Position, order.Price, order.Amount, order.Id)
        {
        }

        public Money TotalToBuy { get; }
    }
}