using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence;
using TradeExchangeDomain.Orders;

namespace TradeExchangeDomain
{
    public class UserBalance : ReceivePersistentActor
    {
        public UserBalance()
        {
            PersistenceId = Self.Path.Name;

            Command<GracefulShutdown>(s =>
                                      {
                                          var actorRefs = Context.GetChildren().ToArray();
                                          Task.WhenAll(actorRefs
                                                           .Select(c => c.GracefulStop(TimeSpan.FromSeconds(10), s))
                                                           .ToArray())
                                              .ContinueWith(t => PoisonPill.Instance)
                                              .PipeTo(Self);
                                      });
            Command<GetBalance>(m =>
                                {
                                    Sender.Tell(Balances.TryGetValue(m.Currency, out Money money)
                                                    ? money
                                                    : m.Currency.Emit(0));
                                });
            Command<AddMarket>(m =>
                               {
                                   Markets[m.Symbol] = m.Market;
                                   foreach (var pending in PendingBuyOrders.Values.Where(o => o.Position == m.Symbol))
                                   {
                                       ActiveOrders.Add(pending.Id);
                                       var orderActor = Context.ActorOf<BuyOrderActor>(pending.Id);
                                       orderActor.Tell(pending);
                                       orderActor.Tell(Self);
                                       orderActor.Forward(new OrderActor.Execute(m.Market));
                                   }

                                   foreach (var pending in PendingSellOrders.Values.Where(o => o.Position == m.Symbol))
                                   {
                                       ActiveOrders.Add(pending.Id);
                                       var orderActor = Context.ActorOf<SellOrderActor>(pending.Id);
                                       orderActor.Tell(Self);
                                       orderActor.Tell(pending);
                                       orderActor.Tell(new OrderActor.Execute(m.Market));
                                   }
                               });
            Command<AddFunds>(f =>
                              {
                                  Persist(new MoneyAdded(f.Value),
                                          a => { IncreaseBalance(f.Value); });
                              });
            Command<AddDueOrderExecuted>(e =>
                                         {
                                             Persist(new BalanceChangedDueToOrderExecution(e.OrderNum, e.TotalChange),
                                                     ex => { IncreaseBalance(ex.ObjPrice); });
                                         },
                                         e => ActiveOrders.Contains(e.OrderNum));
            Command<OrderActor.OrderCompleted>(e =>
                                               {
                                                   Log.Info("order completed");
                                                   Persist(new OrderCompleted(e.OrderNum),
                                                           c => ActiveOrders.Remove(c.Num));
                                               },
                                               e => ActiveOrders.Contains(e.OrderNum));
            Command<BuyOrder>(o =>
                                 {
                                     if (!Markets.TryGetValue(o.Position, out var market))
                                     {
                                         Log.Info("order cannot be added due to market mismatch");
                                         Sender.Tell(new Status.Failure(new UnsupportedMarketException()));
                                         return;
                                     }
                                     if (!CheckBalance(o.Total))
                                     {
                                         Log.Info("order cannot be added due not lack of funds");
                                         Sender.Tell(new Status.Failure(new NotEnoughFundsException()));
                                         return;
                                     }
                                     var sender = Sender;

                                     Persist(new BuyOrderCreated(o),
                                             c => {
                                                 var o1 = c.Order;
                                                 DecreaseBalance(o1.Price * o1.Amount);
                                                 ActiveOrders.Add(o1.Id);
                                                 var orderActor = Context.ActorOf<BuyOrderActor>("order_"+o1.Id);
                                                 orderActor.Tell(o1);
                                                 orderActor.Tell(Self);
                                                 orderActor.Tell(new OrderActor.Execute(market),sender);
                                             });
                                 });
            Command<SellOrder>(o =>
                                  {
                                      if (!Markets.TryGetValue(o.Position, out var market))
                                      {
                                          Log.Info("order cannot be added due to market mismatch");
                                          Sender.Tell(new Status.Failure(new UnsupportedMarketException()));
                                          return;
                                      }
                                      if (!CheckBalance(o.Total))
                                      {
                                          Log.Info("order cannot be added due not lack of funds");
                                          Sender.Tell(new Status.Failure(new NotEnoughFundsException()));
                                          return;
                                      }
                                      var sender = Sender;
                                      Persist(new SellOrderCreated(o),
                                              c => {
                                                  var o1 = c.Order;
                                                  DecreaseBalance(o1.Position.Target.Emit(o1.Amount));
                                                  ActiveOrders.Add(o1.Id);
                                                  var orderActor = Context.ActorOf<SellOrderActor>("order_"+o1.Id);
                                                  orderActor.Tell(o1);
                                                  orderActor.Tell(Self);
                                                  orderActor.Tell(new OrderActor.Execute(market),sender);
                                              });
                                  });

            Recover<SellOrderCreated>(e =>
                                      {
                                          PendingSellOrders.Add(e.Order.Id, e.Order);
                                          DecreaseBalance(e.Order.Position.Target.Emit(e.Order.Amount));
                                      });
            Recover<BuyOrderCreated>(e =>
                                     {
                                         PendingBuyOrders.Add(e.Order.Id, e.Order);
                                         DecreaseBalance(e.Order.Price * e.Order.Amount);
                                     });
            Recover<OrderCompleted>(e =>
                                    {
                                        PendingSellOrders.Remove(e.Num);
                                        PendingBuyOrders.Remove(e.Num);
                                    });
            Recover<MoneyAdded>(e => IncreaseBalance(e.Money));
            Recover<BalanceChangedDueToOrderExecution>(c => IncreaseBalance(c.ObjPrice));
        }

