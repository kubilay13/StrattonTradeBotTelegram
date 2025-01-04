using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;

namespace StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService
{
    public class PriceEstimateService
    {
        private readonly BinanceRestClient _binanceRestClient;
        public PriceEstimateService(BinanceRestClient binanceRestClient)
        {
            _binanceRestClient = binanceRestClient;
        }


        // Kline verisi dönüşümü için yardımcı metod
        public List<decimal> GetClosesFromKlines(IEnumerable<IBinanceKline> klines)
        {
            return klines.Select(kline => kline.ClosePrice).ToList();
        }


        public async Task<string> GetTradeSuggestion(string symbol)
        {
            // Kline verisi (5 dakikalık mumlar)
            var klineResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.FiveMinutes, limit: 150);
            if (!klineResult.Success)
                return $"Kline verileri alınamadı: {klineResult.Error?.Message}";

            var closes = klineResult.Data.Select(k => k.ClosePrice).ToList();

            // Teknik göstergeler
            var ema50 = CalculateEMA(closes, 50).Last();
            var ema200 = CalculateEMA(closes, 200).Last();

            // Mevcut fiyat
            var priceResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol);
            if (!priceResult.Success)
                return $"Fiyat bilgisi alınamadı: {priceResult.Error?.Message}";

            decimal currentPrice = priceResult.Data.Price;

            // Orta vadeli trend analizi
            string trendSuggestion;
            if (currentPrice > ema50 && currentPrice < ema200)
            {
                trendSuggestion = "Orta vadeli trend yukarı yönlü. Fiyat, kısa vadede güçlü bir momentum gösteriyor ancak uzun vadeli direncin altında.";
            }
            else if (currentPrice < ema50 && currentPrice > ema200)
            {
                trendSuggestion = "Orta vadeli trend aşağı yönlü. Fiyat, kısa vadede zayıflık gösteriyor ancak uzun vadeli desteğin üzerinde.";
            }
            else if (currentPrice > ema200)
            {
                trendSuggestion = "Fiyat hem kısa hem de uzun vadeli ortalamaların üzerinde. Genel trend yukarı yönlü.";
            }
            else
            {
                trendSuggestion = "Fiyat hem kısa hem de uzun vadeli ortalamaların altında. Genel trend aşağı yönlü.";
            }

            // Ticaret önerisi
            string tradeSuggestion = $"Mevcut Fiyat: {currentPrice}\n" +
                                     $"EMA50: {ema50}\n" +
                                     $"EMA200: {ema200}\n" +
                                     $"{trendSuggestion}";

            return tradeSuggestion;
        }

        // Bollinger Bands hesaplama
        public (decimal, decimal) CalculateBollingerBands(List<decimal> closes)
        {
            var sma = CalculateSMA(closes, 20);
            var stdDev = Math.Sqrt(closes.Select(c => Math.Pow((double)c - (double)sma.Last(), 2)).Average());
            decimal upperBand = sma.Last() + (decimal)(2 * stdDev);
            decimal lowerBand = sma.Last() - (decimal)(2 * stdDev);
            return (lowerBand, upperBand);
        }

        // ATR hesaplama
        public decimal CalculateATR(List<IBinanceKline> klines, int period)
        {
            var trList = new List<decimal>();
            for (int i = 1; i < klines.Count; i++)
            {
                decimal tr = Math.Max(klines[i].HighPrice - klines[i].LowPrice,
                                       Math.Max(Math.Abs(klines[i].HighPrice - klines[i - 1].ClosePrice),
                                                Math.Abs(klines[i].LowPrice - klines[i - 1].ClosePrice)));
                trList.Add(tr);
            }
            return trList.Take(period).Average();
        }

        // RSI hesaplama
        public decimal CalculateRSI(List<decimal> closes, int period)
        {
            if (closes.Count < period)
                throw new Exception("RSI hesaplaması için yeterli veri yok.");

            decimal avgGain = 0;
            decimal avgLoss = 0;

            // İlk periyot için kazanç ve kayıp hesaplaması
            for (int i = 1; i <= period; i++)
            {
                var change = closes[i] - closes[i - 1];
                if (change > 0)
                    avgGain += change;
                else
                    avgLoss -= change;
            }

            avgGain /= period;
            avgLoss /= period;

            // Güncellenmiş RSI değerlerini hesapla
            for (int i = period + 1; i < closes.Count; i++)
            {
                var change = closes[i] - closes[i - 1];
                if (change > 0)
                {
                    avgGain = (avgGain * (period - 1) + change) / period;
                    avgLoss = (avgLoss * (period - 1)) / period;
                }
                else
                {
                    avgGain = (avgGain * (period - 1)) / period;
                    avgLoss = (avgLoss * (period - 1) - change) / period;
                }
            }

            // RS ve RSI hesaplaması
            if (avgLoss == 0)
                return 100; // Hiç kayıp yoksa RSI 100
            decimal rs = avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }


        // MACD hesaplama
        public (decimal macd, decimal signal) CalculateMACD(List<decimal> closes)
        {
            var ema12 = CalculateEMA(closes, 12);
            var ema26 = CalculateEMA(closes, 26);

            decimal macd = ema12.Last() - ema26.Last();
            var signal = CalculateEMA(new List<decimal> { macd }, 9).Last(); // MACD'nin 9 periyotluk EMA'sı

            return (macd, signal);
        }

        // SMA hesaplama
        public List<decimal> CalculateSMA(List<decimal> closes, int period)
        {
            var sma = new List<decimal>();
            for (int i = period - 1; i < closes.Count; i++)
            {
                var avg = closes.Skip(i - period + 1).Take(period).Average();
                sma.Add(avg);
            }
            return sma;
        }

        // EMA hesaplama
        public List<decimal> CalculateEMA(List<decimal> closes, int period)
        {
            var ema = new List<decimal>();
            decimal multiplier = 2m / (period + 1);

            ema.Add(closes.Take(period).Average()); // İlk değer basit ortalama
            for (int i = period; i < closes.Count; i++)
            {
                decimal newEma = (closes[i] - ema.Last()) * multiplier + ema.Last();
                ema.Add(newEma);
            }
            return ema;
        }
    }
}
