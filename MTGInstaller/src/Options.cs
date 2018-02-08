using System;
using System.Collections.Generic;
using CommandLine;
using YamlDotNet.Serialization;
using System.Linq;

namespace MTGInstaller.Options {
	[Verb("download", HelpText = "Download and extract an ETGMod version by key.")]
	public class DownloadOptions {
		[Value(0, MetaName = "component", HelpText = "The component", Required = true)]
		public string Component { get; set; }

		[Value(1, MetaName = "version", HelpText = "The version (default is the latest one)", Required = false)]
		public string Version { get; set; }

		[Option('f', "force", HelpText = "Force a redownload (remove the target directory if it already exists)")]
		public bool Force { get; set; }

		[Option('h', "force-http", HelpText = "Force use of insecure HTTP instead of HTTPS")]
		public bool HTTP { get; set; } 
	}

	[Verb("autodetect", HelpText = "Attempt to autodetect the platform and location of the game.")]
	public class AutodetectOptions {
		[Option('a', "architecture", HelpText = "Change the autodetector architecture")]
		public string Architecture { get; set; }
	}

	[Verb("components", HelpText = "List the available components.")]
	public class ComponentsOptions {
		[Option('h', "force-http", HelpText = "Force use of insecure HTTP instead of HTTPS")]
		public bool HTTP { get; set; }

		[Option('c', "components", HelpText = "Add custom components through a YAML file")]
		public IEnumerable<string> CustomComponentFiles { get; set; }
	}

	[Verb("component", HelpText = "Show detailed information about a certain component.")]
	public class ComponentOptions {
		[Option('h', "http", HelpText = "Force use of insecure HTTP instead of HTTPS")]
		public bool HTTP { get; set; }

		[Value(0, MetaName = "name", HelpText = "Name of a component (to get specific details)", Required = true)]
		public string Name { get; set; }

		[Option('c', "components", HelpText = "Add custom components through a YAML file")]
		public IEnumerable<string> CustomComponentFiles { get; set; }
	}

	[Verb("install", HelpText = "Install components.")]
	public class InstallOptions {
		[Value(0, MetaName = "components", HelpText = "Semicolon separated list of components and versions (e.g. 'ETGMod@0.3;Example@1.0')", Required = true)]
		public string Components { get; set; }

		[Option('a', "architecture", HelpText = "Change the autodetector architecture")]
		public string Architecture { get; set; }

		[Option('e', "executable-path", HelpText = "Path to the executable (required if autodetection fails; you'll be told to use this option if that happens)")]
		public string Executable { get; set; }

		[Option('h', "force-http", HelpText = "Force use of insecure HTTP instead of HTTPS")]
		public bool HTTP { get; set; } = false;

		[Option('b', "force-backup", HelpText = "Force a backup to be made (note that if your current files have been tampered, the backup will contain these tampered files!)")]
		public bool ForceBackup { get; set; } = false;

		[Option('s', "skip-version-checks", HelpText = "Skip version checks (unsupported!)")]
		public bool SkipVersionChecks { get; set; } = false;

		[Option('c', "component-file", HelpText = "Add custom components through YAML files")]
		public IEnumerable<string> CustomComponentFiles { get; set; }

		[Option('d', "leave-patch-dlls", HelpText = "Don't delete the .mm.dll assemblies after finishing patching")]
		public bool LeavePatchDLLs { get; set; } = false;
	}

	[Verb("uninstall", HelpText = "Revert all components.")]
	public class UninstallOptions {
		[Option('a', "architecture", HelpText = "Change the autodetector architecture")]
		public string Architecture { get; set; }

		[Option('e', "executable-path", HelpText = "Path to the executable (required if autodetection fails; you'll be told to use this option if that happens)")]
		public string Executable { get; set; }
	}

	[Verb("settings", HelpText = "View and modify persistent settings.")]
	public class SettingsOptions {
		[Option('e', "executable-path", HelpText = "Path to the executable")]
		public string ExecutablePath { get; set; } = null;

		[Option('E', "clear-executable-path", HelpText = "Clear the executable path")]
		public bool ClearExecutablePath { get; set; } = false;

		[Option('h', "force-http", HelpText = "Force use of insecure HTTP instead of HTTPS")]
		public string ForceHTTP { get; set; } = null;

		[Option('b', "force-backup", HelpText = "Force a backup to be made (note that if your current files have been tampered, the backup will contain these tampered files!)")]
		public string ForceBackup { get; set; } = null;

		[Option('s', "skip-version-checks", HelpText = "Skip version checks (unsupported!)")]
		public string SkipVersionChecks { get; set; } = null;

		[Option('c', "component-file", HelpText = "Add custom components through YAML files")]
		public IEnumerable<string> CustomComponentFiles { get; set; } = null;

		[Option('C', "clear-custom-component-files", HelpText = "Clear the custom component YAML file list")]
		public bool ClearCustomComponentFiles { get; set; } = false;

		[Option('d', "leave-patch-dlls", HelpText = "Don't delete the .mm.dll assemblies after finishing patching")]
		public string LeavePatchDLLs { get; set; } = null;
	}
}
