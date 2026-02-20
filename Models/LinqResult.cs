namespace BojanGrujicSite.Models
{
    public class LinqResult
    {
        public string Expression { get; set; } = string.Empty;
        public int Amount { get; set; }
        public long ExecutionTime { get; set; }
        public long DataGenerationTime { get; set; }
        public double ExecutionTimeMicroseconds { get; set; }
        public int ResultCount { get; set; }
        public long MemoryBefore { get; set; }
        public long MemoryAfter { get; set; }
        public long MemoryUsed => MemoryAfter - MemoryBefore;
        public double FilterPercentage => Amount > 0 ? Math.Round((double)ResultCount / Amount * 100, 2) : 0;
        public double ItemsPerMs => ExecutionTime > 0 ? Math.Round((double)Amount / ExecutionTime, 1) : 0;
        public List<Dictionary<string, object>> ResultData { get; set; } = new();
        public List<string> ColumnNames { get; set; } = new();
        public string? Error { get; set; }
    }
}
