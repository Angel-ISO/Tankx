using backend.domain.entities;
using backend.persistence;
using Microsoft.EntityFrameworkCore;

namespace backend.api.Services;

public class IndexAnalysisService
{
    private readonly TankxContext _context;

    public IndexAnalysisService(TankxContext context)
    {
        _context = context;
    }

    public class IndexInfo
    {
        public string IndexName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string Columns { get; set; } = string.Empty;
        public bool IsUnique { get; set; }
        public string IndexType { get; set; } = string.Empty;
    }

    public class QueryPlanInfo
    {
        public string Query { get; set; } = string.Empty;
        public string ExecutionPlan { get; set; } = string.Empty;
        public bool UsesIndex { get; set; }
        public string? IndexUsed { get; set; }
        public double EstimatedCost { get; set; }
        public double ExecutionTimeMs { get; set; }
    }

    public async Task<List<IndexInfo>> GetAllIndexesAsync()
    {
        var indexes = new List<IndexInfo>();

        var sql = @"
            SELECT
                i.relname as index_name,
                t.relname as table_name,
                (SELECT array_to_string(array_agg(a.attname ORDER BY k.ord), ', ')
                 FROM unnest(ix.indkey) WITH ORDINALITY AS k(attnum, ord)
                 JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum) as columns,
                ix.indisunique as is_unique,
                am.amname as index_type
            FROM pg_class t
            JOIN pg_index ix ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_am am ON i.relam = am.oid
            WHERE t.relname IN ('Profiles', 'Matches', 'MatchParticipants')
            ORDER BY t.relname, i.relname";

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            await _context.Database.OpenConnectionAsync();
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexes.Add(new IndexInfo
                {
                    IndexName = reader.GetString(0),
                    TableName = reader.GetString(1),
                    Columns = reader.GetString(2),
                    IsUnique = reader.GetBoolean(3),
                    IndexType = reader.GetString(4)
                });
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return indexes;
    }

    public async Task<QueryPlanInfo> AnalyzeQueryPlanAsync(string query)
    {
        var planInfo = new QueryPlanInfo
        {
            Query = query
        };

        var explainSql = $"EXPLAIN (ANALYZE, FORMAT JSON, BUFFERS) {query}";

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = explainSql;
            await _context.Database.OpenConnectionAsync();
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var planJson = reader.GetString(0);
                planInfo.ExecutionPlan = planJson;
                
              
                planInfo.UsesIndex = planJson.Contains("Index Scan") || planJson.Contains("Index Only Scan") || planJson.Contains("Bitmap Index Scan");
                
                if (planInfo.UsesIndex)
                {
                    var indexMatch = System.Text.RegularExpressions.Regex.Match(planJson, @"""Index Name"":\s*""([^""]+)""");
                    if (indexMatch.Success)
                    {
                        planInfo.IndexUsed = indexMatch.Groups[1].Value;
                    }
                }

                var timeMatch = System.Text.RegularExpressions.Regex.Match(planJson, @"""Execution Time"":\s*([0-9.]+)");
                if (timeMatch.Success)
                {
                    planInfo.ExecutionTimeMs = double.Parse(timeMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return planInfo;
    }

    public async Task<QueryPlanInfo> AnalyzeDisplayNameSearchAsync()
    {
        var query = "SELECT \"Id\", \"DisplayName\" FROM \"Profiles\" WHERE \"DisplayName\" LIKE '%test%'";
        return await AnalyzeQueryPlanAsync(query);
    }

    public async Task<QueryPlanInfo> AnalyzeWinnerIdQueryAsync()
    {
        var winnerId = await _context.Matches
            .Where(m => m.WinnerId != null)
            .Select(m => m.WinnerId)
            .FirstOrDefaultAsync();

        if (winnerId == null)
        {
            return new QueryPlanInfo
            {
                Query = "SELECT \"Id\", \"WinnerId\" FROM \"Matches\" WHERE \"WinnerId\" IS NOT NULL",
                ExecutionPlan = "No winner data found to analyze",
                UsesIndex = false
            };
        }

        var query = $"SELECT \"Id\", \"WinnerId\" FROM \"Matches\" WHERE \"WinnerId\" = '{winnerId}'";
        return await AnalyzeQueryPlanAsync(query);
    }

    public async Task<QueryPlanInfo> AnalyzeMatchParticipantQueryAsync()
    {
        var matchId = await _context.Matches
            .OrderByDescending(m => m.StartedAt)
            .Select(m => m.Id)
            .FirstOrDefaultAsync();

        if (matchId == Guid.Empty)
        {
            return new QueryPlanInfo
            {
                Query = "SELECT mp.\"Id\" FROM \"MatchParticipants\" mp",
                ExecutionPlan = "No match data found to analyze",
                UsesIndex = false
            };
        }

        var query = $@"
            SELECT mp.""Id"", mp.""MatchId"", mp.""ProfileId""
            FROM ""MatchParticipants"" mp
            WHERE mp.""MatchId"" = '{matchId}'";
        return await AnalyzeQueryPlanAsync(query);
    }

    public async Task<Dictionary<string, object>> GetIndexStatisticsAsync()
    {
        var stats = new Dictionary<string, object>();

        var sql = @"
            SELECT 
                schemaname,
                relname as tablename,
                indexrelname as indexname,
                idx_scan as index_scans,
                idx_tup_read as tuples_read,
                idx_tup_fetch as tuples_fetched
            FROM pg_stat_user_indexes
            WHERE relname IN ('Profiles', 'Matches', 'MatchParticipants')
            ORDER BY idx_scan DESC";

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            await _context.Database.OpenConnectionAsync();
            
            var indexStats = new List<Dictionary<string, object>>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexStats.Add(new Dictionary<string, object>
                {
                    ["schema"] = reader.GetString(0),
                    ["table"] = reader.GetString(1),
                    ["index"] = reader.GetString(2),
                    ["scans"] = reader.GetInt64(3),
                    ["tuples_read"] = reader.GetInt64(4),
                    ["tuples_fetched"] = reader.GetInt64(5)
                });
            }
            stats["index_usage"] = indexStats;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        var tableSizeSql = @"
            SELECT 
                tablename,
                pg_size_pretty(pg_total_relation_size(format('%I.%I', schemaname, tablename))) as size
            FROM pg_tables
            WHERE tablename IN ('Profiles', 'Matches', 'MatchParticipants')";

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = tableSizeSql;
            await _context.Database.OpenConnectionAsync();
            
            var tableSizes = new List<Dictionary<string, object>>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tableSizes.Add(new Dictionary<string, object>
                {
                    ["table"] = reader.GetString(0),
                    ["size"] = reader.GetString(1)
                });
            }
            stats["table_sizes"] = tableSizes;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return stats;
    }
}
