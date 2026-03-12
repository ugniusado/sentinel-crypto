# Sentinel Crypto

Real-time cryptocurrency market dashboard built to handle **1,000+ price updates per second** while maintaining a **60 FPS** UI.

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square)
![Blazor WASM](https://img.shields.io/badge/Blazor-WebAssembly-7B2FBE?style=flat-square)
![SignalR](https://img.shields.io/badge/SignalR-Redis%20Backplane-red?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)

---

## Architecture

```
Binance WebSocket
      │  raw ticks
      ▼
Channel<PriceUpdate>          ← unbounded buffer, never drops messages
      │  drained every 100ms
      ▼
PriceAggregatorService        ← keeps latest price, computes volatility
      │  AggregatedUpdate
      ▼
SignalR Hub (Redis backplane)  ← broadcasts to all connected clients
      │
      ▼
Blazor WASM Client
  PriceStateService            ← Dictionary<string, CoinModel>
  CoinCard.ShouldRender()      ← only re-renders on >0.01% price change
```

### Server — ASP.NET Core 9

| Component | Purpose |
|---|---|
| `BinanceWebSocketService` | BackgroundService — streams 10 symbols from Binance combined feed |
| `PriceChannelService` | `Channel<PriceUpdate>.CreateUnbounded()` — decouples socket from processor |
| `PriceAggregatorService` | 100ms `PeriodicTimer` — aggregates ticks, broadcasts via SignalR |
| `CryptoHub` | Strongly-typed `Hub<ICryptoClient>` — sends initial state on connect |

### Client — Blazor WebAssembly 9

| Component | Purpose |
|---|---|
| `PriceStateService` | Singleton state store, fires `OnPricesUpdated` |
| `CryptoSignalRService` | Manages hub connection with exponential reconnect back-off |
| `DashboardStateService` | View mode, heatmap, color-blind toggle, favorites |
| `CoinCard` | `ShouldRender()` gate + ghost tick micro-animation |
| `SparklineChart` | SVG area chart, 30s samples, 15min history |
| `CommandPalette` | CMD+K fuzzy coin search |
| `GlobalShortcuts` | JS interop for keyboard shortcuts |

---

## Features

### Performance
- **Channel buffer** — socket spikes never block the aggregator
- **`ShouldRender()` optimization** — coin cards skip re-render unless price changed >0.01% or panic state flipped
- **`font-variant-numeric: tabular-nums`** — prices don't jitter horizontally as digits change

### UI / UX
- **Ghost tick animation** — old price slides out as new price slides in (liquid number feel)
- **Live SVG sparklines** — 15-minute price history on medium and large cards
- **Volume profile bar** — right-edge bar shows relative volume vs 20-sample rolling average
- **Volatility glow** — cards emit a radial green/red aura that intensifies with price volatility
- **Panic mode** — if a coin drops >3% in 5 minutes, the card gets a pulsing red glow + "HIGH VOL" badge
- **Heatmap overlay** — toggle card backgrounds to solid green/red based on 24h change intensity
- **Glassmorphism** — `backdrop-filter: blur(10px)` on all cards
- **Skeleton screens** — shimmer placeholders while WebSocket connects
- **Bento grid** — BTC/ETH take 2×2 slots; mid-caps 1×2; alts 1×1

### Accessibility
- **Color-blind mode** — swaps Green/Red for Blue/Orange via CSS custom property override

### Navigation
| Shortcut | Action |
|---|---|
| `1` | All coins |
| `2` | Favorites |
| `3` | Top gainers |
| `4` | Panic alerts |
| `⌘/Ctrl + K` | Open command palette |
| `Esc` | Close command palette |

---

## Running Locally

**Prerequisites:** .NET 9 SDK

```bash
# Terminal 1 — API server
cd src/Server
dotnet run
# → http://localhost:5000
# → http://localhost:5000/health

# Terminal 2 — Blazor WASM client
cd src/Client
dotnet run
# → http://localhost:5173
```

---

## Docker (full stack)

Spins up the app, Redis backplane, and Jaeger tracing in one command.

```bash
docker-compose up --build
```

| Service | URL |
|---|---|
| App | http://localhost:5000 |
| Jaeger UI | http://localhost:16686 |
| Redis | localhost:6379 |

---

## Health Check

```
GET http://localhost:5000/health
```

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "self",              "status": "Healthy" },
    { "name": "binance-websocket", "status": "Healthy", "description": "Receiving data for 10 symbols" },
    { "name": "redis",             "status": "Healthy" }
  ]
}
```

---

## Project Structure

```
sentinel-crypto/
├── docker-compose.yml
├── src/
│   ├── Server/
│   │   ├── Hubs/            SignalR hub + client interface
│   │   ├── Models/          PriceUpdate, AggregatedUpdate, BinanceTicker
│   │   ├── Services/        Channel buffer, WebSocket worker, aggregator
│   │   └── Program.cs       DI, CORS, health checks, OTLP tracing
│   └── Client/
│       ├── Components/      CoinCard, HeatmapGrid, SparklineChart,
│       │                    CommandPalette, Toolbar, GlobalShortcuts
│       ├── Models/          CoinModel (sparkline + relative volume)
│       ├── Services/        PriceStateService, CryptoSignalRService,
│       │                    DashboardStateService
│       └── wwwroot/
│           ├── css/app.css  Mission Control dark UI
│           └── js/app.js    Keyboard shortcut interop
```

---

## License

MIT
