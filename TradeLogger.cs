using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StockBacktestProject
{
    public class TradeLogger
    {
        private readonly string _mainFilePath;
        private readonly List<TradeSummary> _dailyTrades = new();
        private readonly string _dailyTradesFolder;

        public TradeLogger(string mainFolderPath = @"C:\QuantXpress\BackTesting\StockBacktestProject\bin\Debug\net8.0\",
                           string dailyTradesFolder = @"C:\QuantXpress\BackTesting\StockBacktestProject\bin\Debug\net8.0\")
        {
            if (!Directory.Exists(mainFolderPath))
                Directory.CreateDirectory(mainFolderPath);

            if (!Directory.Exists(dailyTradesFolder))
                Directory.CreateDirectory(dailyTradesFolder);

            // Main log file
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _mainFilePath = Path.Combine(mainFolderPath, $"StrategyLog_{timestamp}.csv");
            File.WriteAllText(_mainFilePath, "Timestamp,Type,Message\n");

            _dailyTradesFolder = dailyTradesFolder;
        }

        // Log generic message into main CSV
        public void LogMessage(string type, string message)
        {
            string line = $"{type},{message}";
            File.AppendAllText(_mainFilePath, line + Environment.NewLine);
            Console.WriteLine(line);
        }

        // Add a trade to the daily trade list
        public void AddTrade(TradeSummary trade)
        {
            _dailyTrades.Add(trade);
            LogMessage("TRADE", trade.ToCsv());
        }

        // Save daily trades to a separate file
        public void PrintDailySummary(string strategyName = "Strategy")
        {
            if (_dailyTrades.Count == 0)
            {
                LogMessage("SUMMARY", "No trades to summarize for today.");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dailyTradeFile = Path.Combine(_dailyTradesFolder, $"{strategyName}_DailyTrades_{timestamp}.csv");

            using var sw = new StreamWriter(dailyTradeFile, false);
            sw.WriteLine("Symbol,EntryDate,ExitDate,EntryPrice,ExitPrice,ExitReason,PnL");

            foreach (var trade in _dailyTrades)
            {
                string line = $"{trade.Symbol},{trade.EntryTime:yyyy-MM-dd HH:mm},{trade.ExitTime:yyyy-MM-dd HH:mm}," +
                              $"{trade.EntryPrice:F2},{trade.ExitPrice:F2},{trade.ExitReason},{trade.PnL:F2}";
                sw.WriteLine(line);
            }

            LogMessage("SUMMARY", $"Daily trades saved to: {dailyTradeFile}");
        }

        // Attach to strategy
        public void AttachStrategy(ITradeStrategy strategy)
        {
            strategy.OnTradeClosed += (symbol, entryPrice, exitPrice, qty, entryTime, exitTime, reason) =>
            {
                // Calculate PnL (assuming long/short logic handled by strategy)
                decimal pnl = (exitPrice - entryPrice) * qty;

                var trade = new TradeSummary
                {
                    StrategyName = strategy.StrategyName,
                    Symbol = symbol,
                    EntryPrice = entryPrice,
                    ExitPrice = exitPrice,
                    Quantity = qty, 
                    EntryTime = entryTime,
                    ExitTime = exitTime,
                    ExitReason = reason
                };


                AddTrade(trade);
            };

            strategy.OnCandleAction += (message) =>
            {
                LogMessage("CANDLE", $"Strategy: {strategy.StrategyName}, {message}");
            };
        }

        // Log statistics in main CSV
        public void PrintStatisticsFromDailyTrades()
        {
            if (_dailyTrades.Count == 0)
            {
                LogMessage("STATS", "No trades available to calculate statistics.");
                return;
            }

            var trades = _dailyTrades;

            int totalTrades = trades.Count;
            int winningTrades = trades.Count(t => t.PnL > 0);
            int losingTrades = trades.Count(t => t.PnL <= 0);
            decimal totalProfit = trades.Where(t => t.PnL > 0).Sum(t => t.PnL);
            decimal totalLoss = trades.Where(t => t.PnL <= 0).Sum(t => Math.Abs(t.PnL));
            decimal netProfit = totalProfit - totalLoss;

            // Turnover = sum of entry + exit value for all trades
            decimal turnover = trades.Sum(t => (t.EntryPrice + t.ExitPrice));

            LogMessage("", $"=== Daily Trade Statistics ===");
            LogMessage("", $"TotalNumberOfTrades,{totalTrades}");
            LogMessage("", $"NumberOfWinningTrades,{winningTrades}");
            LogMessage("", $"NumberOfLosingTrades,{losingTrades}");
            LogMessage("", $"TotalProfit,{totalProfit}");
            LogMessage("", $"TotalLoss,{totalLoss}");
            LogMessage("", $"NetProfit,{netProfit}");
            LogMessage("", $"Turnover,{turnover}");

            _dailyTrades.Clear();
        }
    }
}
