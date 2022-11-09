namespace Downloader.Ui.WinForms
{
	using System;
	using System.Collections.Generic;
	using System.Windows.Forms;

	public partial class DownloaderMainForm : Form, IDownloaderView
	{
		public DownloaderMainForm(IDownloaderMainFormController controller)
		{
			this.InitializeComponent();

			this.Controller = controller;

			this.Controller.SetView(this);

			Load += (sender, e) => this.Controller.Load();

			Closing += (sender, e) => this.Controller.Stop();
		}

		private IDownloaderMainFormController Controller { get; set; }

		public void DisplayDownloads(IEnumerable<FileDownloadListItem> items)
		{
			this.dataGridViewDownloads.Do(me =>
				{
					me.DataSource = items;
					me.Refresh();
				});
		}

		public void Log(string message)
		{
			this.textBoxLog.Do(me =>
				{
					textBoxLog.AppendText(message);
					textBoxLog.AppendText(Environment.NewLine);
				});
		}

		private void ButtonDownloadClick(object sender, EventArgs e)
		{
			this.Controller.StartDownload(this.textBoxUrl.Text);
		}

		private void TimerUpdateDisplayTick(object sender, EventArgs e)
		{
			this.Controller.Tick();
		}
	}
}
