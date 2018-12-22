using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Collections.Generic;
using System.Reflection;

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

		public const string LOCAL_COMPONENT_FILE_NAME = "custom-components.yml";

		public Dictionary<string, ETGModComponent> Components;

		public string BaseDomain = "modthegungeon.eu/reloaded";
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

		public Downloader(bool force_http = false, bool offline = false) {
			if (force_http) BaseURL = $"http://{BaseDomain}";
			else BaseURL = $"https://{BaseDomain}";

			ComponentsURL = $"{BaseURL}/components.yml";
			GungeonMetadataURL = $"{BaseURL}/gungeon.yml";

			if (offline) Components = new Dictionary<string, ETGModComponent>();
			else Components = ParseComponentsFile(FetchComponents());

			if (File.Exists(Settings.CustomComponentsFile)) {
				AddComponentsFile(File.ReadAllText(Settings.CustomComponentsFile));
			} else {
				var asm = Assembly.GetExecutingAssembly();
				var stream = asm.GetManifestResourceStream("res::custom-components-template");

				using (var reader = new StreamReader(stream))
				using (var writer = File.CreateText(Settings.CustomComponentsFile)) {
					writer.Write(reader.ReadToEnd());
				}
			}	       
		}

		public void AddComponentsFile(string components) {
			var parsed = SerializationHelper.Deserializer.Deserialize<ETGModComponent[]>(components);
			if (parsed == null) return;
			foreach (var com in parsed) {
				ETGModComponent existing_component;

				if (Components.TryGetValue(com.Name, out existing_component)) {
					// if component already exists, do an intelligent version merge
					foreach (var ver in com.Versions) {
						foreach (var exver in existing_component.Versions) {
							if (exver.Key == ver.Key) {
								existing_component.Versions.Remove(exver);
								break;
							}
						}

						existing_component.Versions.Add(ver);
					}
				} else {
					Components[com.Name] = com;
				}
			}
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
			_Logger.Debug($"components.yml URL: '{ComponentsURL}'");
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
			if (version.URL == null && version.Path == null) throw new ArgumentException("Version has neither a URL nor a file path");
			return Download(version, GenerateUniqueDestination());
		}

		public DownloadedBuild Download(ETGModVersion version, string destination) {
			return Download(version.Path ?? version.URL, destination, version.DisplayName, version.Path != null);
		}

		public DownloadedBuild Download(string url, string dest, string name = null, bool local = false) {
			if (name != null) _Logger.Info($"Downloading {name} from {url} to {dest}");
			else _Logger.Info($"Downloading {url} to {dest}");

			var extract_path = Path.Combine(dest, "EXTRACTED");
			var zip_path = Path.Combine(dest, "DOWNLOAD.zip");
			Directory.CreateDirectory(dest);
			Directory.CreateDirectory(extract_path);
			if (local) File.Copy(url, zip_path, overwrite: true);
			else WebClient.DownloadFile(url, zip_path);
			ZipFile.ExtractToDirectory(zip_path, extract_path);
			return new DownloadedBuild(url, dest, extract_path);
		}
	}
}
