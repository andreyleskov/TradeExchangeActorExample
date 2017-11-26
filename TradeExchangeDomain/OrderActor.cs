using System;
using Akka.Actor;
using Akka.Persistence;

namespace TradeExchangeDomain
{
    //always lives as a child of balance actor
    public abstract class OrderActor : ReceivePersistentActor
    {
        private decimal _amountLeft;
        protected IActorRef BalanceRef;
        protected Order Order;

        public OrderActor()
        {
            PersistenceId = Self.Path.Name;

            Command<GracefulShutdown>(s => Context.Stop(Self));
            Command<Init>(i =>
                          {
                              
                              Persist(new OrderCreated(i.Order),
                                      c =>
                                      {
                                          Order = c.Order;
                                          _amountLeft = c.Order.Amount;
                                      });
                          });
            Command<InitBalance>(i => { BalanceRef = i.BalanceRef; });
            Command<OrderBookActor.OrderExecuted>(e =>
                                                  {
                                                      if (_amountLeft < e.Amount)
                                                          throw new InvalidOrderStateException();

                                                      Persist(new OrderExecution(e.Amount),
                                                              o =>
                                                              {
                                                                  _amountLeft -= o.Amount;
                                                                  OnExecuted(e, Sender);
                                                                  if (_amountLeft == 0)
                                                                      BalanceRef.Tell(new OrderCompleted(Order.Id));
                                                              });
                                                  },
                                                  e => e.OrderNum == Order?.Id);
            Recover<OrderCreated>(o =>
                                  {
                                      Order = o.Order;
                                      _amountLeft = Order.Amount;
                                  });
            Recover<OrderExecution>(o => _amountLeft -= o.Amount);
        }

        public override string PersistenceId { get; }


        protected abstract void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender);

        public class OrderExecution
        {
            public OrderExecution(decimal amount)
            {
                Amount = amount;
            }

            public decimal Amount { get; }
        }

        public class InitBalance
        {
            public InitBalance(IActorRef balance)
            {
                BalanceRef = balance;
            }

            public IActorRef BalanceRef { get; }
        }


        public class Init
        {
            public Init(Order order)
            {
                Order = order;
            }

            public Order Order { get; }
        }

        public class Execute
        {
            public Execute(IActorRef orderBook)
            {
                OrderBook = orderBook;
            }

            public IActorRef OrderBook { get; }
        }


        public class OrderCompleted
        {
            public OrderCompleted(string orderNum)
            {
                OrderNum = orderNum;
            }

            public string OrderNum { get; }
        }

        public class InvalidOrderStateException : Exception
        {
        }
        
        public class OrderCreated
        {
            public OrderCreated(Order order)
            {
                Order = order;
            }

            public Order Order { get; }
        }
    }
}