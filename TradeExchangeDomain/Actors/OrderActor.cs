using System;
using System.ComponentModel;
using Akka.Actor;
using Akka.Persistence;
using TradeExchangeDomain.Orders;

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
                              BalanceRef = i.Balance;
                              Persist(i.Order,
                                      c =>
                                      {
                                          Order = c;
                                          _amountLeft = c.Amount;
                                      });
                          });
            Command<OrderBookActor.OrderExecuted>(e =>
                                                  {
                                                      if (_amountLeft < e.Amount)
                                                          throw new InvalidOrderStateException();

                                                      Persist(e.Amount,
                                                              o =>
                                                              {
                                                                  _amountLeft -= o;
                                                                  OnExecuted(e, Sender);
                                                                  if (_amountLeft == 0)
                                                                      BalanceRef.Tell(new OrderCompleted(Order.Id));
                                                              });
                                                  },
                                                  e => e.OrderNum == Order?.Id);
            Recover<Order>(o =>
                                  {
                                      Order = o;
                                      _amountLeft = Order.Amount;
                                  });
            Recover<decimal>(o => _amountLeft -= o);
        }

        public override string PersistenceId { get; }


        protected abstract void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender);

        public class Init
        {
            public IActorRef Balance;
            public Init(Order order, IActorRef balance)
            {
                Balance = balance;
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
        
      

        public class OrderBalance
        {
            public Money Total;

            public OrderBalance(Money order)
            {
                Total = order;
            }
        }
        public class GetBalance
        {
        }
    }
}