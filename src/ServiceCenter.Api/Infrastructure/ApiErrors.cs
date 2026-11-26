using ServiceCenter.Api.Contracts;

namespace ServiceCenter.Api.Infrastructure;

public static class ApiErrors
{
    public static (int Status, ApiError Error) Create(string code, string? detail = null)
    {
        var (status, message) = code switch
        {
            "invalid_request" => (400, "Не заповнені обов'язкові поля клієнта або пристрою."),
            "invalid_item_values" => (400, "Некоректні дані позиції: назва обов'язкова, кількість має бути > 0, а ціна >= 0."),
            "invalid_warranty_request" => (400, "Опис несправності для гарантійного звернення обов'язковий."),
            "invalid_status_value" => (400, "Невідомий статус замовлення. Допустимі: Accepted, InProgress, Ready, Issued."),
            "engineer_not_found" => (404, "Вказаного сервісного інженера не знайдено."),
            "order_not_found" => (404, "Замовлення з таким ідентифікатором не знайдено."),
            "device_not_found" => (404, "Пристрій із таким серійним номером ще не обслуговувався."),
            "duplicate_active_order" => (409, "Для пристрою з цим серійним номером вже існує активне (незавершене) замовлення."),
            "order_already_issued" => (409, "Замовлення вже видано клієнту (Issued). Редагування заблоковано."),
            "invalid_status_warning" => (409, "Додавати роботи можна лише у статусі 'В роботі' (InProgress)."),
            "invalid_status_transition" => (409, "Порушено послідовність життєвого циклу статусів замовлення."),
            "order_not_issued" => (409, "Гарантійне звернення можливе лише для виданого замовлення (Issued)."),
            "warranty_claim_exists" => (409, "За цим замовленням уже оформлено гарантійне звернення."),
            "warranty_expired" => (409, "Гарантійний термін (90 днів від видачі) минув."),
            "save_failed" => (500, "Не вдалося зберегти гарантійне звернення; жодних змін не внесено."),
            _ => (400, "Некоректний запит.")
        };

        if (!string.IsNullOrWhiteSpace(detail))
            message = $"{message} {detail}";

        return (status, new ApiError(code, message));
    }

    public static IResult Result(string code, string? detail = null)
    {
        var (status, error) = Create(code, detail);
        return Results.Json(error, statusCode: status);
    }
}