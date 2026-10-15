namespace AccountingSystem.Models
{
	public class SequenceCounter
	{
		public int Id { get; set; }
		public string Key { get; set; } = "";
		public string Name { get; set; } = "";
		public string Prefix { get; set; } = "";
		public int CurrentValue { get; set; }
		public int NextValue { get; set; }
		public int Digits { get; set; } = 5;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	}
}