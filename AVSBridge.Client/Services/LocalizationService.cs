namespace AVSBridge.Client.Services;

/// <summary>
/// Simple dictionary-based localization service.
/// Supports Ukrainian (default) and English.
/// </summary>
public sealed class LocalizationService
{
    private string _currentLanguage = "uk";
    public string CurrentLanguage => _currentLanguage;
    public bool IsUkrainian => _currentLanguage == "uk";

    public event Action? OnLanguageChanged;

    public void SetLanguage(string lang)
    {
        if (lang != "uk" && lang != "en") return;
        if (_currentLanguage == lang) return;
        _currentLanguage = lang;
        OnLanguageChanged?.Invoke();
    }

    public void Toggle() => SetLanguage(_currentLanguage == "uk" ? "en" : "uk");

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        var dict = _currentLanguage == "uk" ? Uk : En;
        return dict.TryGetValue(key, out var val) ? val : key;
    }

    private static readonly Dictionary<string, string> Uk = new()
    {
        // Lobby
        ["app.title"] = "AVS Bridge",
        ["app.subtitle"] = "Карткова гра для 2–10 гравців",
        ["lobby.yourName"] = "Ваше ім'я",
        ["lobby.namePlaceholder"] = "Введіть ім'я...",
        ["lobby.createRoom"] = "🎮 Створити кімнату",
        ["lobby.or"] = "або",
        ["lobby.roomCodePlaceholder"] = "Код кімнати",
        ["lobby.join"] = "🚪 Приєднатися",
        ["lobby.connecting"] = "З'єднання...",
        ["lobby.connectionFailed"] = "Не вдалося з'єднатися",

        // Waiting
        ["waiting.title"] = "Очікування гравців",
        ["waiting.players"] = "гравців",
        ["waiting.startGame"] = "▶️ Почати гру",
        ["waiting.needPlayers"] = "Потрібно мінімум 2 гравці",
        ["waiting.waitHost"] = "Очікуйте початку гри від хоста",

        // Game
        ["game.room"] = "Кімната",
        ["game.you"] = "ви",
        ["game.score"] = "Рахунок",
        ["game.points"] = "очків",
        ["game.cards"] = "карт",
        ["game.yourTurn"] = "ваш хід!",
        ["game.turn"] = "Хід",
        ["game.suit"] = "Масть",
        ["game.drawPile"] = "Колода",
        ["game.clickDismiss"] = "натисніть щоб закрити",
        ["game.loading"] = "Завантаження...",

        // Actions
        ["action.play"] = "🃏 Грати",
        ["action.cover"] = "🃏 Покрити",
        ["action.draw"] = "📥 Тягнути",
        ["action.pass"] = "⏩ Пас",
        ["action.skip"] = "⏩ Пропустити",
        ["action.discard"] = "🗑 Скинути",
        ["action.accept"] = "✅ Прийняти",
        ["action.acceptDraw"] = "✅ Тягнути штраф",
        ["action.acceptSkip"] = "✅ Пропустити хід",
        ["action.startNewRound"] = "🔄 Новий раунд",

        // Phases
        ["phase.waiting"] = "Очікування",
        ["phase.dealing"] = "Роздача",
        ["phase.inProgress"] = "Гра",
        ["phase.roundOver"] = "Раунд завершено",
        ["phase.gameOver"] = "Гра закінчена",

        // Round over
        ["roundOver.title"] = "Раунд завершено!",
        ["gameOver.title"] = "Гра закінчена!",
        ["roundOver.player"] = "Гравець",
        ["roundOver.score"] = "Рахунок",
        ["roundOver.status"] = "Статус",
        ["roundOver.eliminated"] = "Вибув",

        // Events
        ["event.joined"] = "приєднався",
        ["event.left"] = "вийшов",
        ["event.deckReshuffled"] = "Колоду перемішано!",
        ["event.jokerCancelled"] = "Джокер скасовано!",
        ["event.queensRoundEnd"] = "4 Дами! Раунд закінчено!",
        ["event.eliminated"] = "вибув!",
        ["event.penaltyCards"] = "штрафних карт",
        ["event.skips"] = "пропуск(ів)",

        // Language
        ["lang.switch"] = "EN",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        // Lobby
        ["app.title"] = "AVS Bridge",
        ["app.subtitle"] = "Card game for 2–10 players",
        ["lobby.yourName"] = "Your name",
        ["lobby.namePlaceholder"] = "Enter name...",
        ["lobby.createRoom"] = "🎮 Create Room",
        ["lobby.or"] = "or",
        ["lobby.roomCodePlaceholder"] = "Room code",
        ["lobby.join"] = "🚪 Join",
        ["lobby.connecting"] = "Connecting...",
        ["lobby.connectionFailed"] = "Connection failed",

        // Waiting
        ["waiting.title"] = "Waiting for players",
        ["waiting.players"] = "players",
        ["waiting.startGame"] = "▶️ Start Game",
        ["waiting.needPlayers"] = "Need at least 2 players",
        ["waiting.waitHost"] = "Waiting for host to start",

        // Game
        ["game.room"] = "Room",
        ["game.you"] = "you",
        ["game.score"] = "Score",
        ["game.points"] = "points",
        ["game.cards"] = "cards",
        ["game.yourTurn"] = "your turn!",
        ["game.turn"] = "Turn",
        ["game.suit"] = "Suit",
        ["game.drawPile"] = "Draw pile",
        ["game.clickDismiss"] = "click to dismiss",
        ["game.loading"] = "Loading...",

        // Actions
        ["action.play"] = "🃏 Play",
        ["action.cover"] = "🃏 Cover",
        ["action.draw"] = "📥 Draw",
        ["action.pass"] = "⏩ Pass",
        ["action.skip"] = "⏩ Skip",
        ["action.discard"] = "🗑 Discard",
        ["action.accept"] = "✅ Accept",
        ["action.acceptDraw"] = "✅ Draw penalty",
        ["action.acceptSkip"] = "✅ Accept skip",
        ["action.startNewRound"] = "🔄 New Round",

        // Phases
        ["phase.waiting"] = "Waiting",
        ["phase.dealing"] = "Dealing",
        ["phase.inProgress"] = "In Progress",
        ["phase.roundOver"] = "Round Over",
        ["phase.gameOver"] = "Game Over",

        // Round over
        ["roundOver.title"] = "Round Over!",
        ["gameOver.title"] = "Game Over!",
        ["roundOver.player"] = "Player",
        ["roundOver.score"] = "Score",
        ["roundOver.status"] = "Status",
        ["roundOver.eliminated"] = "Eliminated",

        // Events
        ["event.joined"] = "joined",
        ["event.left"] = "left",
        ["event.deckReshuffled"] = "Deck reshuffled!",
        ["event.jokerCancelled"] = "Joker cancelled!",
        ["event.queensRoundEnd"] = "4 Queens! Round ends!",
        ["event.eliminated"] = "eliminated!",
        ["event.penaltyCards"] = "penalty cards",
        ["event.skips"] = "skip(s)",

        // Language
        ["lang.switch"] = "УК",
    };
}
