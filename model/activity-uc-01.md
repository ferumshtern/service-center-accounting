# Activity Diagram для UC-01 «Зареєструвати замовлення та призначити майстра»

**Виконавець:** Лавренюк Серафим (`@ferumshtern`)  
**Пов'язані вимоги:** FR-01, FR-02, NFR-02.

```mermaid
flowchart TD
    Start([Початок: приймання пристрою від клієнта]) --> InputClient[Ввести ПІБ і телефон клієнта]
    InputClient --> InputDevice[Ввести бренд, модель, серійний номер та опис несправності]
    InputDevice --> SelectTech[Обрати Сервісного інженера та надіслати форму]
    SelectTech --> ValidateFields{Усі обов'язкові поля заповнені?}
    ValidateFields -- Ні (A1) --> ShowFieldError[Вивести повідомлення про пропущені поля]
    ShowFieldError --> EndError([Завершення без збереження])
    ValidateFields -- Так --> CheckActiveOrder{Є незавершене замовлення для цього серійного номера?}
    CheckActiveOrder -- Так (A2) --> ShowDupError[Блокувати дублікат і показати номер активного замовлення]
    ShowDupError --> EndError
    CheckActiveOrder -- Ні --> CreateOrder[Створити замовлення зі статусом Прийнято і TotalCost = 0]
    CreateOrder --> LogHistory[Додати запис у StatusHistory та зберегти в БД]
    LogHistory --> ShowReceipt[Відобразити картку замовлення з номером квитанції]
    ShowReceipt --> EndSuccess([Успішне завершення])