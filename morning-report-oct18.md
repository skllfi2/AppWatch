# Утренний отчёт — автозапуск oct18 (autoresearch/oct18)

**Дата:** 2026-10-19 · **Ветка:** autoresearch/oct18 · **Run:** 7 экспериментов, 7 keep, 0 discard
**Базовый набор:** 209/209 зелёные на входе И на выходе (не тронут ни одним экспериментом)
**Итоговый набор:** 278/278 (209 база + 69 новых доменных тестов)

## Кривая метрики (domain_tests_green/total, кумулятивно)

| # | Коммит | Метрика | Сюжет |
|---|--------|---------|--------|
| E1 | 49079e2 | 5/5 | D1: буквенные суффиксы Flowseal `1.9.9a..d` |
| E2 | c27505a | 10/10 | D1: приклеенные rc/beta без дефиса (qbittorrent `5.3.0rc1`) + `nightly` не парсится |
| E3 | 0e85d8b | 15/15 | D1: префиксы `rel/`, `version-`, `build-` (до v-trim) |
| E4 | 2af3975 | 42/42 | D2: PlatformAssetFilter — .jar, mac/linux маркеры, yml/blockmap/json/xml |
| E5 | c227561 | 54/54 | D3: CatalogSearch.Score (exact>prefix>substring>typo>subseq>desc) |
| E6 | e064262 | 63/63 | D3: CatalogSearch.Rank на реальном каталоге из 16 записей |
| E7 | 5763ef6 | 69/69 | Интеграция: живые последовательности релизов через Evaluate |

## Живые находки (gh api, 2026-10-19)

1. **Vesktop: баг выбора macOS-сборки на x64** (закрыт в E4). У v1.6.7 есть
   `Vesktop-1.6.7-win.zip` (144 МБ) и `Vesktop-1.6.7-universal-mac.zip` (224 МБ).
   Оба — UniversalMatch по архит., приоритет .zip одинаков, тай-брейк по размеру выбирал
   MAC-ZIP. Теперь mac/linux-маркеры отсекаются фильтром. На ARM64 выбор корректный:
   `arm64-win.zip` (exact arch) всё ещё побеждает.
2. **Flowseal `1.9.9a..1.9.9d`** — буквенные теги раньше вообще не парсились → движок не
   видел обновлений внутри серии. Порядок подтверждён живой историей:
   `1.9.8 < 1.9.8b < 1.9.8c < 1.9.9a < … < 1.9.9d < 1.10.0` — буква = патч-бамп СВЕРХУ базы.
3. **qbittorrent `release-5.3.0rc1`** — rc приклеен к версии без дефиса, не парсился.
   Семантика противоположна Flowseal: rc/beta = пререлиз СНИЗУ стабильной (как на GitHub).
4. **rustdesk `nightly`** — релиз без версии публикуется поверх стабильного; регресс-тест
   гарантирует, что он не затеняет 1.5.0 в сортировке движка.
5. **Мусорные кандидаты**: `latest*.yml`, `*.blockmap`, `metainfo.xml`, `rustdesk.sbom.json`
   раньше проходили платформенный фильтр (не имеют архит.-маркера → Universal → selectable).

## Изменённые файлы

- `src/AppWatch.Core/Domain/VersionParser.cs` — LetterSuffix, keyword-суффиксы, TagPrefixes
- `src/AppWatch.Core/Domain/AssetSelection/PlatformAssetFilter.cs` — НОВЫЙ, выделен из UpdateDecisionEngine
- `src/AppWatch.Core/Domain/UpdateDecisionEngine.cs` — делегирует PlatformAssetFilter
- `src/AppWatch.Core/Domain/CatalogSearch.cs` — НОВЫЙ: Score + Rank (чистые функции)
- Тесты (новые): VersionTagFormatsTests (15), PlatformAssetFilterTests (27),
  CatalogSearchTests (21), LiveReleaseSequenceTests (6)

## Отложенное (вне скоупа запуска)

- Подключить `CatalogSearch.Rank` в UI-поиск каталога (MainViewModel, src/AppWatch.App —
  запрещённая зона этого прогона; чистая функция готова).
- Живая UIA-проверка фикса Vesktop в работающем AppWatch.exe (прогон покрыл Core-уровень).
- GPU-Z (wingetPackage): GitHub-релизов нет (404) — для D1 фикстур брать нечего.
- `.iso`-ассеты (ventoy livecd) остаются selectable: вреда не доказано, вернуть при случае.
