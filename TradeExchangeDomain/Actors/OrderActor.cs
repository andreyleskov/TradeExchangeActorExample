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


        protected virtual void Default()
        {
            Command<GracefulShutdown>(s => Context.Stop(Self));
        }
        
        private void Initializing()
        {
            Command<IActorRef>(i =>
                               {
                                   BalanceRef = i;
                                   if (Order != null)
                                   {
                                       BecomeStacked(() => Working());
                                       Stash.UnstashAll();
                                   }
                               });               
            Command<Order>(p => Persist(p,
                                       c =>
                                       {
                                           Order = c;
                                           _amountLeft = c.Amount;
                                           if (BalanceRef != null)
                                           {
                                               BecomeStacked(() => Working());
                                               Stash.UnstashAll();
                                           }
                                       })
                          );
            Default();
                
            CommandAny(c => Stash.Stash());
        }

        private void Working()
        {
            IActorRef orderReceivedWatcher = null;
            Command<GetBalance>(e => Sender.Tell(new OrderBalance(Order.Total)));

            Command<Execute>(e =>
                             {
                                 OnExecuting(e);
                                 orderReceivedWatcher = Sender;
                             });
            Command<OrderBookActor.OrderReceived>(r => orderReceivedWatcher.Forward(r));
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
            Default();
        }

        protected abstract void OnExecuting(Execute e);

        public OrderActor()
        {
            PersistenceId = Self.Path.Name;
            BecomeStacked(() => Initializing());
          
            Recover<Order>(o =>
                                  {
                                      Order = o;
                                      _amountLeft = Order.Amount;
                                  });
            Recover<decimal>(o => _amountLeft -= o);
        }

        public override string PersistenceId { get; }


        protected abstract void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender);

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