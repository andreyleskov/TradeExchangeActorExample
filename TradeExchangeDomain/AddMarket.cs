using System;
using Akka.Actor;

namespace TradeExchangeDomain
{
    public class AddMarket
    {
        public AddMarket(string name, IActorRef market, Symbol usdBtc)
        {
            Name = name;
            Market = market;
            Symbol = usdBtc;
        }

        public string Name { get; }
        public IActorRef Market { get; }
        public Symbol Symbol { get; }
    }
}