using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using MTGInstaller.Options;

namespace MTGInstaller {
	public static class Program {
		public static Logger Logger = new Logger("ETGMod Installer");

		private static void _WriteError(Exception ex) {
			using (var stderr = Console.OpenStandardError())
			using (var writer = new StreamWriter(stderr)) {
				writer.WriteLine(ex.Message);

				if (System.Environment.GetEnvironmentVariable("MTG_VERBOSE") != null) writer.WriteLine(ex.StackTrace);
			}
		}

		public static int DownloadMain(DownloadOptions opts) {
			try {
				var installer = new InstallerFrontend(opts.Offline ? InstallerFrontend.InstallerOptions.Offline : InstallerFrontend.InstallerOptions.None);
			
				installer.Download(new ComponentInfo(opts.Component, opts.Version), opts.Force).Dispose();
				return 0;
			} catch (Exception e) {
				_WriteError(e);
				return 1;
			}
		}

		public static int AutodetectMain(AutodetectOptions opts) {
			try {
				if (opts.Architecture != null) {
					Autodetector.Architecture = (Architecture)Enum.Parse(typeof(Architecture), opts.Architecture);
				}

				var path = Autodetector.ExePath;
				if (path == null) Console.WriteLine("[Couldn't find the executable]");
				else Console.WriteLine(path);
				return 0;
			} catch (Exception e) {
				_WriteError(e);
				return 1;
			}
		}

		public static int ComponentsMain(ComponentsOptions opts) {
			try {
				var installer = new InstallerFrontend(opts.Offline ? InstallerFrontend.InstallerOptions.Offline : InstallerFrontend.InstallerOptions.None);

				foreach (var component_file in opts.CustomComponentFiles) {
					Logger.Debug($"Adding custom component file: {component_file}");
					installer.LoadComponentsFile(component_file);
				}

				foreach (var com in installer.AvailableComponents) {
					Console.WriteLine(com.Value);
				}
			} catch (Exception e) {
				_WriteError(e);
				return 1;
			}

			return 0;
		}

		public static int ComponentMain(ComponentOptions opts) {
			try {
				var installer = new InstallerFrontend(opts.Offline ? InstallerFrontend.InstallerOptions.Offline : InstallerFrontend.InstallerOptions.None);

				foreach (var component_file in opts.CustomComponentFiles) {
					Logger.Debug($"Adding custom component file: {component_file}");
					installer.LoadComponentsFile(component_file);
				}

				ETGModComponent component;
				if (installer.AvailableComponents.TryGetValue(opts.Name, out component)) {
					Console.WriteLine($"Name: {component.Name}");
					Console.WriteLine($"Author: {component.Author}");
					if (component.Description.Contains("\n")) {
						Console.WriteLine($"Description:");
						Console.WriteLine($"  {component.Description.Replace("\n", "\n  ")}");
					} else {
						Console.WriteLine($"Description: {component.Description}");
					}
					Console.WriteLine("Versions:");
					foreach (var ver in component.Versions) {
						Console.WriteLine($"  {ver}");
					}
				} else {
					Console.WriteLine($"Component {opts.Name} doesn't exist or isn't in the official list.");
					return 1;
				}
			} catch (Exception e) {
				_WriteError(e);
				return 1;
			}
			return 0;
		}

		public static int UninstallMain(UninstallOptions opts) {
			try {
				if (opts.Architecture != null) {
					Autodetector.Architecture = (Architecture)Enum.Parse(typeof(Architecture), opts.Architecture);
				}

				var installer = new InstallerFrontend(InstallerFrontend.InstallerOptions.None);
				var path = opts.Executable;
				if (path == null) path = Autodetector.ExePath;
				if (path == null) {
					Logger.Error($"Failed to autodetect an EtG installation - please use the '--executable' option to specify the location of {Autodetector.ExeName}");
					return 1;
				}
				installer.Uninstall(path);
				return 0;
			} catch (Exception e) {
				_WriteError(e);
				return 1;
			}
		}

