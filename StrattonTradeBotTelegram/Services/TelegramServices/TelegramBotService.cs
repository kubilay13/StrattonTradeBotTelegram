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
                    // Coin sembolünü al ("/BTCUSDT" -> "BTCUSDT")
                    string symbol = messageText.Substring(1).ToUpper();

                    // Eğer coin sembolü "USDT" ile bitiyorsa fiyat bilgisini al
                    if (symbol.EndsWith("USDT"))
                    {
                        try
                        {
                            // Binance API üzerinden fiyat bilgisini al
                            string price = await _binanceCoinService.BinanceCoinAmount(symbol);

                            // Fiyatı temizleyin: '$' ve diğer metinlerden kurtulun
                            string cleanPrice = new string(price.Where(c => char.IsDigit(c) || c == '.').ToArray());

                            // Eğer temizlenmiş fiyat bir decimal'a dönüşebiliyorsa, dönüştürme işlemi yapılır
                            if (decimal.TryParse(cleanPrice, out decimal entryPrice))
                            {
                                // Başarıyla dönüşüm yapıldı, entryPrice kullanılabilir
                                Console.WriteLine($"Giriş fiyatı: {entryPrice}");
                            }
                            else
                            {
                                // Fiyat geçerli bir decimal değilse, hata mesajı
                                Console.WriteLine("Fiyat geçerli bir decimal değeri değil.");
                            }

                            // TP ve SL seviyelerini belirle
                            decimal tpPrice = entryPrice * 1.02m;  // %2 TP
                            decimal slPrice = entryPrice * 0.99m;  // %1 SL

                            // Kullanıcıya giriş fiyatı, TP ve SL seviyelerini gönder
                            await botClient.SendTextMessageAsync(message.Chat.Id,
                                $"{symbol} için işlem bilgileri:\n" +
                                $"Giriş Fiyatı: {entryPrice} USDT\n" +
                                $"Take Profit (TP): {tpPrice} USDT\n" +
                                $"Stop Loss (SL): {slPrice} USDT");
                        }
                        catch (Exception ex)
                        {
                            // Eğer hata olursa hata mesajı gönder
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Fiyat alınırken bir hata oluştu. Lütfen tekrar deneyin.");
                        }
                    }
                    else
                    {
                        // USDT ile bitmeyen coin sembolü için uyarı
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Lütfen geçerli bir USDT sembolü girin (örneğin, BTCUSDT).");
                    }
                }
                //PİYASA FİYATINDAN İŞLEM AÇMA
                if (messageText.StartsWith("/"))
                {
                    // Komutun parçalarını ayrıştır
                    var parts = messageText.Substring(1).Split(' '); // '/' işaretini çıkar ve boşluklardan ayır
                    if (parts.Length == 4)
                    {
                        string symbol = parts[0].ToUpper();  // Sembol (ör. BTCUSDT)
                        string action = parts[1].ToUpper();  // İşlem türü (LONG/SHORT)
                        int leverage;
                        decimal amount;

                        // Geçerli kaldıracı ve miktarı doğrula
                        if (int.TryParse(parts[2], out leverage) && decimal.TryParse(parts[3], out amount))
                        {
                            if (symbol.EndsWith("USDT") && (action == "LONG" || action == "SHORT"))
                            {
                                // Binance API'si üzerinden işlem aç
                                var result = await _binanceCoinService.OpenPosition(symbol, action, leverage, amount);

                                // Kullanıcıya işlem sonucu mesajı gönder
                                await botClient.SendTextMessageAsync(message.Chat.Id, result);
                                var balance = await _binanceCoinService.GetUserFuturesBalanceAsync();
                                await botClient.SendTextMessageAsync(message.Chat.Id, $"Balance: {balance-amount} $");

                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, "Hatalı komut. Doğru format: /symbol LONG/SHORT LEVERAGE AMOUNT");
                            }
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Hatalı kaldırac veya miktar. Örnek: /BTCUSDT LONG 25 100");
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Hatalı komut formatı. Örnek: /BTCUSDT LONG 25 100");
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

