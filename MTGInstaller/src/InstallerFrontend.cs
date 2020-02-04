using System;
using System.Collections.Generic;
using System.IO;

namespace MTGInstaller {
	public class InstallerFrontend {
		[Flags]
		public enum InstallerOptions {
			None = 0,
			SkipVersionChecks = 1,
			ForceBackup = 2,
			HTTP = 4,
			LeavePatchDLLs = 8,
			Offline = 16
		}

		private static Logger _Logger = new Logger("InstallerFrontend");
		private Installer _Installer;
		private Downloader _Downloader;
		private DebugConverter _DebugConverter;

		public InstallerOptions Options;
		public List<ETGModComponent> Components;

		public Dictionary<string, ETGModComponent> AvailableComponents {
			get { return _Downloader.Components; }
		}

		public class InstallationFailedException : Exception { public InstallationFailedException(string msg) : base(msg) {} }

		public InstallerFrontend(InstallerOptions options = InstallerOptions.None) {
			var settings = Settings.Instance;

			if (settings.SkipVersionChecks) Options |= InstallerOptions.SkipVersionChecks;
			if (settings.ForceHTTP) Options |= InstallerOptions.HTTP;
			if (settings.LeavePatchDLLs) Options |= InstallerOptions.LeavePatchDLLs;
			if (settings.ForceBackup) Options |= InstallerOptions.ForceBackup;
			if (settings.Offline) Options |= InstallerOptions.Offline;
			Options |= options;

			_Downloader = new Downloader(force_http: Options.HasFlag(InstallerOptions.HTTP), offline: Options.HasFlag(InstallerOptions.Offline));
			_Installer = new Installer(_Downloader, exe_path: null);

			var cache_dir = Path.Combine(Settings.SettingsDir, "Unity");
			var sevenz_path = Settings.Instance.SevenZipPath;
			_DebugConverter = new DebugConverter(cache_dir, _Installer, sevenz_path);

			Environment.SetEnvironmentVariable("MONOMOD_DEBUG_FORMAT", "MDB");

			foreach (var ent in settings.CustomComponentFiles) {
				LoadComponentsFile(ent);
			}
		}

		public void LoadComponentsFile(string path) {
			try {
				_Downloader.AddComponentsFile(File.ReadAllText(path));
			} catch (FileNotFoundException) {
				throw new InstallationFailedException($"Local component file '{path}' doesn't exist - verify your settings?");
			}
		}

		public ETGModComponent TryGetComponent(string name) {
			ETGModComponent component = null;
			AvailableComponents.TryGetValue(name, out component);
			return component;
		}

		public DownloadedBuild Download(ComponentInfo component_info, bool force = false) {
			var component = TryGetComponent(component_info.Name);
			if (component != null) {
				ETGModVersion version = null;

				if (component_info.Version != null) {
					foreach (var ver in component.Versions) {
						if (ver.Key == component_info.Version) {
							version = ver;
							break;
						}
					}
					if (version == null) {
						throw new InstallationFailedException($"Version {component_info.Version} of component {component.Name} doesn't exist.");
					}
				} else version = component.Versions[0];

				var dest = version.DisplayName;
				if (Directory.Exists(dest)) {
					if (force) {
						Directory.Delete(dest, recursive: true);
					} else {
						throw new InstallationFailedException($"Version is already downloaded in the '{dest}' folder (use --force to redownload)");
					}
				}

				try {
					_Logger.Info($"OPERATION: Download. Target: {dest}");
					var dl = _Downloader.Download(version, dest);
					_Logger.Info($"OPERATION COMPLETED SUCCESSFULLY");
					return dl;
				} catch (System.Net.WebException e) {
					var resp = e.Response as System.Net.HttpWebResponse;
					if (resp?.StatusCode == System.Net.HttpStatusCode.NotFound) {
						throw new InstallationFailedException($"Error 404 while downloading version {version.DisplayName} of component {component.Name}.");
					} else {
						throw new InstallationFailedException($"Unhandled error occured while downloading version {version.DisplayName} of component {component.Name}: {e.Message}.");
					}
				}
			}
			throw new InstallationFailedException($"Component {component_info.Name} doesn't exist.");
		}

		private string _GetExePath(string suggestion = null) {
			if (suggestion == null) suggestion = Settings.Instance.ExecutablePath;
			if (suggestion == null) suggestion = Autodetector.ExePath;
			if (suggestion == null) throw new InstallationFailedException($"Can't find executable - please manually provide a path to {Autodetector.ExeName} manually");
			return suggestion;
		}

