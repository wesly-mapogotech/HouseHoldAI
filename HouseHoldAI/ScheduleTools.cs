using ModelContextProtocol.Server;
using Npgsql;
using System.ComponentModel;
using System.Text.Json;

namespace HouseHoldAI
{
    [McpServerToolType]
    public sealed class ScheduleTools(NpgsqlDataSource dataSource)
    {
        [McpServerTool]
        [Description(
            "Mengambil semua tugas rumah tangga yang dijadwalkan hari ini " +
            "berdasarkan zona waktu Asia/Jakarta. Tool ini hanya membaca data.")]
        public async Task<string> GetTodaySchedule(
            CancellationToken cancellationToken = default)
        {
            var timeZone = GetJakartaTimeZone();

            var now = TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                timeZone);

            var today = DateOnly.FromDateTime(now.DateTime);

            // Awal dan akhir hari di zona waktu Jakarta.
            var localStart = DateTime.SpecifyKind(
                today.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified);

            var localEnd = DateTime.SpecifyKind(
                today.AddDays(1).ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified);

            var startUtc = new DateTimeOffset(
                localStart,
                timeZone.GetUtcOffset(localStart)).ToUniversalTime();

            var endUtc = new DateTimeOffset(
                localEnd,
                timeZone.GetUtcOffset(localEnd)).ToUniversalTime();

            const string sql = """
                        select
            	t2."name" ,
            	t.scheduled_start ,
            	hu."name"
            from
            	task_occurrences t
            join tasks t2 on
            	t.task_id = t2.id
            join household_users hu on
            	t2.assigned_to = hu.id
            where
            	t.scheduled_start >= @start_utc
            	and t.scheduled_start < @end_utc;
            """;

            var items = new List<object>();

            await using var connection =
                await dataSource.OpenConnectionAsync(cancellationToken);

            await using var command =
                new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("start_utc", startUtc);
            command.Parameters.AddWithValue("end_utc", endUtc);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var taskName = reader.GetString(0);
                var start = reader.GetFieldValue<DateTimeOffset>(1);
                var assignedTo = reader.GetString(2);

                items.Add(new
                {
                    taskName,
                    scheduledStart = start,
                    assignedTo
                });
            }
            var result = new
            {
                date = today.ToString("yyyy-MM-dd"),
                timeZone = "Asia/Jakarta",
                count = items.Count,
                message = items.Count == 0
                    ? "Tidak ada jadwal untuk hari ini."
                    : null,
                items
            };

            return JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }

        private static TimeZoneInfo GetJakartaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(
                    "Asia/Jakarta");
            }
            catch (TimeZoneNotFoundException)
            {
                // ID zona waktu Windows.
                return TimeZoneInfo.FindSystemTimeZoneById(
                    "SE Asia Standard Time");
            }
        }
    }
}
