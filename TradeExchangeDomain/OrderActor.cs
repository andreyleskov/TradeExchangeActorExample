using System;
using Akka.Actor;
using Akka.Persistence;

namespace TradeExchangeDomain
{
    //always lives as a child of balance actor
    public abstract class OrderActor : ReceivePersistentActor
    {
        protected Order Order;
        private decimal _amountLeft;
        protected IActorRef BalanceRef;
        
        public OrderActor()
        {
            PersistenceId = Self.Path.Name;
            
            Command<Init>(i =>
                          {
                              Persist(new OrderCreated(Order),
                                      c =>
                                      {
                                          Order = c.Order;
                                          _amountLeft = Order.Amount;
                                      });
                          });
            Command<InitBalance>(i =>
                                   {
                                       BalanceRef = i.BalanceRef;
                                   });
            Command<OrderBookActor.OrderExecuted>(e =>
                                                  {
                                                      if(_amountLeft < e.Amount)
                                                          throw new InvalidOrderStateException();
 
                                                      Persist(new OrderExecution(e.Amount),
                                                              o =>
                                                              {
                                                                  _amountLeft -= o.Amount;
                                                                  OnExecuted(e, Sender);
                                                                  if (_amountLeft == 0)
                                                                      BalanceRef.Tell(new OrderCompleted(Order.Id));
                                                              });
                                                  }, e => e.OrderNum == Order.Id);
            Recover<OrderCreated>(o =>
                           {
                               Order = o.Order;
                               _amountLeft = Order.Amount;
                           });
            Recover<OrderExecution>(o => _amountLeft -= o.Amount);
        }

        public class OrderExecution
        {
            public decimal Amount { get; }

            public OrderExecution(decimal amount)
            {
                Amount = amount;
            }
        }

        public override void AroundPostStop()
        {
            base.AroundPostStop();
        }

        protected abstract void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender);

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

    public class OrderCreated
    {
        public Order Order { get; }

        public OrderCreated(Order order)
        {
            Order = order;
        }
    }

    public class InvalidOrderStateException : Exception
    {
    }
}