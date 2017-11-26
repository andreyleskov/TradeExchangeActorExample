using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace TradeExchangeDomain
{
    public class OrderBookActor : ReceiveActor
    {
        private readonly SortedDictionary<decimal, List<PlacedOrder>> Buyers =
            new SortedDictionary<decimal, List<PlacedOrder>>();

        private readonly OrderMatchContext MatchContext = new OrderMatchContext();

        private readonly SortedDictionary<decimal, List<PlacedOrder>> Sellers =
            new SortedDictionary<decimal, List<PlacedOrder>>();

        public OrderBookActor()
        {
            Receive<NewBuyOrder>(o =>
                                 {
                                     Sender.Tell(new OrderReceived(o.Id));
                                     MatchContext.Init(o, Sender);
                                     var executedSellOrders =
                                         ExecuteOrder(MatchContext, d => d <= o.Price.Amount, Sellers).ToArray();
                                     RemoveOrders(executedSellOrders, Sellers);
                                     AddOrderIfNeed(o, MatchContext, Buyers);
                                 });
            Receive<NewSellOrder>(o =>
                                  {
                                      Sender.Tell(new OrderReceived(o.Id));
                                      MatchContext.Init(o, Sender);
                                      var executedSellOrders =
                                          ExecuteOrder(MatchContext, d => d >= o.Price.Amount, Buyers).ToArray();
                                      RemoveOrders(executedSellOrders, Buyers);

                                      AddOrderIfNeed(o, MatchContext, Sellers);
                                  });
        }

        private void AddOrderIfNeed(Order o,
                                    OrderMatchContext orderMatchContext,
                                    SortedDictionary<decimal, List<PlacedOrder>> sortedDictionary)
        {
            if (orderMatchContext.Done < o.Amount)
            {
                var placedOrder = new PlacedOrder
                                  {
                                      Amount = o.Amount - orderMatchContext.Done,
                                      Order = o,
                                      Sender = Sender
                                  };
                if (sortedDictionary.TryGetValue(o.Price.Amount, out var orders))
                    orders.Add(placedOrder);
                else sortedDictionary.Add(o.Price.Amount, new List<PlacedOrder> {placedOrder});
            }
        }

        private void RemoveOrders(PlacedOrder[] executedSellOrders,
                                  SortedDictionary<decimal, List<PlacedOrder>> sortedDictionary)
        {
            foreach (var ex in executedSellOrders)
            {
                var placedOrders = sortedDictionary[ex.Order.Price.Amount];
                placedOrders.Remove(ex);
                if (!placedOrders.Any())
                    sortedDictionary.Remove(ex.Amount);
            }
        }

        /// <summary>
        ///     returns executed orders to remove
        /// </summary>
        private IEnumerable<PlacedOrder> ExecuteOrder(OrderMatchContext ctx,
                                                      Predicate<decimal> predicate,
                                                      SortedDictionary<decimal, List<PlacedOrder>> sortedDictionary)
        {
            using (var iterator =
                sortedDictionary.Where(s => predicate(s.Key)).SelectMany(i => i.Value).GetEnumerator())
            {
                while (iterator.MoveNext() && !ctx.IsFullfilled)
                {
                    var matchedOrder = iterator.Current;

                    if (matchedOrder.Amount <= ctx.Left)
                    {
                        //matched order fully executed
                        ctx.Fulfill(matchedOrder.Amount);
                        matchedOrder.Sender.Tell(new OrderExecuted(matchedOrder.Order.Id,
                                                                   matchedOrder.Amount,
                                                                   matchedOrder.Order.Price));
                        ctx.OrderSender.Tell(new OrderExecuted(ctx.Order.Id,
                                                               matchedOrder.Amount,
                                                               matchedOrder.Order.Price));

                        yield return matchedOrder;
                    }
                    else
                    {
                        //matched order partially executed
                        //initial order fully executed
                        matchedOrder.Amount -= ctx.Left;
                        matchedOrder.Sender.Tell(new OrderExecuted(matchedOrder.Order.Id,
                                                                   ctx.Left,
                                                                   matchedOrder.Order.Price));
                        ctx.OrderSender.Tell(new OrderExecuted(ctx.Order.Id, ctx.Left, matchedOrder.Order.Price));

                        ctx.Fulfill(ctx.Left);

                        yield break;
                    }
                }
            }
        }

        private class PlacedOrder
        {
            public decimal Amount; //to track order amount - cannot modify it on Original Order
            public Order Order;
            public IActorRef Sender;
        }

        private class OrderMatchContext
        {
            public Order Order;
            public decimal Done { get; private set; }
            public decimal Left { get; private set; }
            public IActorRef OrderSender { get; private set; }

            public bool IsFullfilled => Done >= Order.Amount;

            public void Init(Order o, IActorRef sender)
            {
                Order = o;
                Left = Order.Amount;
                Done = 0;
                OrderSender = sender;
            }

            public void Fulfill(decimal amount)
            {
                Done += amount;
                Left -= amount;
            }
        }


        public class OrderExecuted
        {
            public readonly Money Price;
            public string OrderNum;

            public OrderExecuted(string orderNum, decimal amount, Money price)
            {
                Price = price;
                OrderNum = orderNum;
                Amount = amount;
            }

            public decimal Amount { get; }
        }

        public class OrderReceived
        {
            public string Num;

            public OrderReceived(string Num)
            {
                this.Num = Num;
            }
        }
    }
}