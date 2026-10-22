namespace Snowy.UI;

public enum AppLanguage { English, Russian, German }

public static class I18n
{
    public static AppLanguage Current { get; private set; } = AppLanguage.Russian;
    public static event Action? LanguageChanged;

    public static void SetLanguage(AppLanguage lang)
    {
        if (Current == lang) return;
        Current = lang;
        LanguageChanged?.Invoke();
    }

    public static string T(string key, params object[] args)
    {
        var raw = Raw(key);
        return args.Length == 0 ? raw : string.Format(raw, args);
    }

    private static string Raw(string key)
    {
        if (!Strings.TryGetValue(key, out var v)) return key;
        return Current switch
        {
            AppLanguage.English => v.En,
            AppLanguage.German => v.De,
            _ => v.Ru
        };
    }

    private static readonly Dictionary<string, (string En, string Ru, string De)> Strings = new()
    {
        ["tab.encrypt"] = ("encrypt", "шифровать", "verschlüsseln"),
        ["tab.history"] = ("history", "история", "verlauf"),
        ["tab.settings"] = ("settings", "настройки", "einstellungen"),
        ["tab.themes"] = ("themes", "темы", "themen"),

        ["themes.heading"] = ("choose a look", "выбери оформление", "wähle ein aussehen"),
        ["theme.snowy.name"] = ("snowy", "snowy", "snowy"),
        ["theme.snowy.desc"] = ("frost and ice — the classic look", "иней и лёд — классический вид", "frost und eis — der klassiker"),
        ["theme.fire.name"] = ("red fire", "red fire", "red fire"),
        ["theme.fire.desc"] = ("embers and flame on warm charcoal", "угли и пламя на тёплом угольном фоне", "glut und flammen auf warmer kohle"),
        ["theme.nature.name"] = ("green nature", "green nature", "green nature"),
        ["theme.nature.desc"] = ("deep forest with a soft moss glow", "глубокий лес и мягкое свечение мха", "tiefer wald mit sanftem moosleuchten"),
        ["dropzone.text"] = (
            "drag a file here\n> or click to browse",
            "перетащи файл сюда\n> или кликни, чтобы выбрать",
            "datei hierher ziehen\n> oder klicken zum auswählen"
        ),
        ["filepicker.title"] = ("Choose a file", "Выбери файл", "Datei auswählen"),

        ["disclaimer"] = (
            "snowy is a personal, local file encryptor. Everything happens on your own " +
            "machine — no servers, no telemetry, no uploads anywhere. The cipher is a " +
            "custom, unaudited design (see Core/SnowCipher.cs) — treat this as a fun " +
            "project, not a vetted tool for your most critical secrets.",

            "snowy — локальный шифровальщик файлов для личного использования. Всё " +
            "происходит только на твоей машине: без серверов, телеметрии и отправки " +
            "куда-либо. Шифр самописный и не проходил внешний аудит (см. " +
            "Core/SnowCipher.cs) — относись к этому как к интересному проекту, а не " +
            "как к проверенному средству для по-настоящему критичных секретов.",

            "snowy ist ein persönlicher, lokaler Datei-Verschlüsseler. Alles läuft nur " +
            "auf deinem eigenen Rechner — keine Server, keine Telemetrie, kein Hochladen. " +
            "Die Chiffre ist eine Eigenentwicklung ohne externes Audit (siehe " +
            "Core/SnowCipher.cs) — als Hobbyprojekt zu verstehen, nicht als geprüftes " +
            "Werkzeug für wirklich kritische Geheimnisse."
        ),

        ["settings.language"] = ("language", "язык", "sprache"),
        ["settings.snow"] = ("let it snow", "пусть идёт снег", "lass es schneien"),

        ["history.empty"] = (
            "nothing here yet — encrypt or decrypt something.",
            "тут пока пусто — зашифруй или расшифруй что-нибудь.",
            "hier ist noch nichts — verschlüssele oder entschlüssele etwas."
        ),
        ["history.open"] = ("open", "открыть", "öffnen"),

        ["log.encrypted_simple"] = ("encrypted (simple) -> {0}", "зашифровано (простой) -> {0}", "verschlüsselt (einfach) -> {0}"),
        ["log.encrypted_moderate"] = ("encrypted (moderate) -> {0}", "зашифровано (умеренный) -> {0}", "verschlüsselt (moderat) -> {0}"),
        ["log.password_note"] = (
            "the password isn't stored anywhere — save it yourself.",
            "пароль нигде не хранится — сохрани его сам.",
            "das passwort wird nirgendwo gespeichert — notiere es dir selbst."
        ),
        ["log.encrypted_protected"] = ("encrypted (protected) -> {0}", "зашифровано (защищённый) -> {0}", "verschlüsselt (geschützt) -> {0}"),
        ["log.encrypt_error"] = ("encryption error: {0}", "ошибка шифрования: {0}", "verschlüsselungsfehler: {0}"),
        ["log.not_snowy_file"] = ("doesn't look like a snowy file: {0}", "не похоже на snowy-файл: {0}", "sieht nicht nach einer snowy-datei aus: {0}"),
        ["log.decrypted"] = ("decrypted -> {0}", "расшифровано -> {0}", "entschlüsselt -> {0}"),
        ["log.decrypt_error"] = ("decryption error: {0}", "ошибка расшифровки: {0}", "entschlüsselungsfehler: {0}"),
        ["log.cancelled"] = ("cancelled.", "отменено.", "abgebrochen."),
        ["log.wrong_password"] = ("wrong password (attempt {0}/{1})", "неверный пароль (попытка {0}/{1})", "falsches passwort (versuch {0}/{1})"),
        ["log.locked_out"] = ("access blocked — out of attempts.", "доступ заблокирован — попытки исчерпаны.", "zugriff gesperrt — keine versuche mehr."),
        ["log.locked_wait"] = (
            "too many wrong attempts — try again in {0} s",
            "слишком много неверных попыток — повтори через {0} с",
            "zu viele falsche versuche — erneut versuchen in {0} s"),
        ["log.wrong_puzzle"] = ("wrong answer to the puzzle.", "неверный ответ на головоломку.", "falsche antwort auf das rätsel."),
        ["log.corrupted"] = (
            "file is corrupted — data doesn't match after the answer check.",
            "файл повреждён — данные не сходятся после проверки ответа.",
            "datei beschädigt — daten stimmen nach der antwortprüfung nicht überein."
        ),

        ["common.cancel"] = ("cancel", "отмена", "abbrechen"),
        ["action.encrypt"] = ("encrypt", "зашифровать", "verschlüsseln"),
        ["action.decrypt"] = ("decrypt", "расшифровать", "entschlüsseln"),

        ["mode.title"] = ("choose a mode", "выбор режима", "modus wählen"),
        ["mode.simple.title"] = ("simple", "простой", "einfach"),
        ["mode.simple.desc"] = ("instant, no password  ->  .sny", "мгновенно, без пароля  ->  .sny", "sofort, ohne passwort  ->  .sny"),
        ["mode.moderate.title"] = ("moderate", "умеренный", "moderat"),
        ["mode.moderate.desc"] = ("40-character password  ->  .sny", "40-значный пароль  ->  .sny", "40-stelliges passwort  ->  .sny"),
        ["mode.protected.title"] = ("protected", "защищённый", "geschützt"),
        ["mode.protected.desc"] = (
            "a puzzle instead of a password  ->  .snow",
            "головоломка вместо пароля  ->  .snow",
            "ein rätsel statt eines passworts  ->  .snow"
        ),

        ["pwdsetup.title"] = ("moderate mode", "умеренный режим", "moderater modus"),
        ["pwdsetup.subtitle"] = (
            "needs a password exactly 40 characters long",
            "нужен пароль ровно на 40 символов",
            "benötigt ein passwort mit genau 40 zeichen"
        ),
        ["pwdsetup.placeholder"] = ("40-character password...", "40-значный пароль...", "40-stelliges passwort..."),
        ["pwdsetup.generate"] = ("generate", "сгенерировать", "generieren"),
        ["pwdsetup.length_error"] = ("needs exactly 40 characters (currently {0})", "нужно ровно 40 символов (сейчас {0})", "genau 40 zeichen nötig (aktuell {0})"),
        ["pwdsetup.length_status"] = ("length: {0}/40", "длина: {0}/40", "länge: {0}/40"),

        ["puzzlesetup.title"] = ("protected mode", "защищённый режим", "geschützter modus"),
        ["puzzlesetup.subtitle"] = ("a puzzle instead of a password", "головоломка вместо пароля", "ein rätsel statt eines passworts"),
        ["puzzlesetup.tab_question"] = ("question / answer", "вопрос / ответ", "frage / antwort"),
        ["puzzlesetup.tab_pattern"] = ("pattern key", "граф. ключ", "muster-schlüssel"),
        ["puzzlesetup.question_placeholder"] = ("your question...", "твой вопрос...", "deine frage..."),
        ["puzzlesetup.answer_placeholder"] = ("the correct answer...", "правильный ответ...", "die richtige antwort..."),
        ["puzzlesetup.pattern_hint"] = ("draw a key (at least 4 dots)", "нарисуй ключ (минимум 4 точки)", "zeichne ein muster (mindestens 4 punkte)"),
        ["puzzlesetup.pattern_too_short"] = (
            "too short, at least 4 dots — try again",
            "слишком коротко, минимум 4 точки — попробуй ещё раз",
            "zu kurz, mindestens 4 punkte — versuch's nochmal"
        ),
        ["puzzlesetup.pattern_first"] = (
            "got it. draw the same key again to confirm",
            "запомнил. нарисуй тот же ключ ещё раз для подтверждения",
            "gemerkt. zeichne dasselbe muster nochmal zur bestätigung"
        ),
        ["puzzlesetup.pattern_confirmed"] = (
            "key confirmed — hit \"encrypt\" below",
            "ключ подтверждён — жми \"зашифровать\" внизу",
            "muster bestätigt — unten auf \"verschlüsseln\" klicken"
        ),
        ["puzzlesetup.pattern_mismatch"] = ("didn't match, start over", "не совпало, начни заново", "stimmt nicht überein, von vorne"),
        ["puzzlesetup.warn_fill_both"] = ("fill in both the question and the answer", "заполни вопрос и ответ", "fülle frage und antwort aus"),
        ["puzzlesetup.warn_confirm_pattern"] = (
            "draw and confirm the pattern key",
            "нарисуй и подтверди графический ключ",
            "zeichne und bestätige das muster"
        ),
        ["puzzlesetup.stored_hint"] = ("Repeat the pattern key", "Повтори графический ключ доступа", "Wiederhole den Muster-Schlüssel"),

        ["pwdprompt.title"] = ("password required", "требуется пароль", "passwort erforderlich"),
        ["pwdprompt.subtitle"] = ("moderate protection level", "умеренный уровень защиты", "moderates schutzniveau"),
        ["pwdprompt.placeholder"] = ("password...", "пароль...", "passwort..."),
        ["pwdprompt.attempts"] = ("attempts left: {0}", "осталось попыток: {0}", "verbleibende versuche: {0}"),

        ["puzzlesolve.title"] = ("puzzle", "головоломка", "rätsel"),
        ["puzzlesolve.wrong_notice"] = ("wrong answer, try again", "неверный ответ, попробуй ещё раз", "falsche antwort, versuch's nochmal"),
        ["puzzlesolve.answer_placeholder"] = ("answer...", "ответ...", "antwort..."),

        ["progress.title"] = ("Extracting…", "Идёт разархивирование…", "Wird entpackt…"),

        ["readme.body"] = (
            "snowy is a tiny local file encryptor for anyone who can't be bothered\n" +
            "with settings. drop a file — pick a protection level — done.\n\n" +
            "three modes to choose from:\n" +
            "simple — instant, no password, for a quick \"hide from prying eyes\"\n" +
            "moderate — an honest 40-character password with real protection\n" +
            "protected — a puzzle instead of a password: question/answer or your own pattern key\n\n" +
            "everything runs locally: its own format (.sny / .snow), its own cipher, integrity\n" +
            "checked via HMAC. no servers, no files sent anywhere —\n" +
            "whatever you encrypt stays only with you.\n\n" +
            "drag a file into the window below to try it out.",

            "snowy — маленький локальный шифровальщик файлов для тех, кому лень\n" +
            "разбираться с настройками. кинул файл — выбрал уровень защиты — готово.\n\n" +
            "три режима на выбор:\n" +
            "простой — мгновенно, без пароля, для быстрого \"убрать с глаз\"\n" +
            "умеренный — честный 40-значный пароль с настоящей защитой\n" +
            "защищённый — головоломка вместо пароля: вопрос-ответ или свой графический ключ\n\n" +
            "всё работает локально: свой формат (.sny / .snow), свой шифр, проверка\n" +
            "целостности через HMAC. никаких серверов и отправки файлов куда-либо —\n" +
            "всё, что ты шифруешь, остаётся только у тебя.\n\n" +
            "перетащи файл в окно ниже, чтобы попробовать.",

            "snowy ist ein kleiner lokaler datei-verschlüsseler für alle, die keine\n" +
            "lust auf einstellungen haben. datei reinziehen — schutzstufe wählen — fertig.\n\n" +
            "drei modi zur auswahl:\n" +
            "einfach — sofort, ohne passwort, um schnell etwas \"aus dem blick\" zu bekommen\n" +
            "moderat — ein ehrliches 40-stelliges passwort mit echtem schutz\n" +
            "geschützt — ein rätsel statt eines passworts: frage/antwort oder ein eigenes muster\n\n" +
            "alles läuft lokal: eigenes format (.sny / .snow), eigene chiffre, integritäts-\n" +
            "prüfung per HMAC. keine server, keine dateien werden irgendwohin gesendet —\n" +
            "alles, was du verschlüsselst, bleibt nur bei dir.\n\n" +
            "zieh eine datei ins fenster unten, um es auszuprobieren."
        ),
    };
}