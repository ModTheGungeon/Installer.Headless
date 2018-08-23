using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using MonoMod;
using Mono.Cecil;

namespace MTGInstaller {
	public class Installer {
		public static string Version { 
			get { 
				var attr = Attribute.GetCustomAttribute(Assembly.GetEntryAssembly(), typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
				return attr?.InformationalVersion ?? "???";
			}
		}
		public static Logger _Logger = new Logger(nameof(Installer));

		const string BACKUP_DIR_NAME = ".ETGModBackup";
		const string BACKUP_MANAGED_NAME = "Managed";
		const string BACKUP_PLUGINS_NAME = "Plugins";
		const string BACKUP_ROOT_NAME = "Root";
		const string TMP_PATCHED_EXE_NAME = "EtG.patched";
		const string TMP_PATCHED_UNITYPLAYER_DLL_NAME = "UnityPlayer.patched";
		const string BACKUP_VERSION_FILE_NAME = "backup_version.txt";

		public bool ExePatched = false;
		public string GameDir;
		public Downloader Downloader;

		public Installer(Downloader downloader, string exe_path) {
			ChangeExePath(exe_path);
			Downloader = downloader;
		}

		public void ChangeExePath(string exe_path) {
			GameDir = Path.GetDirectoryName(exe_path);
		}

		public string ExeFile { get { return Path.Combine(GameDir, Autodetector.ExeName); } }
		public string PatchedExeFile { get { return Path.Combine(GameDir, TMP_PATCHED_EXE_NAME); } }
		public string WindowsUnityPlayerDLL { get { return Path.Combine(GameDir, "UnityPlayer.dll"); } }
		public string PatchedWindowsUnityPlayerDLL { get { return Path.Combine(GameDir, TMP_PATCHED_UNITYPLAYER_DLL_NAME); } }
		public string ManagedDir { get { return Path.Combine(GameDir, "EtG_Data", "Managed"); } }
		public string PluginsDir { get { return Path.Combine(GameDir, "EtG_Data", "Plugins"); } }
		public string BackupDir { get { return Path.Combine(GameDir, BACKUP_DIR_NAME); } }
		public string BackupRootDir { get { return Path.Combine(BackupDir, BACKUP_ROOT_NAME); } }
		public string BackupManagedDir { get { return Path.Combine(BackupDir, BACKUP_MANAGED_NAME); } }
		public string BackupPluginsDir { get { return Path.Combine(BackupDir, BACKUP_PLUGINS_NAME); } }
		public string BackupVersionFile { get { return Path.Combine(BackupDir, BACKUP_VERSION_FILE_NAME); } }

		public void Restore(bool force = false) {
			if (!force && !Directory.Exists(BackupDir)) {
				_Logger.Info($"Backup doesn't exist - not restoring");
				return;
			}

			_Logger.Info("Restoring from backup");

			if (!File.Exists(BackupVersionFile)) _Logger.Warn("Backup version file is missing - did an error occur while creating the backup? The game files might be corrupted.");
			else {
				var ver = File.ReadAllText(BackupVersionFile);
				if (ver == Autodetector.Version) {
					_Logger.Debug($"Backup versions match");
				} else {
					_Logger.Debug($"Backup versions DON'T match");
					try {
						var bkp_ver_obj = new Version(ver);
						var cur_ver_obj = new Version(Autodetector.Version);

						if (cur_ver_obj > bkp_ver_obj) {
							_Logger.Info($"Backup version is older - assuming game was updated, wiping backup directory so that a new backup can be made");
						} else {
							_Logger.Warn($"Game version is older than the current backup - did you downgrade? Trying to carry on by wiping the backup directory so that a new backup can be made...");
						}
					} catch {
						_Logger.Warn("Exception while comparing versions (did the Gungeon versioning scheme change?). This is probably bad. Assuming update, wiping backup directory so that a new backup can be made");
					}
					Directory.Delete(BackupDir, recursive: true);
					return;
				}
			}

			if (File.Exists(PatchedExeFile)) {
				_Logger.Info($"Removing old temporary patched executable");
				File.Delete(PatchedExeFile);
			}

			if (!Directory.Exists(BackupRootDir)) _Logger.Warn("Root directory backup is missing - did an error occur while creating the backup? The game files might be corrupted.");
			else {
				var root_entries = Directory.GetFileSystemEntries(BackupRootDir);

				foreach (var ent in root_entries) {
					var file = Path.GetFileName(ent);

					_Logger.Debug($"Restoring root file: {file}");

					var target = Path.Combine(GameDir, file);
					if (File.Exists(target)) File.Delete(target);
					File.Copy(ent, target);
				}
			}

			if (!Directory.Exists(BackupManagedDir)) _Logger.Warn("Managed directory backup is missing - did an error occur while creating the backup? The game files might be corrupted.");
			else {
				_Logger.Debug($"WIPING Managed directory");
				Directory.Delete(ManagedDir, recursive: true);

				Directory.CreateDirectory(ManagedDir);

				var managed_entries = Directory.GetFileSystemEntries(BackupManagedDir);

				foreach (var ent in managed_entries) {
					var file = Path.GetFileName(ent);

					_Logger.Debug($"Restoring managed file: {file}");

					File.Copy(ent, Path.Combine(ManagedDir, file), overwrite: true);
				}
			}

			if (!Directory.Exists(BackupPluginsDir)) _Logger.Warn("Plugins directory backup is missing - did an error occur while creating the backup? The game files might be corrupted.");
			else {
				_Logger.Debug($"WIPING Plugins directory");
				Directory.Delete(PluginsDir, recursive: true);

				Directory.CreateDirectory(PluginsDir);

				var plugins_entries = Directory.GetFileSystemEntries(BackupPluginsDir);

				foreach (var ent in plugins_entries) {
					var file = Path.GetFileName(ent);

					_Logger.Debug($"Restoring plugins file/directory: {file}");

					Utils.CopyRecursive(ent, Path.Combine(PluginsDir, file));
				}
			}
		}

		public void Backup(bool force = false) {
			if (Directory.Exists(BackupDir)) {
				if (force) {
					Directory.Delete(BackupDir, recursive: true);
				} else {
					if (!Directory.Exists(BackupRootDir)) _Logger.Warn("Backup directory exists, but the root backup subdirectory is missing - did an error occure while creating the backup? The game files might be corrupted.");
					if (!Directory.Exists(BackupManagedDir)) _Logger.Warn("Backup directory exists, but the managed backup subdirectory is missing - did an error occure while creating the backup? The game files might be corrupted.");
					if (!Directory.Exists(BackupPluginsDir)) _Logger.Warn("Backup directory exists, but the plugins backup subdirectory is missing - did an error occure while creating the backup? The game files might be corrupted.");

					_Logger.Info($"Backup folder exists - not backing up");
					return;
				}
			}

			_Logger.Info("Performing backup");

			Directory.CreateDirectory(BackupDir);
			Directory.CreateDirectory(BackupRootDir);
			Directory.CreateDirectory(BackupManagedDir);
			Directory.CreateDirectory(BackupPluginsDir);

			var root_entries = Directory.GetFileSystemEntries(GameDir);
			foreach (var ent in root_entries) {
				var file = Path.GetFileName(ent);
				if (Downloader.GungeonMetadata.Executables.Contains(file)) {
					_Logger.Debug($"Backing up root file: {file}");

					File.Copy(ent, Path.Combine(BackupRootDir, file), overwrite: true);
				}
			}

			var managed_entries = Directory.GetFileSystemEntries(ManagedDir);
			foreach (var ent in managed_entries) {
				var file = Path.GetFileName(ent);
				if (Downloader.GungeonMetadata.ManagedFiles.Contains(file)) {
					_Logger.Debug($"Backing up managed file: {file}");

					File.Copy(ent, Path.Combine(BackupManagedDir, file), overwrite: true);
				}
			}

			var plugins_entries = Directory.GetFileSystemEntries(PluginsDir);
			foreach (var ent in plugins_entries) {
				var file = Path.GetFileName(ent);
				// TODO filter?
				_Logger.Debug($"Backing up plugins file: {file}");

				Utils.CopyRecursive(ent, Path.Combine(BackupPluginsDir, file));
			}

			_Logger.Debug($"Backed up for Gungeon {Autodetector.Version}");
			File.WriteAllText(BackupVersionFile, Autodetector.Version);
		}

		public void PatchExe() {
			if (ExePatched) return;
			ExePatched = true;

			var patch_file = ExeFile;
			var target_file = PatchedExeFile;
			if (Autodetector.Platform == Platform.Windows) {
				// why in holy hell does this exist? what's the point?
				patch_file = WindowsUnityPlayerDLL;
				target_file = PatchedWindowsUnityPlayerDLL;
			}

			if (Downloader.GungeonMetadata.ExeOrigSubsitutions == null) return;
			_Logger.Info("Patching executable to substitute symbols");

			string perm_octal = null; // unix only
			if (Autodetector.Unix) {
				var stat_arg = "-c";
				if (Autodetector.Platform == Platform.Mac) stat_arg = "-f";

				Process p = Process.Start(new ProcessStartInfo {
					FileName = "/usr/bin/stat",
					UseShellExecute = false,
					Arguments = $"{stat_arg} '%a' '{patch_file}'",
					RedirectStandardOutput = true
				});
				perm_octal = p.StandardOutput.ReadToEnd().Trim();
				p.WaitForExit();
				p.Close();

				_Logger.Debug($"Permissions on executable: {perm_octal}");
			}

			using (var reader = new BinaryReader(File.OpenRead(patch_file)))
			using (var writer = new BinaryWriter(File.OpenWrite(target_file))) {
				ExePatcher.Patch(reader, writer, Downloader.GungeonMetadata.ExeOrigSubsitutions);
			}

			_Logger.Debug($"Replacing executable");
			if (File.Exists(patch_file)) File.Delete(patch_file);
			File.Move(target_file, patch_file);

			if (Autodetector.Unix) {
				_Logger.Info($"Restoring executable permissions");

				Process p = Process.Start(new ProcessStartInfo {
					FileName = "/usr/bin/chmod",
					UseShellExecute = false,
					Arguments = $"'{perm_octal}' '{patch_file}'"
				});
				p.WaitForExit();
				p.Close();
			}
		}

		public void InstallComponent(InstallableComponent comp, bool leave_mmdlls = false) {
			comp.Install(this, leave_mmdlls);
		}

		public class InstallableComponent {
			private Logger _Logger;
			const string MONOMOD_SUFFIX = ".mm.dll";  // must have priority over DLL_SUFFIX
			const string DLL_SUFFIX = ".dll";
			const string EXE_SUFFIX = ".exe";
			const string TXT_SUFFIX = ".txt";
			const string METADATA = "metadata.yml";
			const string PLUGINS = "Plugins";
			const string TMP_PATCH_SUFFIX = ".patched";

			public List<string> Assemblies = new List<string>();
			public List<string> PatchDLLs = new List<string>();
			public List<string> OtherFiles = new List<string>();
			public List<string> Dirs = new List<string>();
			public ComponentMetadata Metadata;
			private string _Name;
			public string Name { get { return Metadata?.Name ?? _Name; } }
			public string VersionKey;
			public string VersionName;
			public string ExtractedPath;
			public string SupportedGungeon;
			public bool RequiresPatchedExe;
			public bool HasPluginsDir = false;

			public IList<string> InstallInSubdir { get { return Metadata?.InstallInSubdir; } }
			public IList<string> InstallInManaged { get { return Metadata?.InstallInManaged; } }

			private TargetDirectory _GetTargetDir(string entry, TargetDirectory default_target = TargetDirectory.Managed) {
				var target = default_target;
				if (InstallInSubdir != null && InstallInSubdir.Contains(entry)) {
					target = TargetDirectory.Subdir;
				}
				if (InstallInManaged != null && InstallInManaged.Contains(entry)) {
					target = TargetDirectory.Managed;
				}
				return target;
			}

			private enum TargetDirectory {
				Managed,
				Subdir
			}


			public InstallableComponent(string name, string version_key, string version_name, string extracted_path, string supported_gungeon, bool requires_patched_exe, IList<string> dir_entries) {
				_Logger = new Logger($"Component {name}");

				_Name = name;
				VersionKey = version_key;
				VersionName = version_name;
				ExtractedPath = extracted_path;
				SupportedGungeon = supported_gungeon;
				RequiresPatchedExe = requires_patched_exe;

				foreach (var ent in dir_entries) {
					var filename = Path.GetFileName(ent);

					if (ent.EndsWith(MONOMOD_SUFFIX, StringComparison.InvariantCulture)) {
						PatchDLLs.Add(filename);
					} else if (ent.EndsWith(DLL_SUFFIX, StringComparison.InvariantCulture) || ent.EndsWith(EXE_SUFFIX, StringComparison.InvariantCulture)) {
						Assemblies.Add(filename);
					} else if (ent.EndsWith($"{Path.DirectorySeparatorChar}{METADATA}", StringComparison.InvariantCulture)) {
						var mt = File.ReadAllText(ent);
						Metadata = SerializationHelper.Deserializer.Deserialize<ComponentMetadata>(mt);
					} else if (filename == PLUGINS) {
						HasPluginsDir = true;
					} else {
						var attr = File.GetAttributes(ent);
						if (attr.HasFlag(FileAttributes.Directory)) {
							Dirs.Add(filename);
						} else {
							OtherFiles.Add(filename);
						}
					}
				}
			}

			public InstallableComponent(ETGModComponent component, ETGModVersion ver, DownloadedBuild dl) : this(
				component.Name,
				ver.Key,
				ver.DisplayName,
				dl.ExtractedPath,
				ver.SupportedGungeon,
				ver.RequiresPatchedExe,
				Directory.GetFileSystemEntries(dl.ExtractedPath)
			) { }

			public class VersionMismatchException : Exception { public VersionMismatchException(string msg) : base(msg) {} }

			public void ValidateGungeonVersion(string version) {
				if (SupportedGungeon == null) {
					_Logger.Warn($"{Name} {VersionName} does not have a specified supported Gungeon version.");
				} else {
					if (version == SupportedGungeon) {
						_Logger.Debug("Versions match.");
					} else {
						var gungeon_version_obj = new Version(version);
						var supported_version_obj = new Version(SupportedGungeon);

						if (supported_version_obj == gungeon_version_obj) {
							_Logger.Debug("Versions match (through System.Version comparison).");
						} else if (supported_version_obj > gungeon_version_obj) {
							throw new VersionMismatchException($"Version mismatch: your installation of Gungeon appears to be older than the version {Name} {VersionName} supports ({version} vs {SupportedGungeon}). You can update or try force skipping this check at your own responsibility.");
						} else {
							throw new VersionMismatchException($"Version mismatch: your installation of Gungeon appears to be newer than the version {Name} {VersionName} supports ({version} vs {SupportedGungeon}). You can try force skipping this check at your own responsibility.");
						}
					}
				}
			}

			private string AbsPath(string rel_path) {
				return Path.Combine(ExtractedPath, rel_path);
			}

			private void _Install(string name, IList<string> entries, string managed, bool subdir = false) {
				foreach (var ent in entries) {
					_Logger.Info($"Installing {name}: {ent}");

					var target = _GetTargetDir(ent, subdir ? TargetDirectory.Subdir : TargetDirectory.Managed);

					var local_target = managed;
					if (target == TargetDirectory.Subdir) local_target = Path.Combine(managed, Name);
					if (!Directory.Exists(local_target)) Directory.CreateDirectory(local_target);

					Utils.CopyRecursive(AbsPath(ent), Path.Combine(local_target, ent));
				}
			}

			private void _InstallPlugins(string target_dir) {
				var source_dir = Path.Combine(ExtractedPath, PLUGINS);

				var plugin_handler = PlatformPlugin.Create(Autodetector.Platform);
				plugin_handler.Copy(source_dir, target_dir);
			}

			public void Install(Installer installer, bool leave_mmdlls = false) {
				var managed = installer.ManagedDir;

				if (HasPluginsDir) _InstallPlugins(installer.PluginsDir);
				if (RequiresPatchedExe) installer.PatchExe();

				_Install("assembly", Assemblies, managed);
				_Install("MonoMod patch DLL", PatchDLLs, managed);
				_Install("file", OtherFiles, managed, subdir: true);
				_Install("directory", Dirs, managed, subdir: true);

				foreach (var patch_target in Metadata?.OrderedTargets ?? installer.Downloader.GungeonMetadata.ViablePatchTargets) {
					var patch_target_dll = Path.Combine(managed, $"{patch_target}.dll");
					var patch_target_tmp = Path.Combine(managed, $"{patch_target}{TMP_PATCH_SUFFIX}");

					var modder = new MonoModder {
						InputPath = patch_target_dll,
						OutputPath = patch_target_tmp
					};

					if (Metadata != null && Metadata.RelinkMap != null) {
						Dictionary<string, string> rmap;
						if (Metadata.RelinkMap.TryGetValue(patch_target, out rmap)) {
							_Logger.Info($"Reading component relink map for target {patch_target}");
							foreach (var pair in rmap) {
								ModuleDefinition module;
								if (!modder.DependencyCache.TryGetValue(pair.Value, out module)) {
									var path = Path.Combine(managed, pair.Value);
									_Logger.Debug($"Dependency not in cache: {pair.Value} ({path})");
									module = modder.DependencyCache[pair.Value] = ModuleDefinition.ReadModule(path);
								}

								_Logger.Debug($"Mapping {pair.Key} => {pair.Value}");
								modder.RelinkModuleMap[pair.Key] = module;
							}
						}
					}

					modder.Read();

					var found_mods = false;

					foreach (var dll in PatchDLLs) {
						if (File.Exists(Path.Combine(managed, dll)) && dll.StartsWith($"{patch_target}.", StringComparison.InvariantCulture)) {
							found_mods = true;
							_Logger.Debug($"Using patch DLL: {dll}");

							modder.ReadMod(Path.Combine(managed, dll));
						}
					}

					if (!found_mods) {
						_Logger.Info($"Not patching {patch_target} because this component has no patches for it");
						continue;
					}

					_Logger.Info($"Patching target: {patch_target}");
										
					modder.MapDependencies();
					modder.AutoPatch();
					modder.Write();
					modder.Dispose();

					_Logger.Debug($"Replacing original ({patch_target_tmp} => {patch_target_dll})");
					if (File.Exists(patch_target_dll)) File.Delete(patch_target_dll);
					File.Move(patch_target_tmp, patch_target_dll);
				}

				if (!leave_mmdlls) {
					foreach (var dll in PatchDLLs) {
						if (!File.Exists(Path.Combine(managed, dll))) continue;

						_Logger.Debug($"Cleaning up patch DLL {dll}");
						File.Delete(Path.Combine(managed, dll));
					}
				}

				_Logger.Debug($"Cleaning up patched DLL MDB/PDBs");
				foreach (var ent in Directory.GetFileSystemEntries(managed)) {
					if (ent.EndsWith($"{TMP_PATCH_SUFFIX}.mdb", StringComparison.InvariantCulture)) File.Delete(ent);
				}
			}
		}
	}
}
