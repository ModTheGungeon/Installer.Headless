using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using YamlDotNet.Serialization;

namespace MTGInstaller {
	public class Settings {
		public const string DIR_NAME = "Mod the Gungeon";
		public const string CUSTOM_COMPONENTS_YML_NAME = "custom-components.yml";
		public const string SETTINGS_YML_NAME = "settings.yml";

		public static readonly Dictionary<string, string> Entries = new Dictionary<string, string> {
			{ SETTINGS_YML_NAME, SettingsFile },
			{ CUSTOM_COMPONENTS_YML_NAME, CustomComponentsFile }
		};

		public static string SettingsDir {
			get {
				var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DIR_NAME);

				// I know it's bad to do this in the getter...
				// but it seems like the cleanest option
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);

				return path;
			}
		}

		public static string CustomComponentsFile {
			get {
				return Path.Combine(SettingsDir, CUSTOM_COMPONENTS_YML_NAME);
			}
		}

		public static string SettingsFile {
			get {
				return Path.Combine(SettingsDir, SETTINGS_YML_NAME);
			}
		}

		private static Settings _Instance;

		public static Settings Instance {
			get {
				if (_Instance != null) return _Instance;

				if (!File.Exists(SettingsFile)) {
					var asm = Assembly.GetExecutingAssembly();
					var stream = asm.GetManifestResourceStream("res::settings-template");

					using (var reader = new StreamReader(stream))
					using (var writer = File.CreateText(SettingsFile)) {
						writer.Write(reader.ReadToEnd());
					}
				}

				return _Instance = YAML.SerializationHelper.Deserializer.Deserialize<Settings>(File.ReadAllText(SettingsFile));
			}
		}

		[YamlMember(Alias = "VERSION")]
		public int Version { get; set; } = 1;
		[YamlMember(Alias = "executable_path")]
		public string ExecutablePath { get; set; } = null;
		[YamlMember(Alias = "force_http")]
		public bool ForceHTTP { get; set; } = false;
		[YamlMember(Alias = "force_backup")]
		public bool ForceBackup { get; set; } = false;
		[YamlMember(Alias = "skip_version_checks")]
		public bool SkipVersionChecks { get; set; } = false;
		[YamlMember(Alias = "custom_component_files")]
		public List<string> CustomComponentFiles { get; set; } = new List<string>();
		[YamlMember(Alias = "leave_patch_dlls")]
		public bool LeavePatchDLLs { get; set; } = false;

		[YamlIgnore]
		public string UserFriendly {
			get {
				var builder = new StringBuilder();
				builder.Append("Version: ").AppendLine(Version.ToString());
				builder.Append("Executable path: ").AppendLine(ExecutablePath ?? "Not overriden");
				builder.Append("Force insecure HTTP: ").AppendLine(ForceHTTP ? "Yes" : "No");
				builder.Append("Force a backup of the current state: ").AppendLine(ForceBackup ? "Yes" : "No");
				builder.Append("Skip version checks: ").AppendLine(SkipVersionChecks ? "Yes" : "No");
				if (CustomComponentFiles.Count > 0) {
					builder.AppendLine("Custom component files: ");
					foreach (var comp in CustomComponentFiles) {
						builder.Append("  - ").AppendLine(comp);
					}
				}
				builder.Append("Leave patch DLLs: ").Append(LeavePatchDLLs ? "Yes" : "No");
				return builder.ToString();
			}
		}

		public void Save() {
			using (var writer = File.CreateText(SettingsFile)) {
				var ser = YAML.SerializationHelper.Serializer.Serialize(this);
				writer.Write(ser);
			}
		}
	}
}
