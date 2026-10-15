# AppWatch — DESIGN.md

> Временное техническое имя: **AppWatch** — заменяется позже одним рефакторингом неймспейсов.
> Пользовательское решение о финальном имени — отложено.

## Цель

Windows-приложение, которое следит за новыми версиями отслеживаемых приложений и обновляет их
с минимальным ручным участием: winget-пакеты — молча по расписанию; GitHub-релизы (в том числе
портативные zip) — скачивание, проверка целостности, замена файлов, перезапуск процесса.

## Стек

- **C# / .NET 10 LTS**
- **WinUI 3** (WindowsAppSDK 2.5.1) — единый стек с ZuiV2; Mica через `SystemBackdrop`,
  компилируемые `x:Bind`-биндинги, `DISABLE_XAML_GENERATED_MAIN` + свой `Program.cs`
- **Microsoft.Windows.SDK.BuildTools.WinApp** — F5 / `dotnet run` для packaged WinUI-приложений:
  регистрация debug-identity через winapp CLI. Без него F5 в VS не запускает (проверено);
  `Platforms x86;x64;ARM64` + RuntimeIdentifiers — как у ZuiV2
- **CommunityToolkit.Mvvm** — MVVM через source generators (partial properties для WinRT/AOT-совместимости)
- **Velopack** (MIT) — самообновление watcher'а: sidecar, дельта-обновления, GitHub Releases source
- **Serilog** — логирование (rolling files, краш-дампы)
- **xUnit v3** (xunit.v3 4.0.0, единый стек с ZuiV2) + **Microsoft.Testing.Platform**:
  `dotnet test` работает в MTP-режиме через `global.json` (`"test": { "runner":
  "Microsoft.Testing.Platform" }`) — на .NET 10 SDK VSTest-режим для MTP-проектов отклоняется;
  тестовый проект — исполняемый (`OutputType Exe`)
- **CI:** GitHub Actions — build → test → `vpk pack` → публикация в Releases
- **Локализация:** JSON-паки вместо .resx — проще для правки и контрибуций; сервис-локатор +
  `Loc`-обёртка с INPC для x:Bind, переключение языка без рестарта

> Конвенция namespace (важно!): в App-проекте **без сегмента `.App`** — `AppWatch.Platform`,
> `AppWatch.Views`, `AppWatch.ViewModels`, `AppWatch.Localization`; класс `App` — в `namespace AppWatch`.
> Сегмент `AppWatch.App.*` конфликтует с классом `App` (CS0101) — проверено.

## Пакеты, добавленные заранее (спят до своего этапа)

