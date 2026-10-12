# Личный кабинет руководителя образовательной программы

Веб-приложение для хранения, согласования и публикации элементов основных образовательных программ (ОПОП). Система поддерживает работу руководителей ОПОП, согласующих, модераторов и администраторов, ведёт версии файлов, комментарии, уведомления и аудит действий.

## Возможности

- ведение перечня ОПОП и закрепление за факультетами и кафедрами;
- распределение ОПОП по годам с 2020 года: в левой панели программы находятся в папках «год → уровень образования»; год задаётся при создании и редактировании карточки;
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

Адрес приложения выводится в терминале. При первом запуске миграции Entity Framework применяются автоматически. Миграция `GroupProgramsByYear` разово относит все существующие ОПОП, включая архивные, к 2026 году; последующие изменения года сохраняются. Если адрес уже занят, остановите предыдущий процесс приложения либо задайте другой URL:

```powershell
dotnet run --project PersonalCabinetEducationProgram\PersonalCabinetEducationProgram.csproj --urls "http://localhost:7147"
```

### Запуск из Visual Studio

1. Откройте `PersonalCabinetEducationProgram.sln`.
2. Выберите проект `PersonalCabinetEducationProgram` как запускаемый.
3. Остановите ранее запущенный экземпляр приложения, если DLL или порт заняты.
4. Запустите проект клавишей `F5` или `Ctrl+F5`.

## Запуск на сервере через Docker

В корне проекта добавлены `Dockerfile`, `compose.yaml` и `.env.example`. Нужен Linux-сервер с Docker Engine и Docker Compose v2. .NET и MySQL на хосте устанавливать не требуется. Образ приложения собирается в несколько этапов и запускается от пользователя `app`. Compose запускает MySQL 8.0 и ждёт успешного запроса к базе перед запуском приложения ([порядок запуска Compose](https://docs.docker.com/compose/how-tos/startup-order/)).

### Настройка и запуск

В каталоге проекта на сервере:

```bash
cp .env.example .env
nano .env
docker compose up -d --build
docker compose ps
docker compose logs --tail=100 web
```

В `.env` обязательно задайте разные случайные `MYSQL_PASSWORD` и `MYSQL_ROOT_PASSWORD` длиной от 24 символов. Используйте латинские буквы и цифры, без кавычек и `$`. Пустые значения блокируют запуск. `.env` исключён из Git и контекста сборки. Ограничьте доступ к нему: `chmod 600 .env`.

Приложение доступно на сервере по `http://127.0.0.1:8080`; порт можно изменить через `APP_PORT`. Для проверки со своего компьютера используйте SSH-туннель `ssh -L 8080:127.0.0.1:8080 user@server` и откройте `http://localhost:8080`. Для временного доступа по IP можно задать `APP_BIND_ADDRESS=0.0.0.0` и пересоздать контейнер; рабочий сайт публикуйте через HTTPS-прокси.

При первом запуске создаётся отдельная база `personal_cabinet`, автоматически применяются миграции и начальные данные проекта. Существующие данные локальной базы и файлы из `D:\2026` автоматически не переносятся: для переноса нужны SQL-дамп и копия хранилища документов. Перед публичным запуском измените пароли начальных учётных записей.

### Домен и HTTPS

Направьте DNS домена на сервер. На хосте настройте Nginx с сертификатом для домена и проксированием на `127.0.0.1:8080`. Пример блоков `server` после получения сертификата (замените `cabinet.example.com` и пути к сертификатам):

```nginx
server {
    listen 80;
    server_name cabinet.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name cabinet.example.com;
    ssl_certificate /etc/letsencrypt/live/cabinet.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/cabinet.example.com/privkey.pem;
    client_max_body_size 201m;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 180s;
    }
}
```

В `.env` задайте `TRUSTED_PROXY_IP` — адрес Nginx, который виден контейнеру. При Nginx на Linux-хосте это обычно шлюз сети Compose; его можно узнать командой:

```bash
docker inspect "$(docker compose ps -q web)" --format '{{range .NetworkSettings.Networks}}{{.Gateway}}{{end}}'
```

После изменения выполните `docker compose up -d`. Проверьте вход через HTTPS и адрес клиента в журнале: доверенный прокси нужен для корректной работы HTTPS, журналов и блокировок по IP. Для Nginx в отдельном контейнере укажите его адрес в общей сети вместо шлюза.

### Данные, обновление и остановка

Именованные тома сохраняют MySQL (`mysql_data`), документы и архивы PLX (`uploads`), ключи авторизации (`data_protection_keys`). Путь хранилища в контейнере — `/app/storage`; Windows-путь из локальных настроек переопределяется. Ключи в `/app/keys` сохраняют действительность cookie после пересоздания контейнера. Томам документов и ключей при первом создании назначаются права пользователя приложения.

```bash
# Обновление после получения новой версии исходников
docker compose up -d --build

# Журналы
docker compose logs -f --tail=100 web db

# Остановка с сохранением данных
docker compose down
```

Перед обновлением делайте резервную копию базы и томов документов/ключей. Команда `docker compose down -v` удаляет все эти данные. Пароли MySQL в `.env` используются при первоначальном создании базы; для существующего тома смена пароля требует изменения пользователя в самой MySQL. Порт базы данных на хост не публикуется.

Облачные ИИ-ключи можно передать через `AI_API_KEY` и `DOCUMENT_ANALYSIS_API_KEY`. Для смысловой проверки PDF дополнительно настройте `DocumentAnalysis__SemanticAnalysisEnabled` и `DocumentAnalysis__Model` в `environment` сервиса `web`, как описано в [руководстве модуля](docs/document-analysis.md). OCR в базовом образе не установлен; PDF с текстовым слоем обрабатываются, для сканов требуется отдельно добавить OCRmyPDF с языками `rus+eng` и настроить `DocumentAnalysis__OcrExecutable`.

## Тесты

```powershell
dotnet test PersonalCabinetEducationProgram.sln
```

## Импорт PLX

Импорт доступен руководителю закреплённой ОПОП и администратору. Сначала файл проверяется и показывается предварительный состав дисциплин, модулей, практик, курсовых работ и ГИА. Если шифр или уровень образования в PLX отличается от выбранной ОПОП, система требует отдельного подтверждения. Применение импорта выполняется транзакционно, а исходный PLX и результат операции сохраняются в истории.

## Помощник проверки PDF

Для руководителя добавлена проверка одного выбранного PDF перед согласованием: техническая диагностика, формальные и смысловые замечания с цитатами, сравнение с выбранной предыдущей версией, история и обратная связь. Настройки облачной модели, локального OCR и шаблонов организации описаны в [руководстве модуля](docs/document-analysis.md). Административный ИИ работает независимо.

## Хранение файлов

Путь задаётся секцией `FileStorageSettings`. Пользовательские документы и архивы PLX не должны добавляться в Git. На один файл действует ограничение 50 МБ, на PLX — 20 МБ, в одном текущем комплекте допускается до 20 файлов.

## Структура решения

- `PersonalCabinetEducationProgram/Controllers` — MVC-контроллеры;
- `PersonalCabinetEducationProgram/Models` — сущности и статусы;
- `PersonalCabinetEducationProgram/Services` — бизнес-логика, хранение файлов, импорт и аудит;
- `PersonalCabinetEducationProgram/Views` — Razor-представления;
- `PersonalCabinetEducationProgram/Migrations` — миграции MySQL;
- `PersonalCabinetEducationProgram.Tests` — модульные и интеграционные тесты.
