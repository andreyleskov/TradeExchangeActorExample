namespace TradeExchangeDomain
{
    public class NewSellOrder : Order
    {
        public NewSellOrder(Symbol usdbtc, Money money, decimal amount, string id = null) : base(usdbtc, money, amount, id)
        {
        }
        
        public NewSellOrder(Order order) : this(order.Position, order.Price, order.Amount, order.Id)
        {
            
        }
    }
}