		public static int InstallMain(InstallOptions opts) {
			try {
				if (opts.Architecture != null) {
					Autodetector.Architecture = (Architecture)Enum.Parse(typeof(Architecture), opts.Architecture);
				}

				var installer = new InstallerFrontend(opts.Offline ? InstallerFrontend.InstallerOptions.Offline : InstallerFrontend.InstallerOptions.None);
				if (opts.HTTP) installer.Options |= InstallerFrontend.InstallerOptions.HTTP;
				if (opts.ForceBackup) installer.Options |= InstallerFrontend.InstallerOptions.ForceBackup;
				if (opts.LeavePatchDLLs) installer.Options |= InstallerFrontend.InstallerOptions.LeavePatchDLLs;
				if (opts.SkipVersionChecks) installer.Options |= InstallerFrontend.InstallerOptions.SkipVersionChecks;

				foreach (var component_file in opts.CustomComponentFiles) {
					Logger.Debug($"Adding custom component file: {component_file}");
					installer.LoadComponentsFile(component_file);
				}

				var component_list = new List<ComponentInfo>();

				var component_strs = opts.Components.Split(';');
				foreach (var com_str in component_strs) {
					var split = com_str.Split(new char[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
					if (split.Length < 1 || split.Length > 2) {
						Console.WriteLine($"Improperly formatted component list - components should be separated by semicolons and may optionally have a version specified by putting '@VER' right after the name.");
						Console.WriteLine($"Example: ETGMod;Example@1.0;SomethingElse;AnotherComponent@0.banana");
						return 1;
					}

					component_list.Add(new ComponentInfo(split[0], split.Length == 2 ? split[1] : null));
				}

				installer.Install(component_list, opts.Executable);
				if (Settings.Instance.UnityDebug) installer.InstallUnityDebug();
				if (Settings.Instance.ILDebug) installer.InstallILDebug();
				return 0;
			} catch (Exception e) {
				_WriteError(e);
				return 1;
			}
		}

		private static bool? _StrBool(string v) {
			if (v == null) return null;

			var normalized = v.Trim().ToLowerInvariant();
			if (normalized == "y" || normalized == "yes" || normalized == "true" || normalized == "t") return true;
			else if (normalized == "n" || normalized == "no" || normalized == "false" || normalized == "f") return false;
			else {
				Logger.Error($"Unknown boolean value: {v}");
				return null;
			}
		}

		public static int SettingsMain(SettingsOptions opts) {
			try {
				var settings = Settings.Instance;

				if (opts.ClearCustomComponentFiles) settings.CustomComponentFiles = new List<string>();
				if (opts.ClearExecutablePath) settings.ExecutablePath = null;

				if (opts.CustomComponentFiles != null) {
					foreach (var com in opts.CustomComponentFiles) {
						if (settings.CustomComponentFiles.Contains(com)) {
							Logger.Warn($"The custom components file list already contains entry '{com}' - ignoring");
						} else settings.CustomComponentFiles.Add(com);
					}
				}

				var force_http = _StrBool(opts.ForceHTTP);
				var force_backup = _StrBool(opts.ForceBackup);
				var skip_version_checks = _StrBool(opts.SkipVersionChecks);
				var leave_patch_dlls = _StrBool(opts.LeavePatchDLLs);
				var unity_debug = _StrBool(opts.UnityDebug);
				var il_debug = _StrBool(opts.ILDebug);
				var offline_mode = _StrBool(opts.OfflineMode);

				if (opts.ExecutablePath != null) {
					if (Directory.Exists(opts.ExecutablePath)) {
						var f = Path.Combine(opts.ExecutablePath, Autodetector.ExeName);
						if (File.Exists(f)) opts.ExecutablePath = f;
						else {
							Logger.Error($"Provided executable path '{opts.ExecutablePath}' is actually a directory (and it doesn't contain {Autodetector.ExeName})");
							return 1;
						}
					}

					if (!File.Exists(opts.ExecutablePath)) {
						Logger.Error($"File '{opts.ExecutablePath}' doesn't exist");
						return 1;
					}

					if (Path.GetFileName(opts.ExecutablePath) != Autodetector.ExeName) {
						Logger.Error($"File '{opts.ExecutablePath}' is either not a Gungeon executable or a Gungeon executable for a different OS and/or platform (expected {Autodetector.ExeName})");
						return 1;
					}

					settings.ExecutablePath = opts.ExecutablePath;
				}
				if (force_http != null) settings.ForceHTTP = force_http.Value;
				if (force_backup != null) settings.ForceBackup = force_backup.Value;
				if (offline_mode != null) settings.Offline = offline_mode.Value;
				if (skip_version_checks != null) settings.SkipVersionChecks = skip_version_checks.Value;
				if (leave_patch_dlls != null) settings.LeavePatchDLLs = leave_patch_dlls.Value;
				if (unity_debug != null) settings.UnityDebug = unity_debug.Value;
				if (il_debug != null) settings.ILDebug = il_debug.Value;
				if (opts.SevenZipExePath != null) {
					settings.SevenZipPath = opts.SevenZipExePath;
				}

				settings.Save();

				Console.WriteLine(Settings.Instance.UserFriendly);
				return 0;
			} catch (Exception e) {
				_WriteError(e);
				return 1;
			}
		}

		public static int Main(string[] args) {
			var result = Parser.Default.ParseArguments<DownloadOptions, AutodetectOptions, ComponentsOptions, ComponentOptions, InstallOptions, UninstallOptions, SettingsOptions>(args);
			return result.MapResult(
				(DownloadOptions opts) => DownloadMain(opts),
				(AutodetectOptions opts) => AutodetectMain(opts),
				(ComponentsOptions opts) => ComponentsMain(opts),
				(ComponentOptions opts) => ComponentMain(opts),
				(InstallOptions opts) => InstallMain(opts),
				(UninstallOptions opts) => UninstallMain(opts),
				(SettingsOptions opts) => SettingsMain(opts),
				errors => 1
			);
		}
	}
}
