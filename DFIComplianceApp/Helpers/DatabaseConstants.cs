// Helpers/DatabaseConstants.cs
using System.IO;
using Microsoft.Maui.Storage;      // gives FileSystem.AppDataDirectory

namespace DFIComplianceApp;

/// <summary>
/// Single source of truth for the SQLite file‑path so every class
/// (DI registration, static fall‑backs, debug tools, etc.) reads &
/// writes the same physical database.
/// </summary>
public static class DatabaseConstants
{
    public const string FileName = "dfi_management.db";

    /// <summary>
    /// Full path in the per‑app data folder, cross‑platform.
    /// </summary>
    public static string DbPath =>
        Path.Combine(FileSystem.AppDataDirectory, FileName);
}
