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

		private static void _WriteErrorLine(string line) {
			using (var stderr = Console.OpenStandardError())
			using (var writer = new StreamWriter(stderr)) {
				writer.WriteLine(line);
			}
		}

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
			var installer = new InstallerFrontend(InstallerFrontend.InstallerOptions.None);
			
			try {
				installer.Download(new InstallerFrontend.ComponentInfo(opts.Component, opts.Version), opts.Force).Dispose();
			} catch (InstallerFrontend.InstallationFailedException e) {
				_WriteErrorLine(e.Message);
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
			var installer = new InstallerFrontend(InstallerFrontend.InstallerOptions.None);

			try {
				foreach (var component_file in opts.CustomComponentFiles) {
					Logger.Debug($"Adding custom component file: {component_file}");
					installer.LoadComponentsFile(component_file);
				}

				foreach (var com in installer.AvailableComponents) {
					Console.WriteLine(com.Value);
				}
			} catch (InstallerFrontend.InstallationFailedException e) {
				_WriteErrorLine(e.Message);
				return 1;
			}

			return 0;
		}

		public static int ComponentMain(ComponentOptions opts) {
			var installer = new InstallerFrontend(InstallerFrontend.InstallerOptions.None);

			try {
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
			} catch (InstallerFrontend.InstallationFailedException e) {
				_WriteErrorLine(e.Message);
				return 1;
			}
			return 0;
		}

		public static int InstallMain(InstallOptions opts) {
			var installer = new InstallerFrontend(InstallerFrontend.InstallerOptions.None);
			if (opts.HTTP) installer.Options |= InstallerFrontend.InstallerOptions.HTTP;
			if (opts.ForceBackup) installer.Options |= InstallerFrontend.InstallerOptions.ForceBackup;
			if (opts.LeavePatchDLLs) installer.Options |= InstallerFrontend.InstallerOptions.LeavePatchDLLs;
			if (opts.SkipVersionChecks) installer.Options |= InstallerFrontend.InstallerOptions.SkipVersionChecks;

			var path = opts.Executable;
			if (path == null) path = Autodetector.ExePath;
			if (path == null) {
				Logger.Error($"Failed to autodetect an EtG installation - please use the '--executable' option to specify the location of {Autodetector.ExeName}");
				return 1;
			}

			try {
				foreach (var component_file in opts.CustomComponentFiles) {
					Logger.Debug($"Adding custom component file: {component_file}");
					installer.LoadComponentsFile(component_file);
				}

				Logger.Info($"EXE path: {path}");

				var component_list = new List<InstallerFrontend.ComponentInfo>();

				var component_strs = opts.Components.Split(';');
				foreach (var com_str in component_strs) {
					var split = com_str.Split(new char[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
					if (split.Length < 1 || split.Length > 2) {
						Console.WriteLine($"Improperly formatted component list - components should be separated by semicolons and may optionally have a version specified by putting '@VER' right after the name.");
						Console.WriteLine($"Example: ETGMod;Example@1.0;SomethingElse;AnotherComponent@0.banana");
						return 1;
					}

					component_list.Add(new InstallerFrontend.ComponentInfo(split[0], split[1]));
				}

				installer.Install(component_list, path);
			} catch (InstallerFrontend.InstallationFailedException e) {
				_WriteErrorLine(e.Message);
				return 1;
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
