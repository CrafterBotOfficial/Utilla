using BepInEx.Logging;
using System;

namespace Utilla
{
	public static class EventHandlerExtensions
	{
		public static void SafeInvoke(this EventHandler handler, object sender, EventArgs e)
		{
			foreach (EventHandler handler2 in handler?.GetInvocationList())
			{
				try
				{
					handler2?.Invoke(sender, e);
				}
				catch (Exception ex)
				{
					Utilla.Log(ex, LogLevel.Error);
				}
			}
		}

		public static void SafeInvoke<T>(this EventHandler<T> handler, object sender, T e) where T : EventArgs
		{
			foreach (EventHandler<T> handler2 in handler?.GetInvocationList())
			{
				try
				{
					handler2?.Invoke(sender, e);
				}
				catch (Exception ex)
				{
					Utilla.Log(ex, LogLevel.Error);
				}
			}
		}
	}
}