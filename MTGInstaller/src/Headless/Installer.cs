using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using MonoMod;
using MTGInstaller.YAML;


namespace MTGInstaller {
	public class Installer {
		public static Logger _Logger = new Logger(nameof(Installer));

		const string BACKUP_DIR_NAME = ".ETGModBackup";
		const string BACKUP_MANAGED_NAME = "Managed";
		const string BACKUP_ROOT_NAME = "Root";

		public string GameDir;
		public Downloader Downloader;

		public Installer(Downloader downloader, string exe_path) {
			GameDir = Path.GetDirectoryName(exe_path);
			Downloader = downloader;
		}

		public string ManagedDir { get { return Path.Combine(GameDir, "EtG_Data", "Managed"); } }
		public string BackupDir { get { return Path.Combine(GameDir, BACKUP_DIR_NAME); } }
		public string BackupRootDir { get { return Path.Combine(BackupDir, BACKUP_ROOT_NAME); } }
		public string BackupManagedDir { get { return Path.Combine(BackupDir, BACKUP_MANAGED_NAME); } }

		public void Restore() {
			if (!Directory.Exists(BackupDir)) {
				_Logger.Info($"Backup doesn't exist - not restoring");
				return;
			}

			_Logger.Info("Restoring from backup");

			if (!Directory.Exists(BackupRootDir)) _Logger.Warn("Root directory backup is missing - did an error occur while creating the backup? The game files might be corrupted.");
			else {
				var root_entries = Directory.GetFileSystemEntries(BackupRootDir);

				foreach (var ent in root_entries) {
					var file = Path.GetFileName(ent);

					_Logger.Debug($"Restoring root file: {file}");

					File.Copy(ent, Path.Combine(GameDir, file), overwrite: true);
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
		}

		public void Backup(bool force = false) {
			if (Directory.Exists(BackupDir)) {
				if (force) {
					Directory.Delete(BackupDir, recursive: true);
				} else {
					if (!Directory.Exists(BackupRootDir)) _Logger.Warn("Backup directory exists, but the root backup subdirectory is missing - did an error occure while creating the backup? The game files might be corrupted.");
					if (!Directory.Exists(BackupManagedDir)) _Logger.Warn("Backup directory exists, but the managed backup subdirectory is missing - did an error occure while creating the backup? The game files might be corrupted.");

					_Logger.Info($"Backup folder exists - not backing up");
					return;
				}
			}

			_Logger.Info("Performing backup");

			Directory.CreateDirectory(BackupDir);
			Directory.CreateDirectory(BackupRootDir);
			Directory.CreateDirectory(BackupManagedDir);

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
		}

		public void Install(InstallableComponent comp) {
			comp.Install(this);
		}

		public class InstallableComponent {
			private Logger _Logger;
			const string MONOMOD_SUFFIX = ".mm.dll";  // must have priority over DLL_SUFFIX
			const string DLL_SUFFIX = ".dll";
			const string TXT_SUFFIX = ".txt";
			const string METADATA = "metadata.yml";
			const string TMP_PATCH_SUFFIX = ".patched";

			public readonly static string[] PatchTargets = {
				"Assembly-CSharp",
				"UnityEngine"
			};

			public List<string> DLLs = new List<string>();
			public List<string> PatchDLLs = new List<string>();
			public List<string> Texts = new List<string>();
			public ComponentMetadata Metadata;
			public string Name;
			public string VersionKey;
			public string VersionName;
			public string ExtractedPath;
			public string SupportedGungeon;

			public bool InstallAllInSubdir { get { return Metadata != null && Metadata.InstallAllInSubdir; } }
			public IList<string> InstallInSubdir { get { return Metadata?.InstallInSubdir; } }

			public bool InstalledInSubdir(string entry) {
				return entry.EndsWith(TXT_SUFFIX, StringComparison.InvariantCulture) || (InstallInSubdir != null && InstallInSubdir.Contains(entry));
			}

			public InstallableComponent(string name, string version_key, string version_name, string extracted_path, string supported_gungeon, IList<string> dir_entries) {
				_Logger = new Logger($"InstallableComponent:{name}");

				Name = name;
				VersionKey = version_key;
				VersionName = version_name;
				ExtractedPath = extracted_path;
				SupportedGungeon = supported_gungeon;

				foreach (var ent in dir_entries) {
					if (ent.EndsWith(MONOMOD_SUFFIX, StringComparison.InvariantCulture)) {
						PatchDLLs.Add(Path.GetFileName(ent));
					} else if (ent.EndsWith(DLL_SUFFIX, StringComparison.InvariantCulture)) {
						DLLs.Add(Path.GetFileName(ent));
					} else if (ent.EndsWith(TXT_SUFFIX, StringComparison.InvariantCulture)) {
						Texts.Add(Path.GetFileName(ent));
					} else if (ent.EndsWith($"{Path.DirectorySeparatorChar}{METADATA}", StringComparison.InvariantCulture)) {
						var mt = File.ReadAllText(ent);
						Metadata = SerializationHelper.Deserializer.Deserialize<ComponentMetadata>(mt);
					} else {
						_Logger.Warn($"Unknown file type: {Path.GetFileName(ent)}");
					}
				}
			}

			public InstallableComponent(ETGModComponent component, ETGModVersion ver, DownloadedBuild dl) : this(
				component.Name,
				ver.Key,
				ver.DisplayName,
				dl.ExtractedPath,
				ver.SupportedGungeon,
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
							throw new VersionMismatchException($"Version mismatch: your installation of Gungeon appears to be older than the version {Name} {VersionName} supports ({version} vs {SupportedGungeon}). You can update or try using '--force' to skip this check at your own responsibility.");
						} else {
							throw new VersionMismatchException($"Version mismatch: your installation of Gungeon appears to be newer than the version {Name} {VersionName} supports ({version} vs {SupportedGungeon}). You can try using '--force' to skip this check at your own responsibility.");
						}
					}
				}
			}

