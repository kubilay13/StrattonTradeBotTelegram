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
        private readonly PriceReminderService _priceReminderService;
        private bool _isPriceReminderStarted = false;
        public TelegramBotService(string token, string binanceApiKey, string binanceApiSecret, bool isTestnet,BinanceCoinService binanceCoinService, PriceReminderService priceReminderService)
        {
            var botOptions = new TelegramBotClientOptions(token);
            _botService = new TelegramBotClient(botOptions);
            _binanceCoinService = new BinanceCoinService(binanceApiKey, binanceApiSecret, isTestnet);
            _binanceCoinService = binanceCoinService;
            _priceReminderService = priceReminderService;
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
                if (messageText.StartsWith("/Balance"))
                {
                    var balance= await _binanceCoinService.GetUserFuturesBalanceAsync();
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Balance: {balance} $");
                }
                if (messageText.StartsWith("/FiyatTakipAç"))
                {
                    // Fiyat hatırlatıcı servisi başlatılmadıysa başlat
                    if (!_isPriceReminderStarted)
                    {
                        // PriceReminderService başlat
                        _priceReminderService.StartPriceReminder(message.Chat.Id);  // Chat ID'yi buraya geçir
                        _isPriceReminderStarted = true;  // Zamanlayıcı başlatıldı

                        await botClient.SendTextMessageAsync(message.Chat.Id, "Fiyat hatırlatıcı başlatıldı!");
                    }
                    else
                    {
                        // Zaten başlatıldıysa kullanıcıyı bilgilendir
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Fiyat hatırlatıcı zaten başlatıldı.");
                    }
                }

                else if (messageText.StartsWith("/Kapat"))
                {
                    // Fiyat hatırlatıcıyı durdur
                    if (_isPriceReminderStarted)
                    {
                        // Timer'ı durdur
                        _priceReminderService?.Dispose();
                        _isPriceReminderStarted = false;

                        // Kullanıcıya yanıt gönder
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Fiyat hatırlatıcı durduruldu.");
                    }
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

