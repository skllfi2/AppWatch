using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class WingetSearchParserTests
{
    // Live fixture: `winget search "notepad"` — RU locale, 5-column table with the
    // localized "Совпадение" (Match) column. Multi-word names ("NoteTab Light",
    // "Notepad2-mod"), Tag-match values, version shapes incl. "2.3rc02".
    private const string NotepadFixture =
        "Имя                           ИД                             Версия     Совпадение               Источник\n" +
        "---------------------------------------------------------------------------------------------------------\n" +
        "AkelPad                       AkelPad.AkelPad                4.9.9      Tag: notepad             winget\n" +
        "SkyNotepad                    AlperAkca79.SkyNotepad         0.12.0.0   Tag: notepad             winget\n" +
        "NoteTab Light                 FookesHolding.NoteTabLight     7.2        Tag: notepad             winget\n" +
        "Notepad++                     Notepad++.Notepad++            8.9.8.1    Tag: notepad             winget\n" +
        "Notepad2-mod                  Notepad2mod.Notepad2mod        4.2.25.998 Tag: notepad             winget\n" +
        "WikidPad                      WikidPad.WikidPad              2.3rc02    Tag: notepad             winget\n" +
        "Zeus IDE                      Xidicone.ZeusIDE               3.98y      Tag: notepad             winget\n";

    // Live fixture: `winget search "wiztree"` — name match collapses the table to
    // 4 columns (no Match column).
    private const string WizTreeFixture =
        "Имя     ИД                       Версия Источник\n" +
        "-------------------------------------------------\n" +
        "WizTree AntibodySoftware.WizTree 4.33   winget\n";

    // Live fixture: `winget search "telegram"` — Moniker matches, multi-word name
    // ("Telegram Desktop", "All-in-One Messenger").
    private const string TelegramFixture =
        "Имя                            ИД                             Версия  Совпадение          Источник\n" +
        "--------------------------------------------------------------------------------------------------\n" +
        "Telegram Desktop               Telegram.TelegramDesktop       7.3.1   Moniker: telegram   winget\n" +
        "64Gram Desktop                 64Gram.64Gram                  1.2.10  Tag: telegram       winget\n" +
        "All-in-One Messenger           HenrikWenz.All-in-OneMessenger 2.5.0   Tag: telegram       winget\n";

    [Fact]
    public void ParseSearch_TagMatches_FiveColumnTable()
    {
        var results = WingetOutputParser.ParseSearch(NotepadFixture);

        Assert.Equal(7, results.Count);
        var first = results[0];
        Assert.Equal("AkelPad", first.Name);
        Assert.Equal("AkelPad.AkelPad", first.Id);
        Assert.Equal("4.9.9", first.Version);
        Assert.Equal("winget", first.Source);
    }

    [Fact]
    public void ParseSearch_MultiWordNames_Preserved()
    {
        var results = WingetOutputParser.ParseSearch(NotepadFixture);

        var notepad = Assert.Single(results, r => r.Id == "FookesHolding.NoteTabLight");
        Assert.Equal("NoteTab Light", notepad.Name);
        var zeus = Assert.Single(results, r => r.Id == "Xidicone.ZeusIDE");
        Assert.Equal("Zeus IDE", zeus.Name);
    }

    [Fact]
    public void ParseSearch_NameMatch_FourColumnTable()
    {
        var results = WingetOutputParser.ParseSearch(WizTreeFixture);

        var single = Assert.Single(results);
        Assert.Equal("WizTree", single.Name);
        Assert.Equal("AntibodySoftware.WizTree", single.Id);
        Assert.Equal("4.33", single.Version);
        Assert.Equal("winget", single.Source);
    }

    [Fact]
    public void ParseSearch_MonikerMatches_MultiWordName()
    {
        var results = WingetOutputParser.ParseSearch(TelegramFixture);

        Assert.Equal(3, results.Count);
        Assert.Equal("Telegram Desktop", results[0].Name);
        Assert.Equal("Telegram.TelegramDesktop", results[0].Id);
        Assert.Equal("All-in-One Messenger", results[2].Name);
        Assert.Equal("HenrikWenz.All-in-OneMessenger", results[2].Id);
    }

    [Fact]
    public void ParseSearch_PreambleAndNoise_Skipped()
    {
        var noisy =
            "\0Д\0и\0с\0п\0е\0т\0ч\0е\0р\0 \0п\0а\0к\0е\0т\0о\0в\0 \0W\0i\0n\0d\0o\0w\0s\0\n" +
            "   -\n" +
            "\\\n" +
            WizTreeFixture +
            "Было найдено: 1 пакет(ов).\n";

        var results = WingetOutputParser.ParseSearch(noisy);

        var single = Assert.Single(results);
        Assert.Equal("AntibodySoftware.WizTree", single.Id);
    }

    [Fact]
    public void ParseSearch_NameLookingLikeId_NotMisdetected()
    {
        // "Mery" matches the Id shape, but the next token is another id, not a
        // version — the anchor scan must reject it and find the real Id.
        var output =
            "Имя  ИД            Версия Совпадение        Источник\n" +
            "----------------------------------------------------\n" +
            "Mery Kuro.Mery     2.6.7  Tag: notepad       winget\n";

        var results = WingetOutputParser.ParseSearch(output);

        var single = Assert.Single(results);
        Assert.Equal("Kuro.Mery", single.Id);
        Assert.Equal("Mery", single.Name);
        Assert.Equal("2.6.7", single.Version);
    }

    [Fact]
    public void ParseSearch_EmptyAndErrorOutput_NoResults()
    {
        Assert.Empty(WingetOutputParser.ParseSearch(""));
        Assert.Empty(WingetOutputParser.ParseSearch("Не удалось найти пакет, соответствующий критериям.\n"));
        Assert.Empty(WingetOutputParser.ParseSearch("Имя ИД Версия Источник\n---------------------------------\n"));
    }
}
