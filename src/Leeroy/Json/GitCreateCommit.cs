namespace Leeroy.Json
{
	public sealed class GitCreateCommit
	{
		public string Message { get; set; }
		public string[] Parents { get; set; }
		public string Tree { get; set; }
	}
}
