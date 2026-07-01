## Запуск проекта

### Требования

Перед запуском необходимо установить:

- .NET SDK 8.0 или новее;
- MySQL Server 8.0 или совместимую версию;
- Visual Studio 2022, Rider или другой редактор с поддержкой .NET.

### Настройка базы данных

В файле `PersonalCabinetEducationProgram/appsettings.json` находится строка подключения:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=personal_cabinet;Uid=root;Pwd=your_password;"
}
