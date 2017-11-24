using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Akka.Actor;
using Akka.Persistence;

namespace TradeExchangeDomain
{
    public class UserBalance : ReceivePersistentActor
    {
        public IDictionary<Currency, Money> Balances { get; } = new Dictionary<Currency, Money>();
        public override string PersistenceId { get; }
        public HashSet<string> ActiveOrders { get; } = new HashSet<string>();
        public IDictionary<Symbol, IActorRef> Markets { get; } = new Dictionary<Symbol, IActorRef>();

        public Dictionary<string,Order> PendingBuyOrders { get; } = new Dictionary<string, Order>();
        public Dictionary<string,Order> PendingSellOrders { get; } = new Dictionary<string, Order>();
        

        public UserBalance()
        {
            PersistenceId = Self.Path.Name;

            Command<AddMarket>(m =>
                               {
                                   Markets[m.Symbol] = m.Market;
                                   foreach (var pending in PendingBuyOrders.Values.Where(o => o.Position == m.Symbol))
                                   {
                                       CreateBuyOrder(m.Market,pending);
                                   }
                                   foreach (var pending in PendingSellOrders.Values.Where(o => o.Position == m.Symbol))
                                   {
                                       CreateSellOrder(m.Market,pending);
                                   }
                               });
            Command<AddFunds>(f =>
                              {
                                  Persist(new MoneyAdded(f.Value),
                                          a => { IncreaseBalance(f.Value); });
                              });
            Command<AddDueOrderExecuted>(e =>
                                                  {
                                                      Persist(new BalanceChangedDueToOrderExecution(e.OrderNum,e.TotalChange),
                                                              ex =>
                                                              {
                                                                  IncreaseBalance(ex.ObjPrice);
                                                              });
                                                      
                                                  },e => ActiveOrders.Contains(e.OrderNum));
            Command<OrderActor.OrderCompleted>(e =>
                                               {
                                                   Persist(new OrderCompleted(e.OrderNum),
                                                           c => ActiveOrders.Remove(c.Num));
                                               });
            Command<NewBuyOrder>(o =>
                                 {
                                     if (!Markets.TryGetValue(o.Position, out var market))
                                     {
                                         Sender.Tell(new Status.Failure(new UnsupportedMarketException()));
                                         return;
                                     }
                                     if (!CheckBalance(o.TotalToBuy))
                                     {
                                         Sender.Tell(new Status.Failure(new NotEnoughFundsException()));
                                         return;
                                     }

                                     Persist(new BuyOrderCreated(o),
                                             c => { ExecuteBuyOrder(c, market); });
                                 });
            Command<NewSellOrder>(o =>
                                  {
                                      
                                      if (!Markets.TryGetValue(o.Position, out var market))
                                      {
                                          Sender.Tell(new Status.Failure(new UnsupportedMarketException()));
                                          return;
                                      }
                                      if (!CheckBalance(o.TotalToSell))
                                      {
                                          Sender.Tell(new Status.Failure(new NotEnoughFundsException()));
                                          return;
                                      }

                                      Persist(new SellOrderCreated(o),
                                              c => { ExecuteSellOrder(c, market); });
                                  });

            Recover<SellOrderCreated>(e =>
                                      {
                                          PendingSellOrders.Add(e.Order.Id,e.Order);
                                      });
            Recover<BuyOrderCreated>(e =>
                                      {
                                          PendingBuyOrders.Add(e.Order.Id,e.Order);
                                      });
            Recover<OrderCompleted>(e =>
                                    {
                                        PendingSellOrders.Remove(e.Num);
                                        PendingBuyOrders.Remove(e.Num);
                                    });
            Recover<MoneyAdded>(e => IncreaseBalance(e.Money));
            Recover<BalanceChangedDueToOrderExecution>(c => IncreaseBalance(c.ObjPrice));
        }

        private void IncreaseBalance(Money money)
        {
            if (Balances.TryGetValue(money.Currency,out Money existingMoney))
                Balances[money.Currency] = money + existingMoney;
            else
                Balances[money.Currency] = money;
        }

        private void ExecuteSellOrder(SellOrderCreated evt, IActorRef market)
        {
            Order o = evt.Order;
            DecreaseBalance(o.Position.Target.Emit(o.Amount));
            CreateSellOrder(market, o);
        }

        private void CreateSellOrder(IActorRef market, Order o)
        {
            ActiveOrders.Add(o.Id);
            var orderActor = Context.ActorOf<SellOrderActor>(o.Id);
            orderActor.Tell(new OrderActor.Init(o));
            orderActor.Tell(new OrderActor.InitBalance(Self));
            orderActor.Tell(new OrderActor.Execute(market));
        }

        private bool CheckBalance(Money total)
        {
            return Balances.TryGetValue(total.Currency, out Money balance) && balance >= total;
        }
        
        private void DecreaseBalance(Money total)
        {
            if (!Balances.TryGetValue(total.Currency, out Money balance))
                throw new NotEnoughFundsException();

            if (balance < total)
                throw new NotEnoughFundsException();

            Balances[total.Currency] = balance - total;
        }


        private void ExecuteBuyOrder(BuyOrderCreated evt, IActorRef market)
        {
            Order o = evt.Order;
            DecreaseBalance(o.Price * o.Amount);
            CreateBuyOrder(market, o);
        }

        private void CreateBuyOrder(IActorRef market, Order o)
        {
            ActiveOrders.Add(o.Id);
            var orderActor = Context.ActorOf<BuyOrderActor>(o.Id);
            orderActor.Tell(new OrderActor.Init(o));
            orderActor.Tell(new OrderActor.InitBalance(Self));
            orderActor.Tell(new OrderActor.Execute(market));
        }

        public class NotEnoughFundsException : Exception
        {
        }

        public class UnsupportedMarketException : Exception
        {
        }

        public class AddDueOrderExecuted
        {
            public string OrderNum { get; }
            public Money TotalChange { get; }

            public AddDueOrderExecuted(string orderNum, Money totalChange)
            {
                OrderNum = orderNum;
                TotalChange = totalChange;
            }
        }
        
        public class OrderCompleted
        {
            public string Num { get; }

            public OrderCompleted(string num)
            {
                Num = num;
            }
        }

        public class BalanceChangedDueToOrderExecution
        {
            public string ObjOrderNum { get; }
            public Money ObjPrice { get; }

            public BalanceChangedDueToOrderExecution(string objOrderNum, Money objPrice)
            {
                ObjOrderNum = objOrderNum;
                ObjPrice = objPrice;
            }

        }

        public class BuyOrderCreated
        {
            public Order Order { get; }

            public BuyOrderCreated(Order order)
            {
                Order = order;
            }
        }

        public class SellOrderCreated
        {
            public Order Order { get; }

            public SellOrderCreated(Order order)
            {
                Order = order;
            }
        }

        public class MoneyAdded
        {
            public Money Money { get; }

            public MoneyAdded(Money money)
            {
                Money = money;
            }
        }

        public class NotEnoughFunds
        {
        }

        public class AddFunds
        {
            public Money Value { get; }

            public AddFunds(Money value)
            {
                Value = value;
            }
        }
    }
}