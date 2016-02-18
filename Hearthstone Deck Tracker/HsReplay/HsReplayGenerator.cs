﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using static Hearthstone_Deck_Tracker.HsReplay.HsReplayConstants;

#endregion

namespace Hearthstone_Deck_Tracker.HsReplay
{
	public class HsReplayGenerator
	{
		private static XmlMetaData[] GetMetaData(GameMetaData metaData)
			=>
				new[]
				{
					new XmlMetaData("id", metaData?.GameId),
					new XmlMetaData("x-address", metaData?.ServerAddress),
					new XmlMetaData("x-clientid", metaData?.ClientId),
					new XmlMetaData("x-spectateKey", metaData?.SpectateKey),
				};

		public static async Task<string> Generate(List<string> log, GameStats stats, GameMetaData gameMetaData)
		{
			Directory.CreateDirectory(HsReplayPath);
			Directory.CreateDirectory(TmpDirPath);

			if(!File.Exists(HsReplayExe) || CheckForUpdate())
				await Update();

			using(var sw = new StreamWriter(TmpFilePath))
			{
				foreach(var line in log)
					sw.WriteLine(line);
			}

			RunExe(stats?.StartTime);

			if(new FileInfo(HsReplayOutput).Length == 0)
			{
				Logger.WriteLine("Not able to convert log file.", "HsReplayGenerator");
				return null;
			}

			AddMetaData(HsReplayOutput, gameMetaData, stats);
			File.Delete(TmpFilePath);
			return HsReplayOutput;
		}

		private static void RunExe(DateTime? time)
		{
			var dateString = time?.ToString("yyyy-MM-dd");
			var defaultDateArg = time.HasValue ? $"--default-date={dateString} " : "";
			var procInfo = new ProcessStartInfo
			{
				FileName = HsReplayExe,
				Arguments = defaultDateArg + TmpFilePath,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				UseShellExecute = false
			};
			var proc = Process.Start(procInfo);
			using(var sw = new StreamWriter(HsReplayOutput))
				sw.Write(proc?.StandardOutput.ReadToEnd());
			proc?.WaitForExit();
		}

		private static void AddMetaData(string xmlFile, GameMetaData gameMetaData, GameStats stats)
		{
			var xml = XDocument.Load(xmlFile);
			var hsReplay = xml.Elements().FirstOrDefault(x => x.Name == "HSReplay");
			if(hsReplay == null)
				return;
			hsReplay.SetAttributeValue("build", gameMetaData?.HearthstoneBuild);
			var game = hsReplay.Elements().FirstOrDefault(x => x.Name == "Game");
			if(game != null)
			{
				foreach (var pair in GetMetaData(gameMetaData))
					game.SetAttributeValue(pair.Key, pair.Value);
				var player = game.Elements().FirstOrDefault(x => x.Name == "Player" && x.Attributes().Any(a => a.Name == "name" && a.Value == stats?.PlayerName));
				if (stats?.Rank > 0)
					player?.SetAttributeValue("rank", stats.Rank);
				if (gameMetaData?.LegendRank > 0)
					player?.SetAttributeValue("legendRank", gameMetaData.LegendRank);
				if(stats?.OpponentRank > 0)
					game.Elements().FirstOrDefault(x => x.Name == "Player" && x.Attributes().Any(a => a.Name == "name" && a.Value == stats.OpponentName))?
								   .SetAttributeValue("rank", stats.OpponentRank);
			}
			xml.Save(xmlFile);
		}

		private static async Task Update()
		{
			var version = "0.1";
			var zipPath = string.Format(ZipFilePath, version);
			Logger.WriteLine($"Downloading hsreplay converter version {version}...", "HsReplay");
			using(var wc = new WebClient())
				await wc.DownloadFileTaskAsync(string.Format(DownloadUrl, version), zipPath);
			Logger.WriteLine("Finished downloading. Unpacking...", "HsReplay");
			using(var fs = new FileInfo(zipPath).OpenRead())
			{
				var archive = new ZipArchive(fs, ZipArchiveMode.Read);
				archive.ExtractToDirectory(HsReplayPath, true);
			}
			File.Delete(zipPath);
		}

		private static bool CheckForUpdate()
		{
			//TODO
			return false;
		}
	}
}