		public void Install(IEnumerable<ComponentInfo> components, string exe_path = null) {
			exe_path = _GetExePath(exe_path);

			var real_components = new List<ComponentVersion>();
			var used_components = new HashSet<ETGModComponent>();
			var gungeon_version = Autodetector.GetVersionIn(exe_path);

			foreach (var com in components) {
				ETGModComponent component = _Downloader.TryGet(com.Name.Trim());

				if (component != null) {
					if (used_components.Contains(component)) {
						throw new InstallationFailedException($"Duplicate {component.Name} component.");
					}
					used_components.Add(component);

					ETGModVersion version = null;
					if (com.Version == null) version = component.Versions[0];
					else {
						foreach (var ver in component.Versions) {
							if (ver.Key == com.Version.Trim()) {
								version = ver;
								break;
							}
						}
						if (version == null) {
							throw new InstallationFailedException($"Version {com.Version} of component {com.Name} doesn't exist.");
						}
					}

					real_components.Add(new ComponentVersion(component, version));
				} else {
					throw new InstallationFailedException($"Component {com.Name} doesn't exist in the list of components.");
				}
			}

			Install(real_components, exe_path);
		}

		public void InstallUnityDebug() {
			_Logger.Info($"OPERATION: InstallUnityDebug. Target: {_Installer.GameDir}");

			if (Settings.Instance.SevenZipPath == null) {
				throw new Exception("7z path must be set if UnityDebug is enabled");
			}
			_DebugConverter.ConvertToDebugBuild(Autodetector.Platform, Autodetector.Architecture);

			_Logger.Info($"OPERATION COMPLETED SUCCESSFULLY");
		}

		public void InstallILDebug() {
			_Logger.Info($"OPERATION: InstallILDebug. Target: {_Installer.GameDir}");

			_DebugConverter.InstallILDebug();

			_Logger.Info($"OPERATION COMPLETED SUCCESSFULLY");
		}

		public void Install(IEnumerable<ComponentVersion> components, string exe_path = null, Action<ComponentVersion> component_installed = null) {
			exe_path = _GetExePath(exe_path);
			_Installer.ChangeExePath(exe_path);

			_Logger.Info($"OPERATION: Install. Target: {exe_path}");
			
			var used_components = new HashSet<ETGModComponent>();
			var gungeon_version = Autodetector.GetVersionIn(exe_path);

			if (!Options.HasFlag(InstallerOptions.ForceBackup)) _Installer.Restore();
			_Installer.Backup(Options.HasFlag(InstallerOptions.ForceBackup));

			foreach (var pair in components) {
				using (var build = _Downloader.Download(pair.Version)) {
					var installable = new Installer.InstallableComponent(pair.Component, pair.Version, build);
					if (!Options.HasFlag(InstallerOptions.SkipVersionChecks)) {
						try {
							installable.ValidateGungeonVersion(gungeon_version);
						} catch (Installer.InstallableComponent.VersionMismatchException e) {
							throw new InstallationFailedException(e.Message);
						}
					}

					_Installer.InstallComponent(installable, Options.HasFlag(InstallerOptions.LeavePatchDLLs));
					if (component_installed != null) component_installed.Invoke(pair);
				}
			}

			_Logger.Info($"OPERATION COMPLETED SUCCESSFULLY");
		}

		public void Uninstall(string exe_path = null) {
			exe_path = _GetExePath(exe_path);
			_Installer.ChangeExePath(exe_path);

			_Logger.Info($"OPERATION: Uninstall. Target: {exe_path}");

			_Installer.Restore(force: true);

			_Logger.Info($"OPERATION COMPLETED SUCCESSFULLY");
		}

		public bool HasETGModInstalled(string exe_path = null) {
			exe_path = _GetExePath(exe_path);
			_Installer.ChangeExePath(exe_path);

			var etgmod_cache_path = Path.Combine(_Installer.ManagedDir, "ModBackup");
			return Directory.Exists(etgmod_cache_path);
		}

		public string[] GetPatchInfo(string exe_path = null) {
			exe_path = _GetExePath(exe_path);
			_Installer.ChangeExePath(exe_path);

			if (HasETGModInstalled(exe_path)) {
				return new string[] { "ETGMod Legacy" };
			}

			if (!File.Exists(_Installer.PatchesInfoFile)) return new string[0];

			var patches_list = new List<string>();
			using (var reader = new StreamReader(File.OpenRead(_Installer.PatchesInfoFile))) {
				while (!reader.EndOfStream) {
					patches_list.Add(reader.ReadLine());
				}
			}
			return patches_list.ToArray();
		}
	}
}
