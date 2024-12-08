using Telegram.Bot;
using StrattonTradeBotTelegram.Services.TelegramServices;
using StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService;
using Binance.Net.Clients;

var builder = WebApplication.CreateBuilder(args);


// Telegram Bot Token'i yapýlandýrmadan alýn
var telegramBotToken = builder.Configuration["TelegramBot:Token"];
if (string.IsNullOrEmpty(telegramBotToken))
{
    throw new InvalidOperationException("Telegram bot token is not configured. Please check appsettings.json.");
}

// Telegram Bot istemcisini baðýmlýlýk enjeksiyonuna ekleyin
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramBotToken));

// Telegram ve Binance servislerini kaydedin
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddSingleton<BinanceCoinService>();
builder.Services.AddSingleton<BinanceRestClient>();

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
