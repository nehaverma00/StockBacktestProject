using System;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Indicators;
using StockSharp.Messages;

class SmaStrategy : Strategy
{
    private readonly SimpleMovingAverage _fast;
    private readonly SimpleMovingAverage _slow;

    public SmaStrategy()
    {
        _fast = new SimpleMovingAverage { Length = 10 };
        _slow = new SimpleMovingAverage { Length = 30 };
    }

    public void ProcessCandle(ICandleMessage candle)
    {
        _fast.Process(candle);
        _slow.Process(candle);

        if (!_fast.IsFormed || !_slow.IsFormed)
            return;

        var fastValue = _fast.GetCurrentValue();
        var slowValue = _slow.GetCurrentValue();

        if (Position == 0)
        {
            if (fastValue > slowValue)
                Buy(1m);        // decimal literal (m suffix)
            else if (fastValue < slowValue)
                Sell(1m);
        }
        else if (Position > 0 && fastValue < slowValue)
        {
            Sell(Position);    // Position is already decimal
        }
        else if (Position < 0 && fastValue > slowValue)
        {
            Buy(-Position);    // -Position is decimal
        }
    }

    private void Buy(decimal volume)
    {
        var order = this.CreateOrder(Sides.Buy, volume);
        RegisterOrder(order);
    }

    private void Sell(decimal volume)
    {
        var order = this.CreateOrder(Sides.Sell, volume);
        RegisterOrder(order);
    }
}
