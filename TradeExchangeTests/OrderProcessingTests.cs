using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using Akka.Actor;
using Akka.Persistence;
using Akka.TestKit.Xunit2;
using Akka.Util.Internal;
using Should;
using Xunit;

namespace TradeExchangeTests
{
    /// <summary>
    ///  did not find good money package for netstandard with custom curriencies support
    /// </summary>
    public class Money
    {
        public decimal Amount { get; }
        public Currency Currency { get; }

        public Money(decimal amount, Currency cur)
        {
            Amount = amount;
            Currency = cur;
        }
    }

    public static class CurrencyExtensions
    {
        public static Money Emit(this Currency currency, decimal amount)
        {
            return new Money(amount, currency);
        }
    }

    public class Currency
    {
        private Currency(string name)
        {
        }

        public static Currency Usd { get; } = new Currency("USD");
        public static Currency Btc { get; } = new Currency("BTC");
    }

    public class Symbol
    {
        private readonly string _name;
        public Currency Base { get; }
        public Currency Target { get; }

        public Symbol(Currency @base, Currency target)
        {
            Base = @base;
            Target = target;
            _name = Base.ToString() + Target.ToString();
        }

        public override string ToString() => _name;

        public static Symbol UsdBtc { get; } = new Symbol(Currency.Usd, Currency.Btc);
    }


    public static class SymbolExtensions
    {
        public static NewSellOrder Sell(this Symbol symbol, decimal price, decimal amount, string id = null)
        {
            return new NewSellOrder(symbol, symbol.Base.Emit(price), amount, id);
        }

        public static NewBuyOrder Buy(this Symbol symbol, decimal price, decimal amount, string id=null)
        {
            return new NewBuyOrder(symbol, new Money(price, symbol.Base), amount, id);
        }
    }

    public static class ActoSystemExtensions
    {
        public static Market NewMarket(this ActorSystem sys, Symbol symbol)
        {
            return new Market(sys.ActorOf<OrderBookActor>(), symbol);
        }
    }

    public class Market
    {
        private readonly Symbol _symbol;
        private readonly IActorRef _orderbook;

        public Market(IActorRef orderbook, Symbol symbol)
        {
            _orderbook = orderbook;
            _symbol = symbol;
        }

        public Market Seller(decimal price, decimal amount)
        {
            _orderbook.Tell(_symbol.Sell(price, amount));
            return this;
        }

        public Market Buyer(decimal price, decimal amount)
        {
            _orderbook.Tell(_symbol.Buy(price, amount));
            return this;
        }

        public IActorRef OrderBook()
        {
            return _orderbook;
        }
    }

    public class OrderProcessingTests : TestKit
    {
        [Fact]
        public void Given_order_actor_When_Restart_it_should_maintain_state()
        {
            var balance = Sys.ActorOf<UserBalance>("test balance");
            balance.Tell(new AddFunds(Currency.Btc.Emit(10)));
            var orderBook = CreateTestProbe();
            balance.Tell(new AddMarket("test market",orderBook, Symbol.UsdBtc));
            var newSellOrder = Symbol.UsdBtc.Sell(7000,5,"a");
            balance.Tell(newSellOrder);
            balance.Tell(new OrderActor.OrderExecuted("a",5,8000));
            
            orderBook.ExpectMsg<NewSellOrder>(o => o.Amount == 5 && o.Price.Amount == 7000);
        }
        

        [Fact]
        public void Given_balance_actor_When_creating_order_Then_order_book_receives_order()
        {
            var balance = Sys.ActorOf<UserBalance>("test balance");
            balance.Tell(new AddFunds(Currency.Btc.Emit(10)));
            var orderBook = CreateTestProbe();
            balance.Tell(new AddMarket("test market",orderBook, Symbol.UsdBtc));
            balance.Tell(Symbol.UsdBtc.Sell(7000,5));
            orderBook.ExpectMsg<NewSellOrder>(o => o.Amount == 5 && o.Price.Amount == 7000);
        }

        [Fact]
        public void
            Given_balance_When_creating_order_more_then_balance_have_Then_got_an_error_and_not_message_for_order_book()
        {
            var balance = Sys.ActorOf<UserBalance>("test balance");
            balance.Tell(new AddFunds(Currency.Btc.Emit(10)));
            var orderBook = CreateTestProbe();
            balance.Tell(new AddMarket("test market",orderBook, Symbol.UsdBtc));
            balance.Tell(Symbol.UsdBtc.Sell(7000,30));
            ExpectMsg<UserBalance.NotEnoughFunds>();
            orderBook.ExpectNoMsg();
        }

       

