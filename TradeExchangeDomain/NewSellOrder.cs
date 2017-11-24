using System.Runtime.CompilerServices;

namespace TradeExchangeDomain
{
    public class NewSellOrder : Order
    {
        public NewSellOrder(Symbol usdbtc, Money money, decimal amount, string id = null) : base(usdbtc, money, amount, id)
        {
            TotalToSell = usdbtc.Target.Emit(amount);
        }
        
        public NewSellOrder(Order order) : this(order.Position, order.Price, order.Amount, order.Id)
        {
            
        }

        public Money TotalToSell;
    }
}