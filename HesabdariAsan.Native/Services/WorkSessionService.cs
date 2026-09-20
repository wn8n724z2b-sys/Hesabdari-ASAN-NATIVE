using HesabdariAsan.Native.Data;

namespace HesabdariAsan.Native.Services;

public sealed class WorkSessionService
{
    private long? _sessionId;
    private DateTime _lastPulse;

    public void Start()
    {
        var now = DateTime.Now;
        using var db = Database.Open();
        using (var cleanup = db.CreateCommand())
        {
            cleanup.CommandText = @"UPDATE work_sessions
SET ended_at=COALESCE(last_pulse_at, started_at)
WHERE ended_at IS NULL";
            cleanup.ExecuteNonQuery();
        }
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"INSERT INTO work_sessions(started_at,duration_seconds,last_pulse_at)
VALUES($s,0,$s); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$s", now.ToString("O"));
        _sessionId = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        _lastPulse = now;
    }

    public void Pulse()
    {
        if (_sessionId is null) return;
        var now = DateTime.Now;
        var elapsed = (long)(now - _lastPulse).TotalSeconds;
        // If Windows slept, the clock changed, or the process was suspended, do not count the whole gap as work time.
        var seconds = elapsed is > 0 and <= 90 ? elapsed : 0;
        if (seconds <= 0) { _lastPulse = now; return; }
        using var db = Database.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"UPDATE work_sessions
SET duration_seconds=duration_seconds+$d,last_pulse_at=$p
WHERE id=$id";
        cmd.Parameters.AddWithValue("$d", seconds);
        cmd.Parameters.AddWithValue("$p", now.ToString("O"));
        cmd.Parameters.AddWithValue("$id", _sessionId.Value);
        cmd.ExecuteNonQuery();
        _lastPulse = now;
    }

    public void Stop()
    {
        if (_sessionId is null) return;
        Pulse();
        using var db = Database.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE work_sessions SET ended_at=$e WHERE id=$id";
        cmd.Parameters.AddWithValue("$e", DateTime.Now.ToString("O"));
        cmd.Parameters.AddWithValue("$id", _sessionId.Value);
        cmd.ExecuteNonQuery();
        _sessionId = null;
    }

    public TimeSpan GetTodayDuration()
    {
        var day = DateTime.Today;
        long seconds;
        using (var db = Database.Open())
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = @"SELECT COALESCE(SUM(duration_seconds),0)
FROM work_sessions WHERE started_at >= $s AND started_at < $e";
            cmd.Parameters.AddWithValue("$s", day.ToString("O"));
            cmd.Parameters.AddWithValue("$e", day.AddDays(1).ToString("O"));
            seconds = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }
        if (_sessionId is not null)
        {
            var live=(long)(DateTime.Now-_lastPulse).TotalSeconds;
            if(live is > 0 and <= 90)seconds+=live;
        }
        return TimeSpan.FromSeconds(seconds);
    }
}
