using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5080";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

Console.WriteLine($"=== Клієнт ServiceCenter v1.0 (Сервер: {baseUrl}) ===\n");

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

// 1. UC-01 (Успіх, AC-01): Реєстрація нового замовлення
Console.WriteLine("--- 1. UC-01 (AC-01): Успішна реєстрація замовлення ---");
var (status1, body1) = await SendAsync(HttpMethod.Post, "/api/orders", new
{
    clientFullName = "Петренко Іван Васильович",
    clientPhone = "+380501234567",
    deviceBrand = "Apple",
    deviceModel = "iPhone 15 Pro",
    serialNumber = "SN-IPH15-9901",
    issueDescription = "Розбитий дисплей, не працює сенсор",
    engineerId = 1
});

if (status1 != 201) return;
using var doc = JsonDocument.Parse(body1);
int orderId = doc.RootElement.GetProperty("id").GetInt32();

// 2. UC-01 (Відмова, AC-02): Спроба створити дублікат активного замовлення на той самий серійний номер
Console.WriteLine("--- 2. UC-01 (AC-02): Відмова при дублюванні активного серійного номера ---");
await SendAsync(HttpMethod.Post, "/api/orders", new
{
    clientFullName = "Інший Клієнт",
    clientPhone = "+380670000000",
    deviceBrand = "Apple",
    deviceModel = "iPhone 15 Pro",
    serialNumber = "SN-IPH15-9901",
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
    serialNumber = "SN-NEW-0002",
    issueDescription = "Тест порожніх полів",
    engineerId = 1
});

// Перевірка незмінності стану після відмов
Console.WriteLine("--- Перевірка стану після відмов (має бути 1 замовлення) ---");
await SendAsync(HttpMethod.Get, "/api/orders");

// 4. UC-04 (Успіх, AC-05): Фільтрація замовлень за статусом та пошук за номером SC-0001
Console.WriteLine("--- 4. UC-04 (AC-05): Пошук замовлення за номером SC-0001 та фільтр status=Accepted ---");
await SendAsync(HttpMethod.Get, "/api/orders?status=Accepted&orderNumber=SC-0001");
