namespace Downloader.Core
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Threading;

	public class DownloadScheduler : IDownloadScheduler
	{
		/// <summary>
		///	Sync object for all state-changing operations
		/// </summary>
		private readonly object collectionSync = new object();

		private readonly AutoResetEvent cycle = new AutoResetEvent(false);
		private readonly ManualResetEvent stopped = new ManualResetEvent(false);
		private readonly object startedSync = new object();
		
		private bool shouldStop;

		public DownloadScheduler(int maxTasks)
		{
			this.MaxTasks = maxTasks;
			this.ActiveDownloads = new List<FileDownload>();
			this.FailedDownloads = new List<FileDownload>();
			this.PendingDownloads = new Queue<FileDownload>();
			this.FinishedDownloads = new List<FileDownload>();
		}

		/// <summary>
		/// Subscribe to check the state of the downloads
		/// </summary>
		public event Action Change;

		private bool Started { get; set; }

		private Queue<FileDownload> PendingDownloads { get; set; }

		private List<FileDownload> ActiveDownloads { get; set; }

		private List<FileDownload> FailedDownloads { get; set; }

		private List<FileDownload> FinishedDownloads { get; set; }

		private int MaxTasks { get; set; }

		public void Start()
		{
			lock (this.startedSync)
			{
				if (this.Started)
				{
					throw new InvalidOperationException("Already started");
				}

				this.Started = true;
			}

			ThreadPool.QueueUserWorkItem(o => this.ProcessQueue());
		}

		/// <summary>
		/// Synchronous stop method. After this method returned, no operation will be carried out, guaranteed
		/// </summary>
		public void Stop()
		{
			// Signal the threads and async ops to stop
			this.shouldStop = true;

			// return from 'infinite' loop
			this.cycle.Set();

			// wait while the processing actually stops
			this.stopped.WaitOne();
		}

		public void ProcessQueue()
		{
			while (!this.shouldStop)
			{
				lock (this.collectionSync)
				{
					var newTaskClassification = this.ActiveDownloads.ToLookup(i => i.State);
					this.ActiveDownloads = newTaskClassification[FileDownloadState.Started].ToList();
					this.FinishedDownloads.AddRange(newTaskClassification[FileDownloadState.Finished]);
					this.FailedDownloads.AddRange(newTaskClassification[FileDownloadState.Failed]);

					var activeTasksQueue =
						from download in this.ActiveDownloads
						from part in download.Parts
						where part.State == FileDownloadState.Started
						select part;

					var activeTasksCount = activeTasksQueue.Take(10).Count();
					var capacity = this.MaxTasks - activeTasksCount;

					while (capacity > 0)
					{
						// Find a donwload item which requires processing
						var download =
							this.ActiveDownloads.FirstOrDefault(
								i =>
								i.State == FileDownloadState.Added
								|| (i.State == FileDownloadState.Started && i.Parts.Any(p => p.State == FileDownloadState.Added)));
						
						// If there's none, add some from pending
						if (download == null)
						{
							if (this.PendingDownloads.Count > 0)
							{
								download = this.PendingDownloads.Dequeue();
								this.ActiveDownloads.Add(download);
							}
							else
							{
								break;
							}
						}

						this.Process(download);

						capacity--;
					}
				}

				this.cycle.WaitOne();
				this.BroadcastChange();
			}

			lock (this.collectionSync)
			{
				foreach (var activeDownload in this.ActiveDownloads)
				{
					activeDownload.OwnState = FileDownloadState.Failed;
				}
			}

			this.stopped.Set();
		}

		public IQueryable<FileDownload> GetAll()
		{
			lock (this.collectionSync)
			{
				var fileDownloads = new List<FileDownload>();

				fileDownloads.AddRange(this.PendingDownloads.OrderByDescending(i => i.AddedDate));
				fileDownloads.AddRange(this.ActiveDownloads.OrderByDescending(i => i.AddedDate));
				fileDownloads.AddRange(this.FailedDownloads.OrderByDescending(i => i.AddedDate));
				fileDownloads.AddRange(this.FinishedDownloads.OrderByDescending(i => i.AddedDate));

				return fileDownloads.AsQueryable();
			}
		}

		public void AddDownload(string url, string path)
		{
			var addedDate = DateTime.Now;
			var fileDownload = new FileDownload { Url = url, Path = path, AddedDate = addedDate, LastActivity = addedDate, };

			lock (this.collectionSync)
			{
				this.PendingDownloads.Enqueue(fileDownload);
			}

			this.Pulse();
		}

		private void Process(FileDownload fileDownload)
		{
			Contract.Requires(fileDownload != null);

			switch (fileDownload.State)
			{
				case FileDownloadState.Added:
					this.ProcessNewDownload(fileDownload);
					break;
				case FileDownloadState.Started:
					this.ProcessStartedDownload(fileDownload);
					break;
				default:
					throw new ArgumentException("Can only process file download in added or started state");
			}
		}

		private void ProcessNewDownload(FileDownload fileDownload)
		{
			fileDownload.OwnState = FileDownloadState.Started;

			try
			{
				var request = WebRequest.Create(fileDownload.Url);

				request.BeginGetResponse(
					requestState =>
					{
						WebResponse webResponse = null;
						try
						{
							webResponse = request.EndGetResponse(requestState);

							this.ProcessInitialResponse(webResponse, fileDownload);
						}
						catch (WebException webException)
						{
							Log(webException);
							this.SetState(fileDownload, FileDownloadState.Failed);
						}
						finally
						{
							if (webResponse != null)
							{
								webResponse.Close();
							}
						}
					},
					null);
			}
			catch (WebException webException)
			{
				this.Log(webException);
				this.SetState(fileDownload, FileDownloadState.Failed);
			}
		}

		private void Log(Exception webException)
		{
			Trace.TraceError(webException.ToString());
		}

		private void ProcessInitialResponse(WebResponse webResponse, FileDownload fileDownload)
		{
			lock (this.collectionSync)
			{
				fileDownload.Size = webResponse.ContentLength;

				fileDownload.DownloadInParts = webResponse.Headers["Accept-Ranges"] == "bytes"
											   && fileDownload.Size >= 1024 * 1024 * 16;
			}

			this.ProcessStartedDownload(fileDownload);
		}

		private void ProcessStartedDownload(FileDownload fileDownload)
		{
			lock (this.collectionSync)
			{
				if (fileDownload.Size == 0)
				{
					return;
				}

				var downloadInParts = fileDownload.DownloadInParts;
				var chunkingStrategy = downloadInParts
														? (IChunkingStrategy)new PartialDownloadChunkingStrategy()
														: new NoneChunkingStrategy();

				var chunk = chunkingStrategy.ReserveChunk(fileDownload);

				if (chunk == null)
				{
					return;
				}

				chunk.WebRequest = chunkingStrategy.CreateRequest(chunk);

				try
				{
					fileDownload.InitializeStorage();
					this.BeginGetChunk(chunk);
				}
				catch (WebException webException)
				{
					this.Log(webException);
					this.SetState(chunk, FileDownloadState.Failed);
				}
			}
		}

		private void BeginGetChunk(FilePart part)
		{
			part.WebRequest.BeginGetResponse(this.OnGetChunkResponse, part);
		}

		private void OnGetChunkResponse(IAsyncResult asyncResult)
		{
			var filePart = (FilePart)asyncResult.AsyncState;

			WebResponse response = null;

			try
			{
				response = filePart.WebRequest.EndGetResponse(asyncResult);

				var stream = response.GetResponseStream();

				this.ReadWriteChunkFromResponse(stream, filePart);
			}
			catch (Exception exception)
			{
				this.Log(exception);
				if (response != null)
				{
					response.Close();
				}
			}
		}

		private void ReadWriteChunkFromResponse(Stream stream, FilePart filePart)
		{
			var fileDownload = filePart.FileDownload;

			const int BufferSize = 1024 * 64;

			var buffer = new byte[BufferSize];

			AsyncCallback readNext = null;
			readNext = streamState =>
				{
					this.Pulse();
					try
					{
						var bytesRead = 0;

						if (!this.SynchronizedOnRunningState(filePart.FileDownload, () => { bytesRead = stream.EndRead(streamState); }))
						{
							return;
						}

						var streamAsyncState = (Tuple<long, byte[]>)streamState.AsyncState;

						var position = streamAsyncState.Item1;
						var asyncBuffer = streamAsyncState.Item2;

						if (bytesRead > 0)
						{
							filePart.WriteBlock(asyncBuffer, position, bytesRead);

							var advandedToPosition = position + bytesRead;

							filePart.DownloadedTotal = advandedToPosition;
							fileDownload.LastActivity = DateTime.Now;

							this.Pulse();

							this.SynchronizedOnRunningState(
// ReSharper disable AccessToModifiedClosure
								fileDownload, () => stream.BeginRead(buffer, 0, BufferSize, readNext, Tuple.Create(advandedToPosition, buffer)));
// ReSharper restore AccessToModifiedClosure
						}
						else
						{
							stream.Close();
							this.SetState(filePart, FileDownloadState.Finished);
						}
					}
					catch (Exception exception)
					{
						this.Log(exception);
						stream.Close();
						this.SetState(filePart, FileDownloadState.Failed);
					}
				};

			this.SynchronizedOnRunningState(
				fileDownload, () => stream.BeginRead(buffer, 0, BufferSize, readNext, Tuple.Create((long)0, buffer)));
		}

		/// <summary>
		/// Executes synchronized operation, if download is not stopped and is still in active state
		/// </summary>
		/// <param name="fileDownload">File download which provides the state</param>
		/// <param name="action">Exection to execute</param>
		/// <returns>true if action is executed, false otherwise</returns>
		private bool SynchronizedOnRunningState(FileDownload fileDownload, Action action)
		{
			if (this.shouldStop)
			{
				return false;
			}

			lock (this.collectionSync)
			{
				if (fileDownload.State == FileDownloadState.Started)
				{
					action();
					return true;
				}

				return false;
			}
		}

		private void SetState(FileDownload fileDownload, FileDownloadState newState)
		{
			lock (this.collectionSync)
			{
				fileDownload.OwnState = newState;
			}

			this.Pulse();
		}

		private void SetState(FilePart filePart, FileDownloadState newState)
		{
			lock (this.collectionSync)
			{
				filePart.State = newState;
			}

			this.Pulse();
		}

		/// <summary>
		/// Let the download thread to know that there is a change in download states
		/// </summary>
		private void Pulse()
		{
			this.cycle.Set();
		}

		private void BroadcastChange()
		{
			if (this.Change != null)
			{
				this.Change();
			}
		}
	}
}