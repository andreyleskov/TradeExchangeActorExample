using Akka.Actor;

namespace TradeExchangeDomain
{
    public class Market
    {
        private readonly IActorRef _orderbook;
        private readonly Symbol _symbol;

        public Market(IActorRef orderbook, Symbol symbol)
        {
            _orderbook = orderbook;
            _symbol = symbol;
        }

        public Market Seller(decimal price, decimal amount)
        {
            _orderbook.Tell(_symbol.Sell(price, amount));
            return this;
        }

        public Market Buyer(decimal price, decimal amount)
        {
            _orderbook.Tell(_symbol.Buy(price, amount));
            return this;
        }

        public IActorRef OrderBook()
        {
            return _orderbook;
        }
    }
}