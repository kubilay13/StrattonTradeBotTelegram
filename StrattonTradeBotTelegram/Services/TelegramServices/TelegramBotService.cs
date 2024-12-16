using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService;

namespace StrattonTradeBotTelegram.Services.TelegramServices
{
    public class TelegramBotService
    {
        private readonly ITelegramBotClient _botService;
        private readonly BinanceCoinService _binanceCoinService;

        public TelegramBotService(string token, string binanceApiKey, string binanceApiSecret, bool isTestnet,BinanceCoinService binanceCoinService)
        {
            var botOptions = new TelegramBotClientOptions(token);
            _botService = new TelegramBotClient(botOptions);
            _binanceCoinService = new BinanceCoinService(binanceApiKey, binanceApiSecret, isTestnet);
            _binanceCoinService = binanceCoinService;

        }
        public void StartReceiving()
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() 
            };

            _botService.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: CancellationToken.None);

            Console.WriteLine("Bot çalışmaya başladı...");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is { } message)
            {
                var messageText = update.Message.Text;
                if (messageText.StartsWith("/Komutlar"))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "/SYMBOL");
                }
                if (messageText.StartsWith("/"))
                {
                    string symbol = messageText.Substring(1).ToUpper(); // '/' işaretini çıkar ve büyük harfe çevir
                    // Eğer USDT ile bitiyorsa fiyat bilgisi al
                    if (symbol.EndsWith("USDT"))
                    {
                        var price = await _binanceCoinService.BinanceCoinAmount(symbol);
                        await botClient.SendTextMessageAsync(message.Chat.Id, $"{symbol} fiyatı: {price}");
                    }
                    else
                    {
                        // USDT ile bitmiyorsa uyarı gönder
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Fiyat sembol formatı! Örnek: /BTCUSDT veya /ETHUSDT");
                    }
                }
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Hata oluştu: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}

