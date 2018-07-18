using Akka.Actor;
using TradeExchangeDomain;

namespace TradeExchangeTests
{
    public static class ActorSystemExtensions
    {
        public static Market NewMarket(this ActorSystem sys, Symbol symbol)
        {
            return new Market(sys.ActorOf<OrderBookActor>(), symbol);
        }
    }
}