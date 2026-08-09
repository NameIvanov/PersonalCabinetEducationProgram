# Личный кабинет руководителя образовательной программы

Веб-приложение для хранения, согласования и публикации элементов основных образовательных программ (ОПОП). Система поддерживает работу руководителей ОПОП, согласующих, модераторов и администраторов, ведёт версии файлов, комментарии, уведомления и аудит действий.

## Возможности

- ведение перечня ОПОП и закрепление за факультетами и кафедрами;
- назначение руководителей и согласующих;
- импорт структуры учебного плана из файла ММИС `.plx` с предварительным просмотром;
- загрузка одиночных файлов и комплектов в форматах PDF, DOC и DOCX;
- согласование по статусам «Загружено», «На согласовании», «Согласовано», «Требует доработки», «Опубликовано на сайте»;
- блокировка зафиксированных, согласованных и опубликованных комплектов;
- замена и удаление отдельных файлов незавершённого комплекта с сохранением истории;
- комментарии, уведомления, архив ОПОП и элементов;
- поиск, сортировка и пагинация списков;
- журнал административных действий.

## Технологии

- ASP.NET Core MVC 8;
- Entity Framework Core 8;
- ASP.NET Core Identity;
- MySQL 8 и Pomelo Entity Framework Core;
- Bootstrap 5;
- xUnit.

## Запуск проекта

### Требования

- .NET SDK 8.0;
- MySQL Server 8.0;
- Visual Studio 2022, Rider либо терминал с `dotnet`.

### 1. Настройка базы данных

Создайте пустую базу MySQL и задайте строку подключения. Рекомендуемый вариант для локальной разработки:

```powershell
dotnet user-secrets init --project PersonalCabinetEducationProgram\PersonalCabinetEducationProgram.csproj
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=personal_cabinet;Uid=root;Pwd=YOUR_PASSWORD;" --project PersonalCabinetEducationProgram\PersonalCabinetEducationProgram.csproj
```

Также строку можно передать переменной окружения `ConnectionStrings__DefaultConnection` или изменить локальный `appsettings.json`.

### 2. Восстановление зависимостей

```powershell
dotnet restore PersonalCabinetEducationProgram.sln
```

### 3. Запуск

```powershell
dotnet run --project PersonalCabinetEducationProgram\PersonalCabinetEducationProgram.csproj
```

Адрес приложения выводится в терминале. При первом запуске миграции Entity Framework применяются автоматически. Если адрес уже занят, остановите предыдущий процесс приложения либо задайте другой URL:

```powershell
dotnet run --project PersonalCabinetEducationProgram\PersonalCabinetEducationProgram.csproj --urls "http://localhost:7147"
```

### Запуск из Visual Studio

1. Откройте `PersonalCabinetEducationProgram.sln`.
2. Выберите проект `PersonalCabinetEducationProgram` как запускаемый.
3. Остановите ранее запущенный экземпляр приложения, если DLL или порт заняты.
4. Запустите проект клавишей `F5` или `Ctrl+F5`.

## Тесты

```powershell
dotnet test PersonalCabinetEducationProgram.sln
```

## Импорт PLX

Импорт доступен руководителю закреплённой ОПОП и администратору. Сначала файл проверяется и показывается предварительный состав дисциплин, модулей, практик, курсовых работ и ГИА. Если шифр или уровень образования в PLX отличается от выбранной ОПОП, система требует отдельного подтверждения. Применение импорта выполняется транзакционно, а исходный PLX и результат операции сохраняются в истории.

## Хранение файлов

Путь задаётся секцией `FileStorageSettings`. Пользовательские документы и архивы PLX не должны добавляться в Git. На один файл действует ограничение 50 МБ, на PLX — 20 МБ, в одном текущем комплекте допускается до 20 файлов.

## Структура решения

- `PersonalCabinetEducationProgram/Controllers` — MVC-контроллеры;
- `PersonalCabinetEducationProgram/Models` — сущности и статусы;
- `PersonalCabinetEducationProgram/Services` — бизнес-логика, хранение файлов, импорт и аудит;
- `PersonalCabinetEducationProgram/Views` — Razor-представления;
- `PersonalCabinetEducationProgram/Migrations` — миграции MySQL;
- `PersonalCabinetEducationProgram.Tests` — модульные и интеграционные тесты.
