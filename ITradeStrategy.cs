using System;

namespace StockBacktestProject
{
    public interface ITradeStrategy
    {
        event Action<string, decimal, decimal, decimal, DateTime, DateTime, string> OnTradeClosed;
        event Action<string> OnCandleAction;
        string StrategyName { get; }
    }
}
