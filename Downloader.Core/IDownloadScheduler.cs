namespace Downloader.Core
{
	using System;
	using System.Diagnostics.Contracts;
	using System.Linq;

	[ContractClass(typeof(DownloadSchedulerContract))]
	public interface IDownloadScheduler
	{
		event Action Change;

		IQueryable<FileDownload> GetAll();

		void AddDownload(string url, string path);

		void Start();

		void Stop();
	}
}