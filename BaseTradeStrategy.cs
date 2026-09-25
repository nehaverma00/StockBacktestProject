using StockSharp.Algo.Strategies;
using StockSharp.Messages;
using System;

namespace StockBacktestProject
{
    public abstract class BaseTradeStrategy : Strategy, ITradeStrategy
    {
        public event Action<string, decimal, decimal, decimal, DateTime, DateTime, string> OnTradeClosed;
        public event Action<string> OnCandleAction;

        public abstract string StrategyName { get; }

        public TradeLogger Logger { get; set; }

        protected decimal EntryPrice { get; set; }
        protected DateTime EntryTime { get; set; }
        protected decimal Quantity { get; set; } = 1m;

        protected void RaiseCandleAction(string message)
        {
            OnCandleAction?.Invoke(message);
            Logger?.LogMessage("CANDLE", $"Strategy: {StrategyName}, {message}");
        }

        protected void RaiseTradeClosed(ICandleMessage candle, decimal exitPrice, string reason)
        {
            OnTradeClosed?.Invoke(
                candle.SecurityId.SecurityCode,
                EntryPrice,
                exitPrice,
                Quantity,
                EntryTime,
                candle.OpenTime.DateTime,
                reason
            );

            Logger?.LogMessage("TRADE", $"Closed trade at {exitPrice} ({reason})");
        }

        public abstract void ProcessCandle(ICandleMessage candle);

        protected void Buy(ICandleMessage candle, decimal volume)
        {
            var order = CreateOrder(Sides.Buy, volume);
            RegisterOrder(order);
            EntryPrice = candle.ClosePrice;
            EntryTime = candle.OpenTime.DateTime;
            Logger?.LogMessage("ORDER", $"BUY {volume} at {candle.ClosePrice}");
        }

        // Helper for Sell
        protected void Sell(ICandleMessage candle, decimal volume, string reason)
        {
            var order = CreateOrder(Sides.Sell, volume);
            RegisterOrder(order);
            Logger?.LogMessage("ORDER", $"SELL {volume} at {candle.ClosePrice} ({reason})");
        }
    }
}