        [Fact]
        public void Given_order_actor_When_completes_more_then_amount_Then_got_an_error()
        {
            var orderActor = Sys.ActorOf<SellOrderActor>("test");
            Watch(orderActor);

            orderActor.Tell(new OrderActor.Init(new Order(Symbol.UsdBtc, Currency.Usd.Emit(5000),2)));
            orderActor.Tell(new OrderActor.OrderExecuted(1));
            //overflow, some mistake !
            orderActor.Tell(new OrderActor.OrderExecuted(1.5M));
            //order should terminate, as we have invalid state; 
            ExpectTerminated(orderActor);
        }


        [Fact]
        public void Given_balance_actor_When_completing_order_by_parts_Then_balance_changes()
        {
            //given market
            var orderBook = Sys.NewMarket(Symbol.UsdBtc)
                               .Seller(20000, 1)
                               .Seller(10000, 0.25M)
                               .Seller(5000, 1)
                               .OrderBook();

            var balance = this.ActorOfAsTestActorRef<UserBalance>("test balance");
            balance.Ref.Tell(new AddFunds(Currency.Btc.Emit(10)));
            balance.Ref.Tell(new AddFunds(Currency.Usd.Emit(15000)));
            balance.Ref.Tell(new AddMarket("test market",orderBook, Symbol.UsdBtc));
            balance.Ref.Tell(Symbol.UsdBtc.Buy(11000,3));

            ExpectMsg<OrderActor.OrderExecuted>();

            //will buy 1 by 5000 + 0.25 by 10000
            balance.UnderlyingActor.Balances[Currency.Btc].Amount.ShouldEqual(11.5M);
            balance.UnderlyingActor.Balances[Currency.Usd].Amount.ShouldEqual(10000M);
        }

