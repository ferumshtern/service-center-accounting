# Activity Diagram для UC-02 «Зафіксувати результати ремонту та розрахувати вартість»

**Виконавець:** Головань Євгенія (`@yvhniiahh`)  
**Пов'язані вимоги:** FR-03, FR-04, NFR-02.

```mermaid
flowchart TD
    Start([Початок: інженер відкриває картку замовлення]) --> CheckIssued{Статус замовлення = Видано?}
    CheckIssued -- Так (A1) --> ErrClosed[Заборонити редагування закритого замовлення]
    ErrClosed --> EndFail([Завершення з помилкою])
    CheckIssued -- Ні --> SetInProgress[Встановити статус В роботі та записати в історію]
    SetInProgress --> EnterDiag[Внести результат діагностики та позиції робіт/деталей]
    EnterDiag --> ValidateItems{Кількість > 0 та Ціна >= 0?}
    ValidateItems -- Ні (A2) --> ErrInvalidPrice[Вивести помилку валідації кількості або ціни]
    ErrInvalidPrice --> EndFail
    ValidateItems -- Так --> CalcTotal[Автоматично перерахувати підсумкову суму TotalCost]
    CalcTotal --> IsFinished{Ремонт повністю завершено?}
    IsFinished -- Ні: очікуються деталі (A3) --> SaveInProgress[Зберегти поточні зміни у статусі В роботі]
    SaveInProgress --> EndSuccess([Успішне завершення])
    IsFinished -- Так --> SetReady[Змінити статус на Готово та записати в StatusHistory]
    SetReady --> SaveOrder[Зберегти замовлення в БД]
    SaveOrder --> EndSuccess
