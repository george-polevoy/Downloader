// -----------------------------------------------------------------------
// <copyright file="ControlExtensions.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Downloader.Ui.WinForms
{
	using System;
	using System.Windows.Forms;

	public static class ControlExtensions
	{
		public static void Do<T>(this T me, Action<T> action) where T : Control
		{
			if (me.InvokeRequired)
			{
				me.Invoke(new Action(() => action(me)));
			}
			else
			{
				action(me);
			}
		}
	}
}
