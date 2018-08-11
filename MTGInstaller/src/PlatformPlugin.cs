using System;
using System.Collections.Generic;
using System.IO;

/*
 * PLUGINS DIR HIERARCHY
 * *All* platforms and architectures must be included
 * 
 * Plugins
 * |-Linux
 * | |-32
 * | | |-libX.so
 * | |-64
 * |   |-libX.so
 * |-Windows
 * | |-32
 * | | |-X.dll
 * | |-64
 * |   |-X.dll
 * |-MacOS
 *   |-X.bundle
 */

namespace MTGInstaller {
	public class UnknownPlatformException : Exception {
		public Platform Platform;

		public UnknownPlatformException(Platform platform) : base($"Unknown Platform value: {platform}") { Platform = platform; }
	}

	public class UnknownArchitectureException : Exception {
		public Architecture Architecture;

		public UnknownArchitectureException(Architecture architecture) : base($"Unknown Architecture value: {architecture}") { Architecture = architecture; }
	}

	public class InvalidPluginHierarchyException : Exception {
		public string MissingDirectory;

		public InvalidPluginHierarchyException(string missing_directory) : base($"Component is missing the following directory in its plugins folder: {missing_directory}") {
			MissingDirectory = missing_directory;
		}
	}

	public abstract class PlatformPlugin {
		protected static Logger Logger = new Logger(nameof(PlatformPlugin));

		private const string TEST_LIB_NAME = "CSteamworks";
		protected static readonly Dictionary<Platform, String> PlatformIdentifiers = new Dictionary<Platform, string> {
			{Platform.Linux, "Linux"},
			{Platform.Windows, "Windows"},
			{Platform.Mac, "MacOS"},
			{Platform.Unknown, "UNKNOWN"}
		};

		private static PlatformPlugin LinuxPlatformPlugin = new LinuxPlatformPlugin();
		private static PlatformPlugin WindowsPlatformPlugin = new WindowsPlatformPlugin();
		private static PlatformPlugin MacPlatformPlugin = new MacPlatformPlugin();

		private string _PathSuffix32Bit;
		private string _PathSuffix64Bit;

		protected PlatformPlugin(Platform plat, bool separate_arch = true) {
			var id = PlatformID(plat);

			if (separate_arch) {
				_PathSuffix32Bit = Path.Combine(id, "32");
				_PathSuffix64Bit = Path.Combine(id, "64");
			} else {
				_PathSuffix32Bit = _PathSuffix64Bit = id;
			}
		}

		protected string Get64BitSourcePath(string base_dir) {
			return Path.Combine(base_dir, _PathSuffix64Bit);
		}

		protected string Get32BitSourcePath(string base_dir) {
			return Path.Combine(base_dir, _PathSuffix32Bit);
		}

		protected string Get64BitTargetPath(string base_dir) {
			return Path.Combine(base_dir, "x86_64");
		}

		protected string Get32BitTargetPath(string base_dir) {
			return Path.Combine(base_dir, "x86");
		}

		protected void CopyPlugins(string dir, string target_dir, string extension) {
			var ents = Directory.GetFileSystemEntries(dir);
			foreach (var ent in ents) {
				if (!ent.EndsWith(extension, StringComparison.InvariantCulture)) continue;
				if (Directory.Exists(ent)) continue;
				var filename = Path.GetFileName(ent);

				var target_path = Path.Combine(target_dir, filename);
				Logger.Debug($"Copying plugin from {ent} to {target_path}");
				File.Copy(ent, target_path, overwrite: true);
			}
		}

		protected static string PlatformID(Platform plat) { return PlatformIdentifiers[plat]; }

		protected abstract void CopyImpl(string source_root_plugin_dir, string target_plugin_dir);
		public void Copy(string source_root_plugin_dir, string target_plugin_dir) {
			Logger.Debug($"Copying platform plugins from {source_root_plugin_dir} to {target_plugin_dir}");
			// Enforce the requirement for all platforms and architectures to be included
			_EnforcePluginHierarchy(source_root_plugin_dir, Platform.Linux, "32", "64");
			_EnforcePluginHierarchy(source_root_plugin_dir, Platform.Windows, "32", "64");
			_EnforcePluginHierarchy(source_root_plugin_dir, Platform.Mac);

			CopyImpl(source_root_plugin_dir, target_plugin_dir);
		}

