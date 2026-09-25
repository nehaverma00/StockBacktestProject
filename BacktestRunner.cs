using StockSharp.Algo.Strategies;
using StockSharp.Algo.Testing;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace StockBacktestProject
{
    public class BacktestRunner
    {
        private readonly string _csvFile;
        private readonly Security _security;
        private readonly Portfolio _portfolio;
        private readonly BreakReversalStrategy _strategy;
        private readonly string _targetSymbol;

        public BacktestRunner(string csvFile, Security security, Portfolio portfolio, BreakReversalStrategy strategy, string targetSymbol)
        {
            _csvFile = csvFile;
            _security = security;
            _portfolio = portfolio;
            _strategy = strategy;
            _targetSymbol = targetSymbol;
        }

        public void Run()
        {
            var connector = new HistoryEmulationConnector(
                new[] { _security },
                new[] { _portfolio }
            );

            _strategy.Connector = connector;

            // Read CSV and convert to candles, filter by target symbol
            var candles = File.ReadAllLines(_csvFile)
                .Skip(1)
                .Select(line =>
                {
                    var parts = line.Split(',');
                    var symbol = parts[0];

                    if (symbol != _targetSymbol)
                        return null;

                    var date = DateTime.ParseExact(parts[1], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                    var time = TimeSpan.Parse(parts[2]);
                    var open = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
                    var high = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
                    var low = decimal.Parse(parts[5], CultureInfo.InvariantCulture);
                    var close = decimal.Parse(parts[6], CultureInfo.InvariantCulture);
                    var volume = decimal.Parse(parts[7], CultureInfo.InvariantCulture);

                    return new TimeFrameCandleMessage
                    {
                        SecurityId = _security.ToSecurityId(),
                        OpenTime = date + time,
                        OpenPrice = open,
                        HighPrice = high,
                        LowPrice = low,
                        ClosePrice = close,
                        TotalVolume = volume
                    } as ICandleMessage;
                })
                .Where(c => c != null)
                .ToList();

            Console.WriteLine($"Total candles to process: {candles.Count}");

            // Process each candle
            foreach (var candle in candles)
            {
                _strategy.ProcessStrategy(candle);
                //Thread.Sleep(50);
            }

            Console.WriteLine("Backtest finished.");

            // Print trades
            Console.WriteLine("=== Trades ===");
            foreach (var t in _strategy.MyTrades)
            {
                var trade = t.Trade;
                Console.WriteLine($"{trade.ServerTime:yyyy-MM-dd HH:mm:ss} {trade.Security?.Code} Vol:{trade.Volume} Price:{trade.Price}");
            }


            connector.Disconnect();
        }

    }
}