			private string AbsPath(string rel_path) {
				return Path.Combine(ExtractedPath, rel_path);
			}

			private void _Install(string name, IList<string> entries, string managed) {
				foreach (var ent in entries) {
					_Logger.Info($"Installing {name}: {ent}");

					var local_target = managed;
					if (InstalledInSubdir(ent)) local_target = Path.Combine(managed, Name);
					if (!Directory.Exists(local_target)) Directory.CreateDirectory(local_target);

					File.Copy(AbsPath(ent), Path.Combine(local_target, ent), overwrite: true);
				}
			}

			public void Install(Installer installer) {
				var managed = installer.ManagedDir;

				var target = managed;
				string[] force_in_subdir = null;

				if (Metadata != null) {
					if (Metadata.InstallAllInSubdir) target = Path.Combine(managed, Name);
					if (Metadata.InstallInSubdir != null) force_in_subdir = Metadata.InstallInSubdir;
				}

				_Install("DLL", DLLs, managed);
				_Install("MonoMod patch DLL", PatchDLLs, managed);
				_Install("text file", Texts, managed);

				foreach (var patch_target in installer.Downloader.GungeonMetadata.ViablePatchTargets) {
					var patch_target_dll = Path.Combine(managed, $"{patch_target}.dll");
					var patch_target_tmp = Path.Combine(managed, $"{patch_target}{TMP_PATCH_SUFFIX}");

					var modder = new MonoModder {
						InputPath = patch_target_dll,
						OutputPath = patch_target_tmp
					};

					modder.Read();

					var found_mods = false;

					foreach (var dll in PatchDLLs) {
						if (!InstalledInSubdir(dll) && dll.StartsWith($"{patch_target}.", StringComparison.InvariantCulture)) {
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

					_Logger.Debug($"Replacing original");
					if (File.Exists(patch_target)) File.Delete(patch_target);
					File.Move(patch_target_tmp, patch_target);
				}

				foreach (var dll in PatchDLLs) {
					if (InstalledInSubdir(dll)) continue;

					_Logger.Debug($"Cleaning up patch DLL {dll}");
					File.Delete(Path.Combine(managed, dll));
				}

				_Logger.Debug($"Cleaning up patch DLL MDB/PDBs");
				foreach (var ent in Directory.GetFileSystemEntries(managed)) {
					if (ent.EndsWith(".patched.mdb", StringComparison.InvariantCulture)) File.Delete(ent);
				}
			}
		}
	}
}
