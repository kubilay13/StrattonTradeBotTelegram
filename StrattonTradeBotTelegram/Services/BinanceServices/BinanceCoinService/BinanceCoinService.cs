using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using Binance.Net.Objects.Options;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.CommonObjects;

namespace StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService
{
    public class BinanceCoinService
    {
        private readonly BinanceRestClient _binanceRestClient;
        private readonly IBinanceRestClientUsdFuturesApi _usdFuturesApiClient;
        private readonly IBinanceRestClientUsdFuturesApiAccount _accountApiClient;
        private readonly IBinanceRestClientUsdFuturesApiTrading _tradingApiClient;


        public BinanceCoinService(string apiKey, string apiSecret, bool isTestnet)
        {
            var binanceOptions = new BinanceRestOptions
            {
                ApiCredentials = new ApiCredentials(apiKey, apiSecret),
                Environment = isTestnet ? BinanceEnvironment.Testnet : BinanceEnvironment.Live,
                AutoTimestamp = true
            };

            _binanceRestClient = new BinanceRestClient(options =>
            {
                options.ApiCredentials = binanceOptions.ApiCredentials;
                options.Environment = binanceOptions.Environment;
            });
            _usdFuturesApiClient = _binanceRestClient.UsdFuturesApi;
            _accountApiClient = _usdFuturesApiClient.Account;
            _tradingApiClient = _usdFuturesApiClient.Trading;
        }

        public async Task<string> BinanceCoinAmount(string Symbol)
        {
         
            var result = await _binanceRestClient.SpotApi.ExchangeData.GetTickerAsync(Symbol);
            var futuresamount = await _binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync();
            if (result.Success)
            {
                return $"{result.Data.LastPrice} $ {futuresamount}";  
            }
                return $"{Symbol} Fiyat bilgisi alındı!"; 
        }

        public async Task<string> BinancePopularCoinAmountTimer()
        {
            var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "TRXUSDT" };
            var pricemessage = new List<string>();
            foreach(var i in symbols)
            {
                var result= await _binanceRestClient.SpotApi.ExchangeData.GetTickerAsync($"{i}");
                if(result.Success)
                {

                    pricemessage.Add($"{i} fiyatı {result.Data.LastPrice} $");
                    
                }
                else
                {
                    pricemessage.Add("Fiyat Bilgileri Alınamadı.");
                }
            }
            return string.Join("\n", pricemessage);
        }

        public async Task<decimal> GetUserFuturesBalanceAsync()
        {
            try
            {
                var result = await _binanceRestClient.UsdFuturesApi.Account.GetAccountInfoV2Async();
                if (result.Success)
                {
                    var balance = result.Data.TotalWalletBalance;
                    return balance;
                }
                else
                {
                    throw new Exception("Vadeli işlemler bakiyesi alınamadı.❌");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message} ❌");
                throw;
            }
        }
        // Kline verisi dönüşümü için yardımcı metod
        public List<decimal> GetClosesFromKlines(IEnumerable<IBinanceKline> klines)
        {
            return klines.Select(kline => kline.ClosePrice).ToList();
        }
        public async Task<IEnumerable<IBinanceKline>> GetKlinesAsync(string symbol, KlineInterval interval, int limit)
        {
            // Şu andan itibaren geriye dönük bir zaman aralığı hesaplayın
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddHours(-limit); // Limit kadar geriye gidin

            // Binance API'den Kline verilerini alın
            var result = await _usdFuturesApiClient.ExchangeData.GetKlinesAsync(symbol, interval, startTime, endTime, limit: limit);
            if (!result.Success)
                throw new Exception($"Kline verileri alınamadı: {result.Error?.Message}");

            return result.Data;
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
        public List<decimal> CalculateFibonacciLevels(decimal highestHigh, decimal lowestLow)
        {
            if (highestHigh <= lowestLow)
            {
                throw new ArgumentException("Highest High değeri Lowest Low değerinden büyük olmalıdır.");
            }

            // Farkı hesapla
            var difference = highestHigh - lowestLow;

            // Fibonacci seviyelerini hesapla
            var fibLevels = new List<decimal>
    {
        lowestLow,                                   // 0.000
        lowestLow + difference * 0.236m,            // 0.236
        lowestLow + difference * 0.382m,            // 0.382
        lowestLow + difference * 0.500m,            // 0.500
        lowestLow + difference * 0.618m,            // 0.618
        lowestLow + difference * 0.786m,            // 0.786
        highestHigh                                 // 1.000
    };

            // Log Fibonacci seviyeleri
            Console.WriteLine("Fibonacci Seviyeleri:");
            for (int i = 0; i < fibLevels.Count; i++)
            {
                Console.WriteLine($"Level {i}: {fibLevels[i]:F2}");
            }

            return fibLevels;
        }


        public async Task<string> GetImprovedFibonacciTradeWithTPAndSL(string symbol)
        {
            // Kline verisi alınması
            var klineResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.OneHour, limit: 150);
            if (!klineResult.Success)
                return $"Kline verileri alınamadı: {klineResult.Error?.Message}";

            var closes = klineResult.Data.Select(k => k.ClosePrice).ToList();
            var highPrices = klineResult.Data.Select(k => k.HighPrice).ToList();
            var lowPrices = klineResult.Data.Select(k => k.LowPrice).ToList();

            // Swing High/Low hesapla (örnek: 20 periyotluk)
            decimal highestHigh = highPrices.TakeLast(20).Max();
            decimal lowestLow = lowPrices.TakeLast(20).Min();

            // Fibonacci seviyelerini hesapla
            var fibLevels = CalculateFibonacciLevels(highestHigh, lowestLow);

            // Mevcut fiyatı al
            var priceResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol);
            if (!priceResult.Success)
                return $"Fiyat bilgisi alınamadı: {priceResult.Error?.Message}";

            decimal currentPrice = priceResult.Data.Price;

            // ATR hesapla
            decimal atr = CalculateATR(klineResult.Data.ToList(), 14);

            // Alım, TP ve SL seviyelerini belirle
            string tradeSuggestion = $"Mevcut Fiyat: {currentPrice}\n" +
                                     $"Fibonacci Seviyeleri:\n" +
                                     $"0.236: {fibLevels[1]}\n" +
                                     $"0.382: {fibLevels[2]}\n" +
                                     $"0.5: {fibLevels[3]}\n" +
                                     $"0.618: {fibLevels[4]}\n";

            // Ticaret önerisi
            if (currentPrice < fibLevels[1]) // Fiyat en alt seviyede
            {
                decimal tp = fibLevels[2];
                decimal sl = currentPrice - atr;

                tradeSuggestion += $"\n**LONG ÖNERİSİ**\n" +
                                   $"Giriş Noktası: {currentPrice}\n" +
                                   $"Take Profit (TP): {tp}\n" +
                                   $"Stop Loss (SL): {sl}\n";
            }
            else if (currentPrice > fibLevels[4]) // Fiyat en üst seviyede
            {
                decimal tp = fibLevels[3];
                decimal sl = currentPrice + atr;

                tradeSuggestion += $"\n**SHORT ÖNERİSİ**\n" +
                                   $"Giriş Noktası: {currentPrice}\n" +
                                   $"Take Profit (TP): {tp}\n" +
                                   $"Stop Loss (SL): {sl}\n";
            }
            else
            {
                tradeSuggestion += "\n**BEKLEME ÖNERİSİ**: Fiyat Fibonacci seviyeleri arasında.";
            }

            return tradeSuggestion;
        }


    }
}