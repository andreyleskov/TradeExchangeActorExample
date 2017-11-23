using Akka.Actor;

namespace TradeExchangeDomain
{
    public static class ActorSystemExtensions
    {
        public static Market NewMarket(this ActorSystem sys, Symbol symbol)
        {
            return new Market(sys.ActorOf<OrderBookActor>(), symbol);
        }
    }
}