
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StockSharp.BusinessEntities;

namespace StockBacktestProject
{
    public class BarRecord
    {
        public DateTime Time { get; set; }
        public string Raw { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string symb = "ADANIENT.NSE";
            var security = new Security { Id = symb, Code = symb };
            var portfolio = new Portfolio { Name = "TestPortfolio", BeginValue = 100000m };

            var csvFolder = @"C:\Users\nehav\Downloads\2025\2025\";

            DateTime startDate = new DateTime(2025, 1, 1, 9, 15, 0);
            DateTime endDate = new DateTime(2025, 8, 31, 15, 30, 0);
            string cachePath = @"C:\QuantXpress\BackTesting\StockBacktestProject\bin\Debug\net8.0";
            string cacheFile = Path.Combine(cachePath, $"{symb}_Cache_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.dat");

            List<string> mergedBars;

            if (File.Exists(cacheFile))
            {
                Console.WriteLine($"Loading cache from {cacheFile}...");
                var json = File.ReadAllText(cacheFile);
                var records = JsonSerializer.Deserialize<List<BarRecord>>(json);
                mergedBars = records.Select(x => x.Raw).ToList();
            }
            else
            {
                Console.WriteLine("Building cache from CSV files...");

                var allBars = new List<string>();
                var csvFiles = Directory.GetFiles(csvFolder, "*.csv", SearchOption.AllDirectories);

                foreach (var csvFile in csvFiles)
                {
                    try
                    {
                        var lines = File.ReadLines(csvFile).Skip(1);
                        var symbolLines = lines
                            .Select(line =>
                            {
                                var parts = line.Split(',');
                                return new
                                {
                                    Symbol = parts[0].Trim(),
                                    Date = DateTime.Parse(parts[1]),
                                    Raw = line
                                };
                            })
                            .Where(x => x.Symbol.Equals(symb, StringComparison.OrdinalIgnoreCase))
                            .Where(x => x.Date >= startDate && x.Date <= endDate)
                            .Select(x => x.Raw);

                        allBars.AddRange(symbolLines);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading file {csvFile}: {ex.Message}");
                    }
                }

                if (allBars.Count == 0)
                {
                    Console.WriteLine($"No bars found for {symb} in given date range {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                    return;
                }

                Console.WriteLine($"Collected {allBars.Count} bars for {symb} between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}.");

                mergedBars = allBars
                      .Select(line =>
                      {
                          var parts = line.Split(',');
                          var date = DateTime.ParseExact(parts[1] + " " + parts[2],
                                                         "dd/MM/yyyy HH:mm:ss",
                                                         CultureInfo.InvariantCulture);
                          return new BarRecord
                          {
                              Raw = line,
                              Time = date
                          };
                      })
                      .GroupBy(x => x.Time)
                      .Select(g => g.First())
                      .OrderBy(x => x.Time)
                      .Select(x => x.Raw)
                      .ToList();

                // Save cache as JSON
                var records = mergedBars.Select(raw =>
                {
                    var parts = raw.Split(',');
                    var dt = DateTime.ParseExact(parts[1] + " " + parts[2],
                                                 "dd/MM/yyyy HH:mm:ss",
                                                 CultureInfo.InvariantCulture);
                    return new BarRecord { Time = dt, Raw = raw };
                }).ToList();

                var json = JsonSerializer.Serialize(records);
                File.WriteAllText(cacheFile, json);
                Console.WriteLine($"Cache saved to {cacheFile}");
            }

            string mergedCsvPath = Path.Combine(csvFolder, $"{symb}_Merged_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv");
            File.WriteAllLines(mergedCsvPath, mergedBars.Prepend("Symbol,Date,Open,High,Low,Close,Volume"));

            var strategy = new BreakReversalStrategy
            {
                Security = security,
                Portfolio = portfolio
            };

            var tradeLogger = new TradeLogger();
            tradeLogger.AttachStrategy(strategy);
            strategy.Logger = tradeLogger;

            var backtester = new BacktestRunner(mergedCsvPath, security, portfolio, strategy, symb);
            backtester.Run();

            tradeLogger.PrintDailySummary(strategy.StrategyName);
            tradeLogger.PrintStatisticsFromDailyTrades();

            Console.WriteLine("Backtest completed with merged data in given range.");
            Console.ReadLine();
        }
    }
}
