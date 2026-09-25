using StockSharp.Messages;
using System;

namespace StockBacktestProject
{
    public class BuyAndSellEodStrategy : BaseTradeStrategy
    {
        public override string StrategyName => nameof(BuyAndSellEodStrategy);

        private decimal _takeProfitPrice = 58.2m;
        private decimal _stopLossPercentage = 10m;
        private int _currentPosition = 0;
        private DateTime? _lastExitDate = null;

        public override void ProcessCandle(ICandleMessage candle)
        {
            RaiseCandleAction($"Processing candle {candle.OpenTime}");

            var currentDate = candle.OpenTime.Date;

            if (_currentPosition == 0 && (_lastExitDate == null || _lastExitDate.Value != currentDate))
            {
                Buy(candle, Quantity);
                _currentPosition++;
            }

            if (_currentPosition > 0)
            {
                decimal stopLossPrice = EntryPrice * (1 - _stopLossPercentage / 100m);

                if (candle.LowPrice <= stopLossPrice)
                {
                    Sell(candle, Quantity, "STOP-LOSS");
                    RaiseTradeClosed(candle, stopLossPrice, "STOP-LOSS");
                    _currentPosition = 0;
                    _lastExitDate = currentDate;
                }

                if (candle.HighPrice >= _takeProfitPrice && candle.LowPrice <= _takeProfitPrice)
                {
                    Sell(candle, Quantity, "TAKE-PROFIT");
                    RaiseTradeClosed(candle, _takeProfitPrice, "TAKE-PROFIT");
                    _currentPosition = 0;
                    _lastExitDate = currentDate;
                }
            }

            if (_currentPosition > 0 && IsEndOfDay(candle))
            {
                Sell(candle, Quantity, "EOD EXIT");
                RaiseTradeClosed(candle, candle.ClosePrice, "EOD EXIT");
                _currentPosition = 0;
                _lastExitDate = currentDate;
            }
        }

        private bool IsEndOfDay(ICandleMessage candle)
            => candle.OpenTime.TimeOfDay >= new TimeSpan(15, 30, 0);
    }
}
