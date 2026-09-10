using Npgsql;

namespace LucyAPI.Data.Repositories;

/// <summary>
/// Admin-specific queries that need user-scoped access with shared object awareness.
/// These queries mirror the Python admin_resources.py inline SQL.
/// </summary>
public sealed class AdminRepository(NpgsqlDataSource dataSource)
{
    // ── Agents with last session ──────────────────────────────────

    public async Task<List<(int AgentId, string Name, int? SessionId, DateTimeOffset? StartedAt, string? Project)>>
        ListAgentsWithLastSessionAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT a.agent_id, a.name,
                   s.session_id, s.started_at, s.project
            FROM agents a
            LEFT JOIN LATERAL (
                SELECT session_id, started_at, project
                FROM sessions
                WHERE agent_id = a.agent_id
                ORDER BY started_at DESC
                LIMIT 1
            ) s ON true
            WHERE a.user_id = $1
            ORDER BY a.name
            """, conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, string, int?, DateTimeOffset?, string?)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)
            ));
        }
        return results;
    }

    // ── Projects with shared access ──────────────────────────────

    public async Task<List<(int ProjectId, string Title, string? Description, string? Status, string? StatusLabel, string Access, int PermissionLevel)>>
        ListProjectsWithAccessAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT p.project_id, p.title, p.description, ps.code as status, ps.label as status_label,
                   'owned' as access, 3 as permission_level
            FROM projects p
            JOIN project_statuses ps ON p.status_id = ps.status_id
            WHERE p.user_id = $1
            UNION ALL
            SELECT p.project_id, p.title, p.description, ps.code as status, ps.label as status_label,
                   'shared' as access, so.permission_level
            FROM projects p
            JOIN project_statuses ps ON p.status_id = ps.status_id
            JOIN shared_objects so ON so.object_id = p.project_id AND so.object_type_id = 1
            WHERE so.shared_to_user_id = $1
            ORDER BY project_id
            """, conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, string, string?, string?, string?, string, int)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6)
            ));
        }
        return results;
    }

    // ── Wikis with shared access ─────────────────────────────────

    public async Task<List<(int WikiId, string Title, string? Description, DateTimeOffset? UpdatedAt, string Access, int PermissionLevel)>>
        ListWikisWithAccessAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT wiki_id, title, description, updated_at, 'owned' as access, 3 as permission_level
            FROM wikis WHERE user_id = $1
            UNION ALL
            SELECT w.wiki_id, w.title, w.description, w.updated_at, 'shared' as access, so.permission_level
            FROM wikis w
            JOIN shared_objects so ON so.object_id = w.wiki_id AND so.object_type_id = 3
            WHERE so.shared_to_user_id = $1
            ORDER BY wiki_id
            """, conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, string, string?, DateTimeOffset?, string, int)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetString(4),
                reader.GetInt32(5)
            ));
        }
        return results;
    }

    // ── Hints with shared access ─────────────────────────────────

    public async Task<List<(int HintId, int ParentId, string Title, string? Description, int HintCategoryId, string Access, int PermissionLevel)>>
        ListHintsWithAccessAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT hint_id, parent_id, title, description, hint_category_id, 'owned' as access, 3 as permission_level
            FROM hints WHERE user_id = $1
            UNION ALL
            SELECT h.hint_id, h.parent_id, h.title, h.description, h.hint_category_id, 'shared' as access, so.permission_level
            FROM hints h
            JOIN shared_objects so ON so.object_id = h.hint_category_id AND so.object_type_id = 2
            WHERE so.shared_to_user_id = $1
            ORDER BY parent_id, hint_id
            """, conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, int, string, string?, int, string, int)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetInt32(6)
            ));
        }
        return results;
    }

    // ── Sharing ──────────────────────────────────────────────────

    public async Task<List<(int ShareId, int ObjectTypeId, string ObjectType, int ObjectId,
        int? SharedToUserId, string? SharedToName, int? SharedByUserId, string? SharedByName,
        int PermissionLevel, string? ObjectTitle)>>
        ListSharesByMeAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT so.share_id, so.object_type_id, ot.name as object_type, so.object_id,
                   so.shared_to_user_id, u.name as shared_to_name,
                   NULL::int as shared_by_user_id, NULL::text as shared_by_name,
                   so.permission_level,
                   COALESCE(p.title, h.title, w.title) as object_title
            FROM shared_objects so
            JOIN object_types ot ON so.object_type_id = ot.object_type_id
            JOIN users u ON so.shared_to_user_id = u.user_id
            LEFT JOIN projects p ON so.object_type_id = 1 AND so.object_id = p.project_id
            LEFT JOIN hints h ON so.object_type_id = 2 AND so.object_id = h.hint_id
            LEFT JOIN wikis w ON so.object_type_id = 3 AND so.object_id = w.wiki_id
            WHERE so.shared_by_user_id = $1
            ORDER BY so.object_type_id, so.object_id
            """, conn)
        {
            Parameters = { new() { Value = userId } }
        };
        return await ReadShareRows(cmd, ct);
    }

    public async Task<List<(int ShareId, int ObjectTypeId, string ObjectType, int ObjectId,
        int? SharedToUserId, string? SharedToName, int? SharedByUserId, string? SharedByName,
        int PermissionLevel, string? ObjectTitle)>>
        ListSharesToMeAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT so.share_id, so.object_type_id, ot.name as object_type, so.object_id,
                   NULL::int as shared_to_user_id, NULL::text as shared_to_name,
                   so.shared_by_user_id, u.name as shared_by_name,
                   so.permission_level,
                   COALESCE(p.title, h.title, w.title) as object_title
            FROM shared_objects so
            JOIN object_types ot ON so.object_type_id = ot.object_type_id
            JOIN users u ON so.shared_by_user_id = u.user_id
            LEFT JOIN projects p ON so.object_type_id = 1 AND so.object_id = p.project_id
            LEFT JOIN hints h ON so.object_type_id = 2 AND so.object_id = h.hint_id
            LEFT JOIN wikis w ON so.object_type_id = 3 AND so.object_id = w.wiki_id
            WHERE so.shared_to_user_id = $1
            ORDER BY so.object_type_id, so.object_id
            """, conn)
        {
            Parameters = { new() { Value = userId } }
        };
        return await ReadShareRows(cmd, ct);
    }

    private static async Task<List<(int ShareId, int ObjectTypeId, string ObjectType, int ObjectId,
        int? SharedToUserId, string? SharedToName, int? SharedByUserId, string? SharedByName,
        int PermissionLevel, string? ObjectTitle)>>
        ReadShareRows(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, int, string, int, int?, string?, int?, string?, int, string?)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                (int)reader.GetInt16(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                (int)reader.GetInt16(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)
            ));
        }
        return results;
    }

    // ── Secrets (key list with timestamps) ───────────────────────

    public async Task<List<(string Key, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt)>>
        ListSecretKeysAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT key, created_at, updated_at FROM secrets WHERE user_id = $1 ORDER BY key", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(string, DateTimeOffset?, DateTimeOffset?)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2)
            ));
        }
        return results;
    }

    // ── Share Update ───────────────────────────────────────────────

    public async Task<(int ShareId, int ObjectTypeId, int ObjectId, int SharedToUserId, int PermissionLevel)?>
        UpdateSharePermissionAsync(int shareId, int userId, int permissionLevel, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            UPDATE shared_objects SET permission_level = $1
            WHERE share_id = $2 AND shared_by_user_id = $3
            RETURNING share_id, object_type_id, object_id, shared_to_user_id, permission_level
            """, conn)
        {
            Parameters =
            {
                new() { Value = permissionLevel },
                new() { Value = shareId },
                new() { Value = userId }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4)
        );
    }

    // ── Dashboard CTE ────────────────────────────────────────────

    public async Task<(long Agents, long Projects, long Wikis, long HintCategories,
        long Secrets, long Images, long PendingHandoffs, long SharedToMe)>
        GetDashboardStatsAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            WITH agent_count AS (
                SELECT count(*) AS n FROM agents WHERE user_id = $1
            ),
            project_count AS (
                SELECT count(*) AS n FROM (
                    SELECT project_id FROM projects WHERE user_id = $1
                    UNION
                    SELECT object_id FROM shared_objects WHERE shared_to_user_id = $1 AND object_type_id = 1
                ) t
            ),
            wiki_count AS (
                SELECT count(*) AS n FROM (
                    SELECT wiki_id FROM wikis WHERE user_id = $1
                    UNION
                    SELECT object_id FROM shared_objects WHERE shared_to_user_id = $1 AND object_type_id = 3
                ) t
            ),
            hint_cat_count AS (
                SELECT count(*) AS n FROM (
                    SELECT DISTINCT hint_category_id FROM hints WHERE user_id = $1 AND parent_id = 0
                    UNION
                    SELECT object_id FROM shared_objects WHERE shared_to_user_id = $1 AND object_type_id = 2
                ) t
            ),
            secret_count AS (
                SELECT count(*) AS n FROM secrets WHERE user_id = $1
            ),
            image_count AS (
                SELECT count(*) AS n FROM images WHERE user_id = $1
            ),
            handoff_count AS (
                SELECT count(*) AS n FROM handoffs
                WHERE agent_id IN (SELECT agent_id FROM agents WHERE user_id = $1)
                AND picked_up_at IS NULL
            ),
            shared_to_me_count AS (
                SELECT count(*) AS n FROM shared_objects WHERE shared_to_user_id = $1
            )
            SELECT
                (SELECT n FROM agent_count) AS agents,
                (SELECT n FROM project_count) AS projects,
                (SELECT n FROM wiki_count) AS wikis,
                (SELECT n FROM hint_cat_count) AS hint_categories,
                (SELECT n FROM secret_count) AS secrets,
                (SELECT n FROM image_count) AS images,
                (SELECT n FROM handoff_count) AS pending_handoffs,
                (SELECT n FROM shared_to_me_count) AS shared_to_me
            """, conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetInt64(7)
        );
    }

    public async Task<List<(int SessionId, string AgentName, DateTimeOffset? StartedAt, string? Project)>>
        GetRecentSessionsAsync(int userId, int limit = 5, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT s.session_id, a.name AS agent_name, s.started_at, s.project
            FROM sessions s
            JOIN agents a ON a.agent_id = s.agent_id
            WHERE a.user_id = $1
            ORDER BY s.started_at DESC
            LIMIT $2
            """, conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = limit } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, string, DateTimeOffset?, string?)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)
            ));
        }
        return results;
    }
}
