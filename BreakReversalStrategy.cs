using StockSharp.Algo.Strategies;
using StockSharp.Algo.Indicators;
using StockSharp.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StockBacktestProject
{
    public class BreakReversalStrategy : BaseTradeStrategy
    {
        public override string StrategyName => nameof(BreakReversalStrategy);


        // Pattern & Position Tracking
        private ICandleMessage _firstCandle = null;
        private ICandleMessage _secondCandle = null;
        private int _barsSinceFirst = 0;
        private bool _waitingForThird = false;
        private string _patternDirection = null;

        // Day High/Low
        private decimal _dayHigh;
        private decimal _dayLow;
        private decimal _prevDayHigh;
        private decimal _prevDayLow;

        // Position
        private decimal _entryPrice;
        private decimal _stopLoss;
        private decimal _target;
        private bool _inPosition = false;
        private Sides _positionSide;
        private DateTime _entryTime;

        private readonly List<decimal> _closePrices = new List<decimal>();
        private const int SMA10Length = 10;
        private const int SMA20Length = 20;
        decimal SMA10;
        decimal SMA20;

        private bool _waitForRebreak = false;
        private decimal _rebreakLevel = 0;
        private string _rebreakReason = string.Empty;
        private Sides _lastExitSide;

        private string _lastExitReason = string.Empty;
        private decimal _lastExitPrice = 0;

        private List<ICandleMessage> _oneMinBuffer = new List<ICandleMessage>();

        #region Public Entry
        public void OnNewOneMinCandle(ICandleMessage oneMinCandle)
        {
            ProcessStrategy(oneMinCandle);

            if (_inPosition)
                ManagePosition(oneMinCandle);
        }
        #endregion

        #region Candle Aggregation
        public void ProcessStrategy(ICandleMessage candle)
        {
            if (candle.OpenTime.TimeOfDay >= new TimeSpan(15, 30, 59)) return;

            _oneMinBuffer.Add(candle);

            if (_oneMinBuffer.Count == 5)
            {
                var first = _oneMinBuffer.First();
                var last = _oneMinBuffer.Last();

                var fiveMinCandle = new TimeFrameCandleMessage
                {
                    SecurityId = first.SecurityId,
                    OpenTime = first.OpenTime,
                    CloseTime = last.CloseTime,
                    OpenPrice = first.OpenPrice,
                    HighPrice = _oneMinBuffer.Max(c => c.HighPrice),
                    LowPrice = _oneMinBuffer.Min(c => c.LowPrice),
                    ClosePrice = last.ClosePrice,
                    TotalVolume = _oneMinBuffer.Sum(c => c.TotalVolume),
                    State = CandleStates.Finished
                };

                _oneMinBuffer.Clear();

                ProcessCandle(fiveMinCandle);
                //UpdateIndicators(fiveMinCandle);
                //LogCandle(fiveMinCandle);
            }
        }
        #endregion

        #region Strategy Core (Runs on 5-min candles)
        public override void ProcessCandle(ICandleMessage candle)
        {
            LogCandle(candle);
            UpdateIndicators(candle);
            UpdateDayHighLow(candle);
            CheckNewDay(candle);

            if (CheckEODExit(candle)) return;

            if (_inPosition)
            {
                ManagePosition(candle);
            }
            else
            {
                if (!string.IsNullOrEmpty(_lastExitReason))
                {
                    SetupRebreakConditions(_lastExitReason, _lastExitSide);
                   
                }

                if (_waitForRebreak)
                {
                    CheckRebreak(candle);
                    return; 
                }

                if (SMA10 != 0 && SMA20 != 0 && _prevDayHigh != 0 && _prevDayLow != 0 && candle.OpenTime.TimeOfDay >= new TimeSpan(9, 30, 59))
                    HandlePatternDetection(candle);
            }

        }
        #endregion

        #region Logging & Indicators
        private void LogCandle(ICandleMessage candle)
        {
            Logger?.LogMessage("BarData",
                $"Symbol: {candle.SecurityId.SecurityCode}, Date: {candle.OpenTime}, " +
                $"O:{candle.OpenPrice} H:{candle.HighPrice} L:{candle.LowPrice} C:{candle.ClosePrice} V:{candle.TotalVolume}, " +
                $"PrevHigh: {_prevDayHigh}, PrevLow: {_prevDayLow}, SMA20: {SMA20}, SMA10: {SMA10}");
        }

        private void UpdateIndicators(ICandleMessage candle)
        {
            _closePrices.Add(candle.ClosePrice);

            if (_closePrices.Count > SMA20Length)
                _closePrices.RemoveAt(0);

            if (_closePrices.Count >= SMA10Length)
                SMA10 = _closePrices.TakeLast(SMA10Length).Average();

            if (_closePrices.Count >= SMA20Length)
                SMA20 = _closePrices.TakeLast(SMA20Length).Average();
        }
        #endregion

        #region Day High/Low Handling
        private void UpdateDayHighLow(ICandleMessage candle)
        {
            if (candle.OpenTime.TimeOfDay > new TimeSpan(15, 25, 59)) return;

            if (_dayHigh == 0 && _dayLow == 0)
            {
                _dayHigh = candle.HighPrice;
                _dayLow = candle.LowPrice;
            }
            else
            {
                _dayHigh = Math.Max(_dayHigh, candle.HighPrice);
                _dayLow = Math.Min(_dayLow, candle.LowPrice);
            }
        }

        private void CheckNewDay(ICandleMessage candle)
        {
            if (candle.OpenTime.TimeOfDay >= new TimeSpan(15, 25, 59) && candle.OpenTime.TimeOfDay <= new TimeSpan(15, 30, 59))
            {
                _prevDayHigh = _dayHigh;
                _prevDayLow = _dayLow;

                _dayHigh = 0;
                _dayLow = 0;
            }
        }
        #endregion

        #region EOD & Position Management
        private bool CheckEODExit(ICandleMessage candle)
        {
            if (candle.OpenTime.TimeOfDay >= new TimeSpan(15, 20, 0))
            {
                if (_inPosition)
                    ClosePosition(candle, "EOD EXIT", candle.ClosePrice);
                ResetPattern();
                return true;
            }
            return false;
        }

        private void ManagePosition(ICandleMessage candle)
        {
            if (!_inPosition) return;

            if (_positionSide == Sides.Buy)
            {
                if (_stopLoss >= candle.LowPrice && _stopLoss <= candle.HighPrice)
                {
                    ClosePosition(candle, "StopLoss Hit", _stopLoss);
                }
                else if (_target >= candle.LowPrice && _target <= candle.HighPrice)
                {
                    ClosePosition(candle, "Target Hit", _target);
                }
            }
            else
            {
                if (_stopLoss >= candle.LowPrice && _stopLoss <= candle.HighPrice)
                {
                    ClosePosition(candle, "StopLoss Hit", _stopLoss);
                }
                else if (_target >= candle.LowPrice && _target <= candle.HighPrice)
                {
                    ClosePosition(candle, "Target Hit", _target);
                }
            }
        }
        #endregion

        #region Pattern Detection
        private void HandlePatternDetection(ICandleMessage candle)
        {
            if (_firstCandle == null)
            {
                DetectFirstCandle(candle);
                return;
            }
            if (_patternDirection == "LOW")
            {
                if (candle.HighPrice <= _firstCandle.HighPrice)
                {
                    _barsSinceFirst++;
                }
                else
                {
                    _firstCandle = candle;
                    _barsSinceFirst = 0;
                }
            }
            else if (_patternDirection == "HIGH")
            {
                if (candle.LowPrice >= _firstCandle.LowPrice)
                {
                    _barsSinceFirst++;
                }
                else
                {
                    _firstCandle = candle;
                    _barsSinceFirst = 0;
                }
            }

            //_barsSinceFirst++;

            if (!_waitingForThird)
                DetectSecondCandle(candle);
        }

        private void DetectFirstCandle(ICandleMessage candle)
        {
            bool isGreen = candle.ClosePrice > candle.OpenPrice;
            bool isRed = candle.ClosePrice < candle.OpenPrice;

            decimal _minLow = Math.Min(_prevDayLow, Math.Min(SMA10, SMA20));
            decimal _maxHigh = Math.Max(_prevDayHigh, Math.Max(SMA20, SMA10));

            if (_inPosition) return;
            
            if (isRed && candle.LowPrice <= _minLow && candle.HighPrice >= _minLow)
            {
                _firstCandle = candle;
                _patternDirection = "LOW";
                _barsSinceFirst = 0;
                Logger?.LogMessage("PATTERN", $"First candle breaks PDL: {candle.OpenTime} Low={candle.LowPrice}");
            }
            else if (isGreen && candle.HighPrice >= _maxHigh && candle.LowPrice <= _maxHigh)
            {
                _firstCandle = candle;
                _patternDirection = "HIGH";
                _barsSinceFirst = 0;
                Logger?.LogMessage("PATTERN", $"First candle breaks PDH: {candle.OpenTime} High={candle.HighPrice}");
            }
        }

        private void DetectSecondCandle(ICandleMessage candle)
        {
            if (_patternDirection == "LOW" && candle.LowPrice < _firstCandle.LowPrice && candle.ClosePrice < _firstCandle.ClosePrice)
            {
                _secondCandle = candle;
                _waitingForThird = true;
                Logger?.LogMessage("PATTERN", $"Second LOW confirmed: {candle.OpenTime}");

                EnterPosition(candle, Sides.Sell);
                ResetPattern();
            }
            else if (_patternDirection == "HIGH" && candle.HighPrice > _firstCandle.HighPrice && candle.ClosePrice > _firstCandle.ClosePrice)
            {
                _secondCandle = candle;
                _waitingForThird = true;
                Logger?.LogMessage("PATTERN", $"Second HIGH confirmed: {candle.OpenTime}");
                EnterPosition(candle, Sides.Buy);
                ResetPattern();
            }

            if (_barsSinceFirst >= 2)
            {
                Logger?.LogMessage("CANCLE PATTERN", $"More Then two candles");
                ResetPattern();
            }
                
        }
        #endregion

        #region Entry & Exit
        private void EnterPosition(ICandleMessage candle, Sides side)
        {
            _inPosition = true;
            _positionSide = side;

            _entryTime = candle.OpenTime.DateTime;

            if (side == Sides.Buy)
            {
                _entryPrice = _secondCandle.HighPrice;
                _stopLoss = _secondCandle.LowPrice - (_entryPrice * 1 / 1000);
                _target = _entryPrice + 2 * (_secondCandle.HighPrice - _secondCandle.LowPrice);
            }
            else
            {
                _entryPrice = _secondCandle.LowPrice;
                _stopLoss = _secondCandle.HighPrice + (_entryPrice * 1 / 1000);
                _target = _entryPrice - 3 * (_secondCandle.HighPrice - _secondCandle.LowPrice);
            }

            EntryPrice = _entryPrice;
            EntryTime = _entryTime;
            Quantity = 1;              
            _lastExitSide = side;         

            RegisterOrder(CreateOrder(side, 1));
            Logger?.LogMessage("ORDER", $"{side} ENTRY at {_entryPrice}, SL={_stopLoss}, Target={_target}");
        }


        private void SetupRebreakConditions(string reason, Sides exitSide)
        {
            if (reason != "Target Hit" && reason != "StopLoss Hit")
                return;

            _waitForRebreak = true;
            _rebreakReason = reason;
            _lastExitSide = exitSide;

            if (exitSide == Sides.Buy)
            {
                _rebreakLevel = Math.Max(_prevDayHigh, Math.Max(SMA10, SMA20));
                Logger?.LogMessage("REB", $"Waiting for REBREAK UP after {reason}, must CROSS above {_rebreakLevel} before detecting candles again.");
            }
            else
            {
                _rebreakLevel = Math.Min(_prevDayLow, Math.Min(SMA10, SMA20));
                Logger?.LogMessage("REB", $"Waiting for REBREAK DOWN after {reason}, must CROSS below {_rebreakLevel} before detecting candles again.");
            }
        }

        private void CheckRebreak(ICandleMessage candle)
        {
            if (!_waitForRebreak) return;

            if (_lastExitSide == Sides.Buy)
            {
                if (candle.LowPrice <= _rebreakLevel && candle.HighPrice >= _rebreakLevel)
                {
                    Logger?.LogMessage("REB", $"Rebreak UP crossed at {candle.OpenTime}, Level={_rebreakLevel}");
                    _waitForRebreak = false;
                    ResetPattern();
                    _lastExitReason = string.Empty;
                }
            }
            else if (_lastExitSide == Sides.Sell)
            {
                if (candle.LowPrice <= _rebreakLevel && candle.HighPrice >= _rebreakLevel)
                {
                    Logger?.LogMessage("REB", $"Rebreak DOWN crossed at {candle.OpenTime}, Level={_rebreakLevel}");
                    _waitForRebreak = false;
                    ResetPattern();
                    _lastExitReason = string.Empty;
                }
            }
        }
        private void ClosePosition(ICandleMessage candle, string reason, decimal exitPrice)
        {
            _inPosition = false;

            var side = _positionSide == Sides.Buy ? Sides.Sell : Sides.Buy;
            RegisterOrder(CreateOrder(side, 1));

            Logger?.LogMessage("ORDER", $"EXIT {side} at {exitPrice} ({reason})");

            _lastExitReason = reason;
            _lastExitPrice = exitPrice;
            _lastExitSide = _positionSide;
            RaiseTradeClosed(candle, exitPrice, reason);
        }


        private void ResetPattern()
        {
            _firstCandle = null;
            _secondCandle = null;
            _barsSinceFirst = 0;
            _waitingForThird = false;
            _patternDirection = null;
        }
        #endregion
    }
}
