using System;

namespace Leeroy.Json
{
	public sealed class GitBlob
	{
		public string Encoding { get; set; }

		public string Content { get; set; }

		public Uri Url { get; set; }

		public int Size { get; set; }

		public string Sha { get; set; }

		public string GetContent()
		{
			switch (Encoding)
			{
			case "base64":
				return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Content));

			case "utf-8":
				return Content;

			default:
				throw new InvalidOperationException(string.Format("'encoding' type '{0}' is not supported.", Encoding));
			}
		}
	}
}
