using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Akka.Util.Internal;
using NMoneys;
using Xunit;

namespace TradeExchangeTests
{
    public class OrderProcessingTests:TestKit
    {
        
        [Fact]
        public void Given_sell_order_When_adding_new_not_matching_buy_Then_non_orders_are_executed()
        {
            //Given  order book with a sell order
            var givenSellOrder = new NewSellOrder("USDBTC", new Money(8000, Currency.Usd), 1);
            var givenSellOrderActorProbe = CreateTestProbe("givenSellOrderActor");

            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrder, givenSellOrderActorProbe.Ref);
            
            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder("USDBTC", new Money(7000, Currency.Usd), 1);
            var buyOrderActorProbe = CreateTestProbe("buyOrderActor");
            orderBook.Tell(newBuyOrder, buyOrderActorProbe.Ref);

            givenSellOrderActorProbe.ExpectNoMsg(TimeSpan.FromSeconds(1));
            buyOrderActorProbe.ExpectNoMsg(TimeSpan.FromSeconds(1));
        }
        
        [Fact]
        public void Given_sell_order_When_adding_new_matching_buy_Then_it_should_be_executed()
        {
            //Given  order book with a sell order
            var givenSellOrder = new NewSellOrder("USDBTC", new Money(8000, Currency.Usd), 1);
            var givenSellOrderActorProbe = CreateTestProbe("givenSellOrderActor");

            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrder, givenSellOrderActorProbe.Ref);
            
            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder("USDBTC", new Money(9000, Currency.Usd) , givenSellOrder.Amount);
            var buyOrderActorProbe = CreateTestProbe("buyOrderActor");
            orderBook.Tell(newBuyOrder, buyOrderActorProbe.Ref);

            givenSellOrderActorProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == givenSellOrder.Amount);
            buyOrderActorProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);
        }
        
        [Fact]
        public void Given_big_order_When_adding_new_small_matching_Then_it_should_be_executed_and_initial_order_executed_partially()
        {
            //Given order book with a sell order
            var givenSellOrder = new NewSellOrder("USDBTC", new Money(8000, Currency.Usd), 10);
            var givenSellOrderProbe = CreateTestProbe();
            
            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrder,givenSellOrderProbe);
            
            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder("USDBTC", new Money(10000, Currency.Usd),1);
            var buyOrderProbe = CreateTestProbe();

            orderBook.Tell(newBuyOrder,buyOrderProbe);
            
            //executed a sell order
            givenSellOrderProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);
            
            //Executed a buy order
            buyOrderProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);
        }
        
        [Fact]
        public void Given_big_orders_When_adding_new_big_matching_Then_it_should_be_executed_and_initial_order_executed_partially()
        {
            //Given order book with a sell order
            var givenSellOrderA = new NewSellOrder("USDBTC", new Money(8000, Currency.Usd), 10);
            var givenSellOrderProbeA = CreateTestProbe();

            var givenSellOrderB = new NewSellOrder("USDBTC", new Money(8000, Currency.Usd), 10);
            var givenSellOrderProbeB = CreateTestProbe();

            
            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrderA,givenSellOrderProbeA);
            orderBook.Tell(givenSellOrderB,givenSellOrderProbeB);
            
            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder("USDBTC", new Money(8000, Currency.Usd),15);
            var givenBuyOrderProbe = CreateTestProbe();

            orderBook.Tell(newBuyOrder,givenBuyOrderProbe);
            
            //executed a sell order
            givenSellOrderProbeA.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == givenSellOrderA.Amount);
            givenSellOrderProbeB.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount - givenSellOrderA.Amount);
            
            //Executed a buy order
            givenBuyOrderProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);
        }


        [Fact]
        public void Given_big_orders_When_adding_many_small_partly_matching_orders_Then_it_should_be_executed()
        {
            //Given order book with a sell order
            var givenSellOrderA = new NewSellOrder("USDBTC", new Money(8000, Currency.Usd), 10);
            var givenSellOrderProbeA = CreateTestProbe();

            var givenSellOrderB = new NewSellOrder("USDBTC", new Money(9000, Currency.Usd), 10);
            var givenSellOrderProbeB = CreateTestProbe();

            
            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrderA, givenSellOrderProbeA);
            orderBook.Tell(givenSellOrderB, givenSellOrderProbeB);
            
            //when adding matching buy order
            var newBuyOrderA = new NewBuyOrder("USDBTC", new Money(8200, Currency.Usd),5);
            var buyOrderProbeA = CreateTestProbe();

            var newBuyOrderB = new NewBuyOrder("USDBTC", new Money(9500, Currency.Usd),10);
            var buyOrderProbeB = CreateTestProbe();

            orderBook.Tell(newBuyOrderA, buyOrderProbeA);
            orderBook.Tell(newBuyOrderB, buyOrderProbeB);

            //executed first buy order from first sell order
            givenSellOrderProbeA.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrderA.Amount);
            buyOrderProbeA.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrderA.Amount);
            
            //executed second buy order from both sell orders(5 from first, 5 from second)
            var leftovers = givenSellOrderA.Amount - newBuyOrderA.Amount;
            givenSellOrderProbeA.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == leftovers);
            givenSellOrderProbeB.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrderB.Amount - leftovers);
            buyOrderProbeB.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrderB.Amount);
        }
        
    }

    public class OrderActor   : ReceiveActor
    {
        public OrderActor()
        {
        }

      
        public class OrderExecuted
        {
            public decimal? Amount { get; }

            public Order Order { get; }
            public OrderExecuted(decimal? amount = null)
            {
                Amount = amount;
            }
        
            public static OrderExecuted Fully { get; } = new OrderExecuted(null);
        }
    }
    public class BuyOrderActor
    {
        public BuyOrderActor()
        {
        }
    }


    public class Order
    {
        public string Position { get; }
        public Money Price { get; } // per 1 Amount, no Lot support
        public decimal Amount { get; }

        protected Order(string position, Money price, decimal amount)
        {
            Position = position;
            Price = price;
            Amount = amount;
        }
    }
    
    public class NewBuyOrder : Order
    {
        public NewBuyOrder(string position, Money price, decimal amount)     :base(position, price, amount)
        {
        }
    }

    public class NewSellOrder : Order
    {
        public NewSellOrder(string usdbtc, Money money, decimal amount):base(usdbtc,money,amount)
        {
        }
    }

    public class OrderBookActor  :ReceiveActor
    {
        class PlacedOrder
        {
            public IActorRef Sender;
            public Order Order;
            public decimal Amount; //to track order amount - cannot modify it on Original Order
        }
        
        SortedDictionary<decimal,List<PlacedOrder>> Sellers = new SortedDictionary<decimal, List<PlacedOrder>>();
        SortedDictionary<decimal,List<PlacedOrder>> Buyers = new SortedDictionary<decimal, List<PlacedOrder>>();

        class OrderMatchContext
        {
            public decimal Done { get; private set; }
            public decimal Left { get; private set; }
            
            private Order _order;

            public void Init(Order o)
            {
                _order = o;
                Left = _order.Amount;
                Done = 0;
            }

            public bool IsFullfilled => Done >= _order.Amount;

            public void Fulfill(decimal amount)
            {
                Done += amount;
                Left -= amount;
            }
        }
        private OrderMatchContext MatchContext = new OrderMatchContext();
        
        public OrderBookActor()
        {
            Receive<NewBuyOrder>(o =>
                                 {
                                     MatchContext.Init(o);
                                     var executedSellOrders = ExecuteOrder(MatchContext, d => d <= o.Price.Amount, Sellers).ToArray();
                                     RemoveOrders(executedSellOrders, Sellers);
                                     AddOrderIfNeed(o, MatchContext, Buyers);
                                     
                                     if(MatchContext.Done > 0)
                                         Sender.Tell(new OrderActor.OrderExecuted(MatchContext.Done));
                                     
                                 });
            Receive<NewSellOrder>(o =>
                                  {
                                      MatchContext.Init(o);
                                      var executedSellOrders = ExecuteOrder(MatchContext, d => d >= o.Price.Amount, Buyers).ToArray();
                                      RemoveOrders(executedSellOrders, Buyers);

                                      AddOrderIfNeed(o, MatchContext, Sellers);
                                      
                                      if(MatchContext.Done > 0)
                                          Sender.Tell(new OrderActor.OrderExecuted(MatchContext.Done));

                                  });


        }

        private void AddOrderIfNeed(Order o, OrderMatchContext orderMatchContext, SortedDictionary<decimal, List<PlacedOrder>> sortedDictionary)
        {
            if (orderMatchContext.Done < o.Amount)
            {
                var placedOrder = new PlacedOrder()
                                  {
                                      Amount = o.Amount - orderMatchContext.Done,
                                      Order = o,
                                      Sender = Sender
                                  };
                if (sortedDictionary.TryGetValue(o.Price.Amount, out List<PlacedOrder> orders))
                    orders.Add(placedOrder);
                else sortedDictionary.Add(o.Price.Amount, new List<PlacedOrder>() {placedOrder});

            }
        }

        private void RemoveOrders(PlacedOrder[] executedSellOrders, SortedDictionary<decimal, List<PlacedOrder>> sortedDictionary)
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
/// returns executed orders to remove
/// </summary>
        private IEnumerable<PlacedOrder> ExecuteOrder(OrderMatchContext ctx, Predicate<decimal> predicate, SortedDictionary<decimal, List<PlacedOrder>> sortedDictionary)
        {

            using (var iterator = sortedDictionary.Where(s => predicate(s.Key)).SelectMany(i => i.Value).GetEnumerator())
            {
                while (iterator.MoveNext() && !ctx.IsFullfilled)
                {

                    var matchedOrder = iterator.Current;
                      
                    if (matchedOrder.Amount <= ctx.Left)
                    {
                        //matched order fully executed
                        ctx.Fulfill(matchedOrder.Amount);
                        matchedOrder.Sender.Tell(new OrderActor.OrderExecuted(matchedOrder.Amount));
                        yield return matchedOrder; 

                    }
                    else
                    {
                        //matched order partially executed
                        //initial order fully executed
                        matchedOrder.Amount -= ctx.Left;
                        matchedOrder.Sender.Tell(new OrderActor.OrderExecuted(ctx.Left));
                        ctx.Fulfill(ctx.Left);
                    
                        yield break;
                    }
                }
            }
        }
    }
}