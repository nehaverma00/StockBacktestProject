using System;

namespace StockBacktestProject
{
    public class TradeSummary
    {
        public string StrategyName { get; set; }
       
        public string Symbol { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Quantity { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public string ExitReason { get; set; }
        public decimal PnL => (ExitPrice - EntryPrice) * Quantity;

        public string ToCsv()
        {
            return $"Strategy: {StrategyName}, Symbol: {Symbol}, Enter Price: {EntryPrice}, Exit Price: {ExitPrice}, Quantity: {Quantity}, " +
                   $"EntryTradeDT: {EntryTime:dd-MM-yyyy HH:mm:ss}, ExitTradeDT: {ExitTime:dd-MM-yyyy HH:mm:ss}, ExitText: {ExitReason}, PnL: {PnL:F2}";
        }

        public override string ToString() => ToCsv();
    }
}
