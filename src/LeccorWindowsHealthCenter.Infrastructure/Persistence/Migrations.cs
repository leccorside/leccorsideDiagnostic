namespace LeccorWindowsHealthCenter.Infrastructure.Persistence;

public sealed record Migration(int Version, string Name, string Sql);

/// <summary>
/// Migrations versionadas do banco (seção 66). Regra: NUNCA editar uma migration já publicada —
/// sempre adicionar uma nova versão no fim da lista.
/// </summary>
public static class Migrations
{
    public static IReadOnlyList<Migration> All { get; } =
    [
        new(1, "Esquema inicial", """
            CREATE TABLE Scan (
                Id                 TEXT PRIMARY KEY,
                Mode               TEXT NOT NULL,
                StartedAt          TEXT NOT NULL,
                CompletedAt        TEXT NOT NULL,
                WasCancelled       INTEGER NOT NULL,
                Score              INTEGER NULL,
                Rating             TEXT NOT NULL,
                MachineName        TEXT NOT NULL,
                CategoryScoresJson TEXT NOT NULL
            );
            CREATE INDEX IX_Scan_StartedAt ON Scan (StartedAt DESC);

            CREATE TABLE ScannerRun (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                ScanId      TEXT NOT NULL REFERENCES Scan (Id) ON DELETE CASCADE,
                ScannerId   TEXT NOT NULL,
                ScannerName TEXT NOT NULL,
                Module      TEXT NOT NULL,
                Status      TEXT NOT NULL,
                DurationMs  INTEGER NOT NULL,
                Error       TEXT NULL
            );
            CREATE INDEX IX_ScannerRun_ScanId ON ScannerRun (ScanId);

            CREATE TABLE Finding (
                Id                TEXT PRIMARY KEY,
                ScanId            TEXT NOT NULL REFERENCES Scan (Id) ON DELETE CASCADE,
                RuleId            TEXT NOT NULL,
                ScannerId         TEXT NOT NULL,
                Category          TEXT NOT NULL,
                Title             TEXT NOT NULL,
                Description       TEXT NOT NULL,
                Severity          INTEGER NOT NULL,
                Confidence        INTEGER NOT NULL,
                ProbableCause     TEXT NULL,
                Impact            TEXT NOT NULL,
                Recommendation    TEXT NOT NULL,
                RepairActionId    TEXT NULL,
                RepairRisk        INTEGER NULL,
                RequiresElevation INTEGER NOT NULL,
                RequiresRestart   INTEGER NOT NULL,
                SupportsRollback  INTEGER NOT NULL,
                Status            TEXT NOT NULL,
                DetectedAt        TEXT NOT NULL
            );
            CREATE INDEX IX_Finding_ScanId ON Finding (ScanId);
            CREATE INDEX IX_Finding_RuleId ON Finding (RuleId);

            CREATE TABLE Evidence (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                FindingId   TEXT NOT NULL REFERENCES Finding (Id) ON DELETE CASCADE,
                Source      TEXT NOT NULL,
                Description TEXT NOT NULL,
                Value       REAL NULL,
                Unit        TEXT NULL,
                Reference   TEXT NULL,
                SampleCount INTEGER NOT NULL,
                CollectedAt TEXT NOT NULL
            );
            CREATE INDEX IX_Evidence_FindingId ON Evidence (FindingId);

            CREATE TABLE Metric (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                ScanId    TEXT NOT NULL REFERENCES Scan (Id) ON DELETE CASCADE,
                ScannerId TEXT NOT NULL,
                Name      TEXT NOT NULL,
                Value     REAL NOT NULL,
                Unit      TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                TagsJson  TEXT NULL
            );
            CREATE INDEX IX_Metric_ScanId_Name ON Metric (ScanId, Name);

            CREATE TABLE Repair (
                Id               TEXT PRIMARY KEY,
                ActionId         TEXT NOT NULL,
                ActionTitle      TEXT NOT NULL,
                FindingId        TEXT NULL,
                Risk             INTEGER NOT NULL,
                Outcome          TEXT NOT NULL,
                StartedAt        TEXT NOT NULL,
                CompletedAt      TEXT NULL,
                UserName         TEXT NOT NULL,
                Reason           TEXT NULL,
                Message          TEXT NOT NULL,
                BackupId         TEXT NULL,
                SupportsRollback INTEGER NOT NULL,
                ChangesJson      TEXT NOT NULL,
                ComparisonJson   TEXT NULL,
                RolledBackAt     TEXT NULL
            );
            CREATE INDEX IX_Repair_StartedAt ON Repair (StartedAt DESC);

            CREATE TABLE "Rollback" (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                RepairId   TEXT NOT NULL REFERENCES Repair (Id) ON DELETE CASCADE,
                ExecutedAt TEXT NOT NULL,
                Success    INTEGER NOT NULL,
                Message    TEXT NOT NULL
            );

            CREATE TABLE HardwareSnapshot (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                ScanId     TEXT NULL REFERENCES Scan (Id) ON DELETE SET NULL,
                CapturedAt TEXT NOT NULL,
                DataJson   TEXT NOT NULL
            );

            CREATE TABLE EventSummary (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                ScanId     TEXT NOT NULL REFERENCES Scan (Id) ON DELETE CASCADE,
                LogName    TEXT NOT NULL,
                Provider   TEXT NOT NULL,
                EventId    INTEGER NOT NULL,
                Level      TEXT NOT NULL,
                Count      INTEGER NOT NULL,
                Last24h    INTEGER NOT NULL,
                FirstSeen  TEXT NULL,
                LastSeen   TEXT NULL
            );
            CREATE INDEX IX_EventSummary_ScanId ON EventSummary (ScanId);

            CREATE TABLE Application (
                Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                Name               TEXT NOT NULL,
                Version            TEXT NULL,
                Publisher          TEXT NULL,
                InstallLocation    TEXT NULL,
                EstimatedSizeBytes INTEGER NULL,
                FirstSeen          TEXT NOT NULL,
                LastSeen           TEXT NOT NULL,
                UNIQUE (Name, Version, Publisher)
            );

            CREATE TABLE Configuration (
                Key       TEXT PRIMARY KEY,
                Value     TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """),
    ];
}
