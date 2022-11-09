namespace Downloader.Core.Tests
{
	using NUnit.Framework;
	using System.Linq;

	public class FileDownloadStateTests
	{
		[Test]
		public void StateEqualsOwnStatePartsCollectionIsEmpty()
		{
			foreach (var expected in new[]
				{
					FileDownloadState.Added,
					FileDownloadState.Started,
					FileDownloadState.Finished,
					FileDownloadState.Failed
				})
			{
				var fileDownload = new FileDownload{ OwnState = expected };

				var actual = fileDownload.State;

				Assert.AreEqual(expected, actual);
			}	
		}

		[Test]
		public void StateIsFailedIfAnyPartFailed()
		{
			var fileDownload = new FileDownload { Size = 100, OwnState = FileDownloadState.Started };
			var chunk1 = fileDownload.Chunk(50);
			var chunk2 = fileDownload.Chunk(50);

			if (fileDownload.Parts.Count() < 2)
			{
				Assert.Inconclusive();
			}

			if (chunk2.State != FileDownloadState.Started)
			{
				Assert.Inconclusive();
			}

			chunk1.State = FileDownloadState.Failed;

			Assert.AreEqual(FileDownloadState.Failed, fileDownload.State);
		}

		[Test]
		public void FileStateIsFailedIfOwnStateIsFailedNoMatterThePartsAreOk()
		{
			var fileDownload = new FileDownload { Size = 100, OwnState = FileDownloadState.Started };
		
			fileDownload.Chunk(50);
			fileDownload.Chunk(50);

			if (fileDownload.Parts.Count() < 2)
			{
				Assert.Inconclusive();
			}

			if (fileDownload.Parts.Any(i => i.State != FileDownloadState.Started))
			{
				Assert.Inconclusive();
			}

			fileDownload.OwnState = FileDownloadState.Failed;

			Assert.AreEqual(FileDownloadState.Failed, fileDownload.State);
		}
	}
}