        public IDictionary<Currency, Money> Balances { get; } = new Dictionary<Currency, Money>();
        public override string PersistenceId { get; }
        public HashSet<string> ActiveOrders { get; } = new HashSet<string>();
        public IDictionary<Symbol, IActorRef> Markets { get; } = new Dictionary<Symbol, IActorRef>();

        public Dictionary<string, Order> PendingBuyOrders { get; } = new Dictionary<string, Order>();
        public Dictionary<string, Order> PendingSellOrders { get; } = new Dictionary<string, Order>();

        private void IncreaseBalance(Money money)
        {
            if (Balances.TryGetValue(money.Currency, out var existingMoney))
                Balances[money.Currency] = money + existingMoney;
            else
                Balances[money.Currency] = money;
        }

        private bool CheckBalance(Money total)
        {
            return Balances.TryGetValue(total.Currency, out var balance) && balance >= total;
        }

        private void DecreaseBalance(Money total)
        {
            if (!Balances.TryGetValue(total.Currency, out var balance))
                throw new NotEnoughFundsException();

            if (balance < total)
                throw new NotEnoughFundsException();

            Balances[total.Currency] = balance - total;
        }


        public class NotEnoughFundsException : Exception
        {
        }

        public class UnsupportedMarketException : Exception
        {
        }

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

        public class AddDueOrderExecuted
        {
            public AddDueOrderExecuted(string orderNum, Money totalChange)
            {
                OrderNum = orderNum;
                TotalChange = totalChange;
            }

            public string OrderNum { get; }
            public Money TotalChange { get; }
        }

        public class OrderCompleted
        {
            public OrderCompleted(string num)
            {
                Num = num;
            }

            public string Num { get; }
        }

        public class BalanceChangedDueToOrderExecution
        {
            public BalanceChangedDueToOrderExecution(string objOrderNum, Money objPrice)
            {
                ObjOrderNum = objOrderNum;
                ObjPrice = objPrice;
            }

            public string ObjOrderNum { get; }
            public Money ObjPrice { get; }
        }

        public class BuyOrderCreated
        {
            public BuyOrderCreated(Order order)
            {
                Order = order;
            }

            public Order Order { get; }
        }

        public class SellOrderCreated
        {
            public SellOrderCreated(Order order)
            {
                Order = order;
            }

            public Order Order { get; }
        }

        public class MoneyAdded
        {
            public MoneyAdded(Money money)
            {
                Money = money;
            }

            public Money Money { get; }
        }


        public class AddFunds
        {
            public AddFunds(Money value)
            {
                Value = value;
            }

            public Money Value { get; }
        }

        public class GetBalance
        {
            public Currency Currency { get; }

            public GetBalance(Currency currency)
            {
                Currency = currency;
            }
        }
    }
}