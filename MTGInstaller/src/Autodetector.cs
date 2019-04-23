using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace MTGInstaller {
	public enum Platform {
		Unknown,
		Windows,
		Linux,
		Mac
	}

	public enum Architecture {
		Unknown,
		X86,
		X86_64
	}

	public enum Distributor {
		Unknown,
		Other,
		Steam,
		GOG,
		WindowsStore
	}

	public static class Autodetector {
		private static Logger _Logger = new Logger(nameof(Autodetector));

		private static Architecture _Architecture = Architecture.Unknown;
		public static Architecture Architecture {
			get {
				if (_Architecture != Architecture.Unknown) return _Architecture;
				return _Architecture = IntPtr.Size == 4 ? Architecture.X86 : Architecture.X86_64;
			}
			set {
				_Architecture = value;
			}
		}

		private static Platform _Platform = Platform.Unknown;
		public static Platform Platform {
			get {
				if (_Platform != Platform.Unknown) return _Platform;

				var property_platform = typeof(Environment).GetProperty("Platform", BindingFlags.NonPublic | BindingFlags.Static);
				string platID;
				if (property_platform != null) {
					platID = property_platform.GetValue(null).ToString();
				} else {
					//for .net, use default value
					platID = Environment.OSVersion.Platform.ToString();
				}
				platID = platID.ToLowerInvariant();

				if (platID.Contains("win")) {
					_Platform = Platform.Windows;
				} else if (platID.Contains("mac") || platID.Contains("osx")) {
					_Platform = Platform.Mac;
				} else if (platID.Contains("lin") || platID.Contains("unix")) {
					_Platform = Platform.Linux;
				}

				return _Platform;
			}

			set {
				_Platform = value;
			}
		}

		public static bool Unix { get { return Platform == Platform.Linux || Platform == Platform.Mac; } }

		private static Distributor _Distributor = Distributor.Unknown;
		public static Distributor Distributor {
			get {
				if (_Distributor != Distributor.Unknown) return _Distributor;

				if (SteamPath != null) _Distributor = Distributor.Steam;
				if (GOGPath != null) _Distributor = Distributor.GOG;
				return _Distributor = Distributor.Other;
			}

			set {
				_Distributor = value;
			}
		}

		public static string ExeName {
			get {
				if (Platform == Platform.Linux) {
					if (Architecture == Architecture.X86) return "EtG.x86";
					return "EtG.x86_64";
				}
				if (Platform == Platform.Windows) return "EtG.exe";
				if (Platform == Platform.Mac) return "EtG_OSX";
				return null;
			}
		}

		public static string ProcessName {
			get {
				if (Platform == Platform.Linux) {
					if (Architecture == Architecture.X86) return "EtG.x86";
					return "EtG.x86_64";
				}
				if (Platform == Platform.Windows) return "EtG";
				if (Platform == Platform.Mac) return "EtG_OSX";
				return null; 
			}
		}

		public static string SteamPath {
			get {
				string path = null;

				if (Platform != Platform.Mac) {
					// On macOS, Steam is installed separately to the games library...
					Process[] processes = Process.GetProcesses(".");

					for (int i = 0; i < processes.Length; i++) {
						Process p = processes[i];

						try {
							if (!p.ProcessName.Contains("steam") || path != null) {
								p.Dispose();
								continue;
							}

							if (p.MainModule.ModuleName.ToLower().Contains("steam")) {
								path = p.MainModule.FileName;
								p.Dispose();
							}

							while (path.Contains("cef")) {
								path = Path.GetDirectoryName(path);
							}
							if (Path.GetFileName(path) == "bin")
								path = Path.Combine(path, "steam.exe");
						} catch (Exception) {
							//probably the service acting up, a process quitting or bitness mismatch
							p.Dispose();
						}
					}
				}

				if (path == null) {
					if (Platform == Platform.Linux) {
						path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".local/share/Steam");
						if (!Directory.Exists(path)) {
							return null;
						} else {
							path = Path.Combine(path, "ubuntu12_32/steam");
						}

					} else if (Platform == Platform.Mac) {
						//$HOME/Library/Application Support/Steam/SteamApps/common/Enter the Gungeon/EtG_OSX.app/
						path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "Library/Application Support/Steam");
						if (!Directory.Exists(path)) {
							return null;
						}
					} else if (Platform == Platform.Windows) {
						path = @"C:\Program Files (x86)\Steam\steam.exe";
						if (!File.Exists(path)) {
							path = @"C:\Program Files\Steam\steam.exe";
							if (!File.Exists(path)) {
								path = @"D:\Steam\steam.exe";
								if (!File.Exists(path)) return null;
							}
						}
					}
				}


				if (Platform == Platform.Windows) {
					//I think we're running in Windows right now...
					var dir = Directory.GetParent(path);
					if (dir.Name == "Steam") path = dir.FullName; //PF/Steam[/steam.exe]
					else path = dir.Parent.FullName; //PF/Steam[/bin/steam.exe]
				} else if (Platform == Platform.Mac) {
					//macOS is so weird...
					if (!Directory.Exists(path)) return null;
				} else if (Platform == Platform.Linux) {
					path = Directory.GetParent(path).Parent.FullName; //~/.local/share/Steam[/ubuntuX_Y/steam]
				} else {
					return null;
				}

				if (Directory.Exists(Path.Combine(path, "SteamApps"))) {
					path = Path.Combine(path, "SteamApps");
				} else {
					path = Path.Combine(path, "steamapps");
				}
				path = Path.Combine(path, "common"); //SA/common

				path = Path.Combine(path, "Enter the Gungeon");
				if (Platform == Platform.Mac) {
					path = Path.Combine(path, "EtG_OSX.app", "Contents", "MacOS");
				}

				return path;
			}
		}

		public static string GOGPath {
			get {
				string path = null;

				if (Platform == Platform.Mac) return null; // idk this shit


				if (Platform == Platform.Linux) {
					path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "GOG Games");
					if (!Directory.Exists(path)) {
						return null;
					}
				} else if (Platform == Platform.Windows) {
					path = @"C:\GOG Games";
					if (!Directory.Exists(path)) {
						return null;
					}
				} else {
					return null;
				}

				path = Path.Combine(path, "Enter the Gungeon");
				if (!Directory.Exists(path)) return null;

				return path;
			}
		}

		private static string _ExePath = null;
		public static string ExePath {
			get {
				if (_ExePath != null) return _ExePath;
				var path = SteamPath;
				if (path == null) path = GOGPath;
				if (path == null) return _ExePath = null;
				path = Path.Combine(path, ExeName);
				if (!File.Exists(path)) return _ExePath = null;
				return _ExePath = path;
			}
		}

		private static string[] _ReadVersion(string exe_path) {
			if (exe_path == null) return null;

			var game_dir = Path.GetDirectoryName(exe_path);
			var streaming_assets = Path.Combine(game_dir, "EtG_Data", "StreamingAssets");
			if (Platform == Platform.Mac) {
				streaming_assets = Path.Combine(game_dir, "Contents", "Resources", "Data", "StreamingAssets");
			}

			var txt = File.ReadAllLines(Path.Combine(streaming_assets, "version.txt"));
			if (txt.Length < 1 || txt.Length > 2) {
				_Logger.Error("The Gungeon version.txt file is corrupted or in an unrecognized format.");
				if (txt.Length < 1) return new string[] { "CORRUPTED VERSION.TXT" };
			}

			return txt;
		}

		public static string GetVersionIn(string exe_path) {
			var v = _ReadVersion(exe_path);
			if (v == null) return null;
			if (v.Length == 1) return v[0];
			return v[1];
		}

		public static string GetVersionNameIn(string exe_path) {
			var v = _ReadVersion(exe_path);
			if (v == null) return null;
			return v[0];
		}
	}
}
