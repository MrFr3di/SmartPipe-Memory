namespace SmartPipe.Memory.Storage;

/// <summary>
/// SQL schema for SQLite-based graph persistence.
/// Uses WAL mode for concurrent reads and writes.
/// All SQL is parameterized to prevent injection.
/// </summary>
public static class SqliteSchema
{
    /// <summary>
    /// Complete DDL for creating tables, indexes, and views.
    /// Idempotent — uses IF NOT EXISTS.
    /// </summary>
    public const string CreateTables =
        @"
        PRAGMA journal_mode = WAL;
        PRAGMA foreign_keys = ON;
        PRAGMA cache_size = -64000;

        CREATE TABLE IF NOT EXISTS nodes (
            id                  TEXT PRIMARY KEY NOT NULL,
            type                TEXT NOT NULL,
            label               TEXT NOT NULL DEFAULT '',
            properties          TEXT NOT NULL DEFAULT '{}',
            health_score        REAL NOT NULL DEFAULT 1.0,
            failure_prob        REAL NOT NULL DEFAULT 0.0,
            predicted_latency_ms REAL NOT NULL DEFAULT 0.0,
            resource_strain     REAL NOT NULL DEFAULT 0.0,
            valid_from          TEXT NOT NULL,
            valid_to            TEXT,
            tx_time             TEXT NOT NULL,
            version             INTEGER NOT NULL DEFAULT 1
        ) WITHOUT ROWID;

        CREATE TABLE IF NOT EXISTS edges (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            from_node_id    TEXT NOT NULL REFERENCES nodes(id) ON DELETE CASCADE,
            to_node_id      TEXT NOT NULL REFERENCES nodes(id) ON DELETE CASCADE,
            type            TEXT NOT NULL,
            weight          REAL NOT NULL DEFAULT 1.0,
            confidence      REAL NOT NULL DEFAULT 1.0,
            steps           TEXT NOT NULL DEFAULT '[]',
            valid_from      TEXT NOT NULL,
            valid_to        TEXT,
            tx_time         TEXT NOT NULL,
            UNIQUE(from_node_id, to_node_id, type)
        );

        CREATE TABLE IF NOT EXISTS insights (
            id              TEXT PRIMARY KEY NOT NULL,
            type            TEXT NOT NULL,
            title           TEXT NOT NULL,
            description     TEXT,
            related_node_ids TEXT NOT NULL DEFAULT '[]',
            confidence      REAL NOT NULL DEFAULT 0.0,
            severity        TEXT NOT NULL DEFAULT 'Info',
            generated_at    TEXT NOT NULL,
            dismissed_at    TEXT DEFAULT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_nodes_type ON nodes(type);
        CREATE INDEX IF NOT EXISTS idx_nodes_type_health ON nodes(type, health_score);
        CREATE INDEX IF NOT EXISTS idx_nodes_health ON nodes(health_score);
        CREATE INDEX IF NOT EXISTS idx_edges_from_type ON edges(from_node_id, type);
        CREATE INDEX IF NOT EXISTS idx_edges_to ON edges(to_node_id);
        CREATE INDEX IF NOT EXISTS idx_insights_type ON insights(type);

        CREATE VIEW IF NOT EXISTS v_degraded_nodes AS
        SELECT id, type, label, health_score, failure_prob, predicted_latency_ms, resource_strain
        FROM nodes
        WHERE health_score < 0.7 AND valid_to IS NULL
        ORDER BY health_score ASC;
    ";
}
