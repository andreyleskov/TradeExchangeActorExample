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
        
        public OrderActor()
        {
            Command<Init>(i =>
                          {
                              Order = i.Order;
                              _amountLeft = 0;
                          });
            Command<OrderBookActor.OrderExecuted>(e =>
                                   {
                                       _amountLeft -= e.Amount;
                                       if (_amountLeft < 0)
                                           throw new InvalidOrderStateException();
                                       
                                       Context.Parent.Tell(e);
                                       if(_amountLeft == 0)
                                           Context.Parent.Tell(new OrderCompleted(Order.Id));
                                   });
            Recover<Order>(o => Order = o);
            Recover<OrderBookActor.OrderExecuted>(o => _amountLeft -= o.Amount);
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