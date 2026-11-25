using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5080";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

Console.WriteLine($"=== Клієнт ServiceCenter (Сервер: {baseUrl}) ===\n");

async Task<(int Status, string Body)> SendAsync(HttpMethod method, string url, object? payload = null)
{
    using var req = new HttpRequestMessage(method, url);
    if (payload is not null)
        req.Content = JsonContent.Create(payload);

    using var resp = await http.SendAsync(req);
    var text = await resp.Content.ReadAsStringAsync();
    Console.WriteLine($"[{method} {url}] -> HTTP {(int)resp.StatusCode}");
    Console.WriteLine($"Відповідь: {text}\n");
    return ((int)resp.StatusCode, text);
}

async Task<int> CountOrdersAsync()
{
    var (_, body) = await SendAsync(HttpMethod.Get, "/api/orders");
    using var list = JsonDocument.Parse(body);
    return list.RootElement.GetArrayLength();
}

// Режим перевірки після перезапуску сервера (спринт S1):
//   dotnet run --project src/ServiceCenter.Client -- history <серійний номер>
if (args.Length == 2 && args[0] == "history")
{
    Console.WriteLine($"--- Історія ремонтів пристрою {args[1]} ---");
    await SendAsync(HttpMethod.Get, $"/api/devices/{Uri.EscapeDataString(args[1])}/orders");
    return;
}

// Дані зберігаються в базі й накопичуються між запусками, тому кожен прогін
// використовує новий серійний номер, а номер замовлення бере з відповіді сервера.
var serial = $"SN-IPH15-{DateTime.Now:yyyyMMddHHmmss}";

// 1. UC-01 (Успіх, AC-01): Реєстрація нового замовлення
Console.WriteLine("--- 1. UC-01 (AC-01): Успішна реєстрація замовлення ---");
var (status1, body1) = await SendAsync(HttpMethod.Post, "/api/orders", new
{
    clientFullName = "Петренко Іван Васильович",
    clientPhone = "+380501234567",
    deviceBrand = "Apple",
    deviceModel = "iPhone 15 Pro",
    serialNumber = serial,
    issueDescription = "Розбитий дисплей, не працює сенсор",
    engineerId = 1
});

if (status1 != 201) return;
using var doc = JsonDocument.Parse(body1);
int orderId = doc.RootElement.GetProperty("id").GetInt32();
string orderNumber = doc.RootElement.GetProperty("orderNumber").GetString()!;

Console.WriteLine("--- Кількість замовлень до відмов ---");
int countBefore = await CountOrdersAsync();

// 2. UC-01 (Відмова, AC-02): Спроба створити дублікат активного замовлення на той самий серійний номер
Console.WriteLine("--- 2. UC-01 (AC-02): Відмова при дублюванні активного серійного номера ---");
await SendAsync(HttpMethod.Post, "/api/orders", new
{
    clientFullName = "Інший Клієнт",
    clientPhone = "+380670000000",
    deviceBrand = "Apple",
    deviceModel = "iPhone 15 Pro",
    serialNumber = serial,
    issueDescription = "Повторна заявка",
    engineerId = 2
});

// 3. UC-01 (Відмова, AC-07): Спроба відправити порожні поля клієнта
Console.WriteLine("--- 3. UC-01 (AC-07): Відмова при порожніх обов'язкових полях ---");
await SendAsync(HttpMethod.Post, "/api/orders", new
{
    clientFullName = "   ", 
    clientPhone = "+380501234567",
    deviceBrand = "Apple",
    deviceModel = "iPhone 15 Pro",
    serialNumber = $"{serial}-X",
    issueDescription = "Тест порожніх полів",
    engineerId = 1
});

// Перевірка незмінності стану після відмов
Console.WriteLine("--- Перевірка стану після відмов (кількість замовлень не змінилась) ---");
int countAfter = await CountOrdersAsync();
Console.WriteLine($"Замовлень до відмов: {countBefore}, після: {countAfter}\n");

// 4. UC-04 (Успіх, AC-05): Фільтрація замовлень за статусом та пошук за номером
Console.WriteLine($"--- 4. UC-04 (AC-05): Пошук замовлення за номером {orderNumber} та фільтр status=Accepted ---");
await SendAsync(HttpMethod.Get, $"/api/orders?status=Accepted&orderNumber={orderNumber}");


