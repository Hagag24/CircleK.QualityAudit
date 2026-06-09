using CircleK.QualityAudit.Client.Dtos;

namespace CircleK.QualityAudit.Client.State;

public sealed class AuditFormState : IDisposable
{
    private readonly Dictionary<Guid, AuditAnswerDto> _answers = new();
    private readonly Dictionary<Guid, string> _notes = new();
    private readonly Dictionary<Guid, List<AuditTimingEntryDto>> _timings = new();
    private readonly Dictionary<Guid, DateTime> _activeTimers = new();
    private readonly System.Timers.Timer _timer;

    public IReadOnlyDictionary<Guid, AuditAnswerDto> Answers => _answers;
    public IReadOnlyDictionary<Guid, string> Notes => _notes;

    public AuditFormState()
    {
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    public void Clear()
    {
        _answers.Clear();
        _notes.Clear();
        _timings.Clear();
        _activeTimers.Clear();
    }

    public void SetAnswer(Guid itemId, AuditAnswerDto answer)
    {
        _answers[itemId] = answer;
    }

    public void SetNote(Guid itemId, string? note)
    {
        _notes[itemId] = note ?? string.Empty;
    }

    public void SetTiming(Guid itemId, AuditItemTimingDto timing)
    {
        _timings[itemId] = timing.Entries.ToList();
    }

    public void StartTimer(Guid itemId)
    {
        _activeTimers[itemId] = DateTime.UtcNow;
    }

    public int StopTimerAndGetSeconds(Guid itemId)
    {
        if (_activeTimers.TryGetValue(itemId, out var startTime))
        {
            var seconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;
            _activeTimers.Remove(itemId);

            if (!_timings.ContainsKey(itemId))
            {
                _timings[itemId] = new List<AuditTimingEntryDto>();
            }

            _timings[itemId].Add(new AuditTimingEntryDto(Guid.NewGuid(), seconds, DateTime.UtcNow));
            return seconds;
        }
        return 0;
    }

    public bool IsTimingRunning(Guid itemId) => _activeTimers.ContainsKey(itemId);

    public string GetRunningText(Guid itemId)
    {
        if (_activeTimers.TryGetValue(itemId, out var startTime))
        {
            var seconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;
            return FormatTime(seconds);
        }
        return "0:00";
    }

    public int GetTimingCount(Guid itemId) => _timings.TryGetValue(itemId, out var entries) ? entries.Count : 0;

    public string GetTimingAverageText(Guid itemId)
    {
        if (_timings.TryGetValue(itemId, out var entries) && entries.Count > 0)
        {
            var average = entries.Average(e => e.DurationSeconds);
            return FormatTime((int)average);
        }
        return "0:00";
    }

    public IReadOnlyList<AuditTimingEntryDto> GetTimingEntries(Guid itemId) => 
        _timings.TryGetValue(itemId, out var entries) ? entries : Array.Empty<AuditTimingEntryDto>();

    public int GetTimingIndex(Guid itemId, Guid entryId)
    {
        if (_timings.TryGetValue(itemId, out var entries))
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id == entryId)
                    return i;
            }
        }
        return -1;
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Timer elapsed - UI will update via StateHasChanged
    }

    private static string FormatTime(int totalSeconds)
    {
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:D2}";
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
