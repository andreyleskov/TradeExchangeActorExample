using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using NBench;
using Pro.NBench.xUnit.XunitExtensions;
using Pro.NBench.xUnit.XunitExtensions.Pro.NBench.xUnit.XunitExtensions;
using Should;
using TradeExchangeDomain;
using Xunit;
using Xunit.Abstractions;

//Important - disable test parallelization at assembly or collection level
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TradeExchange.Tests.Stress
{
    public class BenchSettings
    {
        private readonly Random _random = new Random();
        public int BaseBtcPrice = 10;
        public int PriceRange = 3;
        public int BaseAmount { get; } = 3;
        public int AmountRange = 2;
        public int UsersCount { get; } = 10;
        public int OrdersToCreatePerUser { get; } = 10;

     
        public IEnumerable<NextOrder> GetOrders()
        {
            var usersTotal = UsersCount;
           
            while(usersTotal-->0)
                for (int i = 0; i < OrdersToCreatePerUser; i++)
                {
                    yield return new NextOrder(_random.Next(0,UsersCount-1),
                                               CreateOrder(BaseBtcPrice,PriceRange,BaseAmount,AmountRange));
                }
        }
        private object CreateOrder(decimal baseBtcPrice, decimal range, decimal baseAmount, decimal amountRange)
        {
            var price = baseBtcPrice + ((decimal)_random.NextDouble() - 0.5M) * range;
            var amount =  baseAmount + ((decimal)_random.NextDouble() - 0.5M) * amountRange;
            
            if (_random.Next(0, 1) == 1)
            {
                //buy btc
                return new NewBuyOrder(Symbol.UsdBtc,price.Usd(),amount); 
            }
            else
            {
                //sell btc
                return new NewSellOrder(Symbol.UsdBtc,price.Usd(),amount); 
            }
        }
    }

    public class NextOrder
    {
        public int UserNum { get; }
        public object OrderMsg { get; }

        public NextOrder(int userNum, object orderMsg)
        {
            UserNum = userNum;
            OrderMsg = orderMsg;
        }
    }
    
    public class OrderMatch_inMem_Bench
    {

        protected const string TotalCommandsExecutedCounter = "TotalCommandsExecutedCounter";
        private Counter _counter;
        private ActorSystem _actorSystem;
        private IActorRef[] _users;

        private BenchSettings settings = new BenchSettings();
        public decimal TotalMarketUsd { get; private set; }
        public decimal TotalMarketBtc { get; private set; }

        
        public OrderMatch_inMem_Bench(ITestOutputHelper output)
        {
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new XunitTraceListener(output));
        }

        [PerfSetup]
        public void SetUp()
        {
            _actorSystem = ActorSystem.Create("perf system");
            var orderBook = _actorSystem.ActorOf<OrderBookActor>("OrderBook");
            var addMarket = new UserBalance.AddMarket("test_market",orderBook,Symbol.UsdBtc);
            var addFundsUsd = new UserBalance.AddFunds(10000.Usd());
            var addFundsBtc = new UserBalance.AddFunds(10000.Btc());
            
            TotalMarketBtc = addFundsBtc.Value.Amount * settings.UsersCount;
            TotalMarketUsd = addFundsUsd.Value.Amount * settings.UsersCount;
            
            _users = Enumerable.Range(0, settings.UsersCount)
                               .Select(n =>
                                       {
                                           var user = _actorSystem.ActorOf<UserBalance>("user_" + n.ToString());
                                           user.Tell(addMarket);
                                           user.Tell(addFundsUsd);
                                           user.Tell(addFundsBtc);
                                           return user;
                                       })
                               .ToArray();
        }
        
      
        //best value = 60 microseconds (10ˆ-6) =  approx 17.000 req\second total
        [NBenchFact]
        [PerfBenchmark(Description = "Measuring order creation time without IIS server",
            NumberOfIterations = 3, 
            RunMode = RunMode.Iterations,
            TestMode = TestMode.Test)]
        [CounterThroughputAssertion(TotalCommandsExecutedCounter, MustBe.GreaterThanOrEqualTo, 10000)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void Test1()
        {
            foreach (var nextOrder in settings.GetOrders())
            {
                _users[nextOrder.UserNum].Tell(nextOrder.OrderMsg);
            }
        }

        [PerfCleanup]
        public void CheckTotals()
        {
            var totalUsdMarket =_users.Select(u => u.Ask<Money>(new UserBalance.GetBalance(Currency.Usd)).Result.Amount).Sum();
            totalUsdMarket.ShouldEqual(TotalMarketUsd);
            
            var totalBtcMarket =_users.Select(u => u.Ask<Money>(new UserBalance.GetBalance(Currency.Btc)).Result.Amount).Sum();
            totalBtcMarket.ShouldEqual(TotalMarketBtc);
            _actorSystem.Terminate();
        }
    }
}