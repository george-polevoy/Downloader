namespace Downloader.Core
{
	using System.Collections.Generic;

	public interface IMimeMapping
	{
		IEnumerable<string> GetExtensionsByMime(string mime);
	}
}