		private static void _EnforcePluginHierarchy(string plugin_dir, Platform plat, params string[] subdirs) {
			var dir = PlatformID(plat);
			var full_path = Path.Combine(plugin_dir, dir);
			Logger.Debug($"Checking for plugin dir: {full_path}");
			if (!Directory.Exists(full_path)) throw new InvalidPluginHierarchyException(dir);
			if (subdirs.Length == 0) {
				foreach (var subdir in subdirs) {
					var subdir_path = Path.Combine(full_path, subdir);
					if (!Directory.Exists(subdir_path)) throw new InvalidPluginHierarchyException(Path.Combine(dir, subdir));
				}
			}
		}

		public static PlatformPlugin Create(Platform? platform = null) {
			if (platform == null) platform = Autodetector.Platform;

			switch(platform) {
			case Platform.Linux: return LinuxPlatformPlugin;
			case Platform.Windows: return WindowsPlatformPlugin;
			case Platform.Mac: return MacPlatformPlugin;
			default: throw new UnknownPlatformException(platform.Value);
			}
		}
	}

	public class LinuxPlatformPlugin : PlatformPlugin {
		internal LinuxPlatformPlugin() : base(Platform.Linux) {}

		protected override void CopyImpl(string source_root_plugin_dir, string target_plugin_dir) {
			Logger.Info("Copying 32 bit plugins");
			CopyPlugins(Get32BitSourcePath(source_root_plugin_dir), Get32BitTargetPath(target_plugin_dir), extension: ".so");
			Logger.Info("Copying 64 bit plugins");
			CopyPlugins(Get64BitSourcePath(source_root_plugin_dir), Get64BitTargetPath(target_plugin_dir), extension: ".so");
        }
    }

	public class MacPlatformPlugin : PlatformPlugin {
		internal MacPlatformPlugin() : base(Platform.Mac, separate_arch: false) {}

		protected override void CopyImpl(string source_root_plugin_dir, string target_plugin_dir) {
			Logger.Info("Copying plugins");
			CopyPlugins(source_root_plugin_dir, target_plugin_dir, extension: ".bundle");
		}
	}

	public class WindowsPlatformPlugin : PlatformPlugin {
		private const string TEST_DLL = "CSteamworks.dll";

		internal WindowsPlatformPlugin() : base(Platform.Windows, separate_arch: false) { }

		private string DetermineArch(string target_plugin_dir) {
			var test_dll_path = Path.Combine(target_plugin_dir, TEST_DLL);
			if (!File.Exists(test_dll_path)) throw new Exception($"Missing DLL (used to guess the architecture): {test_dll_path}");

			using (var r = new BinaryReader(File.OpenRead(test_dll_path))) {
				// DOS header
				var dosmagic = r.ReadBytes(2);
				if (dosmagic[0] != (byte)'M' || dosmagic[1] != (byte)'Z') throw new Exception($"DLL used to guess the architecture is corrupted: {test_dll_path} (DOS magic number is not MZ)");
				r.ReadBytes(58); // padding
				var offset = r.ReadUInt32();

				// PE header
				r.BaseStream.Seek(offset, SeekOrigin.Begin);
				var pemagic = r.ReadBytes(2);
				if (pemagic[0] != (byte)'P' || pemagic[1] != (byte)'E') throw new Exception($"DLL used to guess the architecture is corrupted: {test_dll_path} (PE magic number is not PE)");
				r.ReadBytes(2); // padding
				var machine_flag = r.ReadUInt16();

				if (machine_flag == 0x8664 || machine_flag == 0x0200) return "64";
				else if (machine_flag == 0x014c) return "32";
				else throw new Exception($"Unknown PE machine flag (can't determine architecture): {machine_flag}");
			}
		}
		
		protected override void CopyImpl(string source_root_plugin_dir, string target_plugin_dir) {
			Logger.Info("Determining architecture");
			var arch = DetermineArch(target_plugin_dir);

			Logger.Info($"Architecture is: {arch} bit");

			Logger.Info("Copying plugins");

			CopyPlugins(Path.Combine(source_root_plugin_dir, arch), target_plugin_dir, extension: ".dll");
		}
	}
}
