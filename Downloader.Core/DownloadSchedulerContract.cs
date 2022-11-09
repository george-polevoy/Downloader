namespace Downloader.Core
{
	using System;
	using System.Diagnostics.Contracts;
	using System.Linq;

	[ContractClassFor(typeof(IDownloadScheduler))]
	internal abstract class DownloadSchedulerContract : IDownloadScheduler
	{
		public event Action Change;

		public IQueryable<FileDownload> GetAll()
		{
			return default(IQueryable<FileDownload>);
		}

		public void AddDownload(string url, string path)
		{
			Contract.Requires(url != null);
			Contract.Requires(path != null);
		}

		public void Start()
		{
		}

		public void Stop()
		{
		}
	}
}