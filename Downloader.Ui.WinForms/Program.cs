namespace Downloader.Ui.WinForms
{
	using System;
	using System.Windows.Forms;

	using Downloader.Core;

	internal static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		private static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			var downloaderMainForm = new DownloaderMainForm(new DownloaderMainFormController(new DownloadScheduler(10)));
			Application.Run(downloaderMainForm);
		}
	}
}
