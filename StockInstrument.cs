//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace StockBacktestProject
//{
//    [ProtoContract(SkipConstructor = true)]
//    public class StockInstrument : InstrumentBase
//    {
//        [ProtoMember(15)]
//        [JsonProperty(PropertyName = "ISIN")]
//        public string ISIN { get; set; }

//        [ProtoMember(16)]
//        [JsonProperty(PropertyName = "Sector")]
//        public string Sector { get; set; }

//        [ProtoMember(17)]
//        [JsonProperty(PropertyName = "ExchangeCode")]
//        public string ExchangeCode { get; set; }

//        public StockInstrument(
//            ExchangeSegment exchangeSegment,
//            uint exchangeInstrumentID,
//            string symbol,
//            string ticker,
//            string instrumentName,
//            string instrumentDescription,
//            int lotSize = 1,
//            double minTickSize = 0.05,
//            double bigPointValue = 1.0,
//            PriceFormat priceFormat = PriceFormat.DECIMAL)
//            : base(exchangeSegment,
//                   exchangeInstrumentID,
//                   InstrumentType.Stock,
//                   symbol,
//                   ticker,
//                   instrumentName,
//                   instrumentDescription,
//                   lotSize,
//                   minTickSize,
//                   bigPointValue,
//                   priceFormat)
//        {
//            base.InstrumentIndentifer = InstrumentBase.GetInstrumentIdentifier(symbol);
//        }

//        public override string ToString()
//        {
//            return $"{InstrumentName} [{Symbol}] ({ExchangeCode})";
//        }

//        public override bool Equals(object obj)
//        {
//            if (obj is StockInstrument stock &&
//                base.InstrumentName == stock.InstrumentName &&
//                base.Symbol == stock.Symbol)
//                return true;

//            return false;
//        }
//    }
//}
