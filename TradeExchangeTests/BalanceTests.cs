using Akka.Actor;
using Akka.TestKit.Xunit2;
using Should;
using TradeExchangeDomain;
using Xunit;

namespace TradeExchangeTests
{
    public class BalanceTests : TestKit
    {
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

            ExpectMsg<OrderBookActor.OrderExecuted>();

            //will buy 1 by 5000 + 0.25 by 10000
            balance.UnderlyingActor.Balances[Currency.Btc].Amount.ShouldEqual(11.5M);
            balance.UnderlyingActor.Balances[Currency.Usd].Amount.ShouldEqual(10000M);
        }
        
        [Fact]
        public void Given_balance_actor_When_Restart_it_should_maintain_state()
        {
            var balance = Sys.ActorOf<UserBalance>("test_balance");
            balance.Tell(new AddFunds(Currency.Btc.Emit(10)));
            
            var orderBook = CreateTestProbe();
            balance.Tell(new AddMarket("test_market",orderBook, Symbol.UsdBtc));
            var newSellOrder = Symbol.UsdBtc.Sell(7000,5,"a");
            balance.Tell(newSellOrder);
            balance.Tell(new OrderBookActor.OrderExecuted("a",5, 8000.Usd()));
            //balance should be 5 * 8 = 40000
            Watch(balance);
            Sys.Stop(balance);
            ExpectTerminated(balance);

            var balanceTest = ActorOfAsTestActorRef<UserBalance>("test_balance");
            balanceTest.UnderlyingActor.Balances[Currency.Usd].Amount.ShouldEqual(40000M);
            balanceTest.UnderlyingActor.Balances[Currency.Btc].Amount.ShouldEqual(5M);
        }
    }
}