using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace MTGInstaller {
	public class DebugConverter {
		public const string UNITY_WINDOWS_URL = "http://download.unity3d.com/download_unity/0c4b856e4c6e/Windows64EditorInstaller/UnitySetup64-2017.4.27f1.exe";
		public const string UNITY_LINUX_URL = "http://netstorage.unity3d.com/unity/0c4b856e4c6e/TargetSupportInstaller/UnitySetup-Linux-Support-for-Editor-2017.4.27f1.exe";
		public const string UNITY_MAC_URL = "http://netstorage.unity3d.com/unity/0c4b856e4c6e/TargetSupportInstaller/UnitySetup-Mac-Support-for-Editor-2017.4.27f1.exe";

		public Logger Logger = new Logger("DebugConverter");

		public string SevenZipPath;
		public string UnityCacheDir;
		public Installer Installer;

		public string UnityWindowsDir => Path.Combine(UnityCacheDir, "UnitySetup");
		public string UnityLinuxDir => Path.Combine(UnityCacheDir, "UnityLinuxExport");
		public string UnityMacDir => Path.Combine(UnityCacheDir, "UnityMacExport");

		public string UnityWindowsPath => Path.Combine(UnityCacheDir, "UnitySetup.exe");
		public string UnityLinuxPath => Path.Combine(UnityCacheDir, "UnityLinuxExport.exe");
		public string UnityMacPath => Path.Combine(UnityCacheDir, "UnityMacExport.exe");

		public DebugConverter(string unity_cache_dir, Installer installer, string seven_zip_path) {
			UnityCacheDir = unity_cache_dir;
			Installer = installer;
			SevenZipPath = seven_zip_path;
		}

		public void Validate() {
			if (!File.Exists(SevenZipPath)) {
				throw new Exception("Missing or invalid path to 7z executable");
			}
			if (!Directory.Exists(UnityCacheDir)) {
				Logger.Debug($"Creating Unity cache dir");
				Directory.CreateDirectory(UnityCacheDir);
			}
		}

		private void Download(string url, string path) {
			Logger.Debug($"Downloading from '{url}' to '{path}'");
			if (File.Exists(path)) return;

			if (!Directory.Exists(Path.GetDirectoryName(path))) {
				Directory.CreateDirectory(Path.GetDirectoryName(path));
			}
			using (var wc = new WebClient()) {
				wc.DownloadFile(url, path);
			}
		}

		private void Unpack(string path, string dir) {
			Logger.Debug($"Unpacking from '{path}' to '{dir}'");

			var esc_path = path.Replace("\\", "\\\\").Replace("\"", "\\\"");
			var esc_dir = dir.Replace("\\", "\\\\").Replace("\"", "\\\"");

			var p = Process.Start(new ProcessStartInfo {
				FileName = SevenZipPath,
				UseShellExecute = false,
				Arguments = $"x \"{esc_path}\" -o\"{esc_dir}\""
			});
			p.WaitForExit();

			if (p.ExitCode != 0) throw new Exception("Failed unpacking with 7z");
		}

		private void DownloadUnity(Platform plat) {
			Logger.Debug($"Downloading Unity for platform {plat}");
			var url = "";
			var path = "";
			if (plat == Platform.Windows) { path = UnityWindowsPath; url = UNITY_WINDOWS_URL; } else if (plat == Platform.Linux) { path = UnityLinuxPath; url = UNITY_LINUX_URL; } else if (plat == Platform.Mac) { path = UnityMacPath; url = UNITY_MAC_URL; }

			if (File.Exists(path)) return;

			Download(url, path);
		}

		private void UnpackUnity(Platform plat) {
			Logger.Debug($"Unpacking Unity for platform {plat}");

			var dir = "";
			var path = "";
			if (plat == Platform.Windows) { path = UnityWindowsPath; dir = UnityWindowsDir; } else if (plat == Platform.Linux) { path = UnityLinuxPath; dir = UnityLinuxDir; } else if (plat == Platform.Mac) { path = UnityMacPath; dir = UnityMacDir; }

			if (Directory.Exists(dir)) return;

			if (!Directory.Exists(dir)) {
				Directory.CreateDirectory(dir);
			}

			Unpack(path, dir);
		}

		private string GetDebugManagedFileBaseName(string filename) {
			if (filename.EndsWith(".dll.mdb", StringComparison.InvariantCulture)) {
				return filename.Substring(0, filename.Length - ".dll.mdb".Length);
			} else if (filename.EndsWith(".dll", StringComparison.InvariantCulture)) {
				return filename.Substring(0, filename.Length - ".dll".Length);
			} else if (filename.EndsWith(".xml", StringComparison.InvariantCulture)) {
				return filename.Substring(0, filename.Length - ".xml".Length);
			} else return filename;
		}

		public string GetUnityPlaybackEngineRoot(Platform plat) {
			if (plat == Platform.Windows)
				return Path.Combine(UnityWindowsDir, "Editor", "Data", "PlaybackEngines", "windowsstandalonesupport", "Variations");
			else if (plat == Platform.Linux)
				return Path.Combine(UnityLinuxDir, "$INSTDIR$_59_", "Variations");
			else if (plat == Platform.Mac)
				return Path.Combine(UnityMacDir, "$INSTDIR$_59_", "Variations");
			else return null;
		}

		public string GetUnityDebugPlayerExe(Architecture arch, Platform plat, string base_path) {
			var arch_str = arch == Architecture.X86 ? "32" : "64";

			if (plat == Platform.Windows)
				return Path.Combine(base_path, $"win{arch_str}_development_mono", "WindowsPlayer.exe");
			else if (plat == Platform.Linux)
				return Path.Combine(base_path, $"linux{arch_str}_withgfx_development_mono", "LinuxPlayer");
			else if (plat == Platform.Mac) {
				if (arch == Architecture.X86) throw new Exception("Unity doesn't support 32-bit OSX");
				return Path.Combine(base_path, $"macosx64_development_mono", "UnityPlayer.app", "Contents", "MacOS", "UnityPlayer");
			} else return null;

		}

		public string GetUnityDebugPlayerDLL(Architecture arch, Platform plat, string base_path) {
			var arch_str = arch == Architecture.X86 ? "32" : "64";

			if (plat == Platform.Windows)
				return Path.Combine(base_path, $"win{arch_str}_development_mono", "UnityPlayer.dll");
			return null;
		}

		public string GetUnityDebugManagedDir(Architecture arch, Platform plat, string base_path) {
			var arch_str = arch == Architecture.X86 ? "32" : "64";

			if (plat == Platform.Windows)
				return Path.Combine(base_path, $"win{arch_str}_development_mono", "Data", "Managed");
			else if (plat == Platform.Linux)
				return Path.Combine(base_path, $"linux{arch_str}_withgfx_development_mono", "Data", "Managed");
			else if (plat == Platform.Mac) {
				if (arch == Architecture.X86) throw new Exception("Unity doesn't support 32-bit OSX");
				return Path.Combine(base_path, $"macosx64_development_mono", "Data", "Managed");
			} else return null;

		}

		public string GetBootConfigPath(Platform plat) {
			return Installer.BootConfigFile;
		}

		public void ConvertToDebugBuild(Platform plat, Architecture arch) {
			Logger.Info($"Installing debug capabilities");
			DownloadUnity(plat);
			UnpackUnity(plat);

			var base_dir = GetUnityPlaybackEngineRoot(plat);
			Logger.Debug($"Playback engine root: {base_dir}");
			var player_exe = GetUnityDebugPlayerExe(arch, plat, base_dir);
			Logger.Debug($"Debug EXE: {player_exe}");
			var player_dll = GetUnityDebugPlayerDLL(arch, plat, base_dir);
			Logger.Debug($"Debug DLL: {player_dll ?? "N/A"}");
			var managed_dir = GetUnityDebugManagedDir(arch, plat, base_dir);
			Logger.Debug($"Debug Managed dir: {managed_dir}");

			var boot_config_path = GetBootConfigPath(plat);
			Logger.Debug($"Game boot.config path: {boot_config_path}");

			var exe_perm = Installer.GetUnixPermission(Installer.ExeFile);
			File.Delete(Installer.ExeFile);
			File.Copy(player_exe, Installer.ExeFile);
			Installer.SetUnixPermission(Installer.ExeFile, exe_perm);

			if (player_dll != null) {
				File.Delete(Installer.WindowsUnityPlayerDLL);
				File.Copy(player_dll, Installer.WindowsUnityPlayerDLL);
			}

			var viable_target_hash = new HashSet<string>();
			var viable_targets = Installer.Downloader.GungeonMetadata.ViablePatchTargets;
			for (var i = 0; i < viable_targets.Count; i++) {
				viable_target_hash.Add(viable_targets[i]);
			}

			foreach (var path in Directory.EnumerateFiles(managed_dir)) {
				var filename = Path.GetFileName(path);
				var basename = GetDebugManagedFileBaseName(filename);
				if (viable_target_hash.Contains(basename)) {
					// we have to skip any DLL that is able to be patched
					// because it could differ (we'll have to use DebugIL on it)
					Logger.Debug($"Rejected Debug Managed file: {filename} as it is a viable patch target");
					continue;
				}
				var target_path = Path.Combine(Installer.ManagedDir, filename);
				Logger.Debug($"Installing Debug Managed file: {filename} to '{target_path}'");
				if (File.Exists(target_path)) File.Delete(target_path);
				File.Copy(path, target_path);
			}

			var boot_config = File.ReadAllText(boot_config_path);
			boot_config += $"player-connection-debug=1{Environment.NewLine}";
			File.WriteAllText(boot_config_path, boot_config);

			Logger.Info($"Release->Debug conversion done");
		}

		public void InstallILDebug() {
			Logger.Info($"Installing IL debugging");

			var debugil_asm_count = 0;
			var debugil_program_class = typeof(MonoMod.DebugIL.DebugILGenerator).Assembly.GetType("MonoMod.DebugIL.Program");
			var debugil_program_main = debugil_program_class.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);

			foreach (var target in Installer.Downloader.GungeonMetadata.ViablePatchTargets) {
				var filename = $"{target}.dll";
				var path = Path.Combine(Installer.ManagedDir, filename);
				var mdb_path = $"{path}.mdb";
				var mdb_filename = $"{filename}.mdb";

				if (!File.Exists(mdb_path)) {
					Logger.Debug($"Found target without MDB: '{filename}'");
					var debugil_out_dir = Path.Combine(Path.GetDirectoryName(path), $"IL_{Path.GetFileNameWithoutExtension(filename)}");
					Environment.SetEnvironmentVariable("MONOMOD_DEBUGIL_FORMAT", "MDB");
					debugil_program_main.Invoke(null, new object[] { new string[] { path, debugil_out_dir } });
					var debugil_mdb_path = Path.Combine(debugil_out_dir, mdb_filename);
					var debugil_dll_path = Path.Combine(debugil_out_dir, filename);
					File.Delete(path);
					File.Copy(debugil_dll_path, path);
					File.Copy(debugil_mdb_path, mdb_path);
					Logger.Debug($"DebugIL generated MDB: '{mdb_path}'");
					debugil_asm_count += 1;
				}
			}

			Logger.Info($"Installed IL debugging for {debugil_asm_count} assemblies");
		}
	}
}
