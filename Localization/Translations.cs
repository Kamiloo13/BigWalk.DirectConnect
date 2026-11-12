using System;
using System.Collections.Generic;

namespace BigWalk.DirectConnect.Localization;

/// <summary>All texts of the "Join via IP" section in one language.</summary>
internal sealed class LocStrings
{
    /// <summary>Section heading, upper-case like the game's own headings.</summary>
    public readonly string Heading;

    /// <summary>Placeholder of the address field (IP address or domain, with IPv4 and IPv6 examples).</summary>
    public readonly string AddressPlaceholder;

    /// <summary>Placeholder of the password field.</summary>
    public readonly string PasswordPlaceholder;

    /// <summary>Label of the join button.</summary>
    public readonly string Button;

    /// <summary>Shown in the address field when the input is neither an IP address nor a valid host name.</summary>
    public readonly string InvalidAddress;

    /// <summary>Shown in the password field when it was left empty.</summary>
    public readonly string PasswordRequired;

    /// <summary>Shown in the address field when a host name cannot be resolved via DNS.</summary>
    public readonly string HostNotFound;

    /// <summary>Creates a set of texts for one language.</summary>
    public LocStrings(string heading, string addressPlaceholder, string passwordPlaceholder, string button,
                      string invalidAddress, string passwordRequired, string hostNotFound)
    {
        Heading = heading;
        AddressPlaceholder = addressPlaceholder;
        PasswordPlaceholder = passwordPlaceholder;
        Button = button;
        InvalidAddress = invalidAddress;
        PasswordRequired = passwordRequired;
        HostNotFound = hostNotFound;
    }
}

/// <summary>Translation table, keyed by language code (see <see cref="LanguageCodes"/>).</summary>
internal static class Translations
{
    private static readonly Dictionary<string, LocStrings> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new("JOIN VIA IP:", "IP / domain, e.g. 192.168.1.10, 2001:db8::1", "Password", "Join",
                 "Invalid address", "Enter the password", "Host not found"),
        ["pl"] = new("DOŁĄCZ PRZEZ IP:", "IP / domena, np. 192.168.1.10, 2001:db8::1", "Hasło", "Dołącz",
                 "Nieprawidłowy adres", "Wpisz hasło", "Nie znaleziono hosta"),
        ["de"] = new("ÜBER IP BEITRETEN:", "IP / Domain, z. B. 192.168.1.10, 2001:db8::1", "Passwort", "Beitreten",
                 "Ungültige Adresse", "Passwort eingeben", "Host nicht gefunden"),
        ["fr"] = new("REJOINDRE PAR IP :", "IP / domaine, ex. 192.168.1.10, 2001:db8::1", "Mot de passe", "Rejoindre",
                 "Adresse invalide", "Saisissez le mot de passe", "Hôte introuvable"),
        ["es"] = new("UNIRSE POR IP:", "IP / dominio, p. ej. 192.168.1.10, 2001:db8::1", "Contraseña", "Unirse",
                 "Dirección no válida", "Introduce la contraseña", "Host no encontrado"),
        ["it"] = new("UNISCITI TRAMITE IP:", "IP / dominio, es. 192.168.1.10, 2001:db8::1", "Password", "Unisciti",
                 "Indirizzo non valido", "Inserisci la password", "Host non trovato"),
        ["pt"] = new("ENTRAR POR IP:", "IP / domínio, ex.: 192.168.1.10, 2001:db8::1", "Senha", "Entrar",
                 "Endereço inválido", "Digite a senha", "Host não encontrado"),
        ["ru"] = new("ПОДКЛЮЧИТЬСЯ ПО IP:", "IP / домен, напр. 192.168.1.10, 2001:db8::1", "Пароль", "Войти",
                 "Неверный адрес", "Введите пароль", "Хост не найден"),
        ["uk"] = new("ПРИЄДНАТИСЯ ЗА IP:", "IP / домен, напр. 192.168.1.10, 2001:db8::1", "Пароль", "Приєднатися",
                 "Неправильна адреса", "Введіть пароль", "Хост не знайдено"),
        ["cs"] = new("PŘIPOJIT PŘES IP:", "IP / doména, např. 192.168.1.10, 2001:db8::1", "Heslo", "Připojit",
                 "Neplatná adresa", "Zadejte heslo", "Hostitel nenalezen"),
        ["tr"] = new("IP İLE KATIL:", "IP / alan adı, ör. 192.168.1.10, 2001:db8::1", "Şifre", "Katıl",
                 "Geçersiz adres", "Şifreyi girin", "Sunucu bulunamadı"),
        ["ja"] = new("IPで参加：", "IP / ドメイン（例：192.168.1.10、2001:db8::1）", "パスワード", "参加",
                 "無効なアドレス", "パスワードを入力してください", "ホストが見つかりません"),
        ["ko"] = new("IP로 참가:", "IP / 도메인 (예: 192.168.1.10, 2001:db8::1)", "비밀번호", "참가",
                 "잘못된 주소", "비밀번호를 입력하세요", "호스트를 찾을 수 없습니다"),
        ["zh-Hans"] = new("通过 IP 加入：", "IP / 域名，例如 192.168.1.10、2001:db8::1", "密码", "加入",
                      "无效的地址", "请输入密码", "找不到主机"),
        ["zh-Hant"] = new("透過 IP 加入：", "IP / 網域，例如 192.168.1.10、2001:db8::1", "密碼", "加入",
                      "無效的位址", "請輸入密碼", "找不到主機"),
    };

    /// <summary>All supported language codes.</summary>
    public static IEnumerable<string> Codes => Table.Keys;

    /// <summary>Looks up the texts for a language code (case-insensitive).</summary>
    public static bool TryGet(string code, out LocStrings strings) => Table.TryGetValue(code ?? "", out strings);
}
