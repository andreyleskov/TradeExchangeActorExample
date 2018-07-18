using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Akka.Actor;
using Akka.Event;
using NBench;
using Pro.NBench.xUnit.XunitExtensions;
using Serilog;
using Serilog.Core;
using Should;
using TradeExchangeDomain;
using TradeExchangeDomain.Orders;
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
        public int UsersCount { get; } = 20;
        public int OrdersToCreatePerUser { get; } = 20;
        public int TotalOrders => UsersCount * OrdersToCreatePerUser;

        public IEnumerable<NextOrder> GetOrders()
        {
            var usersTotal = UsersCount;

            while (usersTotal-- > 0)
                for (int i = 0; i < OrdersToCreatePerUser; i++)
                {
                    yield return new NextOrder(_random.Next(0, UsersCount - 1),
                                               CreateOrder(BaseBtcPrice, PriceRange, BaseAmount, AmountRange));
                }
        }

        private object CreateOrder(decimal baseBtcPrice, decimal range, decimal baseAmount, decimal amountRange)
        {
            var price = baseBtcPrice + ((decimal) _random.NextDouble() - 0.5M) * range;
            var amount = baseAmount + ((decimal) _random.NextDouble() - 0.5M) * amountRange;

            if (_random.Next(0, 1) == 1)
            {
                //buy btc
                return new BuyOrder(Symbol.UsdBtc, price.Usd(), amount);
            }
            else
            {
                //sell btc
                return new SellOrder(Symbol.UsdBtc, price.Usd(), amount);
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
        private Inbox _inbox;
        private ITestOutputHelper _testOutputHelper;
        public decimal TotalMarketUsd { get; private set; }
        public decimal TotalMarketBtc { get; private set; }


        public OrderMatch_inMem_Bench(ITestOutputHelper output)
        {
            _testOutputHelper = output;
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new XunitTraceListener(output));
        }

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            _actorSystem = ActorSystem.Create("perfSystem",
                                              @"akka {  
                    stdout-loglevel = DEBUG
                    loglevel = DEBUG
                    log-config-on-start = off
                    loggers= [""Akka.Logger.Serilog.SerilogLogger, Akka.Logger.Serilog""]
                    actor {                
                        debug {  
                              receive = on 
                              autoreceive = on
                              lifecycle = on
                              event-stream = on
                              unhandled = on
                        }
                    }  ");
            Log.Logger = new LoggerConfiguration()
                .WriteTo.TestOutput(_testOutputHelper)
                .WriteTo.RollingFile("PerfLog.txt")
                .MinimumLevel.Information()
                .CreateLogger();

            var orderBook = _actorSystem.ActorOf<OrderBookActor>("OrderBook");
            var addMarket = new UserBalance.AddMarket("test_market", orderBook, Symbol.UsdBtc);
            var addFundsUsd = new UserBalance.AddFunds(10000.Usd());
            var addFundsBtc = new UserBalance.AddFunds(10000.Btc());

            TotalMarketBtc = addFundsBtc.Value.Amount * settings.UsersCount;
            TotalMarketUsd = addFundsUsd.Value.Amount * settings.UsersCount;
            _counter = context.GetCounter(TotalCommandsExecutedCounter); 
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

            _inbox = Inbox.Create(_actorSystem);
        }


        //best value = 60 microseconds (10ˆ-6) =  approx 17.000 req\second total
        [NBenchFact]
        [PerfBenchmark(Description = "Measuring order creation time without IIS server",
            NumberOfIterations = 3,
            RunMode = RunMode.Iterations,
            TestMode = TestMode.Test)]
        [CounterThroughputAssertion(TotalCommandsExecutedCounter, MustBe.GreaterThanOrEqualTo, 10)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void OrderCreation_throughput()
        {
            foreach (var nextOrder in settings.GetOrders())
            {
                _users[nextOrder.UserNum]
                    .Ask<OrderBookActor.OrderReceived>(nextOrder.OrderMsg,TimeSpan.FromSeconds(1))
                    .ContinueWith(t => _counter.Increment())
                    .Wait();
            }

           
        }


        class OrderBalanceCountActor : ReceiveActor
        {
            private IActorRef requester;
            decimal orderUsdTotal = 0;
            decimal orderBtcTotal = 0;
            Stopwatch watch = new Stopwatch();
            public OrderBalanceCountActor()
            {
                Receive<GetBalance>(b =>
                                    {
                                        requester = Sender;
                                        Context.ActorSelection("akka://perfSystem/user/user_*/order_*")
                                               .Tell(new OrderActor.GetBalance());
                                        Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(50),
                                                                                        TimeSpan.FromSeconds(1),
                                                                                        Self,
                                                                                        new CheckFinish(),
                                                                                        Self);
                                        watch.Start();
                                    });
                Receive<CheckFinish>(f =>
                                     {
                                         if (watch.Elapsed > TimeSpan.FromSeconds(1))
                                         {
                                            requester.Tell(new Totals(orderUsdTotal, orderBtcTotal));
                                         }
                                     });
                Receive<OrderActor.OrderBalance>(msg =>
                                                 {
                                                     if (msg.Total.Currency == Currency.Btc)
                                                         orderBtcTotal += msg.Total.Amount;
                                                     else if (msg.Total.Currency == Currency.Usd)
                                                         orderUsdTotal += msg.Total.Amount;
                                                     watch.Restart();
                                                 });
                
               
            }
            class CheckFinish
            {
            }
            
            public class GetBalance
            {
            }
            internal class Totals
            {
                public decimal OrderUsdTotal { get; }
                public decimal OrderBtcTotal { get; }

                public Totals(decimal orderUsdTotal, decimal orderBtcTotal)
                {
                    OrderUsdTotal = orderUsdTotal;
                    OrderBtcTotal = orderBtcTotal;
                }
            }
        }
        [PerfCleanup]
        public void CheckTotals()
        {

            var inbox = Inbox.Create(_actorSystem);
            _actorSystem.ActorSelection("user/user_*/order_*").Tell(new OrderActor.GetBalance(), inbox.Receiver);

            var counter = _actorSystem.ActorOf<OrderBalanceCountActor>();
            var orderTotals = counter.Ask<OrderBalanceCountActor.Totals>(new OrderBalanceCountActor.GetBalance()).Result;
     
            
            
            var balancesUsdTotal =_users.Select(u => u.Ask<Money>(new UserBalance.GetBalance(Currency.Usd)).Result.Amount).Sum();
            (balancesUsdTotal + orderTotals.OrderUsdTotal).ShouldEqual(TotalMarketUsd);

            Log.Logger.Information("usd totals is ok");

            
            var balancesBtcTotal = _users.Select(u => u.Ask<Money>(new UserBalance.GetBalance(Currency.Btc)).Result.Amount).Sum();
            (balancesBtcTotal + orderTotals.OrderBtcTotal).ShouldEqual(TotalMarketBtc);
            Log.Logger.Information("btc totals is ok");
            
            _actorSystem.Terminate();
        }
    }

  
}