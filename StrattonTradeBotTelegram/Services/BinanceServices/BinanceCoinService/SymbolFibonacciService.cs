using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;

namespace StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService
{
    public class SymbolFibonacciService
    {
        private readonly IBinanceRestClientUsdFuturesApi _usdFuturesApiClient;
        private readonly BinanceRestClient _binanceRestClient;
        public SymbolFibonacciService(BinanceRestClient binanceRestClient)
        {
            _binanceRestClient = binanceRestClient;
            _usdFuturesApiClient = _binanceRestClient.UsdFuturesApi;
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
    }
}
