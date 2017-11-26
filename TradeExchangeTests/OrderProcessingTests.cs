using System;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Should;
using TradeExchangeDomain;
using Xunit;
using Xunit.Abstractions;

namespace TradeExchangeTests
{
    public class OrderProcessingTests : TestKit
    {
        public OrderProcessingTests(ITestOutputHelper output) : base("test_system", output)
        {
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
            givenSellOrderProbe.ExpectMsg<OrderBookActor.OrderReceived>();
            givenSellOrderProbe.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);

            //Executed a buy order
            buyOrderProbe.ExpectMsg<OrderBookActor.OrderReceived>();
            buyOrderProbe.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);
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
            givenSellOrderProbeA.ExpectMsg<OrderBookActor.OrderReceived>();
            givenSellOrderProbeA.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == newBuyOrderA.Amount);
            
            buyOrderProbeA.ExpectMsg<OrderBookActor.OrderReceived>();
            buyOrderProbeA.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == newBuyOrderA.Amount);

            //executed second buy order from both sell orders(5 from first, 5 from second)
            var leftovers = givenSellOrderA.Amount - newBuyOrderA.Amount;
            givenSellOrderProbeB.ExpectMsg<OrderBookActor.OrderReceived>();
            givenSellOrderProbeA.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == leftovers);
            givenSellOrderProbeB.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount
                                                                              == newBuyOrderB.Amount - leftovers);

            buyOrderProbeB.ExpectMsg<OrderBookActor.OrderReceived>();
            buyOrderProbeB.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == leftovers);
            buyOrderProbeB.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == newBuyOrderB.Amount - leftovers);
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
            givenSellOrderProbeA.ExpectMsg<OrderBookActor.OrderReceived>();
            givenSellOrderProbeA.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == givenSellOrderA.Amount);
            
            givenSellOrderProbeB.ExpectMsg<OrderBookActor.OrderReceived>();
            givenSellOrderProbeB.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount
                                                                              == newBuyOrder.Amount
                                                                              - givenSellOrderA.Amount);

            //Executed a buy order  by parts
            givenBuyOrderProbe.ExpectMsg<OrderBookActor.OrderReceived>();
            givenBuyOrderProbe.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == givenSellOrderA.Amount);
            givenBuyOrderProbe.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount
                                                                            == newBuyOrder.Amount
                                                                            - givenSellOrderA.Amount);
        }

        [Fact]
        public void Given_buy_order_actor_When_placing_Then_order_book_receives_new_buy_order()
        {
            var orderNum = Guid.NewGuid();
            var orderActor = Sys.ActorOf<BuyOrderActor>(orderNum.ToString());
            var givenOrder = new Order(Symbol.UsdBtc, new Money(7000, Currency.Usd), 5);

            orderActor.Tell(new OrderActor.Init(givenOrder));

            var orderBook = CreateTestProbe();
            orderActor.Tell(new OrderActor.Execute(orderBook.Ref));

            orderBook.ExpectMsg<NewBuyOrder>(o => o.Amount == givenOrder.Amount);
        }

        [Fact]
        public void Given_order_actor_When_completes_more_then_amount_Then_got_an_error()
        {
            var orderActor = Sys.ActorOf<SellOrderActor>("test");

            orderActor.Tell(new OrderActor.Init(new Order(Symbol.UsdBtc, Currency.Usd.Emit(5000), 2, "test")));
            orderActor.Tell(new OrderActor.InitBalance(TestActor));
            orderActor.Tell(new OrderBookActor.OrderExecuted("test", 1, 5000.Usd()));
            //overflow, some mistake !
            orderActor.Tell(new OrderBookActor.OrderExecuted("test", 1.5M, 6000.Usd()));
            EventFilter.Exception<OrderActor.InvalidOrderStateException>();
        }
        
        


        [Fact]
        public void Given_sell_order_actor_When_executed_by_parts_Then_balance_receives_notification()
        {
            var orderNum = Guid.NewGuid().ToString();
            var orderActor = Sys.ActorOf<SellOrderActor>(orderNum);
            var givenOrder = new Order(Symbol.UsdBtc, new Money(7000, Currency.Usd), 5, orderNum);
            var userBalance = CreateTestProbe();
            var orderBook = CreateTestProbe();

            orderActor.Tell(new OrderActor.Init(givenOrder));
            orderActor.Tell(new OrderActor.InitBalance(userBalance.Ref));
            orderActor.Tell(new OrderActor.Execute(orderBook.Ref));

            orderBook.Send(orderActor, new OrderBookActor.OrderExecuted(orderNum, givenOrder.Amount / 2, 7500.Usd()));
            orderBook.Send(orderActor, new OrderBookActor.OrderExecuted(orderNum, givenOrder.Amount / 2, 7000.Usd()));

            var msgA = userBalance.ExpectMsg<UserBalance.AddDueOrderExecuted>();
            msgA.TotalChange.ShouldEqual(7500.Usd() * givenOrder.Amount / 2);

            var msgB = userBalance.ExpectMsg<UserBalance.AddDueOrderExecuted>();
            msgB.TotalChange.ShouldEqual(7000.Usd() * givenOrder.Amount / 2);

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
        public void Given_sell_order_When_adding_new_matching_buy_Then_it_should_be_executed()
        {
            //Given  order book with a sell order
            var givenSellOrder = new NewSellOrder(Symbol.UsdBtc, new Money(8000, Currency.Usd), 1);
            var givenSellOrderActorProbe = CreateTestProbe("givenSellOrderActor");

            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrder, givenSellOrderActorProbe.Ref);
            givenSellOrderActorProbe.ExpectMsg<OrderBookActor.OrderReceived>();

            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder(Symbol.UsdBtc, new Money(9000, Currency.Usd), givenSellOrder.Amount);
            var buyOrderActorProbe = CreateTestProbe("buyOrderActor");
            orderBook.Tell(newBuyOrder, buyOrderActorProbe.Ref);
            buyOrderActorProbe.ExpectMsg<OrderBookActor.OrderReceived>();

            givenSellOrderActorProbe.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == givenSellOrder.Amount);
            buyOrderActorProbe.ExpectMsg<OrderBookActor.OrderExecuted>(o => o.Amount == newBuyOrder.Amount);
        }


        [Fact]
        public void Given_sell_order_When_adding_new_not_matching_buy_Then_non_orders_are_executed()
        {
            //Given  order book with a sell order
            var givenSellOrder = new NewSellOrder(Symbol.UsdBtc, new Money(8000, Currency.Usd), 1);
            var givenSellOrderActorProbe = CreateTestProbe("givenSellOrderActor");

            var orderBook = Sys.ActorOf(Props.Create(() => new OrderBookActor()));
            orderBook.Tell(givenSellOrder,givenSellOrderActorProbe);

            //when adding matching buy order
            var newBuyOrder = new NewBuyOrder(Symbol.UsdBtc, new Money(7000, Currency.Usd), 1);
            var buyOrderActorProbe = CreateTestProbe("buyOrderActor");
            orderBook.Tell(newBuyOrder, buyOrderActorProbe.Ref);

            givenSellOrderActorProbe.ExpectMsg<OrderBookActor.OrderReceived>();
            buyOrderActorProbe.ExpectMsg<OrderBookActor.OrderReceived>();
            
            givenSellOrderActorProbe.ExpectNoMsg(TimeSpan.FromSeconds(1));
            buyOrderActorProbe.ExpectNoMsg(TimeSpan.FromSeconds(1));
        }
    }
}