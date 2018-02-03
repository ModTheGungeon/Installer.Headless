using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		const string TMP_PATCHED_EXE_NAME = "EtG.patched";

		public string GameDir;
		public Downloader Downloader;

		public Installer(Downloader downloader, string exe_path) {
			GameDir = Path.GetDirectoryName(exe_path);
			Downloader = downloader;
		}

		public string ExeFile { get { return Path.Combine(GameDir, Autodetector.ExeName); } }
		public string PatchedExeFile { get { return Path.Combine(GameDir, TMP_PATCHED_EXE_NAME); } }
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

		private void _PatchExe() {
			if (Downloader.GungeonMetadata.ExeOrigSubsitutions == null) return;
			_Logger.Info("Patching executable to substitute symbols");

			string perm_octal = null; // unix only
			if (Autodetector.Unix) {
				Process p = Process.Start(new ProcessStartInfo {
					FileName = "/usr/bin/stat",
					UseShellExecute = false,
					Arguments = $"-c '%a' '{ExeFile}'",
					RedirectStandardOutput = true
				});
				perm_octal = p.StandardOutput.ReadToEnd().Trim();
				p.WaitForExit();
				p.Close();

				_Logger.Debug($"Permissions on executable: {perm_octal}");
			}

			using (var reader = new BinaryReader(File.OpenRead(ExeFile)))
			using (var writer = new BinaryWriter(File.OpenWrite(PatchedExeFile))) {
				ExePatcher.Patch(reader, writer, Downloader.GungeonMetadata.ExeOrigSubsitutions);
			}

			_Logger.Debug($"Replacing executable");
			if (File.Exists(ExeFile)) File.Delete(ExeFile);
			File.Move(PatchedExeFile, ExeFile);

			if (Autodetector.Unix) {
				_Logger.Info($"Restoring executable permissions");

				Process p = Process.Start(new ProcessStartInfo {
					FileName = "/usr/bin/chmod",
					UseShellExecute = false,
					Arguments = $"'{perm_octal}' '{ExeFile}'"
				});
				p.WaitForExit();
				p.Close();
			}
		}


		public void Install(InstallableComponent comp, bool leave_mmdlls = false) {
			_PatchExe();
			comp.Install(this, leave_mmdlls);
		}

		public class InstallableComponent {
			private Logger _Logger;
			const string MONOMOD_SUFFIX = ".mm.dll";  // must have priority over DLL_SUFFIX
			const string DLL_SUFFIX = ".dll";
			const string EXE_SUFFIX = ".exe";
			const string TXT_SUFFIX = ".txt";
			const string METADATA = "metadata.yml";
			const string TMP_PATCH_SUFFIX = ".patched";

			public List<string> Assemblies = new List<string>();
			public List<string> PatchDLLs = new List<string>();
			public List<string> OtherFiles = new List<string>();
			public List<string> Dirs = new List<string>();
			public ComponentMetadata Metadata;
			private string _Name;
			public string Name { get { return Metadata.Name ?? _Name; } }
			public string VersionKey;
			public string VersionName;
			public string ExtractedPath;
			public string SupportedGungeon;

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


			public InstallableComponent(string name, string version_key, string version_name, string extracted_path, string supported_gungeon, IList<string> dir_entries) {
				_Logger = new Logger($"Component {name}");

				_Name = name;
				VersionKey = version_key;
				VersionName = version_name;
				ExtractedPath = extracted_path;
				SupportedGungeon = supported_gungeon;

				foreach (var ent in dir_entries) {
					var filename = Path.GetFileName(ent);

					if (ent.EndsWith(MONOMOD_SUFFIX, StringComparison.InvariantCulture)) {
						PatchDLLs.Add(filename);
					} else if (ent.EndsWith(DLL_SUFFIX, StringComparison.InvariantCulture) || ent.EndsWith(EXE_SUFFIX, StringComparison.InvariantCulture)) {
						Assemblies.Add(filename);
					} else if (ent.EndsWith($"{Path.DirectorySeparatorChar}{METADATA}", StringComparison.InvariantCulture)) {
						var mt = File.ReadAllText(ent);
						Metadata = SerializationHelper.Deserializer.Deserialize<ComponentMetadata>(mt);
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

			private static void _Copy(string source, string destination) {
				// https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
				var attr = File.GetAttributes(source);
				if (attr.HasFlag(FileAttributes.Directory)) {
					var dir = new DirectoryInfo(source);

					if (!dir.Exists) {
						throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {source}");
					}

					var dirs = dir.GetDirectories();
					if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);

					var files = dir.GetFiles();
					foreach (var file in files) {
						var path = Path.Combine(destination, file.Name);
						file.CopyTo(path, false);
					}

					foreach (var subdir in dirs) {
						var path = Path.Combine(destination, subdir.Name);
						_Copy(subdir.FullName, path);
					}
				} else {
					File.Copy(source, destination, overwrite: true);
				}


			}

			private void _Install(string name, IList<string> entries, string managed, bool subdir = false) {
				foreach (var ent in entries) {
					_Logger.Info($"Installing {name}: {ent}");

					var target = _GetTargetDir(ent, subdir ? TargetDirectory.Subdir : TargetDirectory.Managed);

					var local_target = managed;
					if (target == TargetDirectory.Subdir) local_target = Path.Combine(managed, Name);
					if (!Directory.Exists(local_target)) Directory.CreateDirectory(local_target);

					_Copy(AbsPath(ent), Path.Combine(local_target, ent));
				}
			}

			public void Install(Installer installer, bool leave_mmdlls = false) {
				var managed = installer.ManagedDir;

				_Install("assembly", Assemblies, managed);
				_Install("MonoMod patch DLL", PatchDLLs, managed);
				_Install("file", OtherFiles, managed, subdir: true);
				_Install("directory", Dirs, managed, subdir: true);

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
