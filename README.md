# ServiceCenter Accounting — Система обліку замовлень сервісного центру (v1.0.0)

Програмний продукт для автоматизації приймання пристроїв у ремонт, призначення сервісних інженерів, обліку виконаних робіт і запчастин, контролю життєвого циклу статусів та формування електронної квитанції.

## Виконавці (Група ТР-42, Команда 1)

* **Лавренюк Серафим** — налаштування базової архітектури, реєстрація замовлень, призначення інженерів, перевірка дублікатів (`UC-01`), пошук і фільтрація (`UC-04`).

* **Головань Євгенія** — фіксація діагностики, додавання робіт/деталей з автопідрахунком вартості (`UC-02`), контроль переходів станів та видача замовлення (`UC-03`).

## Реалізовані можливості випуску v1.0.0

* **`UC-01` (`POST /api/orders`):** реєстрація клієнта, пристрою та заявки на ремонт із призначенням інженера та блокуванням дублікатів активних серійних номерів (`409 duplicate_active_order`).

* **`UC-02` (`POST /api/orders/{id}/items`, `PATCH /api/orders/{id}/status`):** внесення результатів діагностики, додавання робіт і запчастин із автоматичним перерахунком `TotalCost` та валідацією ціни й кількості (`400 invalid_item_values`).

* **`UC-03` (`PATCH /api/orders/{id}/status`):** послідовне переведення замовлення між станами `Accepted -> InProgress -> Ready -> Issued` (і повернення `Ready -> InProgress`, якщо виявлено додаткову несправність) із фіксацією часу в `StatusHistory` та блокуванням редагування після видачі (`409 order_already_issued`).

* **`UC-04` (`GET /api/orders`):** пошук замовлень за номером квитанції та фільтрація за статусом; перегляд повної електронної квитанції через браузерний інтерфейс (Bootstrap) або консольний клієнт.

## Важливе обмеження версії v1.0.0 (In-Memory Storage)

У версії **v1.0.0** усі робочі дані (клієнти, пристрої, замовлення, позиції ремонту та історія статусів) зберігаються в оперативній пам'яті серверного процесу (`ServiceStore`: колекції `List<T>`, доступ до яких узгоджується спільним блокуванням `lock`). Дані залишаються доступними між запитами протягом роботи сервера, проте **після зупинки або перезапуску серверного процесу робочі записи очищуються**. Це відповідає цілям першого навчального релізу згідно з ЛР №4.

## Вимоги до середовища

* Установлений **.NET 9.0 SDK / Runtime** (`net9.0`).

## Інструкція зі складання, тестування та запуску з вихідного коду

1. **Складання рішення та запуск автоматичних тестів (`xUnit`):**

   ```
   dotnet build ServiceCenter.sln
   dotnet test ServiceCenter.sln
   
   ```

2. **Запуск сервера (`ServiceCenter.Api`):**

   ```
   dotnet run --project src/ServiceCenter.Api --urls http://localhost:5080
   
   ```

   * Веб-інтерфейс користувача (Bootstrap) доступний у браузері за адресою: `http://localhost:5080`

   * Перевірка стану сервера: `http://localhost:5080/health`

3. **Запуск консольного клієнта (`ServiceCenter.Client`) у другому терміналі:**

   ```
   dotnet run --project src/ServiceCenter.Client
   
   ```

   Клієнт автоматично виконує контрольний наскрізний сценарій (кроки 1–10: `AC-01`–`AC-05`, `AC-07`) із перевіркою успішних операцій, випадків відмови та стану даних після відмови.

   Адреса сервера за замовчуванням — `http://localhost:5080`; іншу можна задати змінною середовища `API_BASE_URL` (bash: `API_BASE_URL=http://localhost:5081 dotnet run --project src/ServiceCenter.Client`; PowerShell: `$env:API_BASE_URL="http://localhost:5081"; dotnet run --project src/ServiceCenter.Client`).

## Складання релізного пакета (`ServiceCenter-v1.0.0.zip`)

Пакет збирається з коміту, позначеного міткою `v1.0.0`, після успішних тестів:

```
git switch --detach v1.0.0
dotnet test ServiceCenter.sln -c Release
dotnet publish src/ServiceCenter.Api/ServiceCenter.Api.csproj -c Release -o artifacts/ServiceCenter-v1.0.0/server
dotnet publish src/ServiceCenter.Client/ServiceCenter.Client.csproj -c Release -o artifacts/ServiceCenter-v1.0.0/client
cp README.md docs/release-notes-v1.0.0.md artifacts/ServiceCenter-v1.0.0/   # у PowerShell: Copy-Item
```

Каталог `artifacts/ServiceCenter-v1.0.0` архівується в `ServiceCenter-v1.0.0.zip` і додається до випуску GitHub **v1.0.0** разом із [примітками до випуску](docs/release-notes-v1.0.0.md).

## Запуск із готового релізного пакета (`ServiceCenter-v1.0.0.zip`)

Потрібен **.NET 9.0 Runtime** (ASP.NET Core Runtime 9.0 для сервера).

1. Розпакуйте архів `ServiceCenter-v1.0.0.zip` у довільну папку.

2. У першому терміналі перейдіть у каталог `server` і запустіть сервер. Запускати потрібно саме з цього каталогу: сервер шукає `wwwroot` (вебінтерфейс) і `appsettings.json` у поточному каталозі.

   ```
   cd server
   dotnet ServiceCenter.Api.dll --urls http://localhost:5080
   ```

3. У другому терміналі запустіть консольний клієнт (або відкрийте `http://localhost:5080` у браузері):

   ```
   dotnet ./client/ServiceCenter.Client.dll
   ```

   Якщо сервер працює на іншій адресі, задайте її змінною `API_BASE_URL`.

4. Перевірка обмеження v1.0: зупиніть сервер (`Ctrl+C`), запустіть його знову й відкрийте `http://localhost:5080/api/orders` — список замовлень порожній (`[]`).
