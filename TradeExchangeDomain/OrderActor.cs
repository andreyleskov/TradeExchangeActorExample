using System;
using Akka.Actor;
using Akka.Persistence;

namespace TradeExchangeDomain
{
    //always lives as a child of balance actor
    public class OrderActor : ReceivePersistentActor
    {
        protected Order Order;
        private decimal _amountLeft;
        private IActorRef BalanceRef;
        
        public OrderActor()
        {
            PersistenceId = Self.Path.Name;
            
            Command<Init>(i =>
                          {
                              Order = i.Order;
                              _amountLeft = Order.Amount;
                          });
            Command<InitBalance>(i =>
                                   {
                                       BalanceRef = i.BalanceRef;
                                   });
            Command<OrderBookActor.OrderExecuted>(e =>
                                   {
                                       _amountLeft -= e.Amount;
                                       if (_amountLeft < 0)
                                           throw new InvalidOrderStateException();
                                       
                                       BalanceRef.Tell(e);
                                       if(_amountLeft == 0)
                                           BalanceRef.Tell(new OrderCompleted(Order.Id));
                                   });
            Recover<Order>(o =>
                           {
                               Order = o;
                               _amountLeft = Order.Amount;
                           });
            Recover<OrderBookActor.OrderExecuted>(o => _amountLeft -= o.Amount);
        }

        public class InitBalance
        {
            public IActorRef BalanceRef { get; private set; }

            public InitBalance(IActorRef balance)
            {
                BalanceRef = balance;
            }
        }

        public override string PersistenceId { get; }

      

        public class Init
        {
            public Order Order { get; }

            public Init(Order order)
            {
                Order = order;
            }
        }

        public class Execute
        {
            public IActorRef OrderBook { get; }

            public Execute(IActorRef orderBook)
            {
                OrderBook = orderBook;
            }
        }

        public class OrderCompleted
        {
            public string OrderNum { get; }

            public OrderCompleted(string orderNum)
            {
                OrderNum = orderNum;
            }
        }
    }

    public class InvalidOrderStateException : Exception
    {
    }
}