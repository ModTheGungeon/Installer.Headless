using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using MTGInstaller.Options;
using MTGInstaller.YAML;

namespace MTGInstaller {
	public static class Program {
		public static Installer Installer;
		public static Downloader Downloader;
		public static Logger Logger = new Logger("ETGMod Installer");

		private static void _SetupDownloader(bool force_http = false) {
			if (Downloader != null) return;
			Downloader = new Downloader(force_http);
		}

		private static void _SetupInstaller(string game_dir, bool force_http = false) {
			if (Installer != null) return;
			_SetupDownloader(force_http);
			try {
				Installer = new Installer(Downloader, game_dir);
			} catch (System.Net.WebException e) {
				var resp = e.Response as System.Net.HttpWebResponse;
				if (resp?.StatusCode == System.Net.HttpStatusCode.NotFound) Console.WriteLine($"404 error while trying to download version list");
				else Console.WriteLine($"Unhandled error occured while downloading version list: {e}");
				Environment.Exit(1);
			}
		}

		public static int DownloadMain(DownloadOptions opts) {
			_SetupDownloader(opts.HTTP);

			ETGModComponent component = Downloader.TryGet(opts.Component);
			if (component != null) {
				ETGModVersion version = null;

				if (opts.Version != null) {
					foreach (var ver in component.Versions) {
						if (ver.Key == opts.Version) {
							version = ver;
							break;
						}
					}
					if (version == null) {
						Console.WriteLine($"Version {opts.Version} of component {component.Name} doesn't exist.");
						return 1;
					}
				} else version = component.Versions[0];


				var dest = version.DisplayName;
				if (Directory.Exists(dest)) {
					if (opts.Force) {
						Directory.Delete(dest, recursive: true);
					} else {
						Console.WriteLine($"Version is already downloaded in the '{dest}' folder (use --force to redownload)");
						return 1; 
					}
				}

				try {
					var dl = Downloader.Download(version, dest);
					Console.WriteLine(dl.Path);
				} catch (System.Net.WebException e) {
					var resp = e.Response as System.Net.HttpWebResponse;
					if (resp?.StatusCode == System.Net.HttpStatusCode.NotFound) Console.WriteLine($"Error 404 while downloading version {version.DisplayName} of component {component.Name}.");
					else Console.WriteLine($"Unhandled error occured while downloading version {version.DisplayName} of component {component.Name}: {e.Message}.");
					return 1;
				}
			} else {
				Console.WriteLine($"Component {opts.Component} doesn't exist.");
				return 1;
			}

			return 0;
		}

		public static int AutodetectMain(AutodetectOptions opts) {
			var path = Autodetector.ExePath;
			if (path == null) Console.WriteLine("[Couldn't find the executable]");
			else Console.WriteLine(path);
			return 0;
		}

		public static int ComponentsMain(ComponentsOptions opts) {
			_SetupDownloader(opts.HTTP);

			foreach (var component_file in opts.CustomComponentFiles) {
				Logger.Debug($"Adding custom component file: {component_file}");
				Downloader.AddComponentsFile(File.ReadAllText(component_file));
			}

			foreach (var com in Downloader.Components) {
				Console.WriteLine(com.Value);
			}
			return 0;
		}

		public static int ComponentMain(ComponentOptions opts) {
			_SetupDownloader(opts.HTTP);

			foreach (var component_file in opts.CustomComponentFiles) {
				Logger.Debug($"Adding custom component file: {component_file}");
				Downloader.AddComponentsFile(File.ReadAllText(component_file));
			}

			ETGModComponent component;
			if (Downloader.Components.TryGetValue(opts.Name, out component)) {
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
			return 0;
		}

		public static int InstallMain(InstallOptions opts) {
			_SetupDownloader(opts.HTTP);

			var path = opts.Executable;
			if (path == null) path = Autodetector.ExePath;
			if (path == null) {
				Logger.Error($"Failed to autodetect an EtG installation - please use the '--executable' option to specify the location of {Autodetector.ExeName}");
				return 1;
			}

			foreach (var component_file in opts.CustomComponentFiles) {
				Logger.Debug($"Adding custom component file: {component_file}");
				Downloader.AddComponentsFile(File.ReadAllText(component_file));
			}

			Logger.Info($"EXE path: {path}");

			var gungeon_version = Autodetector.GetVersionIn(path);

			_SetupInstaller(path, opts.HTTP);

			if (!opts.ForceBackup) Installer.Restore();
			Installer.Backup(opts.ForceBackup);

			var used_components = new HashSet<ETGModComponent>();

			var component_strs = opts.Components.Split(';');
			foreach (var com_str in component_strs) {
				var split = com_str.Split(new char[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
				if (split.Length < 1 || split.Length > 2) {
					Console.WriteLine($"Improperly formatted component list - components should be separated by semicolons and may optionally have a version specified by putting '@VER' right after the name.");
					Console.WriteLine($"Example: ETGMod;Example@1.0;SomethingElse;AnotherComponent@0.banana");
					return 1;
				}

				var component_name = split[0];
				string component_ver = null;
				if (split.Length == 2) component_ver = split[1];

				ETGModComponent component = Downloader.TryGet(component_name);

				if (component != null) {
					if (used_components.Contains(component)) {
						Console.WriteLine($"Duplicate {component.Name} component.");
						return 1;
					}

					ETGModVersion version = null;
					if (component_ver == null) version = component.Versions[0];
					else {
						foreach (var ver in component.Versions) {
							if (ver.Key == component_ver) {
								version = ver;
								break;
							}
						}
						if (version == null) {
							Console.WriteLine($"Version {component_ver} of component {component_name} doesn't exist.");
							return 1;
						}
					}

					used_components.Add(component);

					using (var build = Downloader.Download(version)) {
						var installable = new Installer.InstallableComponent(component, version, build);
						if (!opts.SkipVersionChecks) {
							try {
								installable.ValidateGungeonVersion(gungeon_version);
							} catch (Installer.InstallableComponent.VersionMismatchException e) {
								Logger.Error(e.Message);
							}
						}
						Installer.Install(installable, opts.LeavePatchDLLs);
					}
				} else {
					Console.WriteLine($"Component {component_name} doesn't exist in the list of components.");
					return 1;
				}
			}
			return 0;
		}

		public static int Main(string[] args) {
			var result = Parser.Default.ParseArguments<DownloadOptions, AutodetectOptions, ComponentsOptions, ComponentOptions, InstallOptions>(args);
			return result.MapResult(
				(DownloadOptions opts) => DownloadMain(opts),
				(AutodetectOptions opts) => AutodetectMain(opts),
				(ComponentsOptions opts) => ComponentsMain(opts),
				(ComponentOptions opts) => ComponentMain(opts),
				(InstallOptions opts) => InstallMain(opts),
				errors => 1
			);
		}
	}
}
