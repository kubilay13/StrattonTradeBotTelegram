using Telegram.Bot;
using StrattonTradeBotTelegram.Services.TelegramServices;
using StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService;
using Binance.Net.Clients;

var builder = WebApplication.CreateBuilder(args);

// Telegram Bot Token'i yapýlandýrmadan alýn
var telegramBotToken = builder.Configuration["TelegramBot:Token"];
var binanceApiKey = builder.Configuration["Binance:ApiKey"];
var binanceApiSecret = builder.Configuration["Binance:ApiSecret"];

// Testnet veya Live seçeneðini belirleyin
bool isTestnet = builder.Configuration.GetValue<bool>("Binance:IsTestnet", true);

// BinanceCoinService'i baðýmlýlýk enjeksiyonuna ekliyoruz
builder.Services.AddSingleton<BinanceCoinService>(serviceProvider =>
{
    return new BinanceCoinService(binanceApiKey, binanceApiSecret, isTestnet);
});

// Telegram Bot istemcisini baðýmlýlýk enjeksiyonuna ekleyin
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramBotToken));
builder.Services.AddSingleton<SymbolFibonacciService>();
builder.Services.AddSingleton<PriceEstimateService>();
builder.Services.AddSingleton<BinanceRestClient>();
// PriceReminderService'i baðýmlýlýklarýný çözerek ekliyoruz
builder.Services.AddSingleton<PriceReminderService>(serviceProvider =>
{
    var priceEstimateService = serviceProvider.GetRequiredService<PriceEstimateService>();
    var telegramBotClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
    var binanceCoinService = serviceProvider.GetRequiredService<BinanceCoinService>();
    var symbolFibonacciService = serviceProvider.GetRequiredService<SymbolFibonacciService>();
    var chatId = builder.Configuration.GetValue<long>("TelegramBot:ChatId");  // Örneðin, konfigürasyondan alabilirsiniz
    return new PriceReminderService(telegramBotClient, binanceCoinService, chatId);
});

// TelegramBotService'i ekliyoruz
builder.Services.AddSingleton<TelegramBotService>(serviceProvider =>
{
    var priceEstimateService = serviceProvider.GetRequiredService<PriceEstimateService>();
    var symbolFibonacciService= serviceProvider.GetRequiredService<SymbolFibonacciService>();
    var priceReminderService = serviceProvider.GetRequiredService<PriceReminderService>();
    var binanceCoinService = serviceProvider.GetRequiredService<BinanceCoinService>();
    return new TelegramBotService(telegramBotToken, binanceApiKey, binanceApiSecret, isTestnet, binanceCoinService, priceReminderService,symbolFibonacciService,priceEstimateService);
});

// Diðer servisleri ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Telegram bot servisini baþlat
var telegramBotService = app.Services.GetRequiredService<TelegramBotService>();
telegramBotService.StartReceiving();

// HTTP pipeline'ý yapýlandýr
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
