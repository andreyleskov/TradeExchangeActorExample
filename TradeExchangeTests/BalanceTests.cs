﻿using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Serilog;
using Serilog.Core;
using Should;
using TradeExchangeDomain;
using TradeExchangeDomain.Orders;
using Xunit;
using Xunit.Abstractions;

namespace TradeExchangeTests
{
    public class BalanceTests : TestKit
    {
        public BalanceTests(ITestOutputHelper output) :
            base(
        @"akka {  
                    stdout-loglevel = DEBUG
                    loglevel = DEBUG
                    log-config-on-start = on
                    actor {                
                        debug {  
                              receive = on 
                              autoreceive = on
                              lifecycle = on
                              event-stream = on
                              unhandled = on
                        }
                    }","test", output)
        {
        
        }


        [Fact]
        public async Task Given_balance_actor_When_completing_order_by_parts_Then_balance_changes()
        {
            //given market
            var orderBook = Sys.NewMarket(Symbol.UsdBtc)
                               .Seller(20000, 1)
                               .Seller(10000, 0.25M)
                               .Seller(5000, 1)
                               .OrderBook();

            var balance = ActorOfAsTestActorRef<UserBalance>("test_balance");
            balance.Ref.Tell(new UserBalance.AddFunds(Currency.Usd.Emit(40000)));
            balance.Ref.Tell(new UserBalance.AddFunds(Currency.Btc.Emit(10)));
            balance.Ref.Tell(new UserBalance.AddMarket("test market", orderBook, Symbol.UsdBtc));
            balance.Ref.Tell(Symbol.UsdBtc.Buy(11000, 3));

            //will buy 1 by 5000 + 0.25 by 10000
            await Task.Delay(TimeSpan.FromSeconds(1));

            balance.UnderlyingActor.Balances[Currency.Btc].Amount.ShouldEqual(11.25M);
            //all money for buy remain booked for order, even if it is not fulfilled
            balance.UnderlyingActor.Balances[Currency.Usd].Amount.ShouldEqual(7000M);
        }
        
        [Fact]
        public async Task Given_balance_actor_When_creating_sell_order_Then_sender_receives_notification()
        {
            //given market
            var orderBook = Sys.NewMarket(Symbol.UsdBtc)
                               .Seller(20000, 1)
                               .OrderBook();

            var balance = Sys.ActorOf<UserBalance>("test_balance");
            balance.Tell(new UserBalance.AddFunds(Currency.Usd.Emit(40000)));
            balance.Tell(new UserBalance.AddFunds(Currency.Btc.Emit(10)));
            balance.Tell(new UserBalance.AddMarket("test market", orderBook, Symbol.UsdBtc));
           
            await balance.Ask<OrderBookActor.OrderReceived>(Symbol.UsdBtc.Buy(11000, 3), TimeSpan.FromSeconds(1));
        }
        [Fact]
        public async Task Given_balance_actor_When_creating_buy_order_Then_sender_receives_notification()
        {
            //given market
            var orderBook = Sys.NewMarket(Symbol.UsdBtc)
                               .Seller(20000, 1)
                               .OrderBook();

            var balance = Sys.ActorOf<UserBalance>("test_balance");
            balance.Tell(new UserBalance.AddFunds(Currency.Usd.Emit(40000)));
            balance.Tell(new UserBalance.AddFunds(Currency.Btc.Emit(10)));
            balance.Tell(new UserBalance.AddMarket("test market", orderBook, Symbol.UsdBtc));
           
            await balance.Ask<OrderBookActor.OrderReceived>(Symbol.UsdBtc.Sell(11000, 3), TimeSpan.FromSeconds(1));
        }
        
        
        
        [Fact]
        public async Task Given_balance_actor_When_asking_for_balance_it_returns_balance()
        {
            var balance = Sys.ActorOf<UserBalance>("test_balance");
            var givenUsd = Currency.Usd.Emit(40000);
            balance.Tell(new UserBalance.AddFunds(givenUsd));
            var givenBtc = Currency.Btc.Emit(10);
            balance.Tell(new UserBalance.AddFunds(givenBtc));

            var usd = await balance.Ask<Money>(new UserBalance.GetBalance(Currency.Usd));
            usd.ShouldEqual(givenUsd);
            
            var btc = await balance.Ask<Money>(new UserBalance.GetBalance(Currency.Btc));
            btc.ShouldEqual(givenBtc);
        }

        
        
        [Fact]
        public void Given_balance_actor_When_creating_order_Then_order_book_receives_order()
        {
            var balance = Sys.ActorOf<UserBalance>("test_balance");
            balance.Tell(new UserBalance.AddFunds(Currency.Btc.Emit(10)));
            var orderBook = CreateTestProbe();
            balance.Tell(new UserBalance.AddMarket("test market", orderBook, Symbol.UsdBtc));
            balance.Tell(Symbol.UsdBtc.Sell(7000, 5));
            orderBook.ExpectMsg<SellOrder>(o => o.Amount == 5 && o.Price.Amount == 7000);
        }

        [Fact]
        public async Task Given_balance_actor_When_Restart_it_should_maintain_state_and_orders()
        {
            var balance = Sys.ActorOf<UserBalance>("test_balance");
            balance.Tell(new UserBalance.AddFunds(Currency.Btc.Emit(10)));

            var orderBook = CreateTestProbe();
            balance.Tell(new UserBalance.AddMarket("test_market", orderBook, Symbol.UsdBtc));
            var newSellOrder = Symbol.UsdBtc.Sell(7000, 5, "order_a");
            balance.Tell(newSellOrder);

            //somebody boughе 2 btc for 8000$ each, 3 remains for sale  

            balance.Tell(new UserBalance.AddDueOrderExecuted("order_a", 8000.Usd() * 2));

            //terminate balance and children orders
            await balance.GracefulStop(TimeSpan.FromSeconds(10), new GracefulShutdown());

            //creating balance from scratch, it should retrive its state from persistence
            var balanceTest = ActorOfAsTestActorRef<UserBalance>("test_balance");
            balanceTest.Tell(new UserBalance.AddMarket("test_market", orderBook, Symbol.UsdBtc));

            //wait some time to recover
            await Task.Delay(Dilated(TimeSpan.FromSeconds(5)));

            //check recovered state
            balanceTest.UnderlyingActor.Balances[Currency.Usd].Amount.ShouldEqual(16000M);
            balanceTest.UnderlyingActor.Balances[Currency.Btc].Amount.ShouldEqual(5M);

            //check orders can be created after recover
            balanceTest.Tell(new UserBalance.AddDueOrderExecuted("order_a", 7500.Usd() * 3));
            await Task.Delay(Dilated(TimeSpan.FromSeconds(1)));

            balanceTest.UnderlyingActor.Balances[Currency.Usd].Amount.ShouldEqual(38500M);
            balanceTest.UnderlyingActor.Balances[Currency.Btc].Amount.ShouldEqual(5M);
        }

        [Fact]
        public void
            Given_balance_When_creating_order_more_then_balance_have_Then_got_an_error_and_not_message_for_order_book()
        {
            var balance = Sys.ActorOf<UserBalance>("test_balance");
            balance.Tell(new UserBalance.AddFunds(Currency.Btc.Emit(10)));
            var orderBook = CreateTestProbe();
            balance.Tell(new UserBalance.AddMarket("test market", orderBook, Symbol.UsdBtc));
            balance.Tell(Symbol.UsdBtc.Sell(7000, 30));
            ExpectMsg<Status.Failure>(f => f.Cause is UserBalance.NotEnoughFundsException);
            orderBook.ExpectNoMsg();
        }
    }
}