using System.IO;
using Microsoft.Data.Sqlite;
using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Repositories;

public sealed class SteamCacheRepository : ISteamCacheRepository
{
    private readonly string _databasePath;

    public SteamCacheRepository()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appData, "OpenSteamAnalyzer");
        Directory.CreateDirectory(directory);
        _databasePath = Path.Combine(directory, "cache.db");
    }

    public async Task<CachedSteamLibrary?> GetLibraryAsync(string steamId64, CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        SteamProfile? profile = null;
        DateTimeOffset cachedAt = DateTimeOffset.MinValue;

        await using (var profileCommand = connection.CreateCommand())
        {
            profileCommand.CommandText = """
                SELECT steam_id, display_name, avatar_url, avatar_frame_url, avatar_frame_video_url, animated_avatar_url, animated_avatar_video_url, profile_background_url, profile_background_video_url, profile_url, country_code, state_text, level, cached_at
                FROM profiles
                WHERE steam_id = $steam_id
                """;
            profileCommand.Parameters.AddWithValue("$steam_id", steamId64);

            await using var reader = await profileCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                profile = new SteamProfile
                {
                    SteamId64 = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    AvatarUrl = reader.GetString(2),
                    AvatarFrameUrl = reader.GetString(3),
                    AvatarFrameVideoUrl = reader.GetString(4),
                    AnimatedAvatarUrl = reader.GetString(5),
                    AnimatedAvatarVideoUrl = reader.GetString(6),
                    ProfileBackgroundUrl = reader.GetString(7),
                    ProfileBackgroundVideoUrl = reader.GetString(8),
                    ProfileUrl = reader.GetString(9),
                    CountryCode = reader.GetString(10),
                    StateText = reader.GetString(11),
                    Level = reader.IsDBNull(12) ? null : reader.GetInt32(12)
                };
                cachedAt = DateTimeOffset.Parse(reader.GetString(13));
            }
        }

        if (profile is null)
        {
            return null;
        }

        var games = new List<SteamGame>();
        await using (var gamesCommand = connection.CreateCommand())
        {
            gamesCommand.CommandText = """
                SELECT app_id, name, playtime_minutes, recent_playtime_minutes, icon_url, last_played_unix
                FROM games
                WHERE steam_id = $steam_id
                ORDER BY playtime_minutes DESC, name
                """;
            gamesCommand.Parameters.AddWithValue("$steam_id", steamId64);

            await using var reader = await gamesCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                games.Add(new SteamGame
                {
                    AppId = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    PlaytimeMinutes = reader.GetInt32(2),
                    RecentPlaytimeMinutes = reader.GetInt32(3),
                    IconUrl = reader.GetString(4),
                    LastPlayedAt = reader.IsDBNull(5)
                        ? null
                        : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5))
                });
            }
        }

        return new CachedSteamLibrary
        {
            Profile = profile,
            Games = games,
            CachedAt = cachedAt
        };
    }

    public async Task SaveLibraryAsync(SteamProfile profile, IReadOnlyList<SteamGame> games, CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var cachedAt = DateTimeOffset.UtcNow.ToString("O");
        await using (var profileCommand = connection.CreateCommand())
        {
            profileCommand.Transaction = transaction;
            profileCommand.CommandText = """
                INSERT INTO profiles (steam_id, display_name, avatar_url, avatar_frame_url, avatar_frame_video_url, animated_avatar_url, animated_avatar_video_url, profile_background_url, profile_background_video_url, profile_url, country_code, state_text, level, cached_at)
                VALUES ($steam_id, $display_name, $avatar_url, $avatar_frame_url, $avatar_frame_video_url, $animated_avatar_url, $animated_avatar_video_url, $profile_background_url, $profile_background_video_url, $profile_url, $country_code, $state_text, $level, $cached_at)
                ON CONFLICT(steam_id) DO UPDATE SET
                    display_name = excluded.display_name,
                    avatar_url = excluded.avatar_url,
                    avatar_frame_url = excluded.avatar_frame_url,
                    avatar_frame_video_url = excluded.avatar_frame_video_url,
                    animated_avatar_url = excluded.animated_avatar_url,
                    animated_avatar_video_url = excluded.animated_avatar_video_url,
                    profile_background_url = excluded.profile_background_url,
                    profile_background_video_url = excluded.profile_background_video_url,
                    profile_url = excluded.profile_url,
                    country_code = excluded.country_code,
                    state_text = excluded.state_text,
                    level = excluded.level,
                    cached_at = excluded.cached_at
                """;
            profileCommand.Parameters.AddWithValue("$steam_id", profile.SteamId64);
            profileCommand.Parameters.AddWithValue("$display_name", profile.DisplayName);
            profileCommand.Parameters.AddWithValue("$avatar_url", profile.AvatarUrl);
            profileCommand.Parameters.AddWithValue("$avatar_frame_url", profile.AvatarFrameUrl);
            profileCommand.Parameters.AddWithValue("$avatar_frame_video_url", profile.AvatarFrameVideoUrl);
            profileCommand.Parameters.AddWithValue("$animated_avatar_url", profile.AnimatedAvatarUrl);
            profileCommand.Parameters.AddWithValue("$animated_avatar_video_url", profile.AnimatedAvatarVideoUrl);
            profileCommand.Parameters.AddWithValue("$profile_background_url", profile.ProfileBackgroundUrl);
            profileCommand.Parameters.AddWithValue("$profile_background_video_url", profile.ProfileBackgroundVideoUrl);
            profileCommand.Parameters.AddWithValue("$profile_url", profile.ProfileUrl);
            profileCommand.Parameters.AddWithValue("$country_code", profile.CountryCode);
            profileCommand.Parameters.AddWithValue("$state_text", profile.StateText);
            profileCommand.Parameters.AddWithValue("$level", (object?)profile.Level ?? DBNull.Value);
            profileCommand.Parameters.AddWithValue("$cached_at", cachedAt);
            await profileCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM games WHERE steam_id = $steam_id";
            deleteCommand.Parameters.AddWithValue("$steam_id", profile.SteamId64);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var game in games)
        {
            await using var gameCommand = connection.CreateCommand();
            gameCommand.Transaction = transaction;
            gameCommand.CommandText = """
                INSERT INTO games (steam_id, app_id, name, playtime_minutes, recent_playtime_minutes, icon_url, last_played_unix)
                VALUES ($steam_id, $app_id, $name, $playtime_minutes, $recent_playtime_minutes, $icon_url, $last_played_unix)
                """;
            gameCommand.Parameters.AddWithValue("$steam_id", profile.SteamId64);
            gameCommand.Parameters.AddWithValue("$app_id", game.AppId);
            gameCommand.Parameters.AddWithValue("$name", game.Name);
            gameCommand.Parameters.AddWithValue("$playtime_minutes", game.PlaytimeMinutes);
            gameCommand.Parameters.AddWithValue("$recent_playtime_minutes", game.RecentPlaytimeMinutes);
            gameCommand.Parameters.AddWithValue("$icon_url", game.IconUrl);
            gameCommand.Parameters.AddWithValue("$last_played_unix", game.LastPlayedAt?.ToUnixTimeSeconds() is long unixTime ? unixTime : DBNull.Value);
            await gameCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_databasePath}");
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS profiles (
                steam_id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                avatar_url TEXT NOT NULL,
                avatar_frame_url TEXT NOT NULL DEFAULT '',
                avatar_frame_video_url TEXT NOT NULL DEFAULT '',
                animated_avatar_url TEXT NOT NULL DEFAULT '',
                animated_avatar_video_url TEXT NOT NULL DEFAULT '',
                profile_background_url TEXT NOT NULL DEFAULT '',
                profile_background_video_url TEXT NOT NULL DEFAULT '',
                profile_url TEXT NOT NULL,
                country_code TEXT NOT NULL,
                state_text TEXT NOT NULL,
                level INTEGER NULL,
                cached_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS games (
                steam_id TEXT NOT NULL,
                app_id INTEGER NOT NULL,
                name TEXT NOT NULL,
                playtime_minutes INTEGER NOT NULL,
                recent_playtime_minutes INTEGER NOT NULL,
                icon_url TEXT NOT NULL,
                last_played_unix INTEGER NULL,
                PRIMARY KEY (steam_id, app_id)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureColumnAsync(connection, "profiles", "avatar_frame_url", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "profiles", "avatar_frame_video_url", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "profiles", "animated_avatar_url", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "profiles", "animated_avatar_video_url", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "profiles", "profile_background_url", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "profiles", "profile_background_video_url", "TEXT NOT NULL DEFAULT ''", cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = $"PRAGMA table_info({tableName})";
            await using var reader = await checkCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