| Пакет | Проект | Где и когда будет использован |
|---|---|---|
| **Microsoft.Extensions.Hosting** | App | Generic Host в `App.OnLaunched` вместо ручного `ServiceCollection` — когда появятся настройки в UI и фоновые сервисы: единый жизненный цикл DI + hot-reload конфига + `IHostedService` для фоновой проверки (заменит DispatcherQueueTimer) |
| **Microsoft.Extensions.Http** | App | `AddHttpClient` + `IHttpClientFactory` — когда добавим больше HTTP-клиентов (зеркала скачивания, импорт из аккаунта, разные источники). Защита от исчерпания сокетов; сейчас один singleton-клиент в `GitHubApiClient` |
| **Microsoft.VCRTForwarders.140** | App | Редирект VC++ runtime DLL — **на первом релизе**: упаковка через Velopack (`vpk pack`), чтобы приложение запускалось на машинах без установленного VC++ Redistributable |
| **SQLitePCLRaw.lib.e_sqlite3** | Infrastructure | Нативная SQLite — если история обновлений перерастёт JSON в БД (модель `UpdateHistoryEntry` уже есть; таблица + миграции в `Config\`). Пока храним всё в `config.json` |

## Архитектура (3 слоя, развязаны)

```
F:\Development\Dev\mydev\AppWatch\
├── AppWatch.sln
├── Directory.Build.props          # общие свойства: TargetFramework, Nullable, LangVersion
├── NOTICE                         # атрибуция Apache-2.0 кода komi-store
├── .github/workflows/ci.yml       # CI: build → test → vpk pack
├── src\
│   ├── AppWatch.Core\             # домен и логика решений — чистые функции без I/O
│   │   ├── Domain\
│   │   │   ├── Models\            # TrackedApp, ReleaseInfo, UpdateRule, UpdateDecision, UpdateHistory
│   │   │   └── AssetSelection\    # AssetSelector — порт из komi-store (см. NOTICE)
│   │   └── Abstractions\          # IGitHubClient, IUpdateEngine, IConfigStore, IProcessService,
│   │                              # IFileStore, IElevationService, IAppLauncher
│   ├── AppWatch.Infrastructure\   # реализации абстракций
│   │   ├── GitHub\                # REST-клиент Releases API; retry/timeout/resume;
│   │   │                          # rate-limit: опциональный PAT (60 → 5000 req/h); зеркала
│   │   ├── Config\                # JSON в %APPDATA%\AppWatch\config.json, schemaVersion + миграции
│   │   ├── AutostartService.cs    # автозапуск: HKCU\Run
│   │   └── System\                # ProcessService (kill → wait), ElevationService,
│   │                              # FileStore (замена файлов + rollback), AppLauncher
│   └── AppWatch.App\              # WinUI 3 (единый стек с ZuiV2)
│       ├── Program.cs             # single instance + Velopack + ComWrappers + crash-лог
│       ├── app.manifest           # PerMonitorV2 DPI, longPathAware
│       ├── App.xaml(.cs)          # XamlControlsResources, DI, Mica (SystemBackdrop)
│       ├── Localization\          # JSON-паки: en.json + ru.json (EmbeddedResource);
│       │                          # LocalizationService (синглтон, смена языка без рестарта)
│       │                          # + Loc-обёртка с INPC для x:Bind
│       ├── Platform\              # SingleInstance (Mutex + named pipe), TrayService
│       │                          # (Shell_NotifyIconW, тёмные меню, балуны), IconGenerator,
│       │                          # NativeMethods
│       ├── Views\                 # MainWindow: Window + Mica, x:Bind
│       └── ViewModels\            # CommunityToolkit.Mvvm
└── tests\
    └── AppWatch.Core.Tests\       # unit-тесты ядра: версии, ассеты, правила
```

### AssetSelector (порт из komi-store, Apache-2.0)

Выбор правильного ассета из релиза — ранжирование по трём осям:

- **extension priority** — `.msi` → `.exe` → прочее (для Windows); приоритет задаётся на приложение
- **flavor rank** — stable (3) / prerelease: alpha, beta, rc, preview (2) / unstable: nightly, canary, snapshot (1) / debug (0)
- **arch rank** — exact match (3) / universal (2) / fallback-совместимый: x86_64→x86, aarch64→arm (1) / incompatible (0)

Порт в C# с сохранением логики и атрибуции (NOTICE).

## Заимствовано из ZuiV2 (свой код, без лицензионных ограничений)

- **Паттерн PS-скрипта замены файлов** (`SelfUpdateService`): detached-скрипт переживает процесс —
  ждёт выхода PID, заменяет файлы с сохранением data-папок, перезапускает. Закрывает самообновление
  watcher'а портативным zip (для чужих приложений — движок kill → replace → rollback в UpdateEngine)
- **Rate-limit трекинг + кэш** в `GitHubApiClient`: 429/403 с `X-RateLimit-Remaining: 0`,
  парсинг `X-RateLimit-Reset`, пропуск запросов до сброса; кэш ответов 15 мин
- **`ElevationService`** — проверка `TOKEN_ELEVATION_TYPE` (отличает filtered token от полного админа),
  ShellExecuteW "runas"
- **`SingleInstance`** — Mutex + named pipe: второй экземпляр шлёт "SHOW" → окно активируется

## Фундамент (заложено с первого коммита)

1. **Логирование** — Serilog, rolling-файлы в `%APPDATA%\AppWatch\logs`, краш-дампы
2. **Безопасность обновлений** — SHA-256/digest из GitHub API + проверка подписи инсталлятора перед запуском; автоматический запуск инсталлятора только после проверок
3. **Модель элевации** — один привилегированный task в Task Scheduler; приложение общается с ним через IPC-очередь заданий; без UAC-спама на каждое обновление
4. **Конфиг с версионируемой схемой** — JSON, поле `schemaVersion`, миграции с первого дня
5. **Движок «процесс → файлы → откат»** — kill процесса → замена файлов → перезапуск; rollback: старая версия удаляется только после успешного запуска новой
6. **Сетевая отказоустойчивость** — retry/timeout/resume + зеркала скачивания (сеть может блокировать GitHub)
7. **GitHub rate limits** — опциональный PAT в настройках
8. **CI/CD** — GitHub Actions с `vpk pack` с первой версии → self-update работает сразу
9. **NOTICE** — атрибуция Apache-2.0 кода komi-store (юридическое требование)
10. **Single instance** (mutex) + автозапуск с Windows + поведение трея
11. **Тестируемость** — движок решений: чистые функции без I/O, unit-тесты с первого дня

## Отслеживаемые приложения (первичный список, меняется в конфиге/UI)

| Приложение | Источник | Тип обновления |
|---|---|---|
| zapret-discord-youtube (Flowseal) | GitHub Releases | portable zip: kill → replace → restart |
| zapret2-next (Dunterbabochka) | GitHub Releases | portable zip: kill → replace → restart |
| VezarusPro | GitHub (Project-Veles) | инсталлятор |
| остальные | добавляются через UI / config.json | |

## Границы MVP (v0.1)

Входит:
- Слежение + обновление портативных GitHub-приложений (kill → replace → restart, rollback)
- **Установка из каталога из коробки** (6 приложений: zapret-discord-youtube, zapret2-next,
  optimizerDuck, GameOptimizer, PowerToys, GPU-Z): zip — в `%LOCALAPPDATA%\AppWatch\Apps`,
  exe-инсталляторы через UAC, winget install
- **Фоновая проверка по расписанию** (DispatcherQueueTimer, интервал из конфига — 6 ч,
  тихое авто-обновление портативных)
- **Трей + автозапуск**: Shell_NotifyIconW-иконка (генерируется кодом), тёмное контекстное меню
  (показать / проверить / автозапуск с чекмарком / выход), балуны; закрытие окна = сворачивание
  в трей; автозапуск через HKCU\Run, при автостарте — свёрнутым в трей
- Слежение + обновление winget-пакетов (`winget upgrade --all` — кнопкой)
- Главное окно (WinUI 3, Mica): вкладки «Приложения» / «Каталог», x:Bind
- Мультиязычность: en + ru (JSON-паки, переключение без рестарта)
- Самообновление через Velopack + PS-скрипт-паттерн (SelfUpdateService)

### Дизайн без токена (для не-GitHub пользователей)

- Публичные релизы читаются **анонимно**: 60 req/ч на IP — хватает при проверке раз в 6 часов
  (даже 20 приложений ≈ 3 req/ч); rate-limit трекинг мягко обрабатывает упор в лимит
- **Каталог из коробки** — статический JSON внутри приложения (`Catalog/catalog.json`),
  обновляется вместе с приложением через self-update: **сетевой инфраструктуры разработчика
  не требуется**
- Список отслеживаемых — **локально** в config.json, без сетевых данных пользователя
- Токен — **опция** для импорта из аккаунта (starred/own repos), не обязателен

Не входит (позже):
- Импорт из GitHub-аккаунта (starred → отслеживание) — клиент готов, фича в настройках
- Дисковери установленных приложений и сопоставление с GitHub-репозиториями
- Магазин / интеграция с komi-store как стором
- Драйверы вендоров (NVIDIA App, GIGABYTE — обновляются своими средствами)

## v0.2 — видимость и контроль (спланировано 2026-10-10, по итогам живого ревью UI)

Порядок работ: winget-экран (✅ готово, сверено с кодом 2026-10-10) → прогресс per-app →
своё репо в каталог → визуал ZuiV2 (детальный план переноса и статус — ниже в разделе).

### Входит в v0.2 (необходимое)

1. **Экран «Обновления winget»**:
   - `winget upgrade` (без `--all`) парсится построчно в список: Name, Id, текущая → доступная версия
   - Список карточками с чекбоксами (все выбраны по умолчанию) → кнопка «Обновить выбранное»
     (для каждого пакета `winget upgrade --id <Id> --exact`, вывод парсится построчно)
   - Прогресс per-package: winget пишет проценты скачивания в stdout — транслировать в
     индикатор на карточке; статусы: ожидание / скачивание N% / установка / готово / ошибка
   - Результат по каждому пакету сохранять (как в UpdateHistory)
2. **Прогресс per-app на вкладке «Приложения»**: этапы (проверка → скачивание N% → замена
   файлов → перезапуск) + цветная статус-точка (зелёная/жёлтая/красная). Протащить
   `IProgress<T>` из `UpdateEngine`/`GitHubApiClient` в `TrackedAppItem` через MainViewModel.
3. **«Добавить своё приложение» в Каталоге**: поле «ссылка на GitHub-репозиторий» →
   валидация → публичный API-запрос релизов → предпросмотр (найденные ассеты) →
   `AssetSelector` подбирает ассет → добавляется в TrackedApps. Работает без токена.
4. **Пояснение Каталога**: заголовок/подсказка — «Проверенная подборка с протестированными
   правилами обновления; любое другое приложение добавьте по ссылке» (Loc-ключи en+ru).
5. **Визуал из ZuiV2** (свой код, переносим): `Styles/DesignSystem.xaml` + `Themes/{Dark,Light}.xaml`
   + `Converters.xaml` — карточки (CornerRadius, CardBackground), Hero-баннер с итогом
   («доступно обновлений: N»), ProgressRing, InfoBar для ошибок, NavigationView вместо Pivot
   (готовит место странице настроек). По живому коду, без переписывания списков.

### Желательное в v0.2 (если влезет)

- Кнопка «Запустить» у каждого портативного отслеживаемого приложения (путь уже известен)
- Балуны из трея «X обновлён 1.2.3 → 1.3.0» (TrayService balloon API уже есть)
- Обновление по одному (кнопка на карточке, не только «Обновить все»)
- Pin версии в UI (модель UpdateRule в Core уже есть)

### Опциональное (после v0.2)

- Импорт starred из GitHub-аккаунта; страница истории обновлений; интервал фонового
  проверки в UI; иконки/монограммы приложений; зеркала скачивания из UI

---

## v0.2: ПЕРЕНОС ВИЗУАЛЬНОГО СТИЛЯ ИЗ ZuiV2 (спланировано 2026-10-10)

Источник: `F:\Development\Dev\ZuiV2` — **свой код, лицензионных ограничений нет.**
Переносим файлы как есть (это одинаковый WinUI 3 / .NET 10 стек), меняя только
namespace на `AppWatch.*`.

### Что переносим (по файлам, сверено с живым кодом ZuiV2)

| # | Файл ZuiV2 | → AppWatch.App | Содержимое (сверено) |
|---|---|---|---|
| 1 | `Styles/DesignSystem.xaml` (228 стр.) | `Styles/DesignSystem.xaml` | Semantic Colors (Success/Error/Warning/Info), Hero Colors, CardBorder/HeroCardBorder/ZapretHeroBorder/ProxyHeroBorder (CornerRadius 12, градиенты), SecondaryButtonStyle/IconButtonStyle/ToggleServiceButtonStyle, Typography (PageHeader 32/Subtitle/SectionHeader/CardTitle/CardSubtitle/CodeText), TabButton styles, implicit styles: InfoBar/NavigationView/NavigationViewItem/TextBox/ComboBox/ListView/ProgressBar/ToggleSwitch/ToggleButton/ProgressRing/Expander/ContentDialog/MenuFlyoutPresenter |
| 2 | `Themes/Dark.xaml` + `Themes/Light.xaml` (по 20 ключей) | `Themes/Dark.xaml` + `Themes/Light.xaml` | CardBackgroundBrush, CardSecondaryBrush, SidebarBackgroundBrush, BorderDefault/SubtleBrush, AccentBrush, ErrorBrush, SuccessBrush, Warning*Brush, TextPrimary/Secondary/TertiaryBrush, TextOnAccentBrush, HeroOverlayBrush, HeroSecondary/TertiaryBrush, SuccessSolidBrush, AccentButtonStyle |
| 3 | `Converters.cs` (ZuiV2 root, ~6 конвертеров) | `Converters/Converters.cs` | BoolToVisibility, InverseBool, StringToBool, NotNullToVisibility, CountToVisibility, StringToBrush |
| 4 | `Services/ThemeService.cs` + `App.xaml`-мержи Mica-ключей | `Platform/ThemeService.cs` + патч App.xaml | Динамическая смена темы (Dark/Light/System), мерж словарей Dark.xaml→DesignSystem.xaml в App.xaml, Mica-прозрачность: NavigationViewContentBackground, NavigationViewExpandedPaneBackground, NavigationViewEnabledBackground, LayerFill*Brush, ApplicationPageBackgroundThemeBrush → Transparent |
| 5 | `Platform/PageTransitionManager.cs` + SoundService.Click() | `Platform/PageTransitionManager.cs` | Slide-переходы FromRight/FromLeft для Frame (NavigationView ItemInvoked); SoundService в ZuiV2 нет — только PageTransitionManager |
| 6 | `UpdateDialogHelper.cs` (ZuiV2/Views/, changelog-диалог апдейта) | `Views/UpdateDialogHelper.cs` | ContentDialog с release-notes, кнопки «Обновить»/«Позже» — паттерн для диалога обновления AppWatch-обновления |

### Shell: Window → NavigationView + Frame (как ZuiV2)

Текущий MainWindow.xaml (159 стр., Pivot) переписывается:

1. `MicaBackdrop` через `Window.SystemBackdrop` (в ZuiV2 — напрямую в MainWindow.xaml).
2. `<TitleBar x:Name="AppTitleBar" Title=" "/>` — WinUI TitleBar-контрол, порт из ZuiV2 как есть.
3. `Frame x:Name="ContentFrame"` + `NavigationView` (OpenPaneLength 240, ItemInvoked →
   PageTransitionManager.NavigateTo). ZuiV2 хостит навигацию в DashboardPage через таб-кнопки
   (TabButtonStyle/TabButtonActiveStyle), NavigationView в его MainWindow нет (только
   TitleBar+Frame). **Решение для AppWatch: NavigationView слева (Приложения / Каталог / Winget /
   Настройки / О программе)** — DESIGN.md v0.2 прямо требует NavigationView вместо Pivot;
   implicit-стили NavigationView/NavigationViewItem уже в DesignSystem.xaml.
4. Страницы (по образцу ZuiV2 Views/*Page.xaml): `AppsPage`, `CatalogPage`, `WingetPage`,
   `SettingsPage`, `AboutPage` — каждая с `Padding="32,24"`, PageHeaderStyle (28–32 SemiBold),
   PageSubtitleStyle, карточки CardBorder.

### Содержимое страниц (визуал ZuiV2 + данные AppWatch)

- **AppsPage**: Hero-баннер «AppWatch» (ProxyHeroBorder-стиль, градиент #2D1B4E→#4A2D7A или
  новый AppWatchHeroBorder на основе HeroCardBorder) с метриками: «доступно обновлений: N»,
  «отслеживается: N», «обновлено сегодня: N». Ниже — ListView карточек TrackedAppItem:
  имя+иконка-монограмма, версия, статусная точка (Ellipse, RunningBrush/ErrorBrush/WarningBrush),
  прогресс-бар этапа, кнопки «Обновить»/«Запустить». x:Bind OneWay на StatusText → расширяется
  StatusDot+StageText (v0.2 п.2).
- **CatalogPage**: hint-текст (уже есть Loc-ключи), карточки CatalogItem: описание, InstallStatus,
  кнопка установки (AccentButtonStyle), …
- **WingetPage**: сохраняем текущую логику чекбоксов/прогресса per-package (готово), обёртку в
  карточный стиль.
- **SettingsPage** (новая, по образцу ZuiV2 SettingsPage.xaml): Expander-секции «Общие» /
  «Поведение» / «GitHub»: язык (ComboBox en/ru), тема (ComboBox Dark/Light/System через
  ThemeService), автозапуск+свёрнутый старт (ToggleSwitch), минимизация в трей, автопроверка
  ToggleSwitch + интервал ComboBox (1/3/6/12/24 ч), PAT-поле (PasswordBox + статус валидации),
  кнопка «Проверить обновления AppWatch» (Velopack) + UpdateDialogHelper-диалог.
- **AboutPage**: логотип/имя/версия AppWatch, Velopack-версия, ссылка на GitHub-репо,
  атрибуции (komi-store Apache-2.0, UniGetUI MIT).

### Чанки работы (петля Hermes → MiMo → build → рантайм-показ)

- **ЧАНК 1 — каркас стиля**: перенести DesignSystem.xaml, Dark/Light.xaml, Converters.cs,
  ThemeService.cs, патч App.xaml (мержи + Mica-ключи). MainWindow.xaml → NavigationView+Frame+TitleBar.
  Ожидаемый результат: окно в стиле ZuiV2, навигация пустыми страницами, тема переключается.
- **ЧАНК 2 — AppsPage**: hero-баннер + карточки отслеживаемых с статусной точкой + прогрессом
  этапа + кнопками. IProgress<T> из UpdateEngine/GitHubApiClient → TrackedAppItem.Stage/
  ProgressValue (v0.2 п.2).
- **ЧАНК 3 — CatalogPage + WingetPage**: карточный стиль, существующая логика не трогается,
  только XAML-обёртка. «Добавить своё приложение» (v0.2 п.3) — ContentDialog: ссылка на репо →
  валидация → предпросмотр ассетов → AssetSelector → TrackedApps.
- **ЧАНК 4 — SettingsPage + AboutPage**: тема/язык/интервал/автозапуск/трей/PAT +
  «Проверить обновления AppWatch» с UpdateDialogHelper. + полировка (пустые состояния, балуны
  «X обновлён 1.2.3 → 1.3.0» — API TrayService уже есть).

### Грабли переноса (из цикла разработки ZuiV2)

- **Converters инлайном в App.xaml** (не через Converters.xaml ResourceDictionary) — ZuiV2
  комментарий: «inline to avoid XAML resource loading issues in packaged mode» — переносим этот
  паттерн.
- **Словари мержатся в строгом порядке**: Themes/Dark.xaml → DesignSystem.xaml (в Dark.xaml
  определён даже переопределённый AccentButtonStyle!). Если порядок нарушить — StaticResource
  lookup падает при XAML-компиляции.
- **Mica**: Root-грид с Background="Transparent" + переопределённые Layer/NavigationView
  фоновые ключи — без них Mica не «пробивается» сквозь NavigationView.
- **Локализация**: для x:Bind все ключи должны существовать как typed-свойства на Loc (или
  Loc.Instance["key"] из code-behind) — иначе ломается XAML-компиляция. Для новых ключей UI
  добавить en+ru в en.json/ru.json заранее.
- **x:Bind в Window (не Page)**: x:Bind в MainWindow работает (уже используется), но DataTemplate
  в Window: Binding через ElementName=Root к командам VM — паттерн уже в текущем MainWindow
  (Command={Binding ..., ElementName=Root}) — сохраняется.
- **TitleBar**: UWP/WinUI TitleBar-контрол портится из ZuiV2 как есть; если ContentDialog
  конфликтует с window-хостингом, заменить на ExtendsContentIntoTitleBar (запасной вариант).
- **SxS-тест тем**: после ЧАНКА 1 переключение Dark/Light не должно ломать InfoBar/ProgressBar
  (используют ThemeResource внутри implicit styles).

### Лицензии заимствованных идей/кода (проверено 2026-10-10)

- **ZuiV2** — свой код: копируем свободно, атрибуция не нужна.
- **komi-store** — Apache-2.0: код уже атрибутирован в NOTICE (AssetSelector). Идеи (curated
  discovery feed, release-notes в карточке, category ranking) можно брать свободно — Apache-2.0
  патентная гранта покрывает и реализацию.
- **UniGetUI** (Devolutions/marticliment) — **MIT**: можно портировать код напрямую (с copyright
  notice в NOTICE: «Portions © 2025 Martí Climent, MIT»). Ценные для нас фичи: per-package
  skip/ignore updates, skip version, custom install options per package, backup/import списка
  пакетов (Export/Import bundles), «Startedinstalling» toast-уведомления, winget arg forwarding.
- **sparkle (thedogecraft)** — GPL-3.0: **код копировать нельзя** (не GPL-совместимо), но идеи
  (debloat-optimization UX) — можно, идеи лицензией не охраняются.
- Прочие starred: у dexox 0 starred, у Skllfi 1 (sparkle).

---

## v0.3 — КАКИЕ ЕЩЁ УЛУЧШЕНИЯ (спланировано 2026-10-10, по запросу)

Принцип отбора: улучшение должно попадать в ядро-ценность продукта — «обновления без
ручного участия». Всё, что не про обновления, — во вторую очередь.

### A. Контроль и видимость (прямое продолжение v0.2)

1. **Per-app правила обновления в UI**: пропустить версию / закрепить версию (PinnedVersion
   в TrackedApp уже есть) / «игнорировать prerelease» на приложение / канал stable-beta-nightly
   (AssetSelector flavor rank уже это умеет). UI: MenuFlyout на карточке. Идея — UniGetUI
   (MIT): skip version + ignore updates per package.
2. **История обновлений** — отдельная страница: из UpdateHistoryEntry (модель в Core уже есть,
   сейчас пишется в history.json): дата, версия до→после, результат, длительность, ссылка на
   release notes. Фильтр по приложению. Апгрейд-путь к SQLite уже подготовлен
   (SQLitePCLRaw.lib.e_sqlite3 в csproj).
3. **Экспорт/импорт списка приложений** (bundle): config.json → файл → перенос на другую
   машину. Идея — UniGetUI backup/import (MIT). Также «Copy as winget/URL list».
   Статус: A3 ✅ (2026-10-14): SettingsViewModel.ExportAppsAsync (FileSavePicker → bundle JSON
   с теми же опциями сериализации, что конфиг) / ImportAppsAsync (FileOpenPicker → дедуп по
   owner/repo → мерж в конфиг + MainViewModel.Apps + AppsTrackedCount) / CopyAppsList
   (winget id | github URL → буфер). Секция «Экспорт и импорт» на SettingsPage, 3 кнопки.
4. **Расписание с джиттером**: проверка не ровно в 6 ч (все клиенты разом бьют GitHub API),
   а ±10% случайно. Одна строка в планировщике, эффект на rate limits заметный.
   Статус: A4 ✅ (2026-10-14): `CheckSchedule.WithJitter` (Core, 3 unit-теста: границы ±10%,
   разброс, passthrough для нуля) — мультипликативный джиттер; BackgroundMonitor и
   CatalogMonitor прогоняют каждый расчёт next-run через него.
5. **Балун/тост при завершении пакетного обновления** (не на каждый чих, а по итогу):
   «Обновлено 3, ошибок 1: <имя>» — TrayService balloon API уже есть.
   Статус: A5 ✅ (2026-10-14): UpdateAllAsync собирает счётчики успехов/ошибок и показывает
   ОДИН балун итога («Обновлено: N» / «Обновлено: N, ошибок: M: <имена>») через
   TrayService.ShowBalloon — только если что-то реально выполнялось.

### B. Надёжность движка (страховка от поломки чужих обновлений)

6. **Откат по-настоящему**: сегодня старая версия удаляется после успешного запуска. Добавить
   health-check после старта (процесс жив N секунд / ответил / не упал) до commit'а; при
   провале — автоматический rollback и запись в историю с причиной. Это главное доверие
   к «тихим» обновлениям.
7. **Проверка подписи инсталлятора + SmartScreen-подсказка**: перед запуском .exe/.msi —
   WinVerifyTrust; без подписи — явный диалог согласия. Уже частично в DESIGN.md п.2
   «Безопасность обновлений», довести до конца.
   Статус: B7 ✅ (2026-10-14): `InstallerTrustPolicy` (Core, 9 unit-тестов): TrustedSignature →
   запуск; Unsigned/UnsupportedFile → только с явным per-app согласием
   (`TrackedApp.AllowUnsignedInstaller`, чекбокс в AddEditAppDialog — большинство мелких
   GitHub-инструментов не подписано, без согласия приложение стало бы unusable);
   InvalidSignature → никогда не запускается. `InstallerTrustService` (Infrastructure):
   `Get-AuthenticodeSignature` через процесс (та же WinVerifyTrust-механика, проверена на
   реальных файлах: embedded-подписанный dotnet.exe → Trusted, наша сборка → Unsigned,
   catalog-подписанный notepad → Trusted; прямой P/Invoke WinVerifyTrust на этой машине
   стабильно отдаёт TRUST_E_SYSTEM_ERROR 0x800B0003 на любом файле — ловушка задокументирована
   в скилле). UpdateEngine: гейт после скачивания ДО запуска стратегий SilentInstaller/
   BuiltInUpdater/ExternalLauncher (PortableReplace не запускает файл — без гейта);
   отказ пишется в историю с причиной. «Диалог согласия» = осознанный чекбокс per-app
   (безопаснее навязчивого попапа на каждое обновление).
8. **Зеркала скачивания** (design уже описывает): ghproxy/зеркала для сетей, где GitHub
   режется — актуально для RU-пользователей. Ротация: github.com → зеркало 1 → зеркало 2.
   Статус: B8 ✅ (2026-10-14): `DownloadMirrors` (Core, чистая политика — 10 unit-тестов):
   зеркалируются только github.com release-asset URL (api.github.com и чужие хосты — мимо),
   кандидаты = оригинал → зеркало1 → зеркало2, дедуп по регистру. GitHubApiClient:
   `DownloadFileAsync` ротация источников по 3 попытки на каждый, .part-докачка общая
   для всех кандидатов (зеркала отдают идентичные байты, поддерживают Range/206 — проверено
   curl'ом), 416 чистит .part; дайджест после скачивания страхует от битого зеркала.
   Конфиг: `AppConfig.DownloadMirrors` (default: ghfast.top, ghproxy.net — живые на
   2026-10-14; gh-proxy.com дал 403, не включён). Редактируется в config.json без UI.
9. **Rate-limit-aware фоновая проверка**: буст priorities — сначала apps с available update
   known, новинки — по остаточному принципу.
   Статус: B9 ✅ (2026-10-14): `BackgroundCheckOrder.Prioritize` (Core, 4 unit-теста) —
   tier-1: известное доступное обновление (LastCheckStatus == UpdateAvailable), tier-2:
   проверенные ранее (up-to-date/error/pinned), tier-3: новинки (null) — стабильный порядок
   внутри тира. `TrackedApp.LastCheckStatus/LastCheckUtc` персистятся в config.json
   (BackgroundMonitor пишет после каждой проверки; finally переиспользует экземпляр конфига
   цикла — ранее reload затирал мутации; попутно исправлена двойная задержка в
   early-continue ветках disabled/rate-limited). MainViewModel.CheckUpdatesAsync — тот же
   порядок + сохранение статусов (ручная проверка обновляет приоритеты фона).
10. **Хранилище старых версий (version cache)**: при обновлении не удалять сразу, а
    складывать в `%LOCALAPPDATA%\AppWatch\Versions\<app>\` с автопаржкой до N версий —
    мгновенный ручной откат из UI («Откатиться на 1.2.5» в MenuFlyout).
   Статус: B10 ✅ (2026-10-14): `VersionArchive` (Core, чистая политика — 4 unit-теста:
   newest-first порядок + prune до N) + `VersionStore` (Infrastructure): Archive (commit
   в UpdateEngine теперь архивирует installDir.old вместо удаления), List (newest-first),
   RollbackTo (текущая версия тоже архивируется — roll-forward возможен; выбранный снапшот
   переносится в install dir; процесс гасится перед откатом). AppConfig.VersionCacheCount
   (default 3). UI: кнопка «Откат» (E7A6) на карточке AppsPage — видна только когда кэш
   не пуст; RollbackVersionDialog (чистый C#) со списком версий+дат; RollbackAppAsync в
   MainViewModel (IProcessService добавлен в DI-цепочку VM).

### C. UX-детали

11. **Пустые состояния**: нет приложений → карточка «Добавьте первое из Каталога» с кнопкой;
    нет обновлений → «Всё актуально ✓» с датой последней проверки. Обязательные Loc-ключи.
12. **Сворачивание секций**: сворачиваемые секции в Каталоге по категориям —
    Expander + SectionHeaderStyle из ZuiV2.
13. **Иконки/монограммы приложений**: первая буква имени в закрашенном скруглённом квадрате
    (IconGenerator для трея уже есть — переиспользовать палитру) — по-настоящему дешёвые иконки.
14. **Обновление по клику из карточки** (v0.2 желательное) + отдельная кнопка «Обновить все»
    в hero-баннере.
15. **«Открыть папку»/«Открыть release notes»** в MenuFlyout карточки — два клика до цели,
    доверие растёт.
16. **Отсрочка обновления (snooze)**: «напомнить через 3 дня» на карточке обновления —
    мягкий контроль, не спорящий с авто-обновлением.

### D. Архитектура (внутренняя готовность к масштабу)

17. **Generic Host + IHostedService**: заменить DispatcherQueueTimer на фоновый сервис
    (Microsoft.Extensions.Hosting уже в csproj) — единый жизненный цикл, отменяемые проверки,
    graceful shutdown.
18. **SQLite для истории** (пакет уже в csproj): когда history.json > 5 МБ или > 1000 записей —
    миграция на таблицу с индексами. Не сейчас.
19. **Отмена операций**: CancellationToken по кнопке «Стоп» на hero-баннере при пакетном
    обновлении. Уже частично поддержано (CancellationToken в командах VM).
   Статус: D19 ✅ (2026-10-14): `MainViewModel.IsUpdateAllRunning` + `CancellationTokenSource`
   для `UpdateAllAsync`; `StopUpdateAllCommand` отменяет токен. В AppsPage hero — кнопка
   «Обновить всё» скрывается при запуске, появляется красная «Стоп» (иконка ⏹, `BtnStop`).
   Локализация Stop/BtnStop (en/ru). Конвертер `InverseBoolToVisibility` добавлен.
20. **Мелкая сетка-подготовка**: schemaVersion 2 (для PinnedVersion и настроек ур. A1),
    IConfigStore.SaveDesyncSafe (atomic write temp+rename), отображение next-run-time в UI.
   Статус: D20 ✅ (2026-10-14): schemaVersion=2 в AppConfig + миграция v1→v2 (HealthCheckSeconds,
   BackgroundCheckNextRunUtc, CatalogCheckNextRunUtc). JsonConfigStore.Save = atomic temp+rename
   (File.Move(tmp, path, overwrite:true)). Load() мигрирует и сразу сохраняет (v1 конфиги обновляются
   без перезапуска). BackgroundMonitor/CatalogMonitor пишут next-run-time в конфиг после каждого
   цикла (BackgroundCheckNextRunUtc / CatalogCheckNextRunUtc). UI: поля добавлены в модель,
   отображение на SettingsPage — следующим шагом (C12).
   C12 ✅ (2026-10-14): секция «Фоновые проверки» на SettingsPage (ZuiV2 SettingRow) —
   «Следующая проверка приложений» и «Следующая проверка каталога» со временем (dd.MM.yyyy HH:mm)
   или «пока не запланировано». SettingsViewModel.RefreshScheduleInfo() перечитывает конфиг с
   диска; вызывается из конструктора страницы и OnNavigatedTo (кэш не даёт устаревших значений).
   Проверено UIA: секция рендерится, fallback-текст корректен.

### Приоритеты v0.3 (порядок реализации)

1. **A1+A2 (правила + история)** — дают видимость, заметны сразу, модели уже есть.
   Статус: A2 ✅ (2026-10-14): history.json пишется для всех движков (GitHub/winget/fonum —
   `_history.Append` на всех вызовах ApplyAsync), HistoryPage — ZuiV2-подстраница (back-кнопка,
   секция, карточки с монограммой/версиями/результатом/датой, пустое состояние), NavCard «История»
   на AppsPage. A1 частично: per-app правила (UpdateKind/Strategy/WingetId/IgnorePreReleases/
   PinnedVersion-поле) — в AddEditAppDialog (чистый C#); UI «закрепить/пропустить версию» из
   карточки — не сделан.
2. **B6 (health-check + auto-rollback)** — доверие к тихим обновлениям, ядро продукта.
   Статус: B6 ✅ (2026-10-14): `UpdateHealthCheck.VerifyStartupAsync` (Core, чистая политика,
   injectable delay — 5 unit-тестов) — новая сборка запускается и должна прожить окно
   `AppConfig.HealthCheckSeconds` (default 6) ДО commit'а. PortableReplace-путь движка:
   replace → health-check → healthy ? DiscardBackup (commit) : RestoreBackup (rollback)
   + запись в историю с причиной «health check failed: …» + relaunch старой версии при
   LaunchAfterUpdate. При !LaunchAfterUpdate проверочный процесс гасится после commit'а.
   Для стратегий-инсталляторов (silent/builtin/winget) откат остаётся на стороне
   установщика — наш rollback только для portable-replace.
3. **C11+C13+C14 (пустые состояния + монограммы + обновление по клику)** — визуальный
   характер за копейки.
   Статус: C11+C13+C14 ✅ (2026-10-14): пустые состояния на всех страницах (AppsPage/HistoryPage
   были; CatalogPage — текст исправлен с самоссылочного NoTrackedApps на CatalogEmpty;
   WingetPage — два состояния: WingetNotAvailable при !WingetAvailable и WingetNoUpdates
   при пустом списке). Монограммы — на всех карточках (Apps/History/Catalog, проверено UIA).
   Обновление по клику — кнопка UpdateOne на каждой карточке AppsPage.
4. **D17 (Generic Host)** — сделать сразу после v0.2, пока фоновый сервис один.
   Статус: D17 ✅ (2026-10-14): BackgroundMonitor и CatalogMonitor переведены на
   `BackgroundService` (IHostedService) через `Microsoft.Extensions.Hosting` —
   `AddHostedService` вместо ручных `Start()/Stop()` в App.xaml.cs. Жизненный цикл
   теперь полностью управляется Generic Host: запуск с задержкой (30с / 2 мин),
   периодические циклы через `await Task.Delay` с `CancellationToken`,
   graceful shutdown по `OperationCanceledException`. Singleton-поля убраны,
   DI регистрирует как hosted services. Пакет `Microsoft.Extensions.Hosting` уже
   был в csproj.
5. **B8 (зеркала)** — как только появятся жалобы «не качает», не раньше.
6. Остальное — по мере роста: A3/A4/A5, B7/B9/B10, C12/C15/C16, D18-D20.

### Не-цели v0.3

- Категоризированный discovery-feed (это komi-store-контентная фича, требует контента и
  модерации) — не раньше, чем появится желание вести каталог.
- Мультипакетные менеджеры (Scoop/Chocolatey) — расширение вширь до того, как закрепимся
  в глубине winget+GitHub, размывает фокус.

---

## v0.4 — Публикация и дистрибуция (план 2026-10-15)

Цель: замкнуть фундамент п.8 (CI/CD с `vpk pack` «с первой версии → self-update работает
сразу») и п.9 (NOTICE). v0.3 закрыт полностью (A1-A5, B6-B10, C11-C14, D17-D20 — все ✅);
v0.4 выводит проект из «работает у меня» в «можно ставить кому угодно».

### Приоритеты v0.4 (порядок реализации; пересмотр 2026-10-15 — сначала живое доказательство ценности, дистрибуция после)

1. **P0 — живое end-to-end доказательство**: приложение должно РЕАЛЬНО обновлять реальные
   приложения ежедневно, а не существовать в вакууме. Полигон — живые записи каталога
   (zapret-семейство обновляется еженедельно, GPU-Z, PowerToys): установка из каталога
   через UI → первый check → cycle обновления (check → download → health-check → commit →
   version cache) → откат из UI → запись в истории. Критерий: tracked apps ≠ 0, движок
   пережил реальный релизный цикл на живом приложении.
2. **P1 — каталог как точка отсчёта**: расширить до 10–15 живых RU-актуальных
   GitHub-проектов (часто обновляемые: DPI-обходы, утилиты, оптимизаторы), polish
   установки одним кликом (прогресс, статус, обработка неудач).
3. **P2 — A1-доделка, per-app правила из карточки**: `TrackedApp.SkippedVersion` +
   `UpdateDecisionEngine.Evaluate` игнорирует skipped-тег; MenuFlyout на карточке
   AppsPage: «Закрепить текущую» / «Пропустить эту версию» / «Сбросить правила».
4. **P3 — Battle.net-hide** (хитрый кейс — витрина «движок справляется с кем угодно»):
   спец-детектор версии в VersionDetector (Battle.net прячет UI — реестр не вариант).
5. **P4 — CI/CD + self-update** (дистрибуция — после доказательства ценности):
   `.github/workflows/ci.yml` (push → build+тесты; тег v* → build → test → vpk pack →
   Release: portable zip + RELEASES.json + дельты), версия из тега,
   `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` в env билда; SelfUpdateService против
   реального CI-релиза; NOTICE (Apache-2.0 komi-store) + README.
6. **P5 — winget-пакет AppWatch**: манифест в `microsoft/winget-pkgs` после первого
   тегового релиза.

### Не-цели v0.4

- Автоматическая публикация в Store (MSIX-упаковка) — unpackaged уже работает, MSIX
  вернёт старую цепочку проблем (Developer Mode, виртуализация AppData).
- Подпись кода (EV-сертификат) — дорого и не нужно для портативного распределения;
  SelfUpdateService верифицирует SHA256, digest уже в движке.
- Linux/macOS — вне фокуса Windows-only проекта.

### Критерии готовности v0.4

- Тег `vX.Y.Z` в git → через ~10 минут живой GitHub Release с рабочими ассетами.
- `SelfUpdateService.PrepareAsync` находит релиз, качает, верифицирует, применяет,
  перезапускает — на свежем CI-релизе.
- Карточка приложения: pin/skip/reset работают, перезапуск сохраняет.
- Battle.net показывает установленную версию в карточке.

### P0 — выполнен 2026-10-15 (живое end-to-end доказательство)

Полный цикл на живых релизах GitHub, реальных файлах, под отладчиком VS (запись истории в
history.json, кэш версий в %LOCALAPPDATA%\AppWatch\Versions):

- ✅ Установка из каталога через UI: zapret-discord-youtube 1.10.3 (маркерлесс .zip),
  GameOptimizer v0.5.9 (.zip с x64-маркером) — download → SHA-256 → распаковка → persist.
- ✅ Живой чек: авто-детект версии бинарника (FileVersionInfo) честно бьёт устаревшую
  строку config — подделка метаданных не проходит (проверено: downgrade реальных файлов
  на v0.5.6 → «Доступно обновление» v0.5.9).
- ✅ Graceful-отказ на живом elevated winws (DPI-обход юзера): файлы не тронуты, в
  истории ясная причина «не удалось остановить процесс… запущен от администратора».
- ✅ Полный цикл обновления v0.5.6 → v0.5.9: download → SHA-256 → ReplaceWithRollback →
  health-check (запуск GameOptimizer.exe, alive-окно, kill) → commit → архив 0560 в
  кэш версий → history ok:true → config v0.5.9.
- ✅ Откат из UI: диалог B10 → выбор 0560 → файлы вернулись на 0.5.6.0, config 0560,
  v0.5.9 сохранена как current* (возврат вперёд возможен).

**Найдено и исправлено 5 живых багов** (все — за пределами юнит-тестов):
1. `GitHubApiClient` без BaseAddress — ВСЕ GitHub-вызовы приложения никогда не работали.
2. `AssetArchitectureMatcher`: markerless-имя ассета считалось ForeignMatch — все
   ассеты без arch-маркера отбрасывались (zapret .zip/.rar).
3. `Directory.Move` без создания родителя — первая установка всегда падала.
4. `ProcessService.Kill`: elevated-цель роняла апдейт сырым исключением → теперь
   `bool` + внятный отказ на русском, файлы нетронуты.
5. `MainViewModel` не звал `item.Refresh()` после апдейта — версия на карточке и
   кнопка «Откат» (B10) не появлялись.

Тесты: 100/100 (+3 регрессионных на markerless-ассеты). Коммит `65199e2`.

**P1-заметки (из живого прогона):**
- Статус «Ошибка обновления» на карточке не показывает Detail — причина только в
  истории/логе (нужен tooltip или вторая строка).
- Лейбл версии после отката показывает id снимка («0560»), а не человеческую версию.
- zapret-карточка осознанно остаётся на 1.10.2 «Доступно обновление»: живой elevated
  winws юзера — корректное поведение; обновится, когда процесс остановлен или AppWatch
  запущен от администратора (это и есть реальный сценарий пользователя).

### P0-дополнение — PortableExe «просто exe» (2026-10-15, живой кейс пользователя)

Пользователь поставил optimizerDuck из каталога → карточка предложила «Запустить» →
краш Win32Exception «файл не найден» в отладчике. Разбор вскрыл пробел движка:
- Реальные ассеты optimizerDuck — **голые .exe по-архитектурно** (~87 МБ, манифест
  `requireAdministrator`), не архив и не классический инсталлер — тип релиза, который
  двигок не поддерживал вовсе (ровно кейс «просто exe» из брифа).
- Запись каталога ошибочно объявляла его installer-видом: движок запускал скачанный
  exe (UAC), наш каталог установки оставался пустым → «Запустить» падал.

Реализовано (коммит `df89cf3`, 113/113 тестов):
- `UpdateKind.PortableExe` + классификатор `AssetContent` (архив vs одиночный exe).
- PortableExe → PortableReplace: копирование без распаковки, переименование в
  `ExecutableName` (имя ассета несёт arch/version суффиксы), тот же ReplaceWithRollback
  и health-check, что и для архивов.
- **Снятие Mark-of-the-Web после SHA-256 верификации** — иначе SmartScreen встречает
  первый запуск одиночного exe (у zip-портативов MotW не наследуется при распаковке —
  потому GameOptimizer не ловился).
- LaunchApp: File.Exists-гард + внятный статус вместо сырого исключения (RU/EN).
- Живая верификация: optimizerDuck v2.29.0 — 87 МБ скачан, digest сверен, MotW снят,
  `optimizerDuck.exe` в каталоге установки; запуск идёт через штатный UAC
  (requireAdministrator — норма для оптимизатора).

**P2-заметка (elevated-приложения):** приложения с манифестом requireAdministrator
(optimizerDuck, winws-семейство) требуют для цикла обновления (kill/replace/relaunch)
запущенного от администратора AppWatch — иначе kill получает Win32Exception(5).
Долгосрочное решение (элевация только для health-check через COM/планировщик) — в
бэклоге; текущее поведение — внятный отказ с причиной.

### W1–W4 — winget search & install «Установить из winget» (2026-10-15, завершено)

Закрывает исходный бриф: «установить нужные программы, не напрягаясь... winget+GitHub».
Раньше winget был только для обновлений; теперь — полноценная установка любого пакета
из Community Repository (ищи → ставь → автотрек).

- **W1**: `WingetService.SearchAsync` + `WingetOutputParser.ParseSearch` — локале-независимый
  anchor-scan парсер (4-колоночная name-match таблица, 5-колоночная с колонкой
  «Совпадение»). Регрессионные тесты на живых фикстурах (`winget search notepad`,
  `wiztree`, `telegram` — RU locale v1.29): 7 тестов + 113 общих. Парсер работает
  структурно: source = последний токен, Id = первый id-образный токен, за которым
  следует версия, затем либо source (4-кол), либо "Tag:"-маркер (5-кол).
- **W2**: WingetPage секция «Установить из winget» — TextBox поиск + AccentButton
  «Искать», результаты ListView карточками (monogram + name/id/version/source + Install).
  Пустые результаты → иконка + «Ничего не найдено». ProgressBar indeterminate при поиске.
  DataTemplate `WingetSearchItem` с прокси-командой к MainViewModel.
- **W3**: успешная установка автотрекает пакет (`UpdateKind.WingetPackage`,
  `WingetPackageId`) — существующий движок обновлений теперь следит за ним.
- **W4**: graceful-пути ошибок:
  - already-installed (0x8A150041) → «Уже установлено — добавлено в отслеживаемые»
  - install failure → winget's message на карточке (`Squash` helper) вместо краша
  - machine-scope-only пакеты (WizTree `requireAdministrator`) в headless-контексте
    падают с кодом 2 (UAC auto-cancelled), в UI пользователе — штатный UAC-диалог

**Атрибуции (MIT, атрибуция в NOTICE):**
- UniGetUI (MIT, https://github.com/Devolutions/UniGetUI) — winget CLI invocation
  patterns, search parsing, install arg sets, user/machine scope strategy with UAC retry.

**Live proof (2026-10-15):**
- Search «telegram» → 24 cards → Install on Telegram Desktop (already installed) →
  'Установлено и добавлено в отслеживаемые' → dashboard Tracked:4 with Telegram card.
- Search «wiztree» → 1 card → Install → UAC exit code 2 in headless (user would see
  UAC) → graceful 'Сбой установки с кодом выхода: 2' on card.
- Dashboard now shows 'Отслеживается: 4' with Telegram Desktop card.

Tests: 120/120.


