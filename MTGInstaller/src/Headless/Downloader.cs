using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using MTGInstaller.YAML;
using System.Collections.Generic;

namespace MTGInstaller {
	public class DownloadedBuild : IDisposable {
		public string URL;
		public string Path;
		public string ExtractedPath;

		public DownloadedBuild(string url, string path, string extracted_path) {
			URL = url;
			Path = path;
			ExtractedPath = extracted_path;
		}

		public void Dispose() {
			Directory.Delete(Path, recursive: true);
		}
	}

	public class Downloader {
		public static WebClient WebClient = new WebClient();
		private static Logger _Logger = new Logger(nameof(Downloader));

		public Dictionary<string, ETGModComponent> Components;

		public string BaseDomain = "modthegungeon.zatherz.eu";
		public string BaseURL;
		public string ComponentsURL;
		public string GungeonMetadataURL;

		private GungeonMetadata _GungeonMetadata = null;
		public GungeonMetadata GungeonMetadata {
			get {
				if (_GungeonMetadata != null) return _GungeonMetadata;
				return _GungeonMetadata = FetchGungeonMetadata();
			}
		}

		public Downloader(bool force_http = false) {
			if (force_http) BaseURL = $"http://{BaseDomain}";
			else BaseURL = $"https://{BaseDomain}";

			ComponentsURL = $"{BaseURL}/components.yml";
			GungeonMetadataURL = $"{BaseURL}/gungeon.yml";

			Components = ParseComponentsFile(FetchComponents());
		}

		public static Dictionary<string, ETGModComponent> ParseComponentsFile(string components) {
			var dict = new Dictionary<string, ETGModComponent>();

			var parsed = SerializationHelper.Deserializer.Deserialize<ETGModComponent[]>(components);
			foreach (var com in parsed) {
				dict[com.Name] = com;
			}

			return dict;
		}

		public ETGModComponent TryGet(string name) {
			ETGModComponent component;
			Components.TryGetValue(name, out component);
			return component;
		}

		public string FetchComponents() {
			return WebClient.DownloadString(ComponentsURL);
		}

		public GungeonMetadata FetchGungeonMetadata() {
			var str = WebClient.DownloadString(GungeonMetadataURL);
			return SerializationHelper.Deserializer.Deserialize<GungeonMetadata>(str);
		}

		public string GenerateDestination() {
			return Path.Combine(Path.GetTempPath(), $"MTGDOWNLOAD_{Guid.NewGuid().ToString()}");
		}

		const int MAX_ATTEMPTS = 5;
		public string GenerateUniqueDestination() {
			var attempts = 1;
			while (attempts++ <= MAX_ATTEMPTS) {
				var dest = GenerateDestination();
				if (!Directory.Exists(dest)) { return dest; }
			}
			throw new Exception($"Couldn't generate unique download destination (tried {MAX_ATTEMPTS} times). Do you have permissions to the temporary files folder?");
		}

		public DownloadedBuild Download(ETGModVersion version) {
			if (version.URL == null) throw new ArgumentException("Version doesn't have a URL");
			return Download(version, GenerateUniqueDestination());
		}

		public DownloadedBuild Download(ETGModVersion version, string destination) {
			return Download(version.URL, destination, version.DisplayName);
		}

		public DownloadedBuild Download(string url, string dest, string name = null) {
			if (name != null) _Logger.Info($"Downloading {name} from {url} to {dest}");
			else _Logger.Info($"Downloading {url} to {dest}");

			var extract_path = Path.Combine(dest, "EXTRACTED");
			var zip_path = Path.Combine(dest, "DOWNLOAD.zip");
			Directory.CreateDirectory(dest);
			Directory.CreateDirectory(extract_path);
			WebClient.DownloadFile(url, zip_path);
			ZipFile.ExtractToDirectory(zip_path, extract_path);
			return new DownloadedBuild(url, dest, extract_path);
		}
	}
}
