using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MudBlazor;
using System.Xml.Linq;
using Novels.Components.Pages;
using Novels.Services;
using PetaPoco;
using System.Data;
using Tetr4lab;

namespace Novels.Data;

[TableName ("settings")]
public class Setting : NovelsBaseModel<Setting>, INovelsBaseModel {

    /// <inheritdoc/>
    public static string TableLabel => "設定";

    /// <inheritdoc/>
    public static Dictionary<string, string> Label { get; } = new () {
        { nameof (Id), "ID" },
        { nameof (Created), "生成日時" },
        { nameof (Modified), "更新日時" },
        { nameof (PersonalDocumentLimitSize), "制限サイズ" },
        { nameof (SmtpMailAddress), "FROM" },
        { nameof (SmtpServer), "メールサーバ" },
        { nameof (SmtpPort), "ポート" },
        { nameof (SmtpUserName), "ユーザ名" },
        { nameof (SmtpPassword), "パスワード" },
        { nameof (SmtpMailto), "TO" },
        { nameof (SmtpCc), "CC" },
        { nameof (SmtpBcc), "BCC" },
        { nameof (SmtpSubject), "表題" },
        { nameof (SmtpBody), "本文" },
        { nameof (UserAgent), "HTTP-UA" },
        { nameof (AccessIntervalTime), "アクセス間隔(ms)" },
        { nameof (DefaultCookiesJson), "クッキー(json)" },
        { nameof (IncludeImage), "画像を含める" },
        { nameof (Remarks), "備考" },
    };

    /// <inheritdoc/>
    public static string BaseSelectSql => @$"select * from `settings`;";

    /// <inheritdoc/>
    public static string UniqueKeysSql => "";

    [Column ("personal_document_limit_size"), Required] public int PersonalDocumentLimitSize { get; set; } = 0;
    [Column ("smtp_mailaddress")] public string SmtpMailAddress { get; set; } = "";
    [Column ("smtp_server")] public string SmtpServer { get; set; } = "";
    [Column ("smtp_port")] public int SmtpPort { get; set; } = 25;
    [Column ("smtp_username")] public string SmtpUserName { get; set; } = "";
    [Column ("smtp_password")] public string SmtpPassword { get; set; } = "";
    [Column ("smtp_mailto")] public string SmtpMailto { get; set; } = "";
    [Column ("smtp_cc")] public string SmtpCc { get; set; } = "";
    [Column ("smtp_bcc")] public string SmtpBcc { get; set; } = "";
    [Column ("smtp_subject")] public string SmtpSubject { get; set; } = "";
    [Column ("smtp_body")] public string SmtpBody { get; set; } = "";
    [Column ("user_agent")] public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko";
    [Column ("access_interval_time")] public int AccessIntervalTime { get; set; } = 1000;
    [Column ("default_cookies")] public string DefaultCookiesJson { get; set; } = "{ \"over18\": \"yes\" }";
    [Column ("include_image")] public bool IncludeImage { get; set; } = false;

    /// <summary>デフォルトクッキーの辞書表現</summary>
    public Dictionary<string, string> DefaultCookies {
        get {
            try {
                return JsonSerializer.Deserialize<Dictionary<string, string>> (DefaultCookiesJson) ?? new ();
            }
            catch {
            }
            return new ();
        }
    }

    /// <inheritdoc/>
    public override string? [] SearchTargets => [ ];

    /// <summary>ノーマルコンストラクタ</summary>
    public Setting () { }

    /// <inheritdoc/>
    public override Setting Clone () {
        var item = base.Clone ();
        item.PersonalDocumentLimitSize = PersonalDocumentLimitSize;
        item.SmtpMailAddress = SmtpMailAddress;
        item.SmtpServer = SmtpServer;
        item.SmtpPort = SmtpPort;
        item.SmtpUserName = SmtpUserName;
        item.SmtpPassword = SmtpPassword;
        item.SmtpMailto = SmtpMailto;
        item.SmtpCc = SmtpCc;
        item.SmtpBcc = SmtpBcc;
        item.SmtpSubject = SmtpSubject;
        item.SmtpBody = SmtpBody;
        item.UserAgent = UserAgent;
        item.AccessIntervalTime = AccessIntervalTime;
        item.DefaultCookiesJson = DefaultCookiesJson;
        item.IncludeImage = IncludeImage;
        return item;
    }

    /// <inheritdoc/>
    public override Setting CopyTo (Setting destination) {
        destination.PersonalDocumentLimitSize = PersonalDocumentLimitSize;
        destination.SmtpMailAddress = SmtpMailAddress;
        destination.SmtpServer = SmtpServer;
        destination.SmtpPort = SmtpPort;
        destination.SmtpUserName = SmtpUserName;
        destination.SmtpPassword = SmtpPassword;
        destination.SmtpMailto = SmtpMailto;
        destination.SmtpCc = SmtpCc;
        destination.SmtpBcc = SmtpBcc;
        destination.SmtpSubject = SmtpSubject;
        destination.SmtpBody = SmtpBody;
        destination.UserAgent = UserAgent;
        destination.AccessIntervalTime = AccessIntervalTime;
        destination.DefaultCookiesJson = DefaultCookiesJson;
        destination.IncludeImage = IncludeImage;
        return base.CopyTo (destination);
    }

    /// <inheritdoc/>
    public override bool Equals (Setting? other) =>
        other != null
        && Id == other.Id
        && PersonalDocumentLimitSize == other.PersonalDocumentLimitSize
        && SmtpMailAddress == other.SmtpMailAddress
        && SmtpServer == other.SmtpServer
        && SmtpPort == other.SmtpPort
        && SmtpUserName == other.SmtpUserName
        && SmtpPassword == other.SmtpPassword
        && SmtpMailto == other.SmtpMailto
        && SmtpCc == other.SmtpCc
        && SmtpBcc == other.SmtpBcc
        && SmtpSubject == other.SmtpSubject
        && SmtpBody == other.SmtpBody
        && UserAgent == other.UserAgent
        && AccessIntervalTime == other.AccessIntervalTime
        && DefaultCookiesJson == other.DefaultCookiesJson
        && IncludeImage == other.IncludeImage
        && Remarks == other.Remarks
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (
        HashCode.Combine (PersonalDocumentLimitSize, SmtpMailAddress, SmtpServer, SmtpPort, SmtpUserName, SmtpPassword, SmtpMailto, SmtpCc),
        HashCode.Combine (SmtpBcc, SmtpSubject, SmtpBody, Remarks, UserAgent, AccessIntervalTime, DefaultCookiesJson, IncludeImage),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {PersonalDocumentLimitSize}, {SmtpMailAddress}, {SmtpServer}, {SmtpPort}, {SmtpUserName}, {SmtpMailto}, {SmtpCc}, {SmtpBcc}, {SmtpSubject}, {SmtpBody}, {(IncludeImage ? "withImage" : "withoutImage")}, \"{Remarks}\"";
}
