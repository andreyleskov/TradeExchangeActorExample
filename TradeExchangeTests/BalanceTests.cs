using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Should;
using TradeExchangeDomain;
using Xunit;
using Xunit.Abstractions;

namespace TradeExchangeTests
{
    public class BalanceTests : TestKit
    {
        public BalanceTests(ITestOutputHelper output):base("test",output)
        {
            
        }
        [Fact]
        public void Given_balance_actor_When_creating_order_Then_order_book_receives_order()
        {
            var balance = Sys.ActorOf<UserBalance>("test_balance");
            balance.Tell(new UserBalance.AddFunds(Currency.Btc.Emit(10)));
            var orderBook = CreateTestProbe();
            balance.Tell(new AddMarket("test market",orderBook, Symbol.UsdBtc));
            balance.Tell(Symbol.UsdBtc.Sell(7000,5));
            orderBook.ExpectMsg<NewSellOrder>(o => o.Amount == 5 && o.Price.Amount == 7000);
        }

        [Fact]
        public void
            Given_balance_When_creating_order_more_then_balance_have_Then_got_an_error_and_not_message_for_order_book()
        {
            var balance = Sys.ActorOf<UserBalance>("test_balance");
            balance.Tell(new UserBalance.AddFunds(Currency.Btc.Emit(10)));
            var orderBook = CreateTestProbe();
            balance.Tell(new AddMarket("test market",orderBook, Symbol.UsdBtc));
            balance.Tell(Symbol.UsdBtc.Sell(7000,30));
            ExpectMsg<Status.Failure>(f => f.Cause is UserBalance.NotEnoughFundsException);
            orderBook.ExpectNoMsg();
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
            balance.Ref.Tell(new AddMarket("test market",orderBook, Symbol.UsdBtc));
            balance.Ref.Tell(Symbol.UsdBtc.Buy(11000,3));

            
            //will buy 1 by 5000 + 0.25 by 10000
            await Task.Delay(Dilated(TimeSpan.FromSeconds(0.3)));
            
            balance.UnderlyingActor.Balances[Currency.Btc].Amount.ShouldEqual(11.25M);
            //all money for buy remain booked for order, even if it is not fulfilled
            balance.UnderlyingActor.Balances[Currency.Usd].Amount.ShouldEqual(7000M);
        }
        
        [Fact]
        public async Task Given_balance_actor_When_Restart_it_should_maintain_state_and_orders()
        {
            var balance = Sys.ActorOf<UserBalance>("test_balance");
            balance.Tell(new UserBalance.AddFunds(Currency.Btc.Emit(10)));
            
            var orderBook = CreateTestProbe();
            balance.Tell(new AddMarket("test_market",orderBook, Symbol.UsdBtc));
            var newSellOrder = Symbol.UsdBtc.Sell(7000,5,"order_a");
            balance.Tell(newSellOrder);
            
            //somebody boughе 2 btc for 8000$ each, 3 remains for sale  
         
            balance.Tell(new UserBalance.AddDueOrderExecuted("order_a",  8000.Usd() * 2));
            
            //terminate balance and children orders
            await balance.GracefulStop(TimeSpan.FromSeconds(10), new GracefulShutdown());
            
            //creating balance from scratch, it should retrive its state from persistence
            var balanceTest = ActorOfAsTestActorRef<UserBalance>("test_balance");
            balanceTest.Tell(new AddMarket("test_market",orderBook, Symbol.UsdBtc));
            
            //wait some time to recover
            await Task.Delay(Dilated(TimeSpan.FromSeconds(5)));

            //check recovered state
            balanceTest.UnderlyingActor.Balances[Currency.Usd].Amount.ShouldEqual(16000M);
            balanceTest.UnderlyingActor.Balances[Currency.Btc].Amount.ShouldEqual(5M);
            
            //check orders can be created after recover
            balanceTest.Tell(new UserBalance.AddDueOrderExecuted("order_a",  7500.Usd() * 3));
            await Task.Delay(Dilated(TimeSpan.FromSeconds(1)));

            balanceTest.UnderlyingActor.Balances[Currency.Usd].Amount.ShouldEqual(38500M);
            balanceTest.UnderlyingActor.Balances[Currency.Btc].Amount.ShouldEqual(5M);
        }
    }
}