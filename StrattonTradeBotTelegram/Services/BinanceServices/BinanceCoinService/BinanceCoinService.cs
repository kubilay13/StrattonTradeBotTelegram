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
            // Kline verisi alınması
            var klineResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.OneHour, limit: 150);
            if (!klineResult.Success)
                return $"Kline verileri alınamadı: {klineResult.Error?.Message}";

            var closes = klineResult.Data.Select(k => k.ClosePrice).ToList();
            var highPrices = klineResult.Data.Select(k => k.HighPrice).ToList();
            var lowPrices = klineResult.Data.Select(k => k.LowPrice).ToList();

            // Teknik göstergeler hesaplaması
            var (macd, signal) = CalculateMACD(closes);
            var rsi = CalculateRSI(closes, 14);
            var sma50 = CalculateSMA(closes, 50).Last();
            var sma200 = CalculateSMA(closes, 200).Last();
            var ema50 = CalculateEMA(closes, 50).Last();
            var ema200 = CalculateEMA(closes, 200).Last();
            var bollingerBands = CalculateBollingerBands(closes);
            var atr = CalculateATR(klineResult.Data.ToList(), 14);

            // Mevcut fiyatı al
            var priceResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol);
            if (!priceResult.Success)
                return $"Fiyat bilgisi alınamadı: {priceResult.Error?.Message}";

            decimal currentPrice = priceResult.Data.Price;

            // Risk-Reward hesaplaması
            decimal tpPrice = currentPrice * 1.03m; // %3 Take Profit
            decimal slPrice = currentPrice - atr * 1.5m; // ATR'ye dayalı Stop Loss
            decimal riskRewardRatio = (tpPrice - currentPrice) / (currentPrice - slPrice); // Risk/Ödül Oranı hesaplama

            // Ticaret önerisi başlatma
            string tradeSuggestion = $"Giriş Fiyatı: {currentPrice}\n" +
                                     $"Take Profit (TP): {tpPrice}\n" +
                                     $"Stop Loss (SL): {slPrice}\n" +
                                     $"Risk/Ödül Oranı: {riskRewardRatio:F2}\n";

            // **LONG** Pozisyonu (Alış)
            if (macd > signal && rsi < 30 && currentPrice < bollingerBands.Item1)
            {
                tradeSuggestion += "**LONG** pozisyon açılabilir.";
            }
            // **SHORT** Pozisyonu (Satış)
            else if (macd < signal && rsi > 70 && currentPrice > bollingerBands.Item2)
            {
                tradeSuggestion += "**SHORT** pozisyon açılabilir.";
            }
            else
            {
                tradeSuggestion += "**BEKLE** pozisyonu.";
            }

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
            decimal gain = 0, loss = 0;
            for (int i = 1; i <= period; i++)
            {
                decimal change = closes[i] - closes[i - 1];
                if (change > 0)
                    gain += change;
                else
                    loss -= change;
            }

            decimal avgGain = gain / period;
            decimal avgLoss = loss / period;

            decimal rs = avgGain / avgLoss;
            decimal rsi = 100 - (100 / (1 + rs));
            return rsi;
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

            return fibLevels;
        }

        public async Task<string> GetFibonacciTradeWithTPAndSL(string symbol)
        {
            // Kline verisi alınması
            var klineResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.OneHour, limit: 150);
            if (!klineResult.Success)
                return $"Kline verileri alınamadı: {klineResult.Error?.Message}";

            var highPrices = klineResult.Data.Select(k => k.HighPrice).ToList();
            var lowPrices = klineResult.Data.Select(k => k.LowPrice).ToList();

            decimal highestHigh = highPrices.Max();
            decimal lowestLow = lowPrices.Min();

            // Fibonacci seviyelerini hesapla
            var fibLevels = CalculateFibonacciLevels(highestHigh, lowestLow);

            // Mevcut fiyatı al
            var priceResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol);
            if (!priceResult.Success)
                return $"Fiyat bilgisi alınamadı: {priceResult.Error?.Message}";

            decimal currentPrice = priceResult.Data.Price;

            // Alım, TP ve SL seviyelerini belirle
            string tradeSuggestion = $"Mevcut Fiyat: {currentPrice}\n" +
                                     $"Fibonacci Seviyeleri:\n" +
                                     $"0.236: {fibLevels[0]}\n" +
                                     $"0.382: {fibLevels[1]}\n" +
                                     $"0.5: {fibLevels[2]}\n" +
                                     $"0.618: {fibLevels[3]}\n" +
                                     $"0.786: {fibLevels[4]}\n";

            // Ticaret önerisi
            if (currentPrice < fibLevels[0])
            {
                decimal tp = fibLevels[1]; // TP, bir sonraki Fibonacci seviyesi
                decimal sl = fibLevels[4]; // SL, en yüksek Fibonacci seviyesi

                tradeSuggestion += $"\n**ALIM ÖNERİSİ**\n" +
                                   $"Giriş Noktası: {currentPrice}\n" +
                                   $"Take Profit (TP): {tp}\n" +
                                   $"Stop Loss (SL): {sl}\n";
            }
            else if (currentPrice > fibLevels[4])
            {
                decimal tp = fibLevels[3]; // TP, bir önceki Fibonacci seviyesi
                decimal sl = fibLevels[0]; // SL, en düşük Fibonacci seviyesi

                tradeSuggestion += $"\n**SATIŞ ÖNERİSİ**\n" +
                                   $"Giriş Noktası: {currentPrice}\n" +
                                   $"Take Profit (TP): {tp}\n" +
                                   $"Stop Loss (SL): {sl}\n";
            }
            else
            {
                tradeSuggestion += "\n**BEKLEME ÖNERİSİ**: Fiyat Fibonacci seviyeleri arasında hareket ediyor.";
            }

            return tradeSuggestion;
        }

    }
}