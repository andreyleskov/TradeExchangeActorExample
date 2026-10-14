# TradeExchangeActorExample

**Low-Latency Limit Order Book & Trade Matching Engine Prototype in C# / Akka.NET**

[![.NET](https://img.shields.io/badge/.NET-C%23-512BD4)](https://dotnet.microsoft.com/)
[![Akka.NET](https://img.shields.io/badge/Concurrency-Akka.NET%20Actors-1565C0)](https://getakka.net/)
[![Benchmark](https://img.shields.io/badge/Benchmarks-NBench-success)](https://github.com/petabridge/NBench)

---

## Overview

`TradeExchangeActorExample` is a reference architecture and performance prototype for an actor-based **financial limit order book and order matching engine** written in C# and **Akka.NET**.

It models high-concurrency order intake, price-time priority matching, and atomic user balance management using isolated actor state rather than shared-memory locks.

---

## Architectural Highlights

- **Lock-Free Actor Concurrency:**
  - `OrderBookActor` — Maintains bid/ask queues per trading pair (`Symbol`) and executes deterministic price-time matching.
  - `BuyOrderActor` / `SellOrderActor` (`OrderActor`) — Encapsulates individual order lifecycle, partial fills, and settlement notifications.
  - `UserBalance` — Tracks multi-currency account balances and reservation invariants.
- **Rich Financial Domain Primitives:**
  - Strongly-typed `Money`, `Currency`, `Symbol`, `BuyOrder`, and `SellOrder` domain value objects preventing cross-currency arithmetic errors (`CurrencyMismatchException`).
- **Deterministic Throughput & Stress Benchmarking:**
  - `TradeExchange.Tests.Stress` (`OrderMatchBench.cs`) — Throughput and allocation benchmarks powered by **NBench** to measure order matching latency under load.
  - `TradeExchangeTests` — Unit and actor-system integration tests (`OrderProcessingTests`, `BalanceTests`, `Market`).

---

## Project Structure

```text
TradeExchangeActorExample/
├── TradeExchangeDomain/          # Core domain primitives (Money, Currency, Symbol, Orders) & Actors
│   ├── Actors/                   # OrderBookActor, BuyOrderActor, SellOrderActor, UserBalance
│   └── Orders/                   # Order, BuyOrder, SellOrder domain models
├── TradeExchangeTests/           # Functional & ActorSystem verification tests
├── TradeExchange.Tests.Stress/   # NBench high-throughput order matching benchmarks
└── TradeExchangeStressTests/     # Stress test runner
```

---

## Author

**[Andrey Leskov](https://github.com/andreyleskov)** — CTO & Principal Systems Architect ([illumi.one](https://illumi.one) · [LinkedIn](https://linkedin.com/in/andrey-leskov)).
