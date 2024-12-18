using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using Binance.Net.Objects.Options;
using CryptoExchange.Net.Authentication;

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
        public async Task<string> OpenPosition(string symbol, string action, int leverage, decimal margin)
        {
            try
            {
                // 1. Marj modunu izole olarak ayarla
                var marginTypeResult = await _usdFuturesApiClient.Account.ChangeMarginTypeAsync(symbol, FuturesMarginType.Isolated);
                if (!marginTypeResult.Success && marginTypeResult.Error?.Message != "No need to change margin type.")
                {
                    // Eğer hata "Değişikliğe gerek yok" dışında bir şeyse, hata döndür
                    return $"Marj modu ayarlanamadı: {marginTypeResult.Error?.Message}";
                }

                // 2. Kaldıraç ayarla
                var leverageResult = await _usdFuturesApiClient.Account.ChangeInitialLeverageAsync(symbol, leverage);
                if (!leverageResult.Success)
                {
                    return $"Kaldıraç ayarlanamadı: {leverageResult.Error?.Message}";
                }

                // 3. Sembol detaylarını al
                var symbolDetailsResult = await _usdFuturesApiClient.ExchangeData.GetExchangeInfoAsync();
                if (!symbolDetailsResult.Success)
                {
                    return $"Sembol bilgisi alınamadı: {symbolDetailsResult.Error?.Message}";
                }

                var symbolDetails = symbolDetailsResult.Data.Symbols.FirstOrDefault(s => s.Name == symbol);
                if (symbolDetails == null)
                {
                    return $"Sembol bulunamadı: {symbol}";
                }

                int precision = symbolDetails.QuantityPrecision; // Miktar hassasiyeti

                // 4. Fiyat bilgisi al
                var priceResult = await _usdFuturesApiClient.ExchangeData.GetPriceAsync(symbol);
                if (!priceResult.Success)
                {
                    return $"Fiyat bilgisi alınamadı: {priceResult.Error?.Message}";
                }

                decimal price = priceResult.Data.Price;

                // 5. Pozisyon boyutunu hesapla
                decimal positionSize = Math.Round(margin * leverage / price, precision);

                // 6. İşlem türünü belirle
                var side = action == "LONG" ? OrderSide.Buy : OrderSide.Sell;

                // 7. Piyasa emri gönder
                var orderResult = await _usdFuturesApiClient.Trading.PlaceOrderAsync(
                    symbol: symbol,
                    side: side,
                    type: FuturesOrderType.Market,
                    quantity: positionSize
                );

                if (orderResult.Success)
                {
                    return $"{action} pozisyon başarıyla açıldı: {symbol}, Kaldıraç: {leverage}X, Miktar: {margin}";
                }
                else
                {
                    return $"Pozisyon açılırken bir hata oluştu: {orderResult.Error?.Message}";
                }
            }
            catch (Exception ex)
            {
                return $"Bir hata oluştu: {ex.Message}";
            }
        }
        public async Task<(decimal support, decimal resistance)> GetSupportResistance(string symbol)
        {
            var klines = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.OneHour, limit: 50);
            var lowPrices = klines.Data.Select(k => k.LowPrice).ToList();
            var highPrices = klines.Data.Select(k => k.HighPrice).ToList();

            decimal supportLevel = lowPrices.Min();
            decimal resistanceLevel = highPrices.Max();

            return (supportLevel, resistanceLevel);
        }


    }
}