        [Fact]
        public void Given_sell_order_actor_When_executed_by_parts_Then_balance_receives_notification()
        {
            var orderNum = Guid.NewGuid();
            var orderActor = Sys.ActorOf<SellOrderActor>(orderNum.ToString());
            var givenOrder = new Order(Symbol.UsdBtc, new Money(7000, Currency.Usd), 5);
            var userBalance = CreateTestProbe();
            var orderBook = CreateTestProbe();

            orderActor.Tell(new OrderActor.Init(givenOrder));
            orderActor.Tell(new OrderActor.Execute(orderBook));

            orderBook.Send(orderActor, new OrderActor.OrderExecuted(givenOrder.Amount / 2));
            orderBook.Send(orderActor, new OrderActor.OrderExecuted(givenOrder.Amount / 2));

            userBalance.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == givenOrder.Amount / 2);
            userBalance.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == givenOrder.Amount / 2);
            userBalance.ExpectMsg<OrderActor.OrderCompleted>();
        }


        [Fact]
        public void Given_sell_order_actor_When_placing_Then_order_book_receives_new_sell_order()
        {
            var orderNum = Guid.NewGuid();
            var orderActor = Sys.ActorOf<SellOrderActor>(orderNum.ToString());
            var givenOrder = new Order(Symbol.UsdBtc, new Money(7000, Currency.Usd), 5);
            var orderBook = CreateTestProbe();

            orderActor.Tell(new OrderActor.Init(givenOrder));
            orderActor.Tell(new OrderActor.Execute(orderBook.Ref));

            orderBook.ExpectMsg<NewSellOrder>(o => o.Amount == givenOrder.Amount);
        }

        [Fact]
        public void Given_buy_order_actor_When_placing_Then_order_book_receives_new_buy_order()
        {
            var orderNum = Guid.NewGuid();
            var orderActor = Sys.ActorOf<BuyOrderActor>(orderNum.ToString());
            var givenOrder = new Order(Symbol.UsdBtc, new Money(7000, Currency.Usd), 5);

            orderActor.Tell(new OrderActor.Init(givenOrder));

            var orderBook = this.CreateTestProbe();
            orderActor.Tell(new OrderActor.Execute(orderBook.Ref));

            orderBook.ExpectMsg<NewBuyOrder>(o => o.Amount == givenOrder.Amount);
        }


        [Fact]
        public void Given_sell_order_When_adding_new_not_matching_buy_Then_non_orders_are_executed()
        {
            //Given  order book with a sell order
            var givenSellOrder = new NewSellOrder(Symbol.UsdBtc, new Money(8000, Currency.Usd), 1);
            var givenSellOrderActorProbe = CreateTestProbe("givenSellOrderActor");

            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrder, givenSellOrderActorProbe.Ref);

            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder(Symbol.UsdBtc, new Money(7000, Currency.Usd), 1);
            var buyOrderActorProbe = CreateTestProbe("buyOrderActor");
            orderBook.Tell(newBuyOrder, buyOrderActorProbe.Ref);

            givenSellOrderActorProbe.ExpectNoMsg(TimeSpan.FromSeconds(1));
            buyOrderActorProbe.ExpectNoMsg(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Given_sell_order_When_adding_new_matching_buy_Then_it_should_be_executed()
        {
            //Given  order book with a sell order
            var givenSellOrder = new NewSellOrder(Symbol.UsdBtc, new Money(8000, Currency.Usd), 1);
            var givenSellOrderActorProbe = CreateTestProbe("givenSellOrderActor");

            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrder, givenSellOrderActorProbe.Ref);

            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder(Symbol.UsdBtc, new Money(9000, Currency.Usd), givenSellOrder.Amount);
            var buyOrderActorProbe = CreateTestProbe("buyOrderActor");
            orderBook.Tell(newBuyOrder, buyOrderActorProbe.Ref);

            givenSellOrderActorProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == givenSellOrder.Amount);
            buyOrderActorProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);
        }

        [Fact]
        public void
            Given_big_order_When_adding_new_small_matching_Then_it_should_be_executed_and_initial_order_executed_partially()
        {
            //Given order book with a sell order
            var givenSellOrder = new NewSellOrder(Symbol.UsdBtc, new Money(8000, Currency.Usd), 10);
            var givenSellOrderProbe = CreateTestProbe();

            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrder, givenSellOrderProbe);

            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder(Symbol.UsdBtc, new Money(10000, Currency.Usd), 1);
            var buyOrderProbe = CreateTestProbe();

            orderBook.Tell(newBuyOrder, buyOrderProbe);

            //executed a sell order
            givenSellOrderProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);

            //Executed a buy order
            buyOrderProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);
        }

        [Fact]
        public void
            Given_big_orders_When_adding_new_big_matching_Then_it_should_be_executed_and_initial_order_executed_partially()
        {
            //Given order book with a sell order
            var givenSellOrderA = new NewSellOrder(Symbol.UsdBtc, new Money(8000, Currency.Usd), 10);
            var givenSellOrderProbeA = CreateTestProbe();

            var givenSellOrderB = new NewSellOrder(Symbol.UsdBtc, new Money(8000, Currency.Usd), 10);
            var givenSellOrderProbeB = CreateTestProbe();


            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrderA, givenSellOrderProbeA);
            orderBook.Tell(givenSellOrderB, givenSellOrderProbeB);

            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder(Symbol.UsdBtc, new Money(8000, Currency.Usd), 15);
            var givenBuyOrderProbe = CreateTestProbe();

            orderBook.Tell(newBuyOrder, givenBuyOrderProbe);

            //executed a sell order
            givenSellOrderProbeA.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == givenSellOrderA.Amount);
            givenSellOrderProbeB.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount
                                                                          == newBuyOrder.Amount
                                                                          - givenSellOrderA.Amount);

            //Executed a buy order
            givenBuyOrderProbe.ExpectMsg<OrderActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);
        }


        [Fact]
        public void Given_big_orders_When_adding_many_small_partly_matching_orders_Then_it_should_be_executed()
        {
            //Given order book with a sell order
            var givenSellOrderA = new NewSellOrder(Symbol.UsdBtc, new Money(8000, Currency.Usd), 10);
            var givenSellOrderProbeA = CreateTestProbe();

            var givenSellOrderB = new NewSellOrder(Symbol.UsdBtc, new Money(9000, Currency.Usd), 10);
            var givenSellOrderProbeB = CreateTestProbe();


            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrderA, givenSellOrderProbeA);
            orderBook.Tell(givenSellOrderB, givenSellOrderProbeB);

            //when adding matching buy order
            var newBuyOrderA = new NewBuyOrder(Symbol.UsdBtc, new Money(8200, Currency.Usd), 5);
            var buyOrderProbeA = CreateTestProbe();

            var newBuyOrderB = new NewBuyOrder(Symbol.UsdBtc, new Money(9500, Currency.Usd), 10);
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

    public class AddMarket
    {
        public AddMarket(string testMarket, IActorRef orderBook, Symbol usdBtc)
        {
            throw new NotImplementedException();
        }
    }

    public class AddFunds
    {
        public AddFunds(Money emit)
        {
            throw new NotImplementedException();
        }
    }

    public class UserBalance : ReceivePersistentActor
    {
        public IDictionary<Currency, Money> Balances { get; } = new Dictionary<Currency, Money>();
        public override string PersistenceId { get; }

        public class NotEnoughFunds
        {
        }
    }

    public class SellOrderActor : ReceivePersistentActor
    {
        public override string PersistenceId { get; }
    }

    public class OrderActor : ReceivePersistentActor
    {
        private Order _order;

            public OrderActor()
        {
            Recover<Order>(o => _order = o);
        }

        public override string PersistenceId { get; }

        public class OrderExecuted
        {
            public decimal? Amount { get; }

            public Order Order { get; }

            public OrderExecuted(decimal? amount = null)
            {
                Amount = amount;
            }

            public OrderExecuted(string amount, int i, int i1)
            {
                throw new NotImplementedException();
            }

            public static OrderExecuted Fully { get; } = new OrderExecuted(null);
        }

        public class Init
        {
            public Init(Order givenOrder)
            {
                throw new NotImplementedException();
            }
        }

        public class Execute
        {
            public Execute(IActorRef orderBookRef)
            {
                throw new NotImplementedException();
            }
        }

        public class OrderCompleted
        {
        }
    }

    public class BuyOrderActor : ReceivePersistentActor
    {
        public BuyOrderActor()
        {
        }

        public override string PersistenceId { get; }
    }


    public class Order
    {
        public Symbol Position { get; }
        public Money Price { get; } // per 1 Amount, no Lot support
        public decimal Amount { get; }
        public DateTime Time { get; }
        public string Id { get; }

        public Order(Symbol position, Money price, decimal amount, string id = null,DateTime? time = null)
        {
            Position = position;
            Price = price;
            Amount = amount;
            Time = time ?? DateTime.Now;
            Id = id ?? Guid.NewGuid().ToString();
        }
    }

    public class NewBuyOrder : Order
    {
        public NewBuyOrder(Symbol position, Money price, decimal amount, string id = null) : base(position, price, amount, id)
        {
        }
    }

    public class NewSellOrder : Order
    {
        public NewSellOrder(Symbol usdbtc, Money money, decimal amount, string id = null) : base(usdbtc, money, amount, id)
        {
        }
    }

    public class OrderBookActor : ReceiveActor
    {
        class PlacedOrder
        {
            public IActorRef Sender;
            public Order Order;
            public decimal Amount; //to track order amount - cannot modify it on Original Order
        }

        SortedDictionary<decimal, List<PlacedOrder>> Sellers = new SortedDictionary<decimal, List<PlacedOrder>>();
        SortedDictionary<decimal, List<PlacedOrder>> Buyers = new SortedDictionary<decimal, List<PlacedOrder>>();

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
                                     var executedSellOrders =
                                         ExecuteOrder(MatchContext, d => d <= o.Price.Amount, Sellers).ToArray();
                                     RemoveOrders(executedSellOrders, Sellers);
                                     AddOrderIfNeed(o, MatchContext, Buyers);

                                     if (MatchContext.Done > 0)
                                         Sender.Tell(new OrderActor.OrderExecuted(MatchContext.Done));
                                 });
            Receive<NewSellOrder>(o =>
                                  {
                                      MatchContext.Init(o);
                                      var executedSellOrders =
                                          ExecuteOrder(MatchContext, d => d >= o.Price.Amount, Buyers).ToArray();
                                      RemoveOrders(executedSellOrders, Buyers);

                                      AddOrderIfNeed(o, MatchContext, Sellers);

                                      if (MatchContext.Done > 0)
                                          Sender.Tell(new OrderActor.OrderExecuted(MatchContext.Done));
                                  });
        }

        private void AddOrderIfNeed(Order o,
                                    OrderMatchContext orderMatchContext,
                                    SortedDictionary<decimal, List<PlacedOrder>> sortedDictionary)
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
        /// returns executed orders to remove
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