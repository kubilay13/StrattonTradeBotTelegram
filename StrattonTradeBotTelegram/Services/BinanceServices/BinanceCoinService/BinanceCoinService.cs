using Binance.Net.Clients;

namespace StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService
{
    public class BinanceCoinService
    {
        private readonly BinanceRestClient _binanceRestClient;

        public BinanceCoinService(BinanceRestClient binanceRestClient)
        {
            _binanceRestClient = binanceRestClient;
        }

        public async Task<string> BinanceCoinAmount(string Symbol)
        {
            var binancerestclient= new BinanceRestClient();
            var result = await binancerestclient.SpotApi.ExchangeData.GetTickerAsync(Symbol);
            if (result.Success)
            {
                return $"{result.Data.LastPrice} $";  
            }
            

        }
        
    }
}
