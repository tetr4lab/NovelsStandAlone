using Microsoft.Data.Sqlite;
using Tetr4lab;

namespace Novels.Data;

/// <summary>内部で使用する例外</summary>
[Serializable]
public class SqliteDataSetException : Exception {
    /// <summary>コンストラクタ</summary>
    public SqliteDataSetException () : base () { }
    /// <summary>コンストラクタ</summary>
    public SqliteDataSetException (string message) : base (message) { }
    /// <summary>コンストラクタ</summary>
    public SqliteDataSetException (string message, Exception innerException) : base (message, innerException) { }
}

/// <summary>例外をエラーに変換するクラス</summary>
public static class ExceptionToErrorHelper {
    /// <summary>例外メッセージからエラーへの変換</summary>
    public static readonly Dictionary<(Type type, string message), Status> ExceptionToErrorDictionary = new () {
        { (typeof (SqliteDataSetException), "Missing entry"), Status.MissingEntry },
        { (typeof (SqliteDataSetException), "Duplicate entry"), Status.DuplicateEntry },
        { (typeof (SqliteDataSetException), "The Command Timeout expired"), Status.CommandTimeout },
        { (typeof (SqliteDataSetException), "Version mismatch"), Status.VersionMismatch },
        { (typeof (SqliteDataSetException), "Cannot add or update a child row: a foreign key constraint fails"), Status.ForeignKeyConstraintFails },
        { (typeof (SqliteDataSetException), "Deadlock found"), Status.DeadlockFound },
        { (typeof (SqliteException), "UNIQUE constraint failed"), Status.DuplicateEntry },
        { (typeof (SqliteException), "locked"), Status.CommandTimeout },
        { (typeof (SqliteException), "Version mismatch"), Status.VersionMismatch },
        { (typeof (SqliteException), "FOREIGN KEY constraint failed"), Status.ForeignKeyConstraintFails },
    };
    /// <summary>例外がエラーか判定して該当するエラー状態を出力する</summary>
    /// <param name="ex"></param>
    /// <param name="status"></param>
    /// <returns></returns>
    public static bool TryGetStatus (this Exception ex, out Status status) {
        foreach (var pair in ExceptionToErrorDictionary) {
            if (ex.GetType () == pair.Key.type && ex.Message.StartsWith (pair.Key.message, StringComparison.CurrentCultureIgnoreCase)) {
                status = pair.Value;
                return true;
            }
        }
        status = Status.Unknown;
        return false;
    }
    /// <summary>例外はデッドロックである</summary>
    /// <param name="ex"></param>
    /// <returns></returns>
    public static bool IsDeadLock (this Exception ex) => false;
    /// <summary>逆引き</summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static Exception GetException (this Status status) {
        if (ExceptionToErrorDictionary.ContainsValue (status)) {
            return new SqliteDataSetException (ExceptionToErrorDictionary.First (p => p.Value == status).Key.message);
        }
        return new Exception ("Unknown exception");
    }
}
