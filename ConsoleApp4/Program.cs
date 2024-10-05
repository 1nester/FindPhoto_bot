using Telegram.Bot; // Подключение библиотеки для работы с Telegram Bot API
using Telegram.Bot.Types; // Работа с типами данных Telegram, такими как Message, Chat и т.д.
using Telegram.Bot.Polling; // Позволяет боту получать обновления с сервера Telegram
using Telegram.Bot.Types.Enums; // Типы данных для различных enum (например, типы обновлений)
using FlickrNet; // Библиотека для работы с API сервиса Flickr

public class Bot
{
    private readonly TelegramBotClient _botClient; // Объявляем объект TelegramBotClient для взаимодействия с ботом

    // Конструктор класса Bot, инициализирует объект _botClient с токеном
    public Bot(string token)
    {
        _botClient = new TelegramBotClient(token);
    }
    
    // Метод для создания команд бота
    public void CreateCommands()
    {
        // Установка списка доступных команд для бота
        _botClient.SetMyCommandsAsync(new List<BotCommand>()
        {
            // Команда "/start", описание — запуск бота
            new()
            {
                Command = CustomBotCommands.START,
                Description = "Запустить бота."
            },
            // Команда "/about", описание — объяснение функционала бота
            new()
            {
                Command = CustomBotCommands.ABOUT,
                Description = "Что делает бот и как им пользоваться?"
            }
        });
    }

    // Метод для начала получения обновлений от Telegram сервера
    public void StartReceiving()
    {
        var cancellationTokenSource = new CancellationTokenSource(); // Токен для отмены получения обновлений
        var cancellationToken = cancellationTokenSource.Token; // Получаем токен для контроля операций

        // Настройка параметров получения обновлений
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new UpdateType[] { UpdateType.Message } // Получаем только обновления с сообщениями
        };
        
        // Запуск получения обновлений, обработчики HandleUpdateAsync и HandleError
        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleError,
            receiverOptions,
            cancellationToken
        );
    }

    // Асинхронный метод для обработки обновлений от Telegram
    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        var chatId = update.Message.Chat.Id; // Получаем ID чата от пользователя

        // Проверка, что сообщение содержит текст
        if (string.IsNullOrEmpty(update.Message.Text))
        {
            await _botClient.SendTextMessageAsync(chatId,
                text: "Данный бот принимает только текстовые сообщения.\n" + "Введите ваш запрос правильно.",
                cancellationToken: cancellationToken); // Сообщение об ошибке
            return;
        }

        var messageText = update.Message.Text; // Сохраняем текст сообщения

        // Если сообщение — это команда "/start"
        if (IsStartCommand(messageText))
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Привет, я бот по поиску картинок. Введите ваш запрос.",
                cancellationToken: cancellationToken); // Ответ на команду "/start"
            return;
        }
        
        // Если сообщение — это команда "/about"
        if (IsAboutCommand(messageText))
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Данный бот возвращает 1 картинку по запросу пользователя. \n" +
                      "Чтобы получить картинку, введите текстовый запрос.",
                cancellationToken: cancellationToken); // Ответ на команду "/about"
            return;
        }
        
        // Если сообщение — это текстовый запрос, отправляем фото
        await SendPhotoAsync(chatId, messageText, cancellationToken);
    }
    
    // Обработчик ошибок при взаимодействии с Telegram API
    private Task HandleError(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(exception); // Выводим ошибку в консоль
        return Task.CompletedTask;
    }

    // Проверка, является ли текст команды командой "/start"
    private bool IsStartCommand(string messageText)
    {
        return messageText.ToLower() == CustomBotCommands.START;
    }
    
    // Проверка, является ли текст команды командой "/about"
    private bool IsAboutCommand(string messageText)
    {
        return messageText.ToLower() == CustomBotCommands.ABOUT;
    }
    
    // Метод для отправки фотографий по запросу
    private async Task SendPhotoAsync(long chatId, string request, CancellationToken cancellationToken)
    {
        var photoUrls = await FlickrAPI.GetPhotoUrlAsync(request, 5); // Получаем до 5 URL фото по запросу
        
        // Если нет результатов, сообщаем об этом пользователю
        if (photoUrls == null || photoUrls.Count == 0)
        {
            await _botClient.SendTextMessageAsync(chatId,
                "Изображений не найдено.",
                cancellationToken: cancellationToken);
            return;
        }

        // Отправляем все найденные фотографии
        foreach (var photoUrl in photoUrls)
        {
            await _botClient.SendPhotoAsync(chatId: chatId,
                photo: new InputFileUrl(photoUrl),
                cancellationToken: cancellationToken);
        }
    }
}

public static class CustomBotCommands
{
    // Определение команд "/start" и "/about" как констант
    public const string START = "/start";
    public const string ABOUT = "/about";
}

public static class FlickrAPI
{
    // Инициализация клиента Flickr API с заданным ключом
    private static readonly Flickr _flickr = new Flickr("780de9e2bba22e551706cf2916b418c7");
    private static readonly Random _random = new Random(); // Генератор случайных чисел
    
    // Асинхронный метод для получения списка URL изображений по запросу
    public static async Task<List<string>> GetPhotoUrlAsync(string request, int count)
    {
        // Параметры поиска изображений по ключевому слову
        var photoSearchOptions = new PhotoSearchOptions
        {
            Text = request, // Текст запроса для поиска
            SortOrder = PhotoSearchSortOrder.Relevance, // Сортировка по релевантности
            PerPage = count // Количество результатов на страницу
        };
        
        // Поиск фотографий через Flickr API
        PhotoCollection photos = await _flickr.PhotosSearchAsync(photoSearchOptions);
        var listPhotos = photos.ToList(); // Преобразование результата в список

        if (listPhotos.Count == 0)
        {
            return null; // Если фото не найдено, возвращаем null
        }
        
        // Ограничиваем количество результатов
        var resultCount = Math.Min(count, listPhotos.Count);
        // Случайным образом выбираем фотографии из списка
        var selectedPhotos = listPhotos.OrderBy(x => _random.Next()).Take(resultCount);
        // Возвращаем URL выбранных фотографий
        return selectedPhotos.Select(photo => photo.LargeUrl).ToList();
    }
}

class Program
{
    static void Main(string[] args)
    {
        var bot = new Bot("7186032530:AAGe4mSLJPWmUWan4mogVINllC_yIG0YItw"); // Инициализация бота с токеном
        
        bot.CreateCommands(); // Создание команд бота
        bot.StartReceiving(); // Запуск получения обновлений
        Console.ReadLine(); // Ожидание ввода в консоль для предотвращения завершения программы
    }
}
