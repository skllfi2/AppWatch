# REVIEW.md — что важно в коде AppWatch

Ревьюер (агент) читает этот файл + дифф. Здесь: что считается ошибкой, что не ревьюим,
какие пути всегда требуют человека. Коротко и по делу.

## Что такое «сломано» для нас
- Упал любой тест из базового набора, сборка, или живая UIA-проверка
- Движок тронул файлы юзера без SHA-256 верификации или без health-check
- Тест, зависящий от сети/реестра/реальных путей вне %TEMP% — все тесты hermetic
- Engine Detail-строка, которую нельзя показать юзеру (сырой exception вместо причины)

## Обязательные правила кода
- x:Bind на свойства, меняющиеся после InitializeComponent → `Mode=OneWay`
  (OneTime молча покажет пустоту — уже дважды был живым багом)
- Новое observable-состояние TrackedAppItem → уведомление в Refresh()/On*Changed,
  иначе карточка не перерисуется
- Новые поля TrackedApp → persist в config + AddEditAppDialog + локализация ru/en
- `dotnet test` НЕ использовать (MTP сломан Test.Sdk 18.11): сборка тестов
  → запуск `AppWatch.Core.Tests.exe` напрямую
- Сборки без `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (ломает PRI на чистой сборке)

## Risk-тиры — метка в коммит-месседже (`Risk:` + `Revert:`)
- **High** — всегда смотрит человек: SelfUpdateService/Velopack, `.github/workflows`,
  UpdateEngine (replace/kill/health-check), VersionStore.RollbackTo
- **Medium** — ревьюер-агент + беглый взгляд: XAML-страницы, MainViewModel флоу, конвертеры
- **Low** — только автоматика: catalog.json, локализация, docs, чистые тесты

## Convergence-правила ревью
- Максимум 3 раунда правок; дальше — stop и пересмотр подхода, а не новые правки
- Повторный finding не постится повторно — фиксируй класс бага (все вхождения разом)
- После первого прохода re-review только нового диффа
- Каждый finding с файлом:строкой, не «где-то здесь»

## Инцидент → тест
- Живой баг закрыт ТОЛЬКО с регрессионным тестом. Прецеденты: Mode=OneWay,
  VCRTForwarders-CodeTaskFactory, APPX1685, dotnet-test-MTP. Баг без теста — не закрыт.

## Не ревьюим
- bin/obj, локализационные json (только автоматическая сверка ключей ru↔en), NOTICE