// 5. UC-02 (AC-04): Відмова при спробі перескочити статус Accepted -> Issued
Console.WriteLine("--- 5. UC-02 (AC-04): Відмова при спробі перескочити статус Accepted -> Issued ---");
await SendAsync(HttpMethod.Patch, $"/api/orders/{orderId}/status", new { newStatus = "Issued" });

// 6. UC-02: Перехід у статус InProgress та додавання позицій ремонту
Console.WriteLine("--- 6. UC-02: Перехід у статус InProgress та додавання позицій ремонту ---");
await SendAsync(HttpMethod.Patch, $"/api/orders/{orderId}/status", new { newStatus = "InProgress" });

await SendAsync(HttpMethod.Post, $"/api/orders/{orderId}/items", new
{
    diagnosticNotes = "Пошкоджено OLED-матрицю, шлейфи цілі",
    name = "Дисплейний модуль iPhone 15 Pro Original",
    quantity = 1,
    unitPrice = 8500
});

await SendAsync(HttpMethod.Post, $"/api/orders/{orderId}/items", new
{
    diagnosticNotes = "Пошкоджено OLED-матрицю, виконано заміну",
    name = "Послуга заміни дисплейного модуля",
    quantity = 1,
    unitPrice = 1200
});

// 7. UC-02 (AC-03): Відмова при спробі передати від'ємну ціну (-500 грн)
Console.WriteLine("--- 7. UC-02 (AC-03): Відмова при спробі передати від'ємну ціну (-500 грн) ---");
await SendAsync(HttpMethod.Post, $"/api/orders/{orderId}/items", new
{
    diagnosticNotes = (string?)null,
    name = "Некоректна позиція",
    quantity = 1,
    unitPrice = -500
});

// 8. UC-02 & UC-03: Перехід у Ready -> Видача замовлення (Issued)
Console.WriteLine("--- 8. UC-02 & UC-03: Перехід у Ready -> Видача замовлення (Issued) ---");
await SendAsync(HttpMethod.Patch, $"/api/orders/{orderId}/status", new { newStatus = "Ready" });
await SendAsync(HttpMethod.Patch, $"/api/orders/{orderId}/status", new { newStatus = "Issued" });

// 9. UC-02 (AC-04): Відмова при спробі змінити закрите замовлення (Issued)
Console.WriteLine("--- 9. UC-02 (AC-04): Відмова при спробі змінити закрите замовлення (Issued) ---");
await SendAsync(HttpMethod.Post, $"/api/orders/{orderId}/items", new
{
    diagnosticNotes = (string?)null,
    name = "Спроба редагування після видачі",
    quantity = 1,
    unitPrice = 300
});

// 10. UC-04 (AC-05): Пошук замовлення за номером та фільтр status=Issued
Console.WriteLine($"--- 10. UC-04 (AC-05): Пошук замовлення за номером {orderNumber} та фільтр status=Issued ---");
await SendAsync(HttpMethod.Get, $"/api/orders?status=Issued&orderNumber={orderNumber}");

// 11. Спринт S1: повторне звернення з тим самим пристроєм після видачі
Console.WriteLine("--- 11. S1: Повторне звернення з тим самим пристроєм після видачі ---");
await SendAsync(HttpMethod.Post, "/api/orders", new
{
    clientFullName = "Петренко Іван Васильович",
    clientPhone = "+380501234567",
    deviceBrand = "Apple",
    deviceModel = "iPhone 15 Pro",
    serialNumber = serial,
    issueDescription = "Не заряджається",
    engineerId = 1
});

// 12. Спринт S1 (критерій 1): історія ремонтів пристрою — два замовлення від найстарішого
Console.WriteLine($"--- 12. S1: Історія ремонтів пристрою {serial} ---");
await SendAsync(HttpMethod.Get, $"/api/devices/{serial}/orders");

// 13. Спринт S1 (критерій 2): відмова для пристрою, який не обслуговувався
Console.WriteLine("--- 13. S1: Відмова для невідомого серійного номера ---");
await SendAsync(HttpMethod.Get, "/api/devices/SN-UNKNOWN-0000/orders");

Console.WriteLine($"Для перевірки після перезапуску сервера: dotnet run --project src/ServiceCenter.Client -- history {